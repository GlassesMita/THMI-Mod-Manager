# <ruby title="You Known So Much">Tou<rt>T</rt>hou<rt>H</rt> Mystia<rt>M</rt> Izakaya<rt>I</rt></ruby> Mod Manager

**This project using GNU General Public License Version 3.0 or later**

A mod manager for Touhou Mystia Izakaya game.

## Features

> [!NOTE]
> This is an early development version. Features may change without notice.
> Use at your own risk.

- Web UI based on ASP.NET Core
- 🎮 **Launch Game** - Start and stop the game with process monitoring
- 📦 **Mod Management** (In Development) - Install and uninstall mods with a single step
- 🔧 **Mod Compatibility** (In Development) - Check mod compatibility & conflicts
- 🌐 **Multi-language Support** - Localization system with multiple language packs

## Requirements

- .NET 8.0 SDK or later
- Windows 10 or later (may not work well on Windows 10 or earlier Windows)
- A game installation copy
- Web browser that supports modern web standards (Google Chrome, Microsoft Edge, etc.)
- Internet connection for downloading mods (optional)

## Build from Source

Prerequisites:

- Installed .NET 8.0 SDK from [Microsoft .NET Download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Installed Git from [Git SCM](https://git-scm.com/)
- Installed Visual Studio 2022 and selected ".NET desktop development", "Node.js development" (optional) and "ASP.NET and web development" workloads.

1. Clone the repository:

    ```pwsh
    git clone https://github.com/GlassesMita/THMI-Mod-Manager.git
    cd THMI-Mod-Manager
    dotnet build
    ```

2. Run the application:

    ```pwsh
    dotnet run
    ```

## Usage

### Launch Game Feature

The mod manager now includes a game launcher with the following capabilities:

- **Start Game**: Click the Launch button to start the game via Steam URL protocol
- **Process Monitoring**: Automatically detects when the game is running
- **Stop Game**: Safely terminate the game process when needed
- **Status Display**: Real-time status of game process (Running/Stopped)

The launcher uses Steam's URL protocol (`steam://rungameid/1584090`) to start the game, ensuring proper Steam integration.
