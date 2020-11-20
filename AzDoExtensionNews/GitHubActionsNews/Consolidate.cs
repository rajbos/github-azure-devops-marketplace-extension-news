using News.Library;
using System;
using System.Collections.Generic;
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

            var actions = await Storage.DownloadAllFilesThatStartWith<GitHubAction>("Actions");

            Log.Message($"Download all files took {(DateTime.Now - started).TotalSeconds:N2}, we have {actions.Count} known actions");
        }
    }
}
