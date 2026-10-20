param(
    [ValidateSet('Debug', 'Release', 'Snapshot')][string]$Configuration = 'Debug',
    [string]$DotnetPath = 'dotnet',
    [switch]$RunTests
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
Push-Location -LiteralPath $PSScriptRoot
try {
    & $DotnetPath build 'Plain Craft Launcher 2.sln' -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed.' }
    if ($RunTests) {
        & $DotnetPath build 'Tests/Launcher.SmokeTests.csproj' -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Smoke test build failed.' }
        & (Join-Path $PSScriptRoot "Tests/bin/$Configuration/net48/Launcher.SmokeTests.exe")
        if ($LASTEXITCODE -ne 0) { throw 'Smoke tests failed.' }
    }
} finally { Pop-Location }
