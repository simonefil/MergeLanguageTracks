# MergeLanguageTracks release script
# Usage: .\release.ps1 -Tag "v1.0.0" -Notes "First release"

param(
    [Parameter(Mandatory=$true)][string]$Tag,
    [Parameter(Mandatory=$true)][string]$Notes
)

$ErrorActionPreference = "Stop"

$project = "MergeLanguageTracks.csproj"
$artifactsDir = "release-artifacts"
$publishDir = "publish"

$rids = @(
    "win-x64",
    "linux-x64",
    "linux-arm64",
    "osx-x64",
    "osx-arm64"
)

function Confirm-Step {
    param([string]$Message)
    $choice = Read-Host "$Message [Y/n]"
    if ($choice -eq "n" -or $choice -eq "N") {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 0
    }
}

# Clean previous builds
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $artifactsDir) { Remove-Item $artifactsDir -Recurse -Force }
New-Item -ItemType Directory -Path $artifactsDir | Out-Null

# Build all targets
Confirm-Step "Build binaries for $($rids.Count) platforms?"
foreach ($rid in $rids) {
    Write-Host "Building $rid..." -ForegroundColor Cyan

    dotnet publish $project -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -p:EnableCompressionInSingleFile=true `
        -o "$publishDir\$rid"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed for $rid" -ForegroundColor Red
        exit 1
    }

    # Zip the binary
    $zipName = "$artifactsDir\MergeLanguageTracks-$rid.zip"
    Compress-Archive -Path "$publishDir\$rid\*" -DestinationPath $zipName
    Write-Host "$rid done." -ForegroundColor Green
}

# Create and push tag
Confirm-Step "Create git tag $Tag and push?"
Write-Host "Creating tag $Tag..." -ForegroundColor Cyan
git tag $Tag
git push origin $Tag

# Create GitHub release
Confirm-Step "Create GitHub release with artifacts?"
Write-Host "Creating GitHub release..." -ForegroundColor Cyan
$files = (Get-ChildItem $artifactsDir -Filter *.zip).FullName
gh release create $Tag $files --title $Tag --notes $Notes

# Cleanup
Remove-Item $publishDir -Recurse -Force
Remove-Item $artifactsDir -Recurse -Force

Write-Host "Release $Tag published successfully." -ForegroundColor Green
