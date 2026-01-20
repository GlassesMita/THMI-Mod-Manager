using System.IO.Compression;
using Tommy;
using THMI_Mod_Manager.Models;

namespace THMI_Mod_Manager.Services
{
    public class ModService
    {
        private readonly ILogger<ModService> _logger;
        private readonly AppConfigManager _appConfig;

        public ModService(ILogger<ModService> logger, AppConfigManager appConfig)
        {
            _logger = logger;
            _appConfig = appConfig;
        }

        public List<ModInfo> LoadMods()
        {
            var mods = new List<ModInfo>();
            var pluginsPath = GetPluginsPath();

            if (!Directory.Exists(pluginsPath))
            {
                _logger.LogWarning($"BepInEx/plugins directory not found: {pluginsPath}");
                return mods;
            }

            try
            {
                // Get all .dll files (including .disabled files)
                var dllFiles = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly);
                var disabledFiles = Directory.GetFiles(pluginsPath, "*.dll.disabled", SearchOption.TopDirectoryOnly);
                
                // Combine both lists
                var allFiles = dllFiles.Concat(disabledFiles).ToArray();
                
                _logger.LogInformation($"Found {dllFiles.Length} DLL files and {disabledFiles.Length} disabled files in {pluginsPath}");

                foreach (var dllFile in allFiles)
                {
                    var modInfo = ExtractModInfo(dllFile);
                    mods.Add(modInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading mods from {pluginsPath}");
            }

            return mods;
        }

        public ModInfo ExtractModInfo(string dllPath)
        {
            var modInfo = new ModInfo
            {
                FilePath = dllPath,
                FileName = Path.GetFileName(dllPath),
                FileSize = new FileInfo(dllPath).Length,
                LastModified = File.GetLastWriteTime(dllPath),
                InstallTime = File.GetCreationTime(dllPath),
                IsValid = false
            };

            try
            {
                var dllFileName = Path.GetFileNameWithoutExtension(dllPath);
                var dllDirectory = Path.GetDirectoryName(dllPath) ?? string.Empty;
                var modFolder = Path.Combine(dllDirectory, dllFileName);
                
                var manifestPath = Path.Combine(modFolder, "Manifest.toml");
                
                if (File.Exists(manifestPath))
                {
                    using var reader = new StreamReader(manifestPath);
                    var manifestData = TOML.Parse(reader);
                    
                    _logger.LogInformation($"Parsed Manifest.toml from {manifestPath}");
                    _logger.LogInformation($"Manifest sections: {string.Join(", ", manifestData.Keys)}");
                    
                    if (manifestData.TryGetNode("Mod", out var modNode) && modNode is TomlTable modSection)
                    {
                        _logger.LogInformation($"Found Mod section with {modSection.ChildrenCount} keys: {string.Join(", ", modSection.Keys)}");
                        
                        if (modSection.TryGetNode("Name", out var nameNode) && nameNode is TomlString nameString)
                        {
                            modInfo.Name = nameString.Value;
                        }
                        
                        if (modSection.TryGetNode("Version", out var versionNode) && versionNode is TomlString versionString)
                        {
                            modInfo.Version = versionString.Value;
                        }
                        
                        if (modSection.TryGetNode("VersionCode", out var versionCodeNode) && versionCodeNode is TomlInteger versionCodeInt)
                        {
                            modInfo.VersionCode = (uint)versionCodeInt.Value;
                        }
                        
                        if (modSection.TryGetNode("Author", out var authorNode) && authorNode is TomlString authorString)
                        {
                            modInfo.Author = authorString.Value;
                        }
                        
                        if (modSection.TryGetNode("UniqueId", out var uniqueIdNode) && uniqueIdNode is TomlString uniqueIdString)
                        {
                            modInfo.UniqueId = uniqueIdString.Value;
                        }
                        
                        if (modSection.TryGetNode("Description", out var descriptionNode) && descriptionNode is TomlString descriptionString)
                        {
                            modInfo.Description = descriptionString.Value;
                        }
                        
                        if (modSection.TryGetNode("Link", out var linkNode) && linkNode is TomlString linkString)
                        {
                            modInfo.ModLink = linkString.Value;
                        }
                        
                        modInfo.IsValid = true;
                        _logger.LogInformation($"Successfully extracted mod info from {manifestPath}: {modInfo.Name}");
                    }
                    else
                    {
                        modInfo.ErrorMessage = "Mod section not found in Manifest.toml";
                        _logger.LogWarning($"Mod section not found in {manifestPath}. Available sections: {string.Join(", ", manifestData.Keys)}");
                    }
                }
                else
                {
                    modInfo.ErrorMessage = "Manifest.toml not found";
                    _logger.LogWarning($"Manifest.toml not found at {manifestPath}");
                    
                    var fileName = Path.GetFileNameWithoutExtension(dllPath);
                    if (fileName.EndsWith(".dll"))
                    {
                        fileName = Path.GetFileNameWithoutExtension(fileName);
                    }
                    
                    modInfo.Name = fileName;
                }
            }
            catch (Exception ex)
            {
                modInfo.ErrorMessage = $"Error reading Manifest.toml: {ex.Message}";
                _logger.LogError(ex, $"Error extracting mod info from {dllPath}");
                
                var fileName = Path.GetFileNameWithoutExtension(dllPath);
                if (fileName.EndsWith(".dll"))
                {
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                }
                
                modInfo.Name = fileName;
            }

            return modInfo;
        }

        private string GetPluginsPath()
        {
            var gamePath = _appConfig.Get("[Game]GamePath", "");
            
            if (!string.IsNullOrEmpty(gamePath))
            {
                var pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
                if (Directory.Exists(pluginsPath))
                {
                    _logger.LogInformation($"Using game plugins path: {pluginsPath}");
                    return pluginsPath;
                }
            }

            var possiblePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "BepInEx", "plugins"),
                Path.Combine(Directory.GetCurrentDirectory(), "BepInEx", "plugins"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "BepInEx", "plugins"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "BepInEx", "plugins"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "BepInEx", "plugins"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BepInEx", "plugins")
            };

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    _logger.LogInformation($"Using workspace plugins path: {fullPath}");
                    return fullPath;
                }
            }
            
            _logger.LogWarning("BepInEx/plugins directory not found in any location");
            return possiblePaths[0];
        }

        public bool DeleteMod(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation($"Successfully deleted mod: {filePath}");
                    return true;
                }
                _logger.LogWarning($"Mod file not found: {filePath}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting mod: {filePath}");
                return false;
            }
        }

        public bool ToggleMod(string fileName)
        {
            try
            {
                _logger.LogInformation($"Attempting to toggle mod: {fileName}");
                
                if (string.IsNullOrEmpty(fileName))
                {
                    _logger.LogError("File name is null or empty");
                    return false;
                }

                string? fullPath = FindModFileByName(fileName);
                if (string.IsNullOrEmpty(fullPath))
                {
                    _logger.LogError($"Mod file not found by name: {fileName}");
                    return false;
                }

                var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
                var fileNameOnly = Path.GetFileName(fullPath);
                string newFilePath;

                if (fileNameOnly.EndsWith(".disabled"))
                {
                    newFilePath = Path.Combine(directory, fileNameOnly.Substring(0, fileNameOnly.Length - ".disabled".Length));
                    _logger.LogInformation($"Enabling mod: {fullPath} -> {newFilePath}");
                }
                else
                {
                    newFilePath = Path.Combine(directory, fileNameOnly + ".disabled");
                    _logger.LogInformation($"Disabling mod: {fullPath} -> {newFilePath}");
                }

                File.Move(fullPath, newFilePath);
                _logger.LogInformation($"Successfully toggled mod: {newFilePath}");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, $"Access denied when toggling mod: {fileName}");
                return false;
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(ex, $"Directory not found when toggling mod: {fileName}");
                return false;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"IO error when toggling mod: {fileName}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error toggling mod: {fileName}");
                return false;
            }
        }

        private string? FindModFileByName(string fileName)
        {
            var pluginsPath = GetPluginsPath();

            // First, try to find the exact file name in the plugins directory
            string fullPath = Path.Combine(pluginsPath, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            // If not found, check if it's a disabled file and try to find the enabled version
            if (fileName.EndsWith(".disabled"))
            {
                string enabledName = fileName.Substring(0, fileName.Length - ".disabled".Length);
                string enabledPath = Path.Combine(pluginsPath, enabledName);
                if (File.Exists(enabledPath))
                {
                    return enabledPath;
                }
            }
            else
            {
                // If it's an enabled file, try to find the disabled version
                string disabledName = fileName + ".disabled";
                string disabledPath = Path.Combine(pluginsPath, disabledName);
                if (File.Exists(disabledPath))
                {
                    return disabledPath;
                }
            }

            // If not found in plugins directory, search in subdirectories
            try
            {
                var allFiles = Directory.GetFiles(pluginsPath, fileName, SearchOption.AllDirectories);
                if (allFiles.Length > 0)
                {
                    return allFiles[0]; // Return the first match
                }

                // If still not found, try searching for both enabled and disabled versions
                if (fileName.EndsWith(".disabled"))
                {
                    var enabledFiles = Directory.GetFiles(pluginsPath, fileName.Substring(0, fileName.Length - ".disabled".Length), SearchOption.TopDirectoryOnly);
                    if (enabledFiles.Length > 0)
                    {
                        return enabledFiles[0];
                    }
                }
                else
                {
                    var disabledFiles = Directory.GetFiles(pluginsPath, fileName + ".disabled", SearchOption.TopDirectoryOnly);
                    if (disabledFiles.Length > 0)
                    {
                        return disabledFiles[0];
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for mod file: {fileName}");
            }

            return null; // File not found
        }

        public bool InstallMod(string zipFilePath)
        {
            try
            {
                _logger.LogInformation($"Attempting to install mod from: {zipFilePath}");

                if (!File.Exists(zipFilePath))
                {
                    _logger.LogError($"Zip file not found: {zipFilePath}");
                    return false;
                }

                if (!zipFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError($"File is not a zip file: {zipFilePath}");
                    return false;
                }

                var pluginsPath = GetPluginsPath();
                if (!Directory.Exists(pluginsPath))
                {
                    Directory.CreateDirectory(pluginsPath);
                    _logger.LogInformation($"Created plugins directory: {pluginsPath}");
                }

                var tempExtractPath = Path.Combine(Path.GetTempPath(), $"THMI_Mod_Install_{Guid.NewGuid()}");
                
                try
                {
                    Directory.CreateDirectory(tempExtractPath);
                    
                    using (var archive = ZipFile.OpenRead(zipFilePath))
                    {
                        archive.ExtractToDirectory(tempExtractPath, true);
                        _logger.LogInformation($"Extracted zip file to: {tempExtractPath}");
                    }

                    var extractedFiles = Directory.GetFiles(tempExtractPath, "*.*", SearchOption.AllDirectories);
                    _logger.LogInformation($"Found {extractedFiles.Length} files in zip archive");

                    var dllFiles = Directory.GetFiles(tempExtractPath, "*.dll", SearchOption.AllDirectories);
                    var manifestFiles = Directory.GetFiles(tempExtractPath, "Manifest.*", SearchOption.AllDirectories);

                    if (dllFiles.Length == 0)
                    {
                        _logger.LogError("No DLL files found in zip archive");
                        return false;
                    }

                    bool includePDB = false;
                    if (manifestFiles.Length > 0)
                    {
                        using var reader = new StreamReader(manifestFiles[0]);
                        var manifestData = TOML.Parse(reader);
                        
                        if (manifestData.TryGetNode("Mod", out var modNode) && modNode is TomlTable modSection && 
                            modSection.TryGetNode("IncludePDB", out var includePDBNode) && includePDBNode is TomlBoolean includePDBBool)
                        {
                            includePDB = includePDBBool.Value;
                            _logger.LogInformation($"IncludePDB setting: {includePDB}");
                        }
                    }

                    foreach (var dllFile in dllFiles)
                    {
                        var dllFileName = Path.GetFileNameWithoutExtension(dllFile);
                        var dllFileNameWithExt = Path.GetFileName(dllFile);
                        var pdbFileName = dllFileName + ".pdb";
                        
                        var dllDestPath = Path.Combine(pluginsPath, dllFileNameWithExt);
                        var modFolderDestPath = Path.Combine(pluginsPath, dllFileName);
                        
                        if (!Directory.Exists(modFolderDestPath))
                        {
                            Directory.CreateDirectory(modFolderDestPath);
                            _logger.LogInformation($"Created mod folder: {modFolderDestPath}");
                        }

                        if (File.Exists(dllDestPath))
                        {
                            var backupPath = dllDestPath + ".backup";
                            if (File.Exists(backupPath))
                            {
                                File.Delete(backupPath);
                            }
                            File.Move(dllDestPath, backupPath);
                            _logger.LogInformation($"Backed up existing DLL: {dllDestPath} -> {backupPath}");
                        }

                        File.Copy(dllFile, dllDestPath, true);
                        _logger.LogInformation($"Installed DLL: {dllFile} -> {dllDestPath}");

                        var pdbFile = Path.Combine(Path.GetDirectoryName(dllFile) ?? tempExtractPath, pdbFileName);
                        if (includePDB && File.Exists(pdbFile))
                        {
                            var pdbDestPath = Path.Combine(pluginsPath, pdbFileName);
                            
                            if (File.Exists(pdbDestPath))
                            {
                                var backupPath = pdbDestPath + ".backup";
                                if (File.Exists(backupPath))
                                {
                                    File.Delete(backupPath);
                                }
                                File.Move(pdbDestPath, backupPath);
                                _logger.LogInformation($"Backed up existing PDB: {pdbDestPath} -> {backupPath}");
                            }
                            
                            File.Copy(pdbFile, pdbDestPath, true);
                            _logger.LogInformation($"Installed PDB: {pdbFile} -> {pdbDestPath}");
                        }

                        foreach (var manifestFile in manifestFiles)
                        {
                            var manifestFileName = Path.GetFileName(manifestFile);
                            var manifestDestPath = Path.Combine(modFolderDestPath, manifestFileName);
                            File.Copy(manifestFile, manifestDestPath, true);
                            _logger.LogInformation($"Installed manifest: {manifestFile} -> {manifestDestPath}");
                        }
                    }

                    _logger.LogInformation($"Successfully installed mod from: {zipFilePath}");
                    return true;
                }
                finally
                {
                    if (Directory.Exists(tempExtractPath))
                    {
                        try
                        {
                            Directory.Delete(tempExtractPath, true);
                            _logger.LogInformation($"Cleaned up temp directory: {tempExtractPath}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to clean up temp directory: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error installing mod from: {zipFilePath}");
                return false;
            }
        }
    }
}
