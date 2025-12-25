# THMI Mod Manager Administrator Privilege Launcher
# This script will attempt to start the program with administrator privileges

Write-Host "THMI Mod Manager - Administrator Privilege Launcher" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Check if running with administrator privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if ($isAdmin) {
    Write-Host "Already has administrator privileges, starting program..." -ForegroundColor Green
} else {
    Write-Host "Administrator privileges required for normal operation." -ForegroundColor Yellow
    Write-Host "Requesting privilege elevation..." -ForegroundColor Yellow
    Write-Host ""
    
    # Restart itself with administrator privileges
    try {
        $processInfo = New-Object System.Diagnostics.ProcessStartInfo
        $processInfo.FileName = "powershell.exe"
        $processInfo.Arguments = "-ExecutionPolicy Bypass -File `"$PSCommandPath`""
        $processInfo.Verb = "runas"  # Request administrator privileges
        $processInfo.UseShellExecute = $true
        
        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $processInfo
        
        if ($process.Start()) {
            Write-Host "Privilege elevation request sent." -ForegroundColor Green
            Write-Host "If you see a UAC prompt, please click 'Yes'." -ForegroundColor Green
            exit 0
        }
    }
    catch {
        Write-Host "Privilege elevation failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Please run this program manually as administrator." -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
}

# Set working directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

Write-Host "Working directory: $scriptPath" -ForegroundColor Gray

# Check if dotnet is available
try {
    $dotnetVersion = dotnet --version
    Write-Host "Found .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host ".NET SDK not found!" -ForegroundColor Red
    Write-Host "Please ensure .NET 8.0 SDK is installed" -ForegroundColor Red
    Write-Host "Download: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host ""
Write-Host "Starting THMI Mod Manager..." -ForegroundColor Cyan
Write-Host ""

# Run the program
try {
    dotnet run --project "$scriptPath\THMI Mod Manager\THMI Mod Manager.csproj"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Startup failed! Error code: $LASTEXITCODE" -ForegroundColor Red
        
        # Provide additional troubleshooting information
        Write-Host ""
        Write-Host "Troubleshooting suggestions:" -ForegroundColor Yellow
        Write-Host "1. Ensure the game is properly installed" -ForegroundColor Yellow
        Write-Host "2. Check if Steam is running" -ForegroundColor Yellow
        Write-Host "3. Verify game file integrity" -ForegroundColor Yellow
        Write-Host "4. Check if antivirus software is blocking the program" -ForegroundColor Yellow
        Write-Host ""
        
        Read-Host "Press Enter to exit"
        exit $LASTEXITCODE
    }
}
catch {
    Write-Host "Error occurred: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}