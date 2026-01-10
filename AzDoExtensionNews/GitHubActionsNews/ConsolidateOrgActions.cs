using News.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GitHubActionsNews
{
    public static class ConsolidateOrgActions
    {
        private const string StorageFileName = "Actions-Org";
        private static readonly Regex SemVerRegex = new Regex(@"^v?\d+\.\d+\.\d+(?:-[a-zA-Z0-9\.\-]+)?(?:\+[a-zA-Z0-9\.\-]+)?$", RegexOptions.Compiled);
        private static readonly HttpClient HttpClient = new HttpClient();

        public static async Task Run()
        {
            Log.Message("Consolidating organization actions and enriching with version data");

            // Set up GitHub API client
            var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(githubToken))
            {
                HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {githubToken}");
            }
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "GitHubActionsNews");

            // Read all organization action files
            var orgActionsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "OrgActionsArtifacts");
            Log.Message($"Looking for organization actions in: {orgActionsPath}");

            if (!Directory.Exists(orgActionsPath))
            {
                Log.Message("No organization actions artifacts found");
                return;
            }

            // Get all marketplace actions for deduplication
            var marketplaceActions = await Storage.DownloadAllFilesThatStartWith<GitHubAction>("Actions");
            var marketplaceRepos = new HashSet<string>(
                marketplaceActions
                    .Where(a => !string.IsNullOrEmpty(a.RepoUrl))
                    .Select(a => NormalizeRepoUrl(a.RepoUrl)),
                StringComparer.OrdinalIgnoreCase
            );
            Log.Message($"Loaded {marketplaceRepos.Count} unique marketplace action repositories for deduplication");

            var allOrgActions = new List<GitHubAction>();

            // Process each organization's actions file
            var orgActionFiles = Directory.GetFiles(orgActionsPath, "*-actions.json", SearchOption.AllDirectories);
            Log.Message($"Found {orgActionFiles.Length} organization action files");

            foreach (var file in orgActionFiles)
            {
                Log.Message($"Processing file: {file}");
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var orgData = JsonSerializer.Deserialize<OrgActionsData>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (orgData?.Actions == null)
                    {
                        Log.Message($"No actions found in file: {file}");
                        continue;
                    }

                    Log.Message($"Found {orgData.Actions.Count} actions in {file}");

                    foreach (var action in orgData.Actions)
                    {
                        // Build repo URL
                        var repoUrl = $"https://github.com/{action.Repo}";
                        var normalizedRepoUrl = NormalizeRepoUrl(repoUrl);

                        // Check for deduplication
                        if (marketplaceRepos.Contains(normalizedRepoUrl))
                        {
                            Log.Message($"Skipping {repoUrl} - already in marketplace");
                            continue;
                        }

                        // Get version from tags
                        var version = await GetLatestSemVerTag(action.Repo);
                        if (string.IsNullOrEmpty(version))
                        {
                            Log.Message($"No semver tag found for {repoUrl}, skipping");
                            continue;
                        }

                        var marketplaceUrl = $"https://github.com/{action.Repo}";
                        var actionPath = !string.IsNullOrEmpty(action.Path) ? $"/{action.Path}" : "";
                        
                        var ghAction = new GitHubAction
                        {
                            Url = marketplaceUrl,
                            Title = action.Name ?? action.Repo,
                            Publisher = GetPublisher(action.Repo),
                            Version = version,
                            Updated = DateTime.UtcNow,
                            RepoUrl = repoUrl,
                            Verified = false, // Organization actions are not verified
                            ActionType = action.Using,
                            NodeVersion = null
                        };

                        allOrgActions.Add(ghAction);
                        Log.Message($"Added action: {ghAction.Title} at {ghAction.RepoUrl} with version {ghAction.Version}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Message($"Error processing file {file}: {ex.Message}");
                }
            }

            Log.Message($"Total organization actions discovered: {allOrgActions.Count}");

            if (allOrgActions.Count == 0)
            {
                Log.Message("No organization actions to store");
                return;
            }

            // Get existing organization actions
            var existingOrgActions = Storage.ReadFromJson<GitHubAction>(StorageFileName, "organization actions");

            // Compare and update
            var newOrUpdatedActions = new List<GitHubAction>();

            foreach (var action in allOrgActions)
            {
                var existingAction = existingOrgActions
                    .Where(item => string.Equals(item.RepoUrl, action.RepoUrl, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.Updated)
                    .FirstOrDefault();

                if (existingAction == null)
                {
                    Log.Message($"Found a new organization action: {action.Title} at {action.RepoUrl}");
                    existingOrgActions.Add(action);
                    newOrUpdatedActions.Add(action);
                }
                else if (!string.Equals(existingAction.Version, action.Version, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Message($"Found an updated organization action: {action.Title}: old version [{existingAction.Version}], new version [{action.Version}]");
                    existingAction.Version = action.Version;
                    existingAction.Updated = DateTime.UtcNow;
                    newOrUpdatedActions.Add(existingAction);
                }
            }

            Log.Message($"New or updated organization actions: {newOrUpdatedActions.Count}");

            // Store the updated list
            Storage.SaveJson(existingOrgActions, StorageFileName);

            Log.Message("Organization actions consolidation complete");
        }

        private static async Task<string> GetLatestSemVerTag(string repo)
        {
            try
            {
                var url = $"https://api.github.com/repos/{repo}/tags?per_page=100";
                var response = await HttpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    Log.Message($"Failed to fetch tags for {repo}: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var tags = JsonSerializer.Deserialize<List<GitHubTag>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tags == null || tags.Count == 0)
                {
                    return null;
                }

                // Find the first tag that matches semver
                foreach (var tag in tags)
                {
                    if (SemVerRegex.IsMatch(tag.Name))
                    {
                        return tag.Name;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Message($"Error fetching tags for {repo}: {ex.Message}");
                return null;
            }
        }

        private static string NormalizeRepoUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            // Remove trailing slashes and convert to lowercase
            url = url.TrimEnd('/').ToLowerInvariant();

            // Extract owner/repo from URL
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (segments.Length >= 2)
            {
                return $"https://github.com/{segments[0]}/{segments[1]}";
            }

            return url;
        }

        private static string GetPublisher(string repo)
        {
            if (string.IsNullOrEmpty(repo))
            {
                return string.Empty;
            }

            var parts = repo.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : string.Empty;
        }

        private class OrgActionsData
        {
            public string LastUpdated { get; set; }
            public List<OrgAction> Actions { get; set; }
        }

        private class OrgAction
        {
            public string Name { get; set; }
            public string Repo { get; set; }
            public string Path { get; set; }
            public string DownloadUrl { get; set; }
            public string Author { get; set; }
            public string Description { get; set; }
            public string Using { get; set; }
            public bool IsArchived { get; set; }
            public string Visibility { get; set; }
            public bool IsFork { get; set; }
        }

        private class GitHubTag
        {
            public string Name { get; set; }
            public GitHubCommit Commit { get; set; }
        }

        private class GitHubCommit
        {
            public string Sha { get; set; }
            public string Url { get; set; }
        }
    }
}
