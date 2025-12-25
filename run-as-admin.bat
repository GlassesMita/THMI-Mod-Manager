@echo off
echo THMI Mod Manager - Administrator Privilege Launcher
echo =====================================
echo.

:: Check if running with administrator privileges
net session >nul 2>&1
if %errorLevel% == 0 (
    echo Already has administrator privileges, starting program...
    goto :launch
) else (
    echo Administrator privileges required for normal operation.
    echo Requesting privilege elevation...
    echo.
)

:: Request administrator privileges
powershell -Command "Start-Process '%~f0' -Verb RunAs"
if %errorLevel% == 0 (
    echo Privilege elevation request sent.
    echo If you see a UAC prompt, please click 'Yes'.
) else (
    echo Privilege elevation failed.
    echo Please run this program manually as administrator.
)
exit /b

:launch
echo Starting THMI Mod Manager...
echo.

:: Set dotnet path (if installed)
where dotnet >nul 2>&1
if %errorLevel% == 0 (
    echo Using system installed dotnet...
    dotnet run --project "%~dp0THMI Mod Manager\THMI Mod Manager.csproj"
) else (
    echo dotnet not found, please ensure .NET 8.0 SDK is installed
    echo Download: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

if %errorLevel% neq 0 (
    echo.
    echo Startup failed!
    echo Error code: %errorLevel%
    pause
)

exit /b %errorLevel%