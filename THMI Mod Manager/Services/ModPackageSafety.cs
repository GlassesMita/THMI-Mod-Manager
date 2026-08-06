using System.IO.Compression;

namespace THMI_Mod_Manager.Services;

internal static class ModPackageSafety
{
    internal const long MaxDownloadBytes = 200L * 1024 * 1024;
    private const int MaxArchiveEntries = 500;
    private const long MaxArchiveEntryBytes = 100L * 1024 * 1024;
    private const long MaxExtractedBytes = 500L * 1024 * 1024;

    internal static bool IsSafeFileName(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !Path.IsPathRooted(value)
            && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !value.Contains("..", StringComparison.Ordinal)
            && !value.Contains(Path.DirectorySeparatorChar)
            && !value.Contains(Path.AltDirectorySeparatorChar);
    }

    internal static bool IsWithinDirectory(string path, string root)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    internal static void ExtractZipSafely(string archivePath, string destinationPath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaxArchiveEntries)
            throw new InvalidDataException($"Archive contains more than {MaxArchiveEntries} entries.");

        long totalUncompressedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaxArchiveEntryBytes)
                throw new InvalidDataException($"Archive entry exceeds {MaxArchiveEntryBytes} bytes: {entry.FullName}");

            totalUncompressedBytes = checked(totalUncompressedBytes + entry.Length);
            if (totalUncompressedBytes > MaxExtractedBytes)
                throw new InvalidDataException($"Archive exceeds {MaxExtractedBytes} bytes when extracted.");

            var targetPath = Path.GetFullPath(Path.Combine(destinationPath, entry.FullName));
            if (!IsWithinDirectory(targetPath, destinationPath))
                throw new InvalidDataException($"Archive entry escapes extraction directory: {entry.FullName}");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, true);
        }
    }

    internal static bool IsPortableExecutable(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D)
                return false;

            stream.Position = 0x3C;
            var peOffset = reader.ReadInt32();
            if (peOffset < 0 || peOffset > stream.Length - 4)
                return false;

            stream.Position = peOffset;
            return reader.ReadUInt32() == 0x00004550;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}