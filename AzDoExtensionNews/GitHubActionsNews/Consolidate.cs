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
                    var previousVersion = allActions.FirstOrDefault(item => item.Url == action.Url);
                    var tweetText = "";
                    if (previousVersion == null)
                    {
                        // tweet new action
                        tweetText = $"A new GitHub Action has been added to the marketplace!" + Environment.NewLine + $"Check out {action.Title} from {action.Publisher} at {action.Url}";
                    }
                    else if (action.Version != previousVersion.Version)
                    {
                        // tweet changes
                        tweetText = $"GitHub Action {action.Title} from {action.Publisher} has been updated to version {action.Version}. Find it here: {action.Url}";
                    }

                    if (!string.IsNullOrEmpty(tweetText))
                    {
                        // send the tweet
                        twitter.SendTweet(tweetText, "");
                        tweetsSend++;
                    }
                }

                Log.Message($"Send {tweetsSend} tweets out in this run");
            }

            if (tweetsSend > 0 && updatedActions.Count > 0)
            {
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
            var actions = allActions.Distinct(new GitHubActionComparer());

            Log.Message($"Download all files took {(DateTime.Now - started).TotalSeconds:N2}, we have {actions.Count()} known actions");

            return actions;
        }
    }

    internal class GitHubActionComparer : IEqualityComparer<GitHubAction>
    {
        public bool Equals(GitHubAction x, GitHubAction y)
        {
            return
                string.Equals(x.Title, y.Title, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Publisher, y.Publisher, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Url, y.Url, StringComparison.OrdinalIgnoreCase)
                ;
        }

        public int GetHashCode(GitHubAction obj)
        {
            return obj.Title.GetHashCode();
        }
    }
}
