using News.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GitHubActionsNews
{
    public static class Consolidate
    {
        private const string FullOverview = "Actions-Full-Overview";

        public static async Task Run(Twitter twitter)
        {
            Log.Message($"Consolidating all current state");
            // download all Actions-**.json files from the storage account
            var updatedActions = (await DownloadAllActionsFiles().ConfigureAwait(false)).ToList();

            // download previous overview
            var allActions = await Storage.DownloadAllFilesThatStartWith<GitHubAction>(FullOverview);

            var tweetsSend = 0;
            if (allActions?.Count > 0)
            {
                // check for changes
                foreach (var action in updatedActions)
                {
                    if (action.Url == "https://github.com/marketplace/actions/version-forget-me-not")
                    {
                        var wait = true;
                    }

                    var previousVersions = allActions.Where(item => item.Url == action.Url)
                                                     .OrderByDescending(item => item.Updated);

                    var previousVersion = previousVersions.FirstOrDefault();
                    var tweetText = "";
                    if (previousVersion == null)
                    {
                        // tweet new action                        
                        tweetText = $"A new GitHub Action has been added to the marketplace!" + Environment.NewLine + Environment.NewLine + $"Check out '{action.Title}' from {action.Publisher}. {action.Url}";
                    }
                    else if (!string.IsNullOrWhiteSpace(action.Version) && !action.Version.Equals(previousVersion.Version, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // only tweet when nothing went wrong with loading the version text from either the current version or the new one
                        if (action.Version.IndexOf(Constants.ErrorText) == -1 && (previousVersion.Version == null || previousVersion.Version.IndexOf(Constants.ErrorText) == -1))
                        {
                            // tweet changes
                            tweetText = $"GitHub Action '{action.Title}' from {action.Publisher} has been updated to version {action.Version}. {action.Url}";
                        }
                    }

                    // only tweet when necessary
                    // and only when there is nothing wrong with the version
                    if (!string.IsNullOrWhiteSpace(tweetText) && tweetText.IndexOf(Constants.ErrorText) == -1)
                    {
                        // send the tweet
                        twitter.SendTweet(tweetText, "", previousVersion == null ? null : $"Old version: [{previousVersion?.Version}]");
                        tweetsSend++;
                    }
                }

                Log.Message($"Send {tweetsSend} tweets out in this run");
            }

            if (updatedActions.Count > 0)
            {
                var test = updatedActions.Where(item => item.Url == "https://github.com/marketplace/actions/version-forget-me-not");

                // store current set as overview
                Storage.SaveJson<GitHubAction>(updatedActions, FullOverview);
            }
        }

        private static async Task<IEnumerable<GitHubAction>> DownloadAllActionsFiles()
        {
            var started = DateTime.Now;
            Log.Message($"DownloadAllActionsFiles");

            var allActions = await Storage.DownloadAllFilesThatStartWith<GitHubAction>("Actions");
            // de-duplicate
            //var actions = allActions.Distinct(new GitHubActionComparer());
            var actions = OnlyLoadLatestUpdatedPerAction(allActions);

            Log.Message($"Download all files took {(DateTime.Now - started).TotalSeconds:N2}s, we have {actions.Count()} known actions");

            return actions;
        }

        private static IEnumerable<GitHubAction> OnlyLoadLatestUpdatedPerAction(List<GitHubAction> allActions)
        {
            Log.Message($"De-duplicating the consolidated actions list, starting with [{allActions.Count()}]");
            var latestVersions = new List<GitHubAction>();
            foreach (var action in allActions)
            {
                var latest = latestVersions.Where(item => item.Url == action.Url)
                                       .OrderByDescending(item => item.Updated)
                                       .FirstOrDefault();

                if (latest != null)
                {
                    // prevent adding it twice
                    if (!latestVersions.Any(item => item.Url == latest.Url))
                    {
                        latestVersions.Add(latest);
                    }
                }
            }

            Log.Message($"End of de-dupe we have [{latestVersions.Count()}] actions")
            return latestVersions;
        }
    }
}
