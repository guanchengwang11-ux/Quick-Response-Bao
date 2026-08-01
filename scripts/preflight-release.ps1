param(
    [string]$Version = '1.0.0',
    [switch]$SkipBuildAndTests,
    [switch]$AllowMissingInstaller
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$setupName = "Quick-Response-Bao-Setup-$Version-x64.exe"
$portableName = "Quick-Response-Bao-Portable-$Version-x64.zip"
$required = @($portableName, 'checksums.txt')
if (-not $AllowMissingInstaller) { $required = @($setupName) + $required }

Push-Location $root
try {
    $status = git status --porcelain
    if ($status) { throw 'Git working tree is not clean.' }
    $branch = git branch --show-current
    $expectedTag = "v$Version"
    $isExactActionsTag = $env:GITHUB_ACTIONS -eq 'true' -and
        $env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -eq $expectedTag
    if ($branch -ne 'main' -and -not $isExactActionsTag) {
        throw "Expected branch main or exact Actions tag $expectedTag, found '$branch'."
    }

    if (-not $SkipBuildAndTests) {
        & (Join-Path $PSScriptRoot 'build-release-candidate.ps1') -Version $Version -SkipInstaller:$AllowMissingInstaller
        if ($LASTEXITCODE) { throw 'Release build or tests failed.' }
    }

    foreach ($name in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $artifacts $name))) { throw "Missing release file: $name" }
    }
    $manifest = Get-Content -LiteralPath (Join-Path $artifacts 'checksums.txt') -Raw
    foreach ($name in $required | Where-Object { $_ -ne 'checksums.txt' }) {
        $actual = (Get-FileHash -LiteralPath (Join-Path $artifacts $name) -Algorithm SHA256).Hash
        $escapedName = [regex]::Escape($name)
        if ($manifest -notmatch "(?im)^$actual\s+$escapedName\s*$") { throw "SHA-256 mismatch or missing checksum entry: $name" }
    }

    [xml]$props = Get-Content -LiteralPath 'Directory.Build.props' -Raw -Encoding utf8
    if ($props.Project.PropertyGroup.Version -ne $Version) { throw 'Directory.Build.props version differs from the release version.' }
    if ((Get-Content 'installer\QuickResponseBao.iss' -Raw) -notmatch ('#define MyAppVersion "' + [regex]::Escape($Version) + '"')) { throw 'Installer version differs from the release version.' }
    if ((Get-Content "docs\release-notes-$Version.md" -Raw) -notmatch [regex]::Escape($Version)) { throw 'Release notes version differs from the release version.' }
    $mainWindowCode = Get-Content 'src\QuickResponseBao.App\MainWindow.xaml.cs' -Raw
    if ($mainWindowCode -notmatch 'AboutVersionValue\.Text.*ApplicationVersion\.Current') { throw 'About page is not bound to the assembly informational version.' }
    $exe = Join-Path $artifacts 'rc-publish\QuickResponseBao.exe'
    if (-not (Test-Path $exe) -or (Get-Item $exe).VersionInfo.ProductVersion -notlike "$Version*") { throw 'Published assembly version differs from the release version.' }

    [xml]$en = Get-Content 'src\QuickResponseBao.App\Resources\Strings.en-US.xaml' -Raw -Encoding utf8
    [xml]$zh = Get-Content 'src\QuickResponseBao.App\Resources\Strings.zh-CN.xaml' -Raw -Encoding utf8
    $enKeys = @($en.ResourceDictionary.ChildNodes | ForEach-Object { $_.Key } | Where-Object { $_ })
    $zhKeys = @($zh.ResourceDictionary.ChildNodes | ForEach-Object { $_.Key } | Where-Object { $_ })
    if (Compare-Object $enKeys $zhKeys) { throw 'English and Simplified Chinese resource keys differ.' }

    $forbidden = git ls-files | Where-Object { $_ -match '(^|/)(bin|obj|\.vs|logs?)(/|$)|\.(db|log|user)$|appsettings\.local|local\.settings' }
    if ($forbidden) { throw "Forbidden tracked files found: $($forbidden -join ', ')" }
    $trackedText = git grep -n -I -E 'ghp_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,}|Bearer [A-Za-z0-9._-]{20,}' -- . ':!scripts/preflight-release.ps1'
    if ($LASTEXITCODE -eq 0 -and $trackedText) { throw 'A possible token was found in tracked text.' }
    if (-not (Test-Path '.github\workflows\ci.yml') -or -not (Test-Path '.github\workflows\release-candidate.yml')) { throw 'Required GitHub Actions workflows are missing.' }
    $archiveEntries = tar -tf (Join-Path $artifacts $portableName)
    if ($LASTEXITCODE -or -not ($archiveEntries -match 'QuickResponseBao\.exe') -or -not ($archiveEntries -match 'licenses/')) { throw 'Portable archive is invalid or omits required license files.' }
    if ($archiveEntries -match '(^|/)(bin|obj|logs?|backups?|data|config)(/|$)|\.(db|log)$') { throw 'Portable archive contains forbidden build or user-data files.' }
    Write-Host 'All release preflight checks passed.'
}
finally { Pop-Location }
