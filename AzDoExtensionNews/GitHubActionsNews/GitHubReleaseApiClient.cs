using News.Library;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace GitHubActionsNews
{
    public class GitHubReleaseLookupResult
    {
        public bool Success { get; set; }
        public string Version { get; set; }
        public bool RateLimited { get; set; }
    }

    /// <summary>
    /// Looks up the latest release/tag for a repository via the GitHub REST API, so callers can
    /// detect version changes without loading the marketplace detail page with Playwright.
    /// </summary>
    public class GitHubReleaseApiClient
    {
        // Stop calling the API once we get this close to the limit, so a single run never exhausts
        // the token's remaining budget for other jobs running in the same hour.
        private const int RateLimitSafetyThreshold = 50;

        private readonly HttpClient _httpClient;
        private readonly object _stateLock = new object();
        private int _rateLimitRemaining = int.MaxValue;
        private DateTimeOffset? _backoffUntil;

        public GitHubReleaseApiClient(HttpClient httpClient, string token)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GitHubActionsNews-Scraper");
            }
            if (!_httpClient.DefaultRequestHeaders.Accept.Any())
            {
                _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            }
            if (!_httpClient.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"))
            {
                _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            }
            if (!string.IsNullOrWhiteSpace(token) && _httpClient.DefaultRequestHeaders.Authorization == null)
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public bool IsRateLimited
        {
            get
            {
                lock (_stateLock)
                {
                    if (_backoffUntil.HasValue)
                    {
                        if (DateTimeOffset.UtcNow < _backoffUntil.Value)
                        {
                            return true;
                        }
                        _backoffUntil = null;
                    }

                    return _rateLimitRemaining <= RateLimitSafetyThreshold;
                }
            }
        }

        public static (string Owner, string Repo) ParseOwnerRepo(string repoUrl)
        {
            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                return (null, null);
            }

            try
            {
                var uri = new Uri(repoUrl, UriKind.Absolute);
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 2)
                {
                    return (null, null);
                }

                return (segments[0], segments[1]);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>
        /// Looks up the latest release tag, falling back to the most recent tag when the
        /// repository has no GitHub Releases. Returns Success = false (never throws) when the
        /// lookup could not be completed, so callers can fall back to scraping the detail page.
        /// </summary>
        public async Task<GitHubReleaseLookupResult> GetLatestVersionAsync(string repoUrl)
        {
            if (IsRateLimited)
            {
                return new GitHubReleaseLookupResult { Success = false, RateLimited = true };
            }

            var (owner, repo) = ParseOwnerRepo(repoUrl);
            if (owner == null || repo == null)
            {
                return new GitHubReleaseLookupResult { Success = false };
            }

            var releaseResult = await FetchAsync($"https://api.github.com/repos/{owner}/{repo}/releases/latest");
            if (releaseResult.RateLimited)
            {
                return new GitHubReleaseLookupResult { Success = false, RateLimited = true };
            }
            if (releaseResult.StatusCode == HttpStatusCode.OK)
            {
                var tag = ExtractTagName(releaseResult.Body);
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    return new GitHubReleaseLookupResult { Success = true, Version = tag };
                }
            }

            // repos without GitHub Releases (only tags) 404 on /releases/latest
            var tagsResult = await FetchAsync($"https://api.github.com/repos/{owner}/{repo}/tags?per_page=1");
            if (tagsResult.RateLimited)
            {
                return new GitHubReleaseLookupResult { Success = false, RateLimited = true };
            }
            if (tagsResult.StatusCode == HttpStatusCode.OK)
            {
                var tag = ExtractFirstTagName(tagsResult.Body);
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    return new GitHubReleaseLookupResult { Success = true, Version = tag };
                }
            }

            return new GitHubReleaseLookupResult { Success = false };
        }

        private async Task<(HttpStatusCode StatusCode, string Body, bool RateLimited)> FetchAsync(string url)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url);
                UpdateRateLimitState(response);

                if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
                {
                    ApplyRetryAfter(response);
                    Log.Message($"GitHub API rate/abuse limit hit for [{url}], status [{response.StatusCode}]");
                    return (response.StatusCode, null, true);
                }

                var body = await response.Content.ReadAsStringAsync();
                return (response.StatusCode, body, false);
            }
            catch (Exception e)
            {
                Log.Message($"Error calling GitHub API [{url}]: {e.Message}");
                return (HttpStatusCode.ServiceUnavailable, null, false);
            }
        }

        private void UpdateRateLimitState(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("X-RateLimit-Remaining", out var values))
            {
                return;
            }

            var value = values.FirstOrDefault();
            if (!int.TryParse(value, out var remaining))
            {
                return;
            }

            lock (_stateLock)
            {
                _rateLimitRemaining = remaining;
            }

            if (remaining <= RateLimitSafetyThreshold)
            {
                Log.Message($"GitHub API rate limit remaining is low [{remaining}]; further version lookups this run will fall back to marketplace scraping.");
            }
        }

        private void ApplyRetryAfter(HttpResponseMessage response)
        {
            TimeSpan backoff;
            if (response.Headers.TryGetValues("Retry-After", out var values) &&
                int.TryParse(values.FirstOrDefault(), out var seconds))
            {
                backoff = TimeSpan.FromSeconds(seconds);
            }
            else
            {
                // No Retry-After header: back off conservatively rather than hammering the API.
                backoff = TimeSpan.FromMinutes(1);
            }

            lock (_stateLock)
            {
                _backoffUntil = DateTimeOffset.UtcNow.Add(backoff);
            }
        }

        internal static string ExtractTagName(string releaseJson)
        {
            if (string.IsNullOrWhiteSpace(releaseJson))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(releaseJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("tag_name", out var tagNameElement))
                {
                    return tagNameElement.GetString();
                }
            }
            catch (JsonException)
            {
                // malformed/unexpected response body, treat as "no version found"
            }

            return null;
        }

        internal static string ExtractFirstTagName(string tagsJson)
        {
            if (string.IsNullOrWhiteSpace(tagsJson))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(tagsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var first = doc.RootElement[0];
                    if (first.TryGetProperty("name", out var nameElement))
                    {
                        return nameElement.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // malformed/unexpected response body, treat as "no version found"
            }

            return null;
        }
    }
}
