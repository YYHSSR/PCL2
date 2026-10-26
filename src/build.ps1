param(
    [ValidateSet('Debug', 'Release', 'Snapshot')][string]$Configuration = 'Debug',
    [string]$DotnetPath = 'dotnet',
    [switch]$RunTests,
    [switch]$LaunchCheck
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
    if ($LaunchCheck) {
        # Starts the real EXE in an isolated folder and settings profile; the window must appear and stay alive.
        $profile = 'PCL2LaunchCheck_' + [guid]::NewGuid().ToString('N')
        $folder = Join-Path ([IO.Path]::GetTempPath()) $profile
        New-Item -ItemType Directory -Path $folder | Out-Null
        $exe = Join-Path $folder 'PCL-YYH.exe'
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe') -Destination $exe
        $env:PCL_SETTINGS_PROFILE = $profile
        $env:PCL_CACHE_PATH = Join-Path $folder 'Cache'
        try {
            $process = Start-Process -FilePath $exe -WorkingDirectory $folder -PassThru
            $timer = [Diagnostics.Stopwatch]::StartNew()
            while ($timer.ElapsedMilliseconds -lt 15000 -and -not $process.HasExited) {
                $process.Refresh()
                if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
                Start-Sleep -Milliseconds 100
            }
            if ($process.HasExited) { throw ('Launcher exited during startup with code 0x{0:X}.' -f $process.ExitCode) }
            if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Launcher window did not appear within 15 seconds.' }
            Write-Host "Launcher window appeared after $($timer.ElapsedMilliseconds) ms."
            Start-Sleep -Seconds 8
            if ($process.HasExited) { throw ('Launcher exited after startup with code 0x{0:X}.' -f $process.ExitCode) }
            Write-Host 'Launch check passed.'
        } finally {
            if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit(5000) | Out-Null }
            Remove-Item Env:\PCL_SETTINGS_PROFILE, Env:\PCL_CACHE_PATH -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath "HKCU:\Software\$profile" -Recurse -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
} finally { Pop-Location }
