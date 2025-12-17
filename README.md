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
- Web browser that supports modern web standards (Google Chrome, Microsoft Edge, Mozilla Firefox, etc.)
- Internet connection for downloading mods (optional)

## Certificate Installation

> [!IMPORTANT]
> This application is signed with a self-signed certificate (`Schale.cer`) to establish trust between the application and Windows. Before running the application, you must install this certificate to your computer's Trusted Root Certification Authorities store. Without this step, Windows may block the application from running and show security warnings.

> [!WARNING]
> For security reasons, you should verify the certificate hash before installation to ensure it matches the expected values:
> 
> **Certificate Information:**
> - Name: Schale.cer
> - SHA256: 2318021fc2af5fb3c94defb6806f83078d1d6bc1a2410a99ff906b235b8a04cb
> - SHA1: 176a1dd309270eb8206c8832326c5d67e7d9c483
> - MD5: 7f0f30cdfe08cf0cd55dceb5c2556be3
>
> You can verify the certificate hash using PowerShell:
> ```powershell
> Get-FileHash -Path ".\Certificate\Schale.cer" -Algorithm SHA256
> ```

You can install the certificate using either of the following methods:

### Method 1: Using the Provided Scripts (Recommended)

Navigate to the `Certificate` folder and run one of the provided scripts as Administrator:

**For Command Prompt:**
1. Right-click on `Install Certificate.cmd`
2. Select "Run as administrator"
3. When prompted, enter the full path to the certificate file: `.\Schale.cer`

**For PowerShell:**
1. Right-click on `Install Certificate.ps1`
2. Select "Run with PowerShell"
3. If prompted by User Account Control, click "Yes"
4. When prompted, enter the full path to the certificate file: `.\Schale.cer`

### Method 2: Manual Installation

1. Double-click on `Schale.cer` in the `Certificate` folder
2. Click "Install Certificate..."
3. Select "Local Machine" and click "Next"
4. Select "Place all certificates in the following store"
5. Click "Browse..." and select "Trusted Root Certification Authorities"
6. Click "Next" and then "Finish"
7. Confirm the security warning dialog

After successful installation, you should see a confirmation message. You can now proceed with building and running the application.

## Build from Source

Prerequisites:

- Installed .NET 8.0 SDK from [Microsoft .NET Download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Installed Git from [Git SCM](https://git-scm.com/)
- Installed Visual Studio 2022 and selected ".NET desktop development", "Node.js development" (optional) and "ASP.NET and web development" workloads.

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

2. Build the application via DotNet CLI:

Using Command Prompt or PowerShell:

```bash
dotnet build -configuration Release
```

3. Copy required files: **Localization** and **wwwroot** folder and **AppConfig.Schale** to the build output:

Using Command Prompt:

```bash
# Copy localization files
xcopy /E /I /Y ".\Localization" ".\bin\Release\net8.0\Localization"
            
# Copy web assets
xcopy /E /I /Y ".\wwwroot" ".\bin\Release\net8.0\wwwroot"
            
# Copy configuration file
copy /Y ".\AppConfig.Schale" ".\bin\Release\net8.0\AppConfig.Schale"
``` 

Using PowerShell:

```bash
# Copy localization files
Copy-Item -Path ".\Localization" -Destination ".\bin\Release\net8.0\Localization" -Recurse
            
# Copy web assets
Copy-Item -Path ".\wwwroot" -Destination ".\bin\Release\net8.0\wwwroot" -Recurse
            
# Copy configuration file
Copy-Item -Path ".\AppConfig.Schale" -Destination ".\bin\Release\net8.0\AppConfig.Schale"
```

Or you can copy the files to build output directory manually.

4. Copy build output to game directory:

*Assume the game is installed in `C:\Program Files (x86)\Steam\steamapps\common\Touhou Mystia Izakaya`. This path may vary depending on your installation location, and operating `Program Files` folder may need administrator privileges to copy files.*

*If your game is installed in a different location(e.g. `D:\SteamLibrary\steamapps\common\Touhou Mystia Izakaya`), it may will not need administrator privileges to copy files.*

Using Command Prompt:

```bash
# Copy build output to game directory
xcopy /E /I /Y ".\bin\Release\net8.0" "C:\Program Files (x86)\Steam\steamapps\common\Touhou Mystia Izakaya"
```

Using PowerShell:

```bash
# Copy build output to game directory
Copy-Item -Path ".\bin\Release\net8.0" -Destination "C:\Program Files (x86)\Steam\steamapps\common\Touhou Mystia Izakaya" -Recurse
```

5. Run the application:

Using Command Prompt:

```bash
# Run the application
cd ".\bin\Release\net8.0"
dotnet "THMI Mod Manager.dll"
```

### Development Build

For development, you can also run directly from source, but it may not work well with the Web UI because there is no localization files:

Using Command Prompt:

```bash
# Run the application in development build
git clone https://github.com/GlassesMita/THMI-Mod-Manager.git
cd './THMI-Mod-Manager/THMI Mod Manager'
dotnet run --configuration Debug
```

Using PowerShell:

```bash
# Run the application in development build
git clone https://github.com/GlassesMita/THMI-Mod-Manager.git
Set-Location -Path './THMI-Mod-Manager/THMI Mod Manager'
dotnet run --configuration Debug
```

## Usage

### Launch Game Feature

The mod manager now includes a game launcher with the following capabilities:

- **Start Game**: Click the Launch button to start the game via Steam URL protocol
- **Process Monitoring**: Automatically detects when the game is running
- **Stop Game**: Safely terminate the game process when needed
- **Status Display**: Real-time status of game process (Running/Stopped)

The launcher uses Steam's URL protocol (`steam://rungameid/1584090`) to start the game, ensuring proper Steam integration.
