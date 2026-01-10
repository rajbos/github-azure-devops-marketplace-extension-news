using System;
using System.Linq;
using System.Threading.Tasks;
using GitHubActionsNews;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitHubActionsNews.Tests
{
    [TestClass]
    public class SearchPagination_Tests
    {
        [TestMethod]
        [TestCategory("MarketplaceScraping")]
        public async Task SearchForMy_TraversesPaginationAndReturnsManyActions()
        {
            const string query = "devops+actions";
            const string queryUrl = "https://github.com/marketplace?type=actions&query=" + query;
            const int pageSize = 20;

            var actions = await Program.GetAllActionsAsync(queryUrl);

            Assert.IsNotNull(actions, "Expected the scraper to return a list of actions.");
            Assert.IsGreaterThanOrEqualTo(actions.Count, pageSize, $"Expected query [{query}] to produce more results than a single marketplace page.");

            var distinctUrls = actions
                .Select(action => action.Url)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            Assert.IsGreaterThanOrEqualTo(distinctUrls, pageSize, "Expected the scraper to collect unique actions across multiple pages.");
        }
    }
}
