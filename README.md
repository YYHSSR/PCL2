# PCL YYH

> **This is YYH's slimmed-down, modified personal-use build of the official PCL2 launcher.**
> It is not an official PCL release. Commercial use is prohibited; non-commercial modification and further development are allowed (see [License](#license)).

Maintainer: **YYH**

A Minecraft: Java Edition launcher for Windows with instance management, Java detection, account login, resource downloads, mod management and modpack installation. This fork continues from the PCL 2.14.0.0 source code.

## Usage

Run `PCL-YYH.exe`. There is no ZIP to extract and no project DLLs to place next to it; built-in resources are extracted on demand, and cache and configuration files are created at runtime.

- System: 64-bit Windows with .NET Framework 4.8.
- Game: a Java version compatible with the target Minecraft version; the launcher can detect and manage Java installations.
- Microsoft login: set your own `PCL_MS_CLIENT_ID` for the launcher process and restart it.
- CurseForge: save your own valid API key under Settings → Other → Community Resources, or set `PCL_CURSEFORGE_API_KEY` (the environment variable takes precedence). The saved key is encrypted with DPAPI. Modrinth works without any key.

Configuration uses the `PCL2Local` identifier, and credentials are protected with DPAPI for the current Windows user. Copying the program does not migrate account credentials to another Windows user.

## Differences from official PCL2

- The UI is fixed to Simplified Chinese; there is no language switch.
- The top bar only has Launch, Download and Settings. The "More" page with help, about, feedback, voting and the toolbox has been removed.
- Multiplayer (Link) has been removed completely, including its pages, settings, invite-code handling and the "join room" custom event.
- Personalization has been removed: theme switching, custom homepage, background images, background music and feature hiding. The UI uses a fixed style.
- The "Other" settings page only keeps Download and Community Resources. Accessibility, launcher and debug options, auto-update, announcements, settings import/export, telemetry and the April Fools feature have been removed.
- The "always verify certificates" option has been removed; HTTPS always uses the default .NET certificate validation.

## Minecraft 1.20.1 + Forge

64-bit Java 17 is preferred. The global Java list is ordered by compatibility, and a Java forced in the per-instance settings takes precedence. G1 is on by default; G1 pause-target tuning, heap pre-touch and string deduplication are off by default. Options you saved manually are not reset by upgrades.

On a PC with 24 GB of RAM, start with about 6 GB for a medium mod setup and try 8 GB for heavier modpacks based on actual usage, leaving headroom for Windows and the game's off-heap memory. More memory does not guarantee smoother gameplay.

Animations run on render frames and stop when idle, list viewport checks are batched, the memory settings page stops refreshing when hidden, and mod counts come from a background snapshot.

## Repository layout

```text
SKILL/                          Development conventions
src/Plain Craft Launcher 2/     Main program (VB.NET / WPF)
src/PCLCS/                      C# business models and helpers
src/MeloongCore/                Base library source
src/Tests/                      Smoke tests and performance tool
src/Plain Craft Launcher 2.sln  Solution
src/build.ps1                   Build entry point
README.md                       This file
LICENSE                         YYH Non-Commercial Derivative License
```

## Building

Install the .NET 9 SDK and run from the repository root:

```powershell
& ./src/build.ps1 -Configuration Release -RunTests -LaunchCheck
```

- `-DotnetPath` selects a specific `dotnet.exe`.
- `-RunTests` runs the smoke tests.
- `-LaunchCheck` starts the built EXE in an isolated profile and fails if the window does not appear or the process exits within a few seconds.

The main program targets .NET Framework 4.8; reference assemblies and other NuGet packages are restored automatically. Development tools do not need to be shipped to end users.

The build output is in `src/Plain Craft Launcher 2/bin/`. For distribution, copy the main EXE and rename it to `PCL-YYH.exe`. All project dependencies are embedded in the EXE; only the system .NET Framework and the game's Java are external.

## License

YYH's additions and modifications may be studied, used non-commercially, modified and further developed. **Commercial use without authorization is prohibited.** Redistributions must keep YYH's attribution, a description of the changes and this license, and must not relicense these modifications under terms that allow commercial use. See [LICENSE](LICENSE) for the full terms.

The original PCL source code and third-party components remain under their own licenses. YYH's license does not change their ownership and does not grant any commercial rights to them. Upstream license: [PCL license](https://github.com/Meloong-Git/PCL/blob/5ef81e490ae46b1100d61e33d0183094c7a80ca8/LICENCE); base library license: [MeloongCore license](https://github.com/Meloong-Git/MeloongCore/blob/c774ae4c85fd94e0eaa171db21e78a65870df19b/LICENSE.txt).

This project is not an official Minecraft product and is not affiliated with or endorsed by Mojang or Microsoft.
