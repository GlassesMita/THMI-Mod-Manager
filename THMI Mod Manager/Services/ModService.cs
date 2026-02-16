using System.IO.Compression;
using Tommy;
using THMI_Mod_Manager.Models;

namespace THMI_Mod_Manager.Services
{
    public class ModService
    {
        private readonly AppConfigManager _appConfig;

        public ModService(AppConfigManager appConfig)
        {
            _appConfig = appConfig;
        }

        public List<ModInfo> LoadMods()
        {
            var mods = new List<ModInfo>();
            var pluginsPath = GetPluginsPath();

            if (!Directory.Exists(pluginsPath))
            {
                Logger.LogWarning($"BepInEx/plugins directory not found: {pluginsPath}");
                return mods;
            }

            try
            {
                // Get all .dll files (including .disabled files)
                var dllFiles = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly);
                var disabledFiles = Directory.GetFiles(pluginsPath, "*.dll.disabled", SearchOption.TopDirectoryOnly);
                
                // Combine both lists
                var allFiles = dllFiles.Concat(disabledFiles).ToArray();
                
                Logger.LogInfo($"Found {dllFiles.Length} DLL files and {disabledFiles.Length} disabled files in {pluginsPath}");

                foreach (var dllFile in allFiles)
                {
                    var modInfo = ExtractModInfo(dllFile);
                    mods.Add(modInfo);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException($"Error loading mods from {pluginsPath}", ex);
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
                // Remove .disabled suffix first, then remove .dll extension to get the mod folder name
                var modFolderName = Path.GetFileNameWithoutExtension(dllFileName.Replace(".disabled", "", StringComparison.OrdinalIgnoreCase));
                var dllDirectory = Path.GetDirectoryName(dllPath) ?? string.Empty;
                var modFolder = Path.Combine(dllDirectory, modFolderName);
                
                var manifestPath = Path.Combine(modFolder, "Manifest.toml");
                
                if (File.Exists(manifestPath))
                {
                    using var reader = new StreamReader(manifestPath);
                    var manifestData = TOML.Parse(reader);
                    
                    Logger.LogInfo($"Parsed Manifest.toml from {manifestPath}");
                    Logger.LogInfo($"Manifest sections: {string.Join(", ", manifestData.Keys)}");
                    
                    if (manifestData.TryGetNode("Mod", out var modNode) && modNode is TomlTable modSection)
                    {
                        Logger.LogInfo($"Found Mod section with {modSection.ChildrenCount} keys: {string.Join(", ", modSection.Keys)}");
                        
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
                        
                        if (modSection.TryGetNode("UpdateUrl", out var updateUrlNode) && updateUrlNode is TomlString updateUrlString)
                        {
                            modInfo.UpdateUrl = updateUrlString.Value;
                        }
                        
                        modInfo.IsValid = true;
                        Logger.LogInfo($"Successfully extracted mod info from {manifestPath}: {modInfo.Name}");
                    }
                    else
                    {
                        modInfo.ErrorMessage = "Mod section not found in Manifest.toml";
                        Logger.LogWarning($"Mod section not found in {manifestPath}. Available sections: {string.Join(", ", manifestData.Keys)}");
                    }
                }
                else
                {
                    modInfo.ErrorMessage = "Manifest.toml not found";
                    Logger.LogWarning($"Manifest.toml not found at {manifestPath}");
                    
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
                Logger.LogException($"Error extracting mod info from {dllPath}", ex);
                
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
                    Logger.LogInfo($"Using game plugins path: {pluginsPath}");
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
                    Logger.LogInfo($"Using workspace plugins path: {fullPath}");
                    return fullPath;
                }
            }
            
            Logger.LogWarning("BepInEx/plugins directory not found in any location");
            return possiblePaths[0];
        }

        public bool DeleteMod(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.LogInfo($"Successfully deleted mod: {filePath}");
                    return true;
                }
                Logger.LogWarning($"Mod file not found: {filePath}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogException($"Error deleting mod: {filePath}", ex);
                return false;
            }
        }

        public bool ToggleMod(string fileName)
        {
            try
            {
                Logger.LogInfo($"Attempting to toggle mod: {fileName}");
                
                if (string.IsNullOrEmpty(fileName))
                {
                    Logger.LogError("File name is null or empty");
                    return false;
                }

                string? fullPath = FindModFileByName(fileName);
                if (string.IsNullOrEmpty(fullPath))
                {
                    Logger.LogError($"Mod file not found by name: {fileName}");
                    return false;
                }

                var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
                var fileNameOnly = Path.GetFileName(fullPath);
                string newFilePath;

                if (fileNameOnly.EndsWith(".disabled"))
                {
                    newFilePath = Path.Combine(directory, fileNameOnly.Substring(0, fileNameOnly.Length - ".disabled".Length));
                    Logger.LogInfo($"Enabling mod: {fullPath} -> {newFilePath}");
                }
                else
                {
                    newFilePath = Path.Combine(directory, fileNameOnly + ".disabled");
                    Logger.LogInfo($"Disabling mod: {fullPath} -> {newFilePath}");
                }

                File.Move(fullPath, newFilePath);
                Logger.LogInfo($"Successfully toggled mod: {newFilePath}");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogException($"Access denied when toggling mod: {fileName}", ex);
                return false;
            }
            catch (DirectoryNotFoundException ex)
            {
                Logger.LogException($"Directory not found when toggling mod: {fileName}", ex);
                return false;
            }
            catch (IOException ex)
            {
                Logger.LogException($"IO error when toggling mod: {fileName}", ex);
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogException($"Unexpected error toggling mod: {fileName}", ex);
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
                Logger.LogException(ex, $"Error searching for mod file: {fileName}");
            }

            return null; // File not found
        }

        public bool InstallMod(string zipFilePath)
        {
            try
            {
                Logger.LogInfo($"Attempting to install mod from: {zipFilePath}");

                if (!File.Exists(zipFilePath))
                {
                    Logger.LogError($"Zip file not found: {zipFilePath}");
                    return false;
                }

                if (!zipFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogError($"File is not a zip file: {zipFilePath}");
                    return false;
                }

                var pluginsPath = GetPluginsPath();
                if (!Directory.Exists(pluginsPath))
                {
                    Directory.CreateDirectory(pluginsPath);
                    Logger.LogInfo($"Created plugins directory: {pluginsPath}");
                }

                var tempExtractPath = Path.Combine(Path.GetTempPath(), $"THMI_Mod_Install_{Guid.NewGuid()}");
                
                try
                {
                    Directory.CreateDirectory(tempExtractPath);
                    
                    using (var archive = ZipFile.OpenRead(zipFilePath))
                    {
                        archive.ExtractToDirectory(tempExtractPath, true);
                        Logger.LogInfo($"Extracted zip file to: {tempExtractPath}");
                    }

                    var extractedFiles = Directory.GetFiles(tempExtractPath, "*.*", SearchOption.AllDirectories);
                    Logger.LogInfo($"Found {extractedFiles.Length} files in zip archive");

                    var dllFiles = Directory.GetFiles(tempExtractPath, "*.dll", SearchOption.AllDirectories);
                    var manifestFiles = Directory.GetFiles(tempExtractPath, "Manifest.*", SearchOption.AllDirectories);

                    if (dllFiles.Length == 0)
                    {
                        Logger.LogError("No DLL files found in zip archive");
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
                            Logger.LogInfo($"IncludePDB setting: {includePDB}");
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
                            Logger.LogInfo($"Created mod folder: {modFolderDestPath}");
                        }

                        if (File.Exists(dllDestPath))
                        {
                            var backupPath = dllDestPath + ".backup";
                            if (File.Exists(backupPath))
                            {
                                File.Delete(backupPath);
                            }
                            File.Move(dllDestPath, backupPath);
                            Logger.LogInfo($"Backed up existing DLL: {dllDestPath} -> {backupPath}");
                        }

                        File.Copy(dllFile, dllDestPath, true);
                        Logger.LogInfo($"Installed DLL: {dllFile} -> {dllDestPath}");

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
                                Logger.LogInfo($"Backed up existing PDB: {pdbDestPath} -> {backupPath}");
                            }
                            
                            File.Copy(pdbFile, pdbDestPath, true);
                            Logger.LogInfo($"Installed PDB: {pdbFile} -> {pdbDestPath}");
                        }

                        foreach (var manifestFile in manifestFiles)
                        {
                            var manifestFileName = Path.GetFileName(manifestFile);
                            var manifestDestPath = Path.Combine(modFolderDestPath, manifestFileName);
                            File.Copy(manifestFile, manifestDestPath, true);
                            Logger.LogInfo($"Installed manifest: {manifestFile} -> {manifestDestPath}");
                        }
                    }

                    Logger.LogInfo($"Successfully installed mod from: {zipFilePath}");
                    return true;
                }
                finally
                {
                    if (Directory.Exists(tempExtractPath))
                    {
                        try
                        {
                            Directory.Delete(tempExtractPath, true);
                            Logger.LogInfo($"Cleaned up temp directory: {tempExtractPath}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"Failed to clean up temp directory: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"Error installing mod from: {zipFilePath}");
                return false;
            }
        }
    }
}
