@echo off
echo ========================================
echo THMI Mod Manager - Logger System Test
echo ========================================
echo.

REM Check if the application is running
echo [1] Checking if application is running...
curl -s -o nul -w "%%{http_code}" http://localhost:5000 > temp_status.txt
set /p status=<temp_status.txt
del temp_status.txt

if "%status%"=="200" (
    echo Application is running - proceeding with logger tests
    echo.
) else (
    echo ERROR: Application is not running on http://localhost:5000
    echo Please start the application first with: dotnet run
    echo.
    pause
    exit /b 1
)

echo [2] Testing Logger Status API...
curl -s "http://localhost:5000/api/logger/status" | findstr "isActive"
if %errorlevel% equ 0 (
    echo Logger status check: PASSED
) else (
    echo Logger status check: FAILED
)
echo.

echo [3] Testing Info Log...
curl -s "http://localhost:5000/api/logger/test?level=info&message=Test info message from batch script" | findstr "success"
if %errorlevel% equ 0 (
    echo Info log test: PASSED
) else (
    echo Info log test: FAILED
)
echo.

echo [4] Testing Warning Log...
curl -s "http://localhost:5000/api/logger/test?level=warning&message=Test warning message from batch script" | findstr "success"
if %errorlevel% equ 0 (
    echo Warning log test: PASSED
) else (
    echo Warning log test: FAILED
)
echo.

echo [5] Testing Error Log...
curl -s "http://localhost:5000/api/logger/test?level=error&message=Test error message from batch script" | findstr "success"
if %errorlevel% equ 0 (
    echo Error log test: PASSED
) else (
    echo Error log test: FAILED
)
echo.

echo [6] Testing Formatted Log...
curl -s "http://localhost:5000/api/logger/test?level=info&message=User Admin performed settings update at %date% %time%" | findstr "success"
if %errorlevel% equ 0 (
    echo Formatted log test: PASSED
) else (
    echo Formatted log test: FAILED
)
echo.

echo [7] Retrieving Recent Logs...
curl -s "http://localhost:5000/api/logger/logs?lines=10" | findstr "logs"
if %errorlevel% equ 0 (
    echo Log retrieval test: PASSED
) else (
    echo Log retrieval test: FAILED
)
echo.

echo [8] Testing Settings Page Logging...
echo Accessing settings page to trigger logging...
curl -s "http://localhost:5000/Settings" > nul
echo Settings page accessed - check logs for entries
echo.

echo ========================================
echo Logger System Test Summary
echo ========================================
echo.
echo Test pages available:
echo - Logger Test Page: http://localhost:5000/test-logger.html
echo - Settings Page: http://localhost:5000/Settings
echo.
echo To view log file location, visit: http://localhost:5000/api/logger/status
echo.
echo Press any key to exit...
pause > nul