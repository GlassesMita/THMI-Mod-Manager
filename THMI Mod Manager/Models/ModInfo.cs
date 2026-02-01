namespace THMI_Mod_Manager.Models
{
    public class ModInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public uint VersionCode { get; set; }
        public string Author { get; set; } = string.Empty;
        public string UniqueId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ModLink { get; set; }
        public string? UpdateUrl { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime InstallTime { get; set; }
        
        // Property to indicate if the mod is currently disabled
        public bool IsDisabled => FileName.EndsWith(".disabled");
        
        // Update-related properties
        public bool HasUpdateAvailable { get; set; }
        public string? LatestVersion { get; set; }
        public string? DownloadUrl { get; set; }
        public long? FileSizeBytes { get; set; }
    }

    public class UpdateProgress
    {
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }
}
