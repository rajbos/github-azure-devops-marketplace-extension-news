using GitHubActionsNews;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using News.Library;
using System;
using System.Collections.Generic;

namespace GitHubActionsNews.Tests
{
    [TestClass]
    public class BuildExistingActionsByUrlLookup_Tests
    {
        [TestMethod]
        public void BuildExistingActionsByUrlLookup_KeysByUrl_CaseInsensitively()
        {
            var actions = new List<GitHubAction>
            {
                new GitHubAction { Url = "https://github.com/marketplace/actions/checkout", RepoUrl = "https://github.com/actions/checkout", Updated = DateTime.UtcNow }
            };

            var lookup = Program.BuildExistingActionsByUrlLookup(actions);

            Assert.IsTrue(lookup.ContainsKey("HTTPS://GITHUB.COM/MARKETPLACE/ACTIONS/CHECKOUT"));
            Assert.AreEqual("https://github.com/actions/checkout", lookup["https://github.com/marketplace/actions/checkout"].RepoUrl);
        }

        [TestMethod]
        public void BuildExistingActionsByUrlLookup_SkipsEntriesWithoutUrl()
        {
            var actions = new List<GitHubAction>
            {
                new GitHubAction { Url = null, RepoUrl = "https://github.com/actions/checkout" },
                new GitHubAction { Url = "", RepoUrl = "https://github.com/actions/cache" }
            };

            var lookup = Program.BuildExistingActionsByUrlLookup(actions);

            Assert.AreEqual(0, lookup.Count);
        }

        [TestMethod]
        public void BuildExistingActionsByUrlLookup_PrefersMostRecentlyUpdated_OnDuplicateUrls()
        {
            var url = "https://github.com/marketplace/actions/checkout";
            var actions = new List<GitHubAction>
            {
                new GitHubAction { Url = url, RepoUrl = "old-repo-url", Updated = DateTime.UtcNow.AddDays(-2) },
                new GitHubAction { Url = url, RepoUrl = "new-repo-url", Updated = DateTime.UtcNow }
            };

            var lookup = Program.BuildExistingActionsByUrlLookup(actions);

            Assert.AreEqual(1, lookup.Count);
            Assert.AreEqual("new-repo-url", lookup[url].RepoUrl);
        }
    }
}
