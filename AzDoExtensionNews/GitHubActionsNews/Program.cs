using News.Library;
using System;

namespace GitHubActionsNews
{
    class Program
    {
        private const string GitHubMarketplaceUrl = "https://github.com/marketplace&type=actions";

        static void Main(string[] args)
        {
            var started = DateTime.Now;
            Console.WriteLine("Hello World!");

            ScrapeGitHubMarketPlace(GitHubMarketplaceUrl);

            Log.Message($"Duration: {(DateTime.Now - started).TotalSeconds:N2} seconds");
        }

        private static void ScrapeGitHubMarketPlace(string gitHubMarketplaceUrl)
        {
            throw new NotImplementedException();
        }
    }
}
