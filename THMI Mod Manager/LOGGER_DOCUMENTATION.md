# THMI Mod Manager - Logger System Documentation

## Overview

The Logger system has been successfully integrated into the THMI Mod Manager project. It provides comprehensive logging capabilities for debugging, monitoring, and auditing application behavior.

## Features

### Core Logging Features
- **Multiple Log Levels**: Info, Warning, Error
- **Thread-Safe Operations**: Uses locking mechanism for concurrent access
- **Automatic Log File Management**: Creates log directory and files automatically
- **Timestamp Logging**: All logs include precise timestamps
- **Formatted Messages**: Support for string formatting with parameters
- **Exception Handling**: Robust error handling with fallback mechanisms

### API Integration
- **RESTful API**: Complete API for logger operations
- **Log Testing**: Built-in test endpoints
- **Log Retrieval**: Fetch recent log entries
- **Status Monitoring**: Check logger health and configuration

## Usage

### Basic Logging

```csharp
// Import the logger namespace
using THMI_Mod_Manager.Services;

// Log different levels
Logger.LogInfo("Application started successfully");
Logger.LogWarning("Configuration file not found, using defaults");
Logger.LogError("Failed to connect to database: " + ex.Message);

// Formatted logging
Logger.Log("User {0} logged in at {1}", Logger.LogLevel.Info, username, DateTime.Now);
```

### Integration Examples

The logger has been integrated into the Settings page:

```csharp
public void OnGet()
{
    Logger.LogInfo($"Settings page accessed - RequestId: {RequestId}");
    
    try
    {
        // Load settings with logging
        CurrentLanguage = _appConfig.Get("[Localization]Language", "en_US");
        Logger.LogInfo($"Loaded language settings: {CurrentLanguage}");
    }
    catch (Exception ex)
    {
        Logger.LogError($"Error loading settings: {ex.Message}");
        throw;
    }
}
```

## API Endpoints

### Logger Status
```
GET /api/logger/status
```
Returns logger status and file path information.

### Write Test Log
```
GET /api/logger/test?level=info&message=test message
```
Writes a test log entry.

### Retrieve Logs
```
GET /api/logger/logs?lines=100
```
Retrieves recent log entries (default: 100 lines).

### Clear Logs
```
DELETE /api/logger/clear
```
Clears the log file.

### Write Log (POST)
```
POST /api/logger/log
Content-Type: application/json

{
    "message": "Custom log message",
    "level": "info"
}
```

## File Structure

### Logger Service
- **File**: `Services/Logger.cs`
- **Namespace**: `THMI_Mod_Manager.Services`
- **Class**: `Logger`

### Logger Controller
- **File**: `Controllers/LoggerController.cs`
- **Route**: `/api/logger`
- **Methods**: Status, Test, GetLogs, ClearLogs, WriteLog

### Test Files
- **Test Page**: `wwwroot/test-logger.html`
- **Test Script**: `test-logger.bat`
- **Documentation**: `LOGGER_DOCUMENTATION.md`

## Configuration

### Log File Location
- **Default Path**: `{ApplicationBaseDirectory}/Logs/Latest.Log`
- **Automatic Creation**: Log directory created if not exists
- **File Management**: Existing log file cleared on initialization

### Log Format
```
[yyyy/MM/dd HH:mm:ss][LevelTag] Message
```

Examples:
```
[2024/01/15 14:30:45][I] Application started successfully
[2024/01/15 14:31:02][W] Configuration file not found, using defaults
[2024/01/15 14:31:15][E] Failed to connect to database: Connection timeout
```

## Testing

### Automated Testing
Run the test script to verify logger functionality:
```batch
test-logger.bat
```

### Manual Testing
1. Visit the test page: `http://localhost:5000/test-logger.html`
2. Click different log level buttons
3. Check log output and file contents
4. Test API endpoints

### Integration Testing
1. Access the Settings page to trigger automatic logging
2. Change settings to generate log entries
3. Verify logs are properly recorded

## Error Handling

### Logger Initialization
- Handles missing directories
- Manages file access permissions
- Provides fallback to console output
- Graceful degradation on errors

### Log Writing
- Thread-safe file operations
- Exception handling with console fallback
- Prevents application crashes from logging failures

## Performance Considerations

### Thread Safety
- Uses `lock` statement for concurrent access
- Prevents file corruption from multiple threads
- Maintains log integrity under load

### File Operations
- Efficient file append operations
- Minimal performance impact
- Automatic file management

## Future Enhancements

### Potential Improvements
1. **Log Rotation**: Automatic log file rotation by size/date
2. **Multiple Loggers**: Different loggers for different components
3. **Log Levels Configuration**: Runtime log level configuration
4. **External Log Services**: Integration with external logging services
5. **Log Filtering**: Advanced log filtering and searching
6. **Performance Metrics**: Logging performance statistics

### Configuration Options
1. **Log Level Filtering**: Configurable minimum log level
2. **Output Formats**: Multiple log output formats (JSON, XML, etc.)
3. **Multiple Outputs**: File, console, database outputs
4. **Log Retention**: Configurable log retention policies

## Troubleshooting

### Common Issues

1. **Log File Not Created**
   - Check application permissions
   - Verify directory creation rights
   - Check disk space availability

2. **Logs Not Appearing**
   - Verify logger initialization
   - Check file path permissions
   - Review exception handling

3. **API Endpoints Not Working**
   - Ensure application is running
   - Check route configuration
   - Verify controller registration

### Debug Steps
1. Check logger status: `GET /api/logger/status`
2. Test basic logging: `GET /api/logger/test`
3. Review log file contents
4. Check application logs for errors

## Conclusion

The logger system provides a robust, scalable logging solution for the THMI Mod Manager. It integrates seamlessly with the existing application architecture and provides comprehensive logging capabilities for development, debugging, and production monitoring.

The implementation follows best practices for logging systems, including thread safety, error handling, and performance optimization. The API provides easy access to log management functionality, making it suitable for both development and production environments.