using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace THMI_Mod_Manager.Services;

/// <summary>BepInEx IL2CPP 安装状态：齐全 / 缺少 ModInjector / 未安装。</summary>
public enum BepInExInstallState
{
    /// <summary>IL2CPP 核心程序集与 ModInjector（winhttp.dll + doorstop_config.ini）齐全。</summary>
    Complete,
    /// <summary>已检测到 BepInEx IL2CPP 核心，但缺少 ModInjector，Plugin DLL 无法被注入游戏。</summary>
    InjectorMissing,
    /// <summary>未检测到 BepInEx IL2CPP。</summary>
    Missing,
}

/// <summary>本机 BepInEx IL2CPP 探测结果。</summary>
public sealed class BepInExDetectionResult
{
    public BepInExInstallState State { get; init; }
    public bool HasCore { get; init; }
    public bool HasWinHttp { get; init; }
    public bool HasDoorstopConfig { get; init; }
    /// <summary>从 BepInEx.Core.dll 读取的已安装版本号（读取失败时为 null）。</summary>
    public string? InstalledVersion { get; init; }
    /// <summary>目标游戏可执行文件的架构（win-x64 / win-x86），决定下载哪种 BepInEx 包。</summary>
    public string Architecture { get; init; } = "win-x64";
}

/// <summary>BepInEx IL2CPP 安装阶段：探测 / 下载 / 解压安装 / 完成。</summary>
public enum BepInExInstallPhase
{
    /// <summary>正在获取最新版本信息。</summary>
    Checking,
    /// <summary>正在下载构建压缩包。</summary>
    Downloading,
    /// <summary>正在校验并解压安装。</summary>
    Extracting,
    /// <summary>安装完成。</summary>
    Done,
}

/// <summary>BepInEx 安装进度：Phase 之外，Downloading 阶段携带字节进度（TotalBytes 为 0 表示服务器未返回总大小）。</summary>
public sealed record BepInExInstallProgress(BepInExInstallPhase Phase, long BytesDownloaded, long TotalBytes);

/// <summary>builds.bepinex.dev 上的一份 BepInEx IL2CPP 构建。</summary>
public sealed class BepInExRelease
{
    public int BuildId { get; init; }
    public string Version { get; init; } = string.Empty;
    public string Architecture { get; init; } = "win-x64";
    /// <summary>站点返回的文件名（含 %2B 等已编码字符），直接拼入下载 URL。</summary>
    public string FileName { get; init; } = string.Empty;
    public string DownloadUrl => $"https://builds.bepinex.dev/projects/bepinex_be/{BuildId}/{FileName}";
}

/// <summary>
/// BepInEx IL2CPP 自动探测与安装服务。
/// 探测目标：游戏根目录下 BepInEx/core/BepInEx.Unity.IL2CPP.dll（核心）与
/// winhttp.dll + doorstop_config.ini（Doorstop 注入代理，即 ModInjector）。
/// 缺少任一项时 Plugin DLL 都无法注入游戏；此服务可从 builds.bepinex.dev
/// 下载最新 IL2CPP 构建并解压到游戏根目录。
/// </summary>
public class BepInExService
{
    /// <summary>BepInEx Bleeding Edge 构建列表页（手动下载入口）。</summary>
    public const string BuildsPageUrl = "https://builds.bepinex.dev/projects/bepinex_be";
    private const string BuildsHost = "builds.bepinex.dev";
    private const string GameExecutableName = "Touhou Mystia Izakaya.exe";
    /// <summary>下载流读停滞超时：连续无数据超过该时长判定连接挂死，触发重试。</summary>
    private static readonly TimeSpan ReadStallTimeout = TimeSpan.FromMinutes(2);

    /// <summary>允许解压的包内顶层条目：官方 IL2CPP 构建仅包含以下内容，其余一律拒绝。</summary>
    private static readonly HashSet<string> AllowedTopLevelDirectories = new(StringComparer.OrdinalIgnoreCase) { "BepInEx", "dotnet" };
    private static readonly HashSet<string> AllowedTopLevelFiles = new(StringComparer.OrdinalIgnoreCase) { "winhttp.dll", "doorstop_config.ini", "changelog.txt", ".doorstop_version" };

    private readonly AppConfigManager _appConfig;
    private readonly HttpClient _httpClient;

    /// <summary>游戏根目录（与 ModService.GetPluginsPath 的定位保持一致：管理器部署在游戏目录内）。</summary>
    public string GameRoot { get; } = AppContext.BaseDirectory;
    private string BepInExRoot => Path.Combine(GameRoot, "BepInEx");
    private string CoreDirectory => Path.Combine(BepInExRoot, "core");

    public BepInExService(AppConfigManager appConfig, HttpClient httpClient)
    {
        _appConfig = appConfig;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        // builds.bepinex.dev 会拦截空/可疑 User-Agent，补一个应用标识
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"THMI-Mod-Manager/{typeof(BepInExService).Assembly.GetName().Version?.ToString(3) ?? "1.0"}");
    }

    /// <summary>探测当前 BepInEx IL2CPP 安装状态（纯文件检查，可安全在 UI 线程调用）。</summary>
    public BepInExDetectionResult Detect()
    {
        var hasCore = File.Exists(Path.Combine(CoreDirectory, "BepInEx.Unity.IL2CPP.dll"))
            // 早期 BE 构建的核心程序集名为 BepInEx.IL2CPP.dll
            || File.Exists(Path.Combine(CoreDirectory, "BepInEx.IL2CPP.dll"));
        var hasWinHttp = File.Exists(Path.Combine(GameRoot, "winhttp.dll"));
        var hasDoorstop = File.Exists(Path.Combine(GameRoot, "doorstop_config.ini"));

        var state = !hasCore
            ? BepInExInstallState.Missing
            : (hasWinHttp && hasDoorstop) ? BepInExInstallState.Complete : BepInExInstallState.InjectorMissing;

        return new BepInExDetectionResult
        {
            State = state,
            HasCore = hasCore,
            HasWinHttp = hasWinHttp,
            HasDoorstopConfig = hasDoorstop,
            InstalledVersion = hasCore ? ReadCoreVersion() : null,
            Architecture = DetectGameArchitecture(),
        };
    }

    /// <summary>从 BepInEx.Core.dll 的文件版本信息读取已安装版本。</summary>
    private string? ReadCoreVersion()
    {
        try
        {
            var coreDll = Path.Combine(CoreDirectory, "BepInEx.Core.dll");
            if (!File.Exists(coreDll))
                return null;
            var info = FileVersionInfo.GetVersionInfo(coreDll);
            return string.IsNullOrWhiteSpace(info.ProductVersion) ? info.FileVersion : info.ProductVersion;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to read BepInEx core version: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 读取游戏主程序 PE 头中的机器类型判断架构（0x8664 = x64，0x014C = x86）。
    /// 游戏主程序不存在或读取失败时默认 x64（现代 Steam 版本为 64 位）。
    /// </summary>
    public string DetectGameArchitecture()
    {
        try
        {
            var gameExe = Path.Combine(GameRoot, GameExecutableName);
            if (!File.Exists(gameExe))
                return "win-x64";

            using var stream = new FileStream(gameExe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 0x40 + 4 || reader.ReadUInt16() != 0x5A4D)
                return "win-x64";

            stream.Position = 0x3C;
            var peOffset = reader.ReadInt32();
            if (peOffset < 0 || peOffset > stream.Length - 6)
                return "win-x64";

            stream.Position = peOffset;
            if (reader.ReadUInt32() != 0x00004550)
                return "win-x64";

            return reader.ReadUInt16() switch
            {
                0x014C => "win-x86",
                _ => "win-x64",
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to detect game architecture, defaulting to win-x64: {ex.Message}");
            return "win-x64";
        }
    }

    /// <summary>
    /// 从 builds.bepinex.dev 构建列表页解析最新的 IL2CPP 构建下载链接。
    /// 站点未提供 JSON API，列表页按构建时间倒序排列，取首个匹配架构的条目即为最新。
    /// </summary>
    public async Task<BepInExRelease?> GetLatestReleaseAsync(string architecture = "win-x64", CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(BuildsPageUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        // href="/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip"
        var pattern = $"href=\"(/projects/bepinex_be/(\\d+)/BepInEx-Unity\\.IL2CPP-{Regex.Escape(architecture)}-[^\"]+\\.zip)\"";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            Logger.LogWarning($"No BepInEx IL2CPP {architecture} build found on builds page");
            return null;
        }

        var fileNameEncoded = match.Groups[1].Value.Split('/')[^1];
        var fileName = Uri.UnescapeDataString(fileNameEncoded);

        // 版本号取自文件名：BepInEx-Unity.IL2CPP-{arch}-<version>.zip
        var architecturePrefix = $"-{architecture}-";
        var prefixIndex = fileName.IndexOf(architecturePrefix, StringComparison.OrdinalIgnoreCase);
        var version = prefixIndex >= 0 ? fileName[(prefixIndex + architecturePrefix.Length)..] : fileName;
        if (version.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            version = version[..^".zip".Length];

        var release = new BepInExRelease
        {
            BuildId = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
            Version = version,
            Architecture = architecture,
            FileName = fileNameEncoded,
        };
        Logger.LogInfo($"Latest BepInEx IL2CPP release: build {release.BuildId}, version {release.Version}, {release.Architecture}");
        return release;
    }

    /// <summary>
    /// 下载最新 BepInEx IL2CPP 构建并安装到游戏根目录。
    /// 安装前校验压缩包布局与 PE 文件；已有 doorstop_config.ini 会先备份为 .backup。
    /// </summary>
    /// <param name="progress">阶段与字节级进度回报（Downloading 阶段按块更新 BytesDownloaded/TotalBytes）。</param>
    /// <returns>安装完成后的 BepInEx 版本号；安装失败时抛出异常。</returns>
    public async Task<string> DownloadAndInstallAsync(BepInExRelease release, IProgress<BepInExInstallProgress>? progress, CancellationToken cancellationToken = default)
    {
        // 游戏运行时 BepInEx 核心 DLL 被占用，无法覆盖，先快速失败
        if (Process.GetProcessesByName("Touhou Mystia Izakaya").Length > 0)
            throw new InvalidOperationException(_appConfig.GetLocalized("Settings:BepInExInstallGameRunning", "游戏正在运行，请先停止游戏再安装 BepInEx。"));

        var tempRoot = Path.Combine(Path.GetTempPath(), $"THMI_BepInEx_Install_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            // 下载（重试 3 次，与 ModUpdateService 保持一致；每次尝试使用独立文件名，避开杀软对旧文件的锁定）
            progress?.Report(new BepInExInstallProgress(BepInExInstallPhase.Downloading, 0, 0));
            Logger.LogInfo($"Downloading BepInEx IL2CPP: {release.DownloadUrl}");
            var zipPath = await DownloadWithRetryAsync(release.DownloadUrl, tempRoot, progress, cancellationToken);

            // 校验并解压到暂存目录
            progress?.Report(new BepInExInstallProgress(BepInExInstallPhase.Extracting, 0, 0));
            var stagingPath = Path.Combine(tempRoot, "staging");
            Directory.CreateDirectory(stagingPath);
            ExtractBuildSafely(zipPath, stagingPath);

            // 备份用户自定义的 doorstop_config.ini
            var doorstopPath = Path.Combine(GameRoot, "doorstop_config.ini");
            if (File.Exists(doorstopPath))
            {
                File.Copy(doorstopPath, doorstopPath + ".backup", true);
                Logger.LogInfo($"Backed up existing doorstop_config.ini");
            }

            // 从暂存目录复制进游戏根目录（按文件覆盖合并，不触碰 BepInEx/plugins 内已装 Mod）
            CopyDirectory(stagingPath, GameRoot);
            Logger.LogInfo($"BepInEx IL2CPP build {release.BuildId} installed to {GameRoot}");

            var detection = Detect();
            if (detection.State != BepInExInstallState.Complete)
                throw new InvalidOperationException($"BepInEx installation verification failed: {detection.State}");

            progress?.Report(new BepInExInstallProgress(BepInExInstallPhase.Done, 0, 0));
            return detection.InstalledVersion ?? release.Version;
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    /// <summary>
    /// 下载 BepInEx 构建压缩包，整个"请求 + 流式写入 + 完整性校验"作为一个重试单元。
    /// 连接中断导致的 zip 截断（End Of Central Directory 校验失败）由重试逻辑重新下载。
    /// 每次尝试写入独立文件名，避免杀毒软件仍持有上一次失败残留文件的锁。
    /// </summary>
    /// <returns>成功下载的 zip 文件完整路径。</returns>
    private async Task<string> DownloadWithRetryAsync(string url, string tempRoot, IProgress<BepInExInstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals(BuildsHost, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Untrusted BepInEx download URL: {url}");

        for (var attempt = 3; attempt > 0; attempt--)
        {
            var zipPath = Path.Combine(tempRoot, $"BepInEx_{Guid.NewGuid():N}.zip");
            try
            {
                // 写入与校验必须分离：预校验要等写句柄完全关闭后才能打开同一文件，
                // 否则写句柄（FileShare.Read）会与读取打开（FileShare.Read）冲突 → 共享冲突
                await DownloadToFileAsync(url, zipPath, progress, cancellationToken);
                ValidateZipArchive(zipPath);
                return zipPath;
            }
            catch (OperationCanceledException)
            {
                throw; // 用户取消，不重试
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
            {
                Logger.LogWarning($"BepInEx download attempt failed: {ex.Message}, retries left: {attempt - 1}");
                TryDeleteFile(zipPath);
                if (attempt <= 1)
                    throw;
                await Task.Delay(2000, cancellationToken);
            }
        }

        throw new InvalidOperationException("Unreachable");
    }

    /// <summary>解析 ZIP 中央目录以提前发现截断/损坏的包（须在写句柄关闭后调用）。</summary>
    private static void ValidateZipArchive(string archivePath)
    {
        using var archive = ModPackageSafety.OpenReadWithRetry(archivePath);
        if (archive.Entries.Count == 0)
            throw new InvalidDataException("Downloaded BepInEx archive is empty.");
    }

    /// <summary>单次下载写入：写盘时校验大小上限，结束后校验 Content-Length（zip 解析由调用方在句柄关闭后进行）。</summary>
    private async Task DownloadToFileAsync(string url, string destinationPath, IProgress<BepInExInstallProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"BepInEx download failed: HTTP {(int)response.StatusCode}");

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        if (totalBytes > ModPackageSafety.MaxDownloadBytes)
            throw new InvalidDataException($"BepInEx download exceeds {ModPackageSafety.MaxDownloadBytes} bytes.");

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        var buffer = new byte[81920];
        long downloadedBytes = 0;
        // 进度按 1% 或 512KB 节流，避免高频 UI 更新
        long nextReportThreshold = 0;
        // 读停滞保护：连续 2 分钟无新数据判定连接挂死，按可重试失败处理
        using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stallCts.CancelAfter(ReadStallTimeout);
        int bytesRead;
        try
        {
            while ((bytesRead = await contentStream.ReadAsync(buffer, stallCts.Token)) > 0)
            {
                // 必须按实际读取字节数写入：网络流 ReadAsync 常返回部分填充的缓冲区，
                // 整块写入会把上一轮残留数据混入文件（字节数计数却依然正确，极难察觉）
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                downloadedBytes += bytesRead;
                if (fileStream.Length > ModPackageSafety.MaxDownloadBytes)
                    throw new InvalidDataException($"BepInEx download exceeds {ModPackageSafety.MaxDownloadBytes} bytes.");

                if (downloadedBytes >= nextReportThreshold)
                {
                    progress?.Report(new BepInExInstallProgress(BepInExInstallPhase.Downloading, downloadedBytes, totalBytes));
                    nextReportThreshold = Math.Max(downloadedBytes + 512 * 1024, downloadedBytes * 101 / 100);
                }
                stallCts.CancelAfter(ReadStallTimeout);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 仅停滞计时器触发（用户未取消）：转为可重试的 IO 异常
            throw new IOException($"BepInEx download stalled: no data received for {ReadStallTimeout.TotalSeconds:0}s.");
        }

        // 服务器声明了总大小但流提前结束：典型的连接中断截断
        if (totalBytes > 0 && downloadedBytes != totalBytes)
            throw new InvalidDataException($"Downloaded BepInEx archive is truncated: {downloadedBytes} of {totalBytes} bytes.");

        progress?.Report(new BepInExInstallProgress(BepInExInstallPhase.Downloading, downloadedBytes, totalBytes));
        // ZIP 中央目录校验由调用方（ValidateZipArchive）在写句柄关闭后进行
    }

    /// <summary>
    /// 校验 BepInEx 构建压缩包并解压到暂存目录：
    /// 条目数量/大小上限、顶层条目白名单、所有 .dll 必须为合法 PE 文件。
    /// PE 校验直接从压缩包内条目流读取文件头（不落地临时文件，避免杀毒软件锁定干扰）。
    /// </summary>
    private static void ExtractBuildSafely(string archivePath, string stagingPath)
    {
        using var archive = ModPackageSafety.OpenReadWithRetry(archivePath);
        if (archive.Entries.Count > ModPackageSafety.MaxArchiveEntries)
            throw new InvalidDataException($"BepInEx archive contains more than {ModPackageSafety.MaxArchiveEntries} entries.");

        foreach (var entry in archive.Entries)
        {
            if (entry.Length > ModPackageSafety.MaxArchiveEntryBytes)
                throw new InvalidDataException($"Archive entry exceeds limit: {entry.FullName}");

            var topLevel = entry.FullName.Split('/', '\\')[0];
            if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\") || string.IsNullOrEmpty(entry.Name))
            {
                // 目录条目：仅允许官方包内的顶层目录
                if (!AllowedTopLevelDirectories.Contains(topLevel))
                    throw new InvalidDataException($"Unexpected directory in BepInEx archive: {topLevel}");
                continue;
            }

            // 文件条目：位于顶层目录内，或位于包根（后者仅允许白名单文件）
            if (topLevel.Equals(entry.FullName, StringComparison.Ordinal))
            {
                if (!AllowedTopLevelFiles.Contains(topLevel))
                    throw new InvalidDataException($"Unexpected root file in BepInEx archive: {topLevel}");
            }
            else if (!AllowedTopLevelDirectories.Contains(topLevel))
            {
                throw new InvalidDataException($"Unexpected directory in BepInEx archive: {topLevel}");
            }

            if (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && !IsEntryPortableExecutable(entry))
            {
                throw new InvalidDataException($"Invalid PE payload in BepInEx archive: {entry.FullName}");
            }
        }

        ModPackageSafety.ExtractZipSafely(archivePath, stagingPath);
    }

    /// <summary>
    /// 从压缩包条目流前部读取 PE 头判断是否合法可执行文件：
    /// 0x00 处 "MZ"、0x3C 处 e_lfanew 指向的位置为 "PE\0\0"。
    /// </summary>
    private static bool IsEntryPortableExecutable(ZipArchiveEntry entry)
    {
        const int headerSize = 0x1000;
        try
        {
            using var stream = entry.Open();
            var header = new byte[headerSize];
            var read = 0;
            while (read < headerSize)
            {
                var n = stream.Read(header, read, headerSize - read);
                if (n == 0)
                    break;
                read += n;
            }

            if (read < 0x40 || BitConverter.ToUInt16(header, 0) != 0x5A4D)
                return false;

            var peOffset = BitConverter.ToInt32(header, 0x3C);
            if (peOffset < 0 || peOffset + 4 > read)
                return false;

            return BitConverter.ToUInt32(header, peOffset) == 0x00004550;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            Logger.LogWarning($"Failed to read archive entry for PE validation: {entry.FullName} ({ex.Message})");
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            // 杀毒软件可能仍持有该文件；残留文件由 tempRoot 的整体清理兜底
            Logger.LogWarning($"Failed to delete failed download file {path}: {ex.Message}");
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to clean up temp directory {path}: {ex.Message}");
        }
    }
}
