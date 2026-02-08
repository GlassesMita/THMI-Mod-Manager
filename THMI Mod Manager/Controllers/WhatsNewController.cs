using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using THMI_Mod_Manager.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace THMI_Mod_Manager.Controllers
{
    /// <summary>
    /// Controller for handling What's New page
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class WhatsNewController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IStringLocalizer<WhatsNewController> _localizer;
        private const string GitHubReleasesUrl = "https://api.github.com/repos/GlassesMita/THMI-Mod-Manager/releases";
        private const string GitHubLatestUrl = "https://api.github.com/repos/GlassesMita/THMI-Mod-Manager/releases/latest";

        public WhatsNewController(
            HttpClient httpClient,
            IStringLocalizer<WhatsNewController> localizer)
        {
            _httpClient = httpClient;
            _localizer = localizer;
        }

        /// <summary>
        /// Get release notes for a specific version
        /// </summary>
        [HttpGet("release-notes")]
        public async Task<IActionResult> GetReleaseNotes([FromQuery] string? version = null)
        {
            try
            {
                Logger.LogInfo($"Fetching release notes for version: {version ?? "latest"}");

                string apiUrl;
                if (!string.IsNullOrEmpty(version))
                {
                    apiUrl = GitHubLatestUrl;
                }
                else
                {
                    var versionTag = version.StartsWith("v") ? version : $"v{version}";
                    apiUrl = $"{GitHubReleasesUrl}/tags/{versionTag}";
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.UserAgent.ParseAdd("THMI-Mod-Manager");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return Ok(new
                    {
                        success = false,
                        message = _localizer["FailedToFetchReleaseNotes"]
                    });
                }

                var content = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(content);

                if (release == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = _localizer["FailedToParseReleaseInfo"]
                    });
                }

                var releaseNotes = ParseMarkdown(release.Body ?? string.Empty);

                return Ok(new
                {
                    success = true,
                    version = release.TagName,
                    name = release.Name,
                    publishedAt = release.PublishedAt,
                    releaseNotes = releaseNotes,
                    htmlUrl = release.HtmlUrl
                });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error fetching release notes");
                return Ok(new
                {
                    success = false,
                    message = _localizer["ErrorFetchingReleaseNotes"],
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get recent releases list
        /// </summary>
        [HttpGet("releases")]
        public async Task<IActionResult> GetReleases([FromQuery] int count = 5)
        {
            try
            {
                Logger.LogInfo($"Fetching recent {count} releases");

                var url = "https://api.github.com/repos/GlassesMita/THMI-Mod-Manager/releases?per_page=" + count;

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("THMI-Mod-Manager");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return Ok(new
                    {
                        success = false,
                        message = _localizer["FailedToFetchReleases"]
                    });
                }

                var content = await response.Content.ReadAsStringAsync();
                var releases = JsonSerializer.Deserialize<GitHubRelease[]>(content);

                if (releases == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = _localizer["FailedToParseReleaseInfo"]
                    });
                }

                var releaseList = releases.Select(r => new
                {
                    version = r.TagName,
                    name = r.Name,
                    publishedAt = r.PublishedAt,
                    isPrerelease = r.Prerelease,
                    htmlUrl = r.HtmlUrl
                }).ToList();

                return Ok(new
                {
                    success = true,
                    releases = releaseList
                });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error fetching releases");
                return Ok(new
                {
                    success = false,
                    message = _localizer["ErrorFetchingReleases"],
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Parse markdown to HTML (simple implementation)
        /// </summary>
        private string ParseMarkdown(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            var html = markdown;

            html = html.Replace("\r\n", "\n");
            html = html.Replace("\n", "<br />");

            html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*(.*?)\*\*", "<strong>$1</strong>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\*(.*?)\*", "<em>$1</em>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"`(.*?)`", "<code>$1</code>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"### (.*?)$", "<h3>$1</h3>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"## (.*?)$", "<h2>$1</h2>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"# (.*?)$", "<h1>$1</h1>");

            html = System.Text.RegularExpressions.Regex.Replace(html, @"\[(.*?)\]\((.*?)\)", "<a href=\"$2\" target=\"_blank\">$1</a>");

            html = System.Text.RegularExpressions.Regex.Replace(html, @"^- (.*?)$", "<li>$1</li>");

            return html;
        }
    }

    public class GitHubRelease
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
        [JsonPropertyName("assets_url")]
        public string AssetsUrl { get; set; } = string.Empty;
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = string.Empty;
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
        [JsonPropertyName("target_commitish")]
        public string TargetCommitish { get; set; } = string.Empty;
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("draft")]
        public bool Draft { get; set; }
        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
    }
}
