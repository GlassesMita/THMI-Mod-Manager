# Touhou Mystia Izakaya Mod Manager

![License](https://img.shields.io/badge/license-GPLv3-red.svg?style=flat-square)
![Language](https://img.shields.io/badge/Language-C%23-cf6fff?style=flat-square)
![Framework](https://img.shields.io/badge/Framework-.NET%2010%20WPF-9fa0db?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%20x64-00b600?style=flat-square)

**This project is licensed under the GNU General Public License Version 3.0**

> [!WARNING]
> **Disclaimer**: This software is provided "AS IS" without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose, and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages, or other liability, whether in an action of contract, tort, or otherwise, arising from, out of, or in connection with the software or the use or other dealings in the software. This project has no affiliation with and is not endorsed by the original game developer (Dichroic Purplion/二色幽紫蝶).
>
> **Free Release**: This project is released for free of charge. If you have paid to obtain this software, please request an immediate refund and leave a negative review for the seller. This software is an independent third-party tool.
>
> This mod manager is designed for **legal use only** with a legitimate copy of Touhou Mystia Izakaya. Using this software implies your agreement to use it only with legally purchased games and to respect intellectual property rights.

A native Windows desktop mod manager for Touhou Mystia Izakaya, built as a WPF application on .NET 10.

## Features

> [!NOTE]
> This is an early development version. Features may change without notice. Use at your own risk.

- 🖥️ **Native WPF Desktop UI** — built on .NET 10 with WPF-UI 4.3.0 (Fluent style). Fully local, no browser or web server involved
- 🎮 **Game Launcher** — launch the game via Steam URL protocol (`steam://rungameid/1584090`) or an external program, with live process monitoring and session timer
- 📦 **Mod Management** — browse, enable, disable, delete and install mods from the `BepInEx/plugins` directory (ZIP install supported)
- ⚠️ **Conflict Detection** — based on `UniqueID` / `IncompatibleWith` fields in `Manifest.toml`; conflicts prompt for confirmation with force-enable option
- 🌐 **Multi-language Support** — localization via INI files, with per-language Home/About markdown resources
- 🎨 **Theme Switching** — light / dark / follow system, with independent accent color
- 📝 **Log Viewer** — built-in viewer for `Logs/Latest.Log`; unhandled exceptions are auto-dumped to KernelPanic logs
- ⚙️ **BepInEx Config Editor** — edit common `BepInEx.cfg` entries directly in the UI
- 🔄 **Update Checks** — app updates via GitHub API, mod updates via online version comparison
- 💾 **Hot-reload Settings** — settings are saved to `AppConfig.Schale` (INI) and applied immediately

## Requirements

- .NET 10.0 SDK (build only; .NET 10 Desktop Runtime required at runtime)
- Windows 10 x64 22H2 or later (may not work well on older Windows versions)
- A legal copy of the game Touhou Mystia Izakaya
- Stable Internet connection for downloading mods (optional)

**If you insist on running on versions below Windows 10 x64, you must add `DOTNET_EnableWriteXorExecute=0` and `DOTNET_GCName=clrgc.dll` to your system environment variables, then restart the system. See [this issue](https://github.com/dotnet/runtime/issues/79469#issuecomment-1371202114) for more details.**

## Build from Source

### Prerequisites

1. Install the .NET 10.0 SDK

- Manual download: [Microsoft .NET Download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Or via WinGet:

```bash
winget install --id Microsoft.DotNet.SDK.10 --source winget
```

> Requires an administrator PowerShell, or use `sudo winget install ...` if the Windows Sudo feature is enabled.

2. Install Git from [Git SCM](https://git-scm.com/)

```bash
winget install --id Git.Git --source winget
```

3. Install a development environment

- **Visual Studio 2022**: select the ".NET desktop development" workload
- **VS Code / Trae**: install the "C# Dev Kit", "C#", "IntelliCode for C# Dev Kit" and ".NET Install Tool" extensions

### Build Steps

1. Clone the repository and enter the project folder:

```bash
git clone https://github.com/GlassesMita/THMI-Mod-Manager.git
cd './THMI-Mod-Manager/THMI Mod Manager'
```

2. Build the application:

```bash
dotnet build --configuration Release --no-incremental
```

To build directly into the game directory (specify the game install path via `-p:GameDir="path"`, or use `-p:BuildToGameDir=true` to use the default path configured in the csproj):

```bash
dotnet build --configuration Release --no-incremental -p:BuildToGameDir=true
```

3. Publish an optimized release:

```bash
dotnet publish --configuration Release
```

Run the generated desktop application from:

```powershell
& ".\bin\Release\net10.0-windows\publish\THMI Mod Manager.exe"
```

*Note: Use `--output <path>` to specify an output directory. Use `-p:SelfContained=true` to include the .NET runtime (no runtime install needed, but larger output). The build automatically copies localization files, theme resources and configuration files to the output directory.*

## Usage

### Main Window

The app uses a sidebar navigation: **Main Menu** (Home / Mods / Explore) and **Manage** (Settings / About). The bottom of the sidebar shows Steam status and a quick launch button; the status bar at the bottom of the content area shows current operation messages.

### Home

Shows three metric cards (game status, game files present, session timer) and a launcher card with Start / Stop. If `Touhou Mystia Izakaya.exe` is not found next to the app, a warning suggests deploying to the game directory or configuring an external launcher in Settings.

### Mods

- Toolbar: **Check Updates**, **Install ZIP**, **Refresh**, and a sort combo (name / install date)
- Each mod card supports **Enable / Disable / Delete** directly (disabled while the game is running)
- Conflict detection reads `Manifest.toml`; enabling a conflicting mod prompts for confirmation and supports force-enable
- Click a mod card to expand details (ID, version, incompatible list)

### Explore

Planned mod browsing / download station (under development, placeholder page).

### Settings

- **Language & Region**: switch UI language (applies immediately)
- **Appearance**: light / dark / follow system theme
- **Launch Settings**: Steam launch or external program (executable path required)
- **Updates**: auto-check toggle and frequency (startup / weekly / monthly)
- **Notifications**: enable or disable update and event notifications
- **Window Title**: add a "Modded" prefix to the game window title
- **BepInEx Config**: pick a `BepInEx.cfg` and edit common entries directly

Settings are hot-reloaded on save and written to `AppConfig.Schale` in the app directory.

### Logs

The built-in log viewer shows `Logs/Latest.Log`; unhandled exceptions are automatically dumped to `KernelPanic_{yyyyMMdd_HHmmss}.log`. Right-click the About nav item to open the Windows system About dialog.
