using News.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHubActionsNews
{
    public static class Consolidate
    {
        public static async Task Run()
        {
            Log.Message($"Consolidating all current state");
            // download all Actions-**.json files from the storage account
            await DownloadAllActionsFiles();
            // download previous overview
            // check for changes
            // tweet changes
            // store current set as overview
        }

        private static async Task DownloadAllActionsFiles()
        {
            var started = DateTime.Now;
            Log.Message($"DownloadAllActionsFiles");

            var allActions = await Storage.DownloadAllFilesThatStartWith<GitHubAction>("Actions");
            // dedupe
            var actions = allActions.Distinct(new GitHubActionComparer());

            Log.Message($"Download all files took {(DateTime.Now - started).TotalSeconds:N2}, we have {actions.Count()} known actions");
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
