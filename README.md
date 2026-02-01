# <ruby title="You Known So Much">Tou<rt>T</rt>hou<rt>H</rt> Mystia<rt>M</rt> Izakaya<rt>I</rt></ruby> Mod Manager

![License](https://img.shields.io/badge/license-GPLv3-red.svg?style=flat-square)
![Language](https://img.shields.io/badge/Language-C%23-cf6fff?style=flat-square)
![Framework](https://img.shields.io/badge/Framework-ASP.NET-9fa0db?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows%20x64-00b600?style=flat-square)
![Editor](https://img.shields.io/badge/Editor-Trae-1af000?style=flat-square)


**This project using GNU General Public License Version 3.0**

A mod manager for Touhou Mystia Izakaya game.

[DeepWiki Document (English Only)](https://deepwiki.com/GlassesMita/THMI-Mod-Manager/)

## Features

> [!NOTE]
> This is an early development version. Features may change without notice.
> Use at your own risk.

- Web UI based on ASP.NET Core
- 🎮 **Launch Game** - Start and stop the game with process monitoring
- 📦 **Mod Management** (In Development) - Install and uninstall mods with a single step
- 🔧 **Mod Compatibility** (In Development) - Check mod compatibility & conflicts
- 🌐 **Multi-language Support** - Localization system with multiple language packs
  - Supports both **.ini** and **.toml** formats for localization files
  - Automatic file format detection and parsing

## Requirements

- .NET 10.0 SDK
- Windows 10 x64 22H2 or later (may not work well on Windows 10 or earlier Windows)
- A legal game installation copy
- Web browser that supports modern web standards (Google Chrome, Microsoft Edge, Mozilla Firefox, etc.)
- Stable Internet connection for downloading mods (optional) *(Note: Some high school may block Github via school firewall, you can try to use proxy to download mods.)*

## Build from Source

### Prerequisites:

1. Install .NET 10.0 SDK

- Manual download and installation from [Microsoft .NET Download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Using WinGet (Windows Package Manager) command

```bash
winget install --id Microsoft.DotNet.SDK.10 --source winget
```

- *Note: you need to run PowerShell as administrator to install .NET SDK using WinGet. If you did not run PowerShell as administrator, you will need to approve the UAC dialog during installation. If you enabled Sudo in Windows Settings, you can run the command line and it will prompt you for administrator permission at Enter key pressed. This feature is very useful when you need to install multiple applications using WinGet at once:*

```bash
sudo winget install --id Microsoft.DotNet.SDK.10 --source winget
```

2. Install Git from [Git SCM](https://git-scm.com/)

- Manual download and installation from [Git SCM](https://git-scm.com/)
- Using WinGet command (Windows Package Manager)

```bash
winget install --id Git.Git --source winget
```

- *Note: you need to run PowerShell as administrator to install Git using WinGet. If you did not run PowerShell as administrator, you will need to approve the UAC dialog during installation. If you enabled Sudo in Windows Settings, you can run the command line bottom in normal user privilege.*

```bash
sudo winget install --id Git.Git --source winget
```

3. Install Development Environment

- (If you prefer Visual Studio 2022) Install Visual Studio 2022 and selected ".NET desktop development", "Node.js development" (optional) and "ASP.NET and web development" workloads.
- (If you prefer Visual Studio Code) Install Visual Studio Code and install "C# Dev Kit", "C#", "IntelliCode for C# Dev Kit", ".NET Install Tool" extensions.
- (If you prefer Trae) Install Trae and open Trae Settings, then click "General" tab, and click Preferences -> "Go to Settings", search "Market", then input `https://marketplace.visualstudio.com/` in Application Extension Market Url config section, and restart Trae. After reboot, install "C# Dev Kit", "C#", "IntelliCode for C# Dev Kit", ".NET Install Tool" extensions.

### Build Steps:

1. Clone the repository:

Using Command Prompt:

```bash
git clone https://github.com/GlassesMita/THMI-Mod-Manager.git
cd './THMI-Mod-Manager/THMI Mod Manager'
```

Using PowerShell:

```bash
git clone https://github.com/GlassesMita/THMI-Mod-Manager.git
Set-Location -Path './THMI-Mod-Manager/THMI Mod Manager'
```

2. Open `THMI Mod Manager.csproj`, and modify the `<OutDir>` property to your legal Touhou Mystia Izakaya game installation directory.

3. Build the application via DotNet CLI:

- Using this method, the program depends on .NET 10 SDK or Runtime to run.

Using Command Prompt or PowerShell:

```bash
dotnet build --configuration Release --no-incremental
```

- Using this method, the program will be optimized for performance.

Using Command Prompt or PowerShell:

```bash
dotnet publish --configuration Release
```

*Note: You can use `--output <path>` to specify the output directory. If you use -p:SelfContained=true option, the output directory will contain all dependencies, no need to install .NET Runtime.*

The build process automatically copies localization files, web assets, and configuration files to the output directory.

## Usage

### Launch Game Feature

The mod manager now includes a game launcher with the following capabilities:

- **Start Game**: Click the Launch button to start the game via Steam URL protocol
- **Process Monitoring**: Automatically detects when the game is running
- **Stop Game**: Safely terminate the game process when needed
- **Status Display**: Real-time status of game process (Running/Stopped)

The launcher uses Steam's URL protocol (`steam://rungameid/1584090`) to start the game, ensuring proper Steam integration.
