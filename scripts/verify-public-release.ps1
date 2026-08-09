param([string]$Version = '1.1.0')

$ErrorActionPreference = 'Stop'
$headers = @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'QuickResponseBao-release-verification' }
if ($env:GH_TOKEN) { $headers.Authorization = "Bearer $env:GH_TOKEN" }
$latest = Invoke-RestMethod -Uri 'https://api.github.com/repos/guanchengwang11-ux/Quick-Response-Bao/releases/latest' -Headers $headers -TimeoutSec 30
if ($latest.tag_name -ne "v$Version" -or $latest.draft -or $latest.prerelease) {
    throw "The public latest-release endpoint did not return stable v$Version."
}
$requiredAssets = @(
    "Quick-Response-Bao-Setup-$Version-x64.exe",
    "Quick-Response-Bao-$Version-Portable-x64.zip",
    'checksums.txt'
)
foreach ($name in $requiredAssets) {
    if ($latest.assets.name -notcontains $name) { throw "The public release is missing asset $name" }
}
$baseline = [version]'1.0.2'
$current = [version]$Version
if ($current -le $baseline) { throw "v$Version was not recognized as newer than v1.0.2." }
Write-Host "Public update discovery returned stable v$Version with all required assets; semantic comparison against v1.0.2 passed."
