param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishRoot = Join-Path $artifactsRoot "publish"
$releaseRoot = Join-Path $artifactsRoot "release"
$bundleRoot = Join-Path $artifactsRoot "bundle"
$appPublishDir = Join-Path $publishRoot "app"
$bundleDir = Join-Path $bundleRoot "cs-live-mute-$Version-$Runtime"
$zipPath = Join-Path $releaseRoot "cs-live-mute-$Version-$Runtime.zip"
$tempBundleDir = Join-Path $env:TEMP "cs-live-mute-$Version-$Runtime"
$tempZipPath = Join-Path $env:TEMP "cs-live-mute-$Version-$Runtime.zip"

Remove-Item $publishRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $bundleDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item $tempBundleDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $tempZipPath -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $publishRoot, $releaseRoot, $bundleDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $bundleDir "app") | Out-Null

dotnet publish `
    (Join-Path $repoRoot "src\CsLiveMute.Desktop\CsLiveMute.Desktop.csproj") `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o $appPublishDir

Copy-Item (Join-Path $appPublishDir "*") (Join-Path $bundleDir "app") -Recurse
Copy-Item (Join-Path $repoRoot "docs\INSTALLATION.md") (Join-Path $bundleDir "INSTALLATION.md")
Copy-Item (Join-Path $repoRoot "README.md") (Join-Path $bundleDir "README.md")

Add-Type -AssemblyName System.IO.Compression.FileSystem
Copy-Item $bundleDir $tempBundleDir -Recurse
[System.IO.Compression.ZipFile]::CreateFromDirectory($tempBundleDir, $tempZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Copy-Item $tempZipPath $zipPath -Force
Write-Host "Created release package: $zipPath"
