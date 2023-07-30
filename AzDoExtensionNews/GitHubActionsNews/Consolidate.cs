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
        private const string UpdatedOverview = "Actions-Updated-Overview";

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
                    if (tweetsSend > 25) 
                    {
                        // to many tweets to send, stop sending any more to prevent ratelimiting issues
                        break;
                    }

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

                // store the updated actions as an extra file
                Storage.SaveJson<GitHubAction>(updatedActions, UpdatedOverview);
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

            var count = actions.Where(item => !String.IsNullOrEmpty(item.RepoUrl)).Count();
            Log.Message($"Download all files took {(DateTime.Now - started).TotalSeconds:N2}s, we have {actions.Count()} known actions with [{count}] filled repo urls");

            return actions;
        }

        private static IEnumerable<GitHubAction> OnlyLoadLatestUpdatedPerAction(List<GitHubAction> allActions)
        {
            var notEmpty = allActions.Where(item => !String.IsNullOrEmpty(item.RepoUrl)).Count();
            Log.Message($"De-duplicating the consolidated actions list, starting with [{allActions.Count()}] with [{notEmpty}] filled repo urls");
            var latestVersions = new List<GitHubAction>();
            // order the incoming dataset to get the most recent and filled ones on top
            allActions = allActions.OrderByDescending(item => item.Updated)
                                   .OrderByDescending(item => String.IsNullOrEmpty(item.RepoUrl) ? "" : item.RepoUrl)
                                   .ToList();

            foreach (var action in allActions)
            {
                GitHubAction latest = null;
                if (latestVersions.Any())
                {
                    var latest2 = latestVersions.Where(item => item.Url == action.Url)
                                       .OrderByDescending(item => item.Updated)
                                       .OrderByDescending(item => String.IsNullOrEmpty(item.RepoUrl) ? "" : item.RepoUrl);

                    latest = latest2
                                       .FirstOrDefault();
                }

                // prevent adding it twice
                if (latest == null)
                {
                   latestVersions.Add(action);
                }
            }
            notEmpty = latestVersions.Where(item => !String.IsNullOrEmpty(item.RepoUrl)).Count();
            Log.Message($"End of de-dupe we have [{latestVersions.Count()}] actions with [{notEmpty}] filled repo urls");
            
            return latestVersions;
        }
    }
}
