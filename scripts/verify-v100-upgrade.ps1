param(
    [Parameter(Mandatory = $true)][string]$PreviousSetup,
    [Parameter(Mandatory = $true)][string]$CurrentSetup
)

$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Version upgrade verification is restricted to an isolated GitHub Actions runner.' }

$root = Split-Path -Parent $PSScriptRoot
$previousSetupPath = [IO.Path]::GetFullPath((Join-Path $root $PreviousSetup))
$currentSetupPath = [IO.Path]::GetFullPath((Join-Path $root $CurrentSetup))
$testRoot = Join-Path $env:RUNNER_TEMP 'quick-response-bao-v100-upgrade'
$install = Join-Path $testRoot 'installed'
$userData = Join-Path $env:LOCALAPPDATA 'QuickResponseBao'
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
    $process = Start-Process -FilePath $path -PassThru
    Start-Sleep -Seconds 5
    if ($process.HasExited) { throw "$operation exited unexpectedly with code $($process.ExitCode)." }
    $process.Kill($true)
    if (-not $process.WaitForExit(15000)) { throw "$operation did not stop within 15 seconds." }
}

try {
    foreach ($path in @($previousSetupPath, $currentSetupPath)) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing installer: $path" }
    }
    foreach ($path in @($testRoot, $userData)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    }
    New-Item -ItemType Directory -Force -Path $testRoot | Out-Null

    Invoke-Installer $previousSetupPath 'v1.0.0 installation'
    $app = Join-Path $install 'QuickResponseBao.exe'
    if ((Get-Item $app).VersionInfo.ProductVersion -notlike '1.0.0*') { throw 'The baseline installer did not install v1.0.0.' }
    Start-And-Stop $app 'v1.0.0 application'
    $database = Join-Path $userData 'data\quick-responses.db'
    if (-not (Test-Path -LiteralPath $database)) { throw 'v1.0.0 did not create the user database.' }
    $databaseHash = (Get-FileHash -LiteralPath $database -Algorithm SHA256).Hash

    $sentinels = @('config\upgrade-101.json', 'backups\upgrade-101.bak', 'logs\upgrade-101.log')
    foreach ($relative in $sentinels) {
        $path = Join-Path $userData $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
        Set-Content -LiteralPath $path -Value $relative -Encoding utf8
    }

    Write-Host 'Checking the authenticated public latest-release endpoint.'
    $releaseHeaders = @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'QuickResponseBao-upgrade-verification' }
    if ($env:GH_TOKEN) { $releaseHeaders.Authorization = "Bearer $env:GH_TOKEN" }
    $latest = Invoke-RestMethod -Uri 'https://api.github.com/repos/guanchengwang11-ux/Quick-Response-Bao/releases/latest' -Headers $releaseHeaders -TimeoutSec 30
    if ($latest.tag_name -ne 'v1.0.1' -or $latest.draft -or $latest.prerelease) { throw 'The public latest-release endpoint did not return stable v1.0.1.' }
    $requiredAssets = @('Quick-Response-Bao-Setup-1.0.1-x64.exe', 'Quick-Response-Bao-Portable-1.0.1-x64.zip', 'checksums.txt')
    foreach ($name in $requiredAssets) {
        if ($latest.assets.name -notcontains $name) { throw "The update release is missing asset $name" }
    }

    Invoke-Installer $currentSetupPath 'v1.0.1 upgrade'
    if ((Get-Item $app).VersionInfo.ProductVersion -notlike '1.0.1*') { throw 'The upgrade did not install v1.0.1.' }
    if ((Get-FileHash -LiteralPath $database -Algorithm SHA256).Hash -ne $databaseHash) { throw 'The v1.0.1 installer changed the existing database.' }
    foreach ($relative in $sentinels) {
        if ((Get-Content -LiteralPath (Join-Path $userData $relative) -Raw).Trim() -ne $relative) { throw "Upgrade changed user data: $relative" }
    }
    Start-And-Stop $app 'upgraded v1.0.1 application'
    Write-Host 'Public update discovery and v1.0.0-to-v1.0.1 upgrade verification passed.'
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
