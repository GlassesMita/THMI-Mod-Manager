namespace THMI_Mod_Manager.Models
{
    /// <summary>
    /// Represents information about a mod / 表示模组的信息
    /// </summary>
    public class ModInfo
    {
        /// <summary>Mod name / 模组名称</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Mod version string / 模组版本字符串</summary>
        public string Version { get; set; } = string.Empty;
        /// <summary>Numeric version code for comparison (deprecated, use semantic versioning instead) / 用于比较的数字版本码（已弃用，现使用语义化版本号进行比较）</summary>
        public uint VersionCode { get; set; }
        /// <summary>Mod author / 模组作者</summary>
        public string Author { get; set; } = string.Empty;
        /// <summary>Unique identifier for the mod / 模组的唯一标识符</summary>
        public string UniqueId { get; set; } = string.Empty;
        /// <summary>Mod description / 模组描述</summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>Link to mod page (GitHub, Thunderstore, etc.) / 模组页面链接</summary>
        public string? ModLink { get; set; }
        /// <summary>URL for checking updates / 检查更新的URL</summary>
        public string? UpdateUrl { get; set; }
        /// <summary>Full path to the mod DLL file / 模组DLL文件的完整路径</summary>
        public string FilePath { get; set; } = string.Empty;
        /// <summary>File name of the mod DLL / 模组DLL的文件名</summary>
        public string FileName { get; set; } = string.Empty;
        /// <summary>Size of the mod file in bytes / 模组文件大小（字节）</summary>
        public long FileSize { get; set; }
        /// <summary>Last modification time of the file / 文件最后修改时间</summary>
        public DateTime LastModified { get; set; }
        /// <summary>Whether the mod has a valid manifest / 模组是否有有效的清单文件</summary>
        public bool IsValid { get; set; }
        /// <summary>Error message if any / 如有错误则包含错误信息</summary>
        public string ErrorMessage { get; set; } = string.Empty;
        /// <summary>Time when the mod was installed / 模组安装时间</summary>
        public DateTime InstallTime { get; set; }
        
        /// <summary>
        /// Indicates whether the mod is currently disabled (file ends with .disabled)
        /// / 指示模组当前是否被禁用（文件名以 .disabled 结尾）
        /// </summary>
        public bool IsDisabled => FileName.EndsWith(".disabled");
        
        /// <summary>Whether a newer version is available / 是否有更新版本可用</summary>
        public bool HasUpdateAvailable { get; set; }
        /// <summary>Latest version string from remote / 远程的最新版本字符串</summary>
        public string? LatestVersion { get; set; }
        /// <summary>Download URL for the latest version / 最新版本下载链接</summary>
        public string? DownloadUrl { get; set; }
        /// <summary>File size of the update in bytes / 更新的文件大小（字节）</summary>
        public long? FileSizeBytes { get; set; }
        /// <summary>Release notes for the latest version / 最新版本的发布说明</summary>
        public string? ReleaseNotes { get; set; }
        /// <summary>URL to the changelog / 更新日志URL</summary>
        public string? ChangelogUrl { get; set; }
        /// <summary>URL to the GitHub release page / GitHub发布页面URL</summary>
        public string? ReleaseHtmlUrl { get; set; }
    }

    /// <summary>
    /// Represents update progress for a mod download / 表示模组下载的更新进度
    /// </summary>
    public class UpdateProgress
    {
        /// <summary>Number of bytes downloaded / 已下载字节数</summary>
        public long BytesDownloaded { get; set; }
        /// <summary>Total bytes to download / 总下载字节数</summary>
        public long TotalBytes { get; set; }
        /// <summary>Current status message / 当前状态消息</summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>Last update time / 最后更新时间</summary>
        public DateTime LastUpdated { get; set; }
    }
}
