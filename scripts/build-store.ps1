param(
    # Patch number of the release being submitted; the Store needs a higher version every time.
    [Parameter(Mandatory)][int]$VersionPatch,
    [string]$OutputDir
)

# Builds the Microsoft Store upload (x64 + ARM64 bundle) with the .NET SDK alone, locally and in CI.
# It is not signed: the Store signs it. No symbols package: the MSIX NuGet tooling cannot create it
# without Visual Studio, and it only improves crash reports in Partner Center.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
# Windows PowerShell 5.1 leaves $PSScriptRoot empty while parameter defaults are evaluated.
if (-not $OutputDir) { $OutputDir = Join-Path $root 'artifacts\store' }
$OutputDir = [IO.Path]::GetFullPath($OutputDir)
if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }
# A published package must not reuse intermediate files from earlier builds (for example an old identity).
foreach ($folder in 'bin', 'obj') {
    $path = Join-Path $root "src\MarkdownMkII.App\$folder"
    if (Test-Path $path) { Remove-Item -Recurse -Force $path }
}

dotnet build (Join-Path $root 'src\MarkdownMkII.App') --nologo -c Release `
    -p:Platform=x64 -p:VersionPatch=$VersionPatch `
    -p:GenerateAppxPackageOnBuild=true -p:UapAppxPackageBuildMode=StoreUpload `
    -p:AppxBundle=Always '-p:AppxBundlePlatforms=x64|arm64' `
    -p:AppxPackageSigningEnabled=false -p:AppxSymbolPackageEnabled=false `
    "-p:AppxPackageDir=$OutputDir\"
if ($LASTEXITCODE -ne 0) { throw 'The Store package build failed.' }

$upload = Get-ChildItem $OutputDir -Filter '*.msixupload' | Select-Object -First 1
if (-not $upload) { throw 'No .msixupload was produced.' }
Write-Host "Upload this file to Partner Center: $($upload.FullName)"
$upload.FullName
