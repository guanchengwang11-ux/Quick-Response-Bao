param(
    [Parameter(Mandatory = $true)][string]$PreviousVersion,
    [Parameter(Mandatory = $true)][string]$PreviousSetup,
    [Parameter(Mandatory = $true)][string]$CurrentVersion,
    [Parameter(Mandatory = $true)][string]$CurrentSetup
)

$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Version upgrade verification is restricted to an isolated GitHub Actions runner.' }

$root = Split-Path -Parent $PSScriptRoot
function Resolve-PackagePath([string]$path) {
    if ([IO.Path]::IsPathRooted($path)) { return [IO.Path]::GetFullPath($path) }
    return [IO.Path]::GetFullPath((Join-Path $root $path))
}

$previousSetupPath = Resolve-PackagePath $PreviousSetup
$currentSetupPath = Resolve-PackagePath $CurrentSetup
$testRoot = Join-Path $env:RUNNER_TEMP "quick-response-bao-upgrade-$($PreviousVersion.Replace('.', '-'))"
$install = Join-Path $testRoot 'installed'
$userData = Join-Path $env:LOCALAPPDATA 'QuickResponseBao'
$database = Join-Path $userData 'data\quick-responses.db'
$settings = Join-Path $userData 'config\settings.json'
$backup = Join-Path $userData 'backups\upgrade-sentinel.bak'
$log = Join-Path $userData 'logs\upgrade-sentinel.log'
$arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=$install", '/TASKS=')

function Invoke-Installer([string]$path, [string]$operation) {
    $process = Start-Process -FilePath $path -ArgumentList $arguments -PassThru
    if (-not $process.WaitForExit(120000)) {
        try { $process.Kill($true) } catch { Write-Warning "$operation cleanup failed: $_" }
        throw "$operation timed out."
    }
    if ($process.ExitCode) { throw "$operation failed with exit code $($process.ExitCode)." }
}

function Start-And-Stop([string]$path, [string]$operation) {
    Write-Host "$operation`: starting $path"
    $process = Start-Process -FilePath $path -PassThru
    Start-Sleep -Seconds 5
    if ($process.HasExited) { throw "$operation exited unexpectedly with code $($process.ExitCode)." }
    Write-Host "$operation`: startup passed; stopping process $($process.Id)."
    Stop-Process -Id $process.Id -Force -ErrorAction Stop
    if (-not $process.WaitForExit(15000)) { throw "$operation did not stop within 15 seconds." }
    Write-Host "$operation`: startup and shutdown check passed."
}

$seedDatabase = @'
import datetime, json, sqlite3, sys, uuid
path, baseline = sys.argv[1], sys.argv[2]
connection = sqlite3.connect(path)
now = datetime.datetime.now(datetime.timezone.utc).isoformat()
categories = ['Upgrade Sales', 'Upgrade Risk', '升级测试']
for order, name in enumerate(categories, 100):
    connection.execute('INSERT OR IGNORE INTO categories(id,name,sort_order,created_at,updated_at) VALUES(?,?,?,?,?)', (str(uuid.uuid4()), name, order, now, now))
for index in range(20):
    category = categories[index % len(categories)]
    language = 'Chinese' if index % 2 else 'English'
    last_used = now if index % 3 == 0 else None
    connection.execute('INSERT INTO quick_responses(id,summary,content,keywords_json,category,language,is_enabled,sort_order,usage_count,created_at,updated_at,last_used_at) VALUES(?,?,?,?,?,?,?,?,?,?,?,?)',
        (str(uuid.uuid4()), f'Upgrade response {index + 1}', f'Preserved content {index + 1} from {baseline}', json.dumps(['upgrade', f'keyword-{index + 1}']), category, language, 1, index, index + 5, now, now, last_used))
connection.commit()
connection.close()
'@

$verifyDatabase = @'
import json, sqlite3, sys
path = sys.argv[1]
connection = sqlite3.connect(path)
count = connection.execute("SELECT COUNT(*) FROM quick_responses WHERE summary LIKE 'Upgrade response %'").fetchone()[0]
categories = {row[0] for row in connection.execute("SELECT name FROM categories WHERE name IN ('Upgrade Sales','Upgrade Risk','升级测试')")}
usage = connection.execute("SELECT MIN(usage_count), MAX(usage_count), COUNT(last_used_at) FROM quick_responses WHERE summary LIKE 'Upgrade response %'").fetchone()
keywords = json.loads(connection.execute("SELECT keywords_json FROM quick_responses WHERE summary='Upgrade response 1'").fetchone()[0])
connection.close()
if count != 20: raise SystemExit(f'Expected 20 responses, found {count}')
if categories != {'Upgrade Sales', 'Upgrade Risk', '升级测试'}: raise SystemExit(f'Categories were not preserved: {categories}')
if usage[0] != 5 or usage[1] != 24 or usage[2] < 1: raise SystemExit(f'Usage metadata was not preserved: {usage}')
if keywords != ['upgrade', 'keyword-1']: raise SystemExit(f'Keywords were not preserved: {keywords}')
'@

try {
    foreach ($path in @($previousSetupPath, $currentSetupPath)) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing installer: $path" }
    }
    foreach ($path in @($testRoot, $userData)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    }
    New-Item -ItemType Directory -Force -Path $testRoot | Out-Null

    Invoke-Installer $previousSetupPath "v$PreviousVersion installation"
    $app = Join-Path $install 'QuickResponseBao.exe'
    if ((Get-Item $app).VersionInfo.ProductVersion -notlike "$PreviousVersion*") { throw "The baseline installer did not install v$PreviousVersion." }
    Start-And-Stop $app "v$PreviousVersion application"
    if (-not (Test-Path -LiteralPath $database)) { throw "v$PreviousVersion did not create the user database." }

    & python -c $seedDatabase $database $PreviousVersion
    if ($LASTEXITCODE) { throw 'Failed to seed the upgrade database.' }
    New-Item -ItemType Directory -Force -Path (Split-Path $settings), (Split-Path $backup), (Split-Path $log) | Out-Null
    $expectedSettings = [ordered]@{
        Language = 'en-US'; Theme = 'Dark'; MinimumTriggerLength = 4; MaximumSuggestions = 10
        AllowedProcesses = @('Lark.exe', 'Telegram.exe', 'Discord.exe', 'chrome.exe', 'msedge.exe', 'UpgradeTarget.exe')
        ReplaceTypedSearchText = $true; PreserveClipboard = $true; RestoreClipboard = $true
    }
    $expectedSettings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $settings -Encoding utf8
    Set-Content -LiteralPath $backup -Value 'backup-preserved' -Encoding utf8
    Set-Content -LiteralPath $log -Value 'log-preserved' -Encoding utf8

    Invoke-Installer $currentSetupPath "v$PreviousVersion to v$CurrentVersion upgrade"
    if ((Get-Item $app).VersionInfo.ProductVersion -notlike "$CurrentVersion*") { throw "The upgrade did not install v$CurrentVersion." }
    Start-And-Stop $app "upgraded v$CurrentVersion application"

    & python -c $verifyDatabase $database
    if ($LASTEXITCODE) { throw 'Upgrade database validation failed.' }
    $actualSettings = Get-Content -LiteralPath $settings -Raw -Encoding utf8 | ConvertFrom-Json
    if ($actualSettings.Language -ne 'en-US' -or $actualSettings.Theme -ne 'Dark' -or $actualSettings.AllowedProcesses -notcontains 'UpgradeTarget.exe') {
        throw 'Language, theme or application whitelist settings were not preserved.'
    }
    if ((Get-Content -LiteralPath $backup -Raw).Trim() -ne 'backup-preserved') { throw 'Backup data was not preserved.' }
    if ((Get-Content -LiteralPath $log -Raw).Trim() -ne 'log-preserved') { throw 'Log data was not preserved.' }

    $uninstaller = Join-Path $install 'unins000.exe'
    $exitCode = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru
    if ($exitCode.ExitCode) { throw "Uninstaller returned exit code $($exitCode.ExitCode)." }
    foreach ($path in @($database, $settings, $backup, $log)) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Default uninstall removed user data: $path" }
    }
    Write-Host "v$PreviousVersion to v$CurrentVersion upgrade preserved 20 responses, categories, keywords, settings, whitelist, theme, backup, usage count and last-used data."
}
finally {
    $uninstaller = Join-Path $install 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        $process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -PassThru
        $null = $process.WaitForExit(120000)
    }
    foreach ($path in @($testRoot, $userData)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    }
}
