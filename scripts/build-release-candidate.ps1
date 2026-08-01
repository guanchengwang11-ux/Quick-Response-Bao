param(
    [string]$Version = '1.0.0',
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repositoryRoot 'artifacts'
$publish = Join-Path $artifacts 'rc-publish'
$updaterPublish = Join-Path $artifacts 'rc-updater'
$portable = Join-Path $artifacts "Quick-Response-Bao-Portable-$Version-x64.zip"
$setup = Join-Path $artifacts "Quick-Response-Bao-Setup-$Version-x64.exe"
$checksums = Join-Path $artifacts 'checksums.txt'
$dotnet = if ($env:DOTNET_EXE) { $env:DOTNET_EXE } elseif (Test-Path (Join-Path $repositoryRoot '.dotnet\dotnet.exe')) { Join-Path $repositoryRoot '.dotnet\dotnet.exe' } else { 'dotnet' }

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
foreach ($path in @($publish, $updaterPublish)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}
foreach ($path in @($portable, $setup, $checksums)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

& $dotnet restore (Join-Path $repositoryRoot 'QuickResponseBao.sln') --runtime win-x64
if ($LASTEXITCODE) { throw 'dotnet restore failed.' }
& $dotnet test (Join-Path $repositoryRoot 'tests\QuickResponseBao.UnitTests\QuickResponseBao.UnitTests.csproj') -c Release -r win-x64 --no-restore
if ($LASTEXITCODE) { throw 'Automated tests failed.' }
& $dotnet publish (Join-Path $repositoryRoot 'src\QuickResponseBao.App\QuickResponseBao.App.csproj') -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $publish
if ($LASTEXITCODE) { throw 'Application publish failed.' }
& $dotnet publish (Join-Path $repositoryRoot 'src\QuickResponseBao.Updater\QuickResponseBao.Updater.csproj') -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $updaterPublish
if ($LASTEXITCODE) { throw 'Updater publish failed.' }
Copy-Item -LiteralPath (Join-Path $updaterPublish 'QuickResponseBao.Updater.exe') -Destination $publish -Force

Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $portable -CompressionLevel Optimal

if (-not $SkipInstaller) {
    $languageDirectory = Join-Path $artifacts 'inno-languages'
    $chineseLanguage = Join-Path $languageDirectory 'ChineseSimplified.isl'
    $languageUrl = 'https://raw.githubusercontent.com/kira-96/Inno-Setup-Chinese-Simplified-Translation/6da09d23e14443d4cf8f07b1c5fd821bfe459788/ChineseSimplified.isl'
    $languageHash = '869E43E7C7B8D20C7E4397C8E98F7D1B7CF0528803ACDF019AD350143EC85469'
    New-Item -ItemType Directory -Force -Path $languageDirectory | Out-Null
    Invoke-WebRequest -Uri $languageUrl -OutFile $chineseLanguage
    if ((Get-FileHash -LiteralPath $chineseLanguage -Algorithm SHA256).Hash -ne $languageHash) {
        Remove-Item -LiteralPath $chineseLanguage -Force
        throw 'Inno Setup Chinese translation SHA-256 verification failed.'
    }
    $iscc = if ($env:ISCC_PATH) { $env:ISCC_PATH } else { 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' }
    if (-not (Test-Path -LiteralPath $iscc)) { throw "Inno Setup compiler was not found at $iscc." }
    & $iscc (Join-Path $repositoryRoot 'installer\QuickResponseBao.iss')
    if ($LASTEXITCODE -or -not (Test-Path -LiteralPath $setup)) { throw 'Installer build failed.' }
}

$releaseFiles = @($portable)
if (Test-Path -LiteralPath $setup) { $releaseFiles = @($setup, $portable) }
$lines = $releaseFiles | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    "$($hash.Hash.ToUpperInvariant())  $([IO.Path]::GetFileName($_))"
}
Set-Content -LiteralPath $checksums -Value $lines -Encoding ascii
Write-Host "Release files created in $artifacts"
