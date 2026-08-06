param(
    [string]$Version = '1.0.1',
    [string]$PackageDirectory
)

$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Package installation verification is restricted to an isolated GitHub Actions runner.' }

$root = Split-Path -Parent $PSScriptRoot
$artifacts = if ($PackageDirectory) { $PackageDirectory } else { Join-Path $root 'artifacts' }
$setup = Join-Path $artifacts "Quick-Response-Bao-Setup-$Version-x64.exe"
$portable = Join-Path $artifacts "Quick-Response-Bao-Portable-$Version-x64.zip"
$testRoot = Join-Path $env:RUNNER_TEMP 'quick-response-bao-rc-validation'
$install = Join-Path $testRoot 'installed'
$expanded = Join-Path $testRoot 'portable'
$userData = Join-Path $env:LOCALAPPDATA 'QuickResponseBao'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Quick Response Bao.lnk'
$menuShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Quick Response Bao\Quick Response Bao.lnk'

if (Test-Path $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null

function Invoke-And-Wait([string]$Executable, [string[]]$Arguments, [int]$TimeoutSeconds, [string]$Operation) {
    Write-Host "$Operation`: starting $Executable"
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill($true) } catch { Write-Warning "$Operation`: failed to terminate timed-out process: $_" }
        throw "$Operation timed out after $TimeoutSeconds seconds."
    }
    Write-Host "$Operation`: completed with exit code $($process.ExitCode)."
    return $process.ExitCode
}

function Start-And-Stop([string]$Executable, [string]$Operation) {
    Write-Host "$Operation`: starting $Executable"
    $process = Start-Process -FilePath $Executable -PassThru
    Start-Sleep -Seconds 4
    if ($process.HasExited) { throw "$Executable exited unexpectedly with code $($process.ExitCode)." }
    $process.Kill($true)
    if (-not $process.WaitForExit(15000)) { throw "$Operation did not stop within 15 seconds." }
    Write-Host "$Operation`: startup and shutdown check passed."
}

try {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=$install", '/TASKS=desktopicon')
    $exitCode = Invoke-And-Wait $setup $arguments 120 'Initial installation'
    if ($exitCode) { throw "Installer returned exit code $exitCode." }
    $installedExe = Join-Path $install 'QuickResponseBao.exe'
    foreach ($path in @($installedExe, (Join-Path $install 'QuickResponseBao.Updater.exe'), (Join-Path $install 'unins000.exe'), $desktopShortcut, $menuShortcut)) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Installation validation failed; missing $path" }
    }
    if ((Get-Item $installedExe).VersionInfo.ProductVersion -notlike "$Version*") { throw 'Installed executable has the wrong version.' }
    Start-And-Stop $installedExe 'Installed application'
    if (-not (Test-Path (Join-Path $userData 'data\quick-responses.db'))) { throw 'Application did not create its database in LocalAppData.' }
    $sentinels = @('data\upgrade-sentinel.db', 'config\upgrade-sentinel.json', 'backups\upgrade-sentinel.bak', 'logs\upgrade-sentinel.log')
    foreach ($relative in $sentinels) {
        $path = Join-Path $userData $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
        Set-Content -LiteralPath $path -Value $relative -Encoding utf8
    }
    $exitCode = Invoke-And-Wait $setup $arguments 120 'Upgrade installation'
    if ($exitCode) { throw "Upgrade installer returned exit code $exitCode." }
    foreach ($relative in $sentinels) {
        if ((Get-Content -LiteralPath (Join-Path $userData $relative) -Raw).Trim() -ne $relative) { throw "Upgrade changed user data: $relative" }
    }
    $programDataLeak = Get-ChildItem -LiteralPath $install -Recurse -File | Where-Object { $_.Extension -in @('.db', '.log') -or $_.Name -eq 'settings.json' -or $_.Directory.Name -eq 'backups' }
    if ($programDataLeak) { throw 'User data was written into the installation directory.' }

    $exitCode = Invoke-And-Wait (Join-Path $install 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') 120 'Uninstallation'
    if ($exitCode) { throw "Uninstaller returned exit code $exitCode." }
    foreach ($relative in $sentinels) {
        if (-not (Test-Path -LiteralPath (Join-Path $userData $relative))) { throw "Silent uninstall did not preserve user data: $relative" }
    }

    Expand-Archive -LiteralPath $portable -DestinationPath $expanded
    $portableExe = Join-Path $expanded 'QuickResponseBao.exe'
    foreach ($path in @($portableExe, (Join-Path $expanded 'QuickResponseBao.Updater.exe'), (Join-Path $expanded 'THIRD-PARTY-NOTICES.md'))) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Portable validation failed; missing $path" }
    }
    if ((Get-Item $portableExe).VersionInfo.ProductVersion -notlike "$Version*") { throw 'Portable executable has the wrong version.' }
    Start-And-Stop $portableExe 'Portable application'
    if (Get-ChildItem -LiteralPath $expanded -Recurse -File | Where-Object { $_.Extension -in @('.db', '.log') -or $_.Name -eq 'settings.json' }) {
        throw 'Portable application wrote user data into its program directory.'
    }
    Write-Host 'Installer, upgrade, uninstall and portable package checks passed.'
}
finally {
    if (Test-Path $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
    if (Test-Path $userData) { Remove-Item -LiteralPath $userData -Recurse -Force }
    foreach ($shortcut in @($desktopShortcut, $menuShortcut)) { if (Test-Path $shortcut) { Remove-Item -LiteralPath $shortcut -Force } }
}
