using System;
using System.Text.RegularExpressions;

namespace THMI_Mod_Manager.Services
{
    /// <summary>
    /// Utility class for semantic version comparison following SemVer 2.0.0 specification
    /// </summary>
    public class SemanticVersion : IComparable<SemanticVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string? PreRelease { get; }
        public string? BuildMetadata { get; }

        private static readonly Regex VersionRegex = new Regex(
            @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)" +
            @"(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?" +
            @"(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$",
            RegexOptions.Compiled);

        public SemanticVersion(int major, int minor, int patch, string? preRelease = null, string? buildMetadata = null)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            PreRelease = preRelease;
            BuildMetadata = buildMetadata;
        }

        /// <summary>
        /// Parses a semantic version string
        /// </summary>
        /// <param name="versionString">Version string in format "MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]"</param>
        /// <returns>Parsed SemanticVersion object</returns>
        /// <exception cref="ArgumentException">Thrown when version string is invalid</exception>
        public static SemanticVersion Parse(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                throw new ArgumentException("Version string cannot be null or empty", nameof(versionString));

            var match = VersionRegex.Match(versionString.Trim());
            if (!match.Success)
                throw new ArgumentException($"Invalid semantic version format: {versionString}", nameof(versionString));

            int major = int.Parse(match.Groups["major"].Value);
            int minor = int.Parse(match.Groups["minor"].Value);
            int patch = int.Parse(match.Groups["patch"].Value);
            
            string? preRelease = match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : null;
            string? buildMetadata = match.Groups["buildmetadata"].Success ? match.Groups["buildmetadata"].Value : null;

            return new SemanticVersion(major, minor, patch, preRelease, buildMetadata);
        }

        /// <summary>
        /// Tries to parse a semantic version string
        /// </summary>
        /// <param name="versionString">Version string to parse</param>
        /// <param name="version">Output SemanticVersion object if parsing succeeds</param>
        /// <returns>True if parsing succeeds, false otherwise</returns>
        public static bool TryParse(string? versionString, out SemanticVersion? version)
        {
            version = null;
            try
            {
                if (string.IsNullOrWhiteSpace(versionString))
                    return false;

                version = Parse(versionString);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Compares this version with another version
        /// </summary>
        /// <param name="other">Other version to compare with</param>
        /// <returns>
        /// Negative value if this version is older than other
        /// Zero if versions are equal
        /// Positive value if this version is newer than other
        /// </returns>
        public int CompareTo(SemanticVersion? other)
        {
            if (other == null) return 1;

            // Compare major.minor.patch
            int result = Major.CompareTo(other.Major);
            if (result != 0) return result;

            result = Minor.CompareTo(other.Minor);
            if (result != 0) return result;

            result = Patch.CompareTo(other.Patch);
            if (result != 0) return result;

            // Compare pre-release versions
            // 1.0.0 > 1.0.0-alpha (stable versions are greater than pre-release)
            if (PreRelease == null && other.PreRelease != null) return 1;
            if (PreRelease != null && other.PreRelease == null) return -1;
            if (PreRelease != null && other.PreRelease != null)
            {
                result = ComparePreRelease(PreRelease, other.PreRelease);
                if (result != 0) return result;
            }

            // Build metadata does not affect version precedence
            return 0;
        }

        /// <summary>
        /// Checks if this version is newer than the other version
        /// </summary>
        public bool IsNewerThan(SemanticVersion other) => CompareTo(other) > 0;

        /// <summary>
        /// Checks if this version is older than the other version
        /// </summary>
        public bool IsOlderThan(SemanticVersion other) => CompareTo(other) < 0;

        /// <summary>
        /// Checks if versions are equal (ignoring build metadata)
        /// </summary>
        public bool IsEqualTo(SemanticVersion other) => CompareTo(other) == 0;

        private static int ComparePreRelease(string preRelease1, string preRelease2)
        {
            var parts1 = preRelease1.Split('.');
            var parts2 = preRelease2.Split('.');

            for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
            {
                if (i >= parts1.Length) return -1; // 1.0.0-alpha < 1.0.0-alpha.1
                if (i >= parts2.Length) return 1;  // 1.0.0-alpha.1 > 1.0.0-alpha

                var part1 = parts1[i];
                var part2 = parts2[i];

                bool isNumeric1 = int.TryParse(part1, out int num1);
                bool isNumeric2 = int.TryParse(part2, out int num2);

                if (isNumeric1 && isNumeric2)
                {
                    int result = num1.CompareTo(num2);
                    if (result != 0) return result;
                }
                else if (isNumeric1 && !isNumeric2)
                {
                    return -1; // Numeric identifiers always have lower precedence than non-numeric
                }
                else if (!isNumeric1 && isNumeric2)
                {
                    return 1; // Non-numeric identifiers always have higher precedence than numeric
                }
                else
                {
                    int result = string.Compare(part1, part2, StringComparison.Ordinal);
                    if (result != 0) return result;
                }
            }

            return 0;
        }

        public override string ToString()
        {
            string version = $"{Major}.{Minor}.{Patch}";
            if (!string.IsNullOrEmpty(PreRelease))
                version += $"-{PreRelease}";
            if (!string.IsNullOrEmpty(BuildMetadata))
                version += $"+{BuildMetadata}";
            return version;
        }

        public override bool Equals(object? obj)
        {
            if (obj is SemanticVersion other)
                return IsEqualTo(other);
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Major, Minor, Patch, PreRelease);
        }

        public static bool operator <(SemanticVersion left, SemanticVersion right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(SemanticVersion left, SemanticVersion right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(SemanticVersion left, SemanticVersion right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(SemanticVersion left, SemanticVersion right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator ==(SemanticVersion left, SemanticVersion right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.IsEqualTo(right);
        }

        public static bool operator !=(SemanticVersion left, SemanticVersion right)
        {
            return !(left == right);
        }
    }
}