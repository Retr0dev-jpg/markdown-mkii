param(
    [switch]$IncludeApp,
    [ValidateSet('Both', 'MSIX', 'Unpackaged')]
    [string]$AppBuild = 'Both',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    foreach ($project in @('MarkdownMkII.Core.Tests', 'MarkdownMkII.App.Logic.Tests', 'MarkdownMkII.Storage.Tests')) {
        dotnet test "tests/$project" --nologo --configuration $Configuration -warnaserror `
            --logger 'trx' --results-directory '.artifacts/test-results'
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $project" }
    }

    if ($IncludeApp) {
        if (-not $IsWindows) { throw 'Building the WinUI application requires Windows.' }
        if ($AppBuild -in @('Both', 'Unpackaged')) {
            dotnet build src/MarkdownMkII.App --nologo --configuration $Configuration -warnaserror `
                -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true
            if ($LASTEXITCODE -ne 0) { throw 'The unpackaged WinUI application build failed.' }
        }
        if ($AppBuild -in @('Both', 'MSIX')) {
            dotnet build src/MarkdownMkII.App --nologo --configuration $Configuration -warnaserror `
                -p:Platform=x64 -p:WindowsPackageType=MSIX -p:WindowsAppSDKSelfContained=false
            if ($LASTEXITCODE -ne 0) { throw 'The MSIX WinUI application build failed.' }
        }
        dotnet build tests/MarkdownMkII.App.RuntimeChecks --nologo --configuration $Configuration -warnaserror
        if ($LASTEXITCODE -ne 0) { throw 'The in-process runtime checks build failed.' }
    }
}
finally {
    Pop-Location
}
