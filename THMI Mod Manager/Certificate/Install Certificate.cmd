@echo off
chcp 65001 >nul
cls

:: Check if running as administrator
openfiles >nul 2>&1
if %errorlevel% neq 0 (
    echo Please run this script as [Administrator]!
    pause
    exit /b 1
)

:: Interactive input for certificate path (can also directly modify set certPath to specify path)
set /p certPath=Please enter the full path of the certificate file (.cer):

:: Verify if file exists
if not exist "%certPath%" (
    echo Error: Certificate file "%certPath%" does not exist!
    pause
    exit /b 1
)

:: Install certificate to [Local Machine - Trusted Root Certification Authorities] (core command)
echo Installing certificate to trusted root certificate store...
certutil -addstore -f "Root" "%certPath%"

:: Verify installation result
if %errorlevel% equ 0 (
    echo Certificate installed successfully!
) else (
    echo Certificate installation failed, error code: %errorlevel%
)

pause