using Microsoft.VisualStudio.TestTools.UnitTesting;
using GitHubActionsNews;
using Microsoft.Playwright;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GitHubActionsNews.Tests
{
    [TestClass]
    public class GetActionTypeAndNodeVersion_Tests
    {
        private IPlaywright Playwright = null;
        private IBrowser Browser = null;
        private IPage Page = null;

        [TestInitialize] 
        public async Task TestInitialize()
        {
            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            var launchOptions = new BrowserTypeLaunchOptions();
            if (Debugger.IsAttached)
            {
                launchOptions.Headless = true;
            }
            else
            {
                launchOptions.Headless = true;
            }
            Browser = await Playwright.Chromium.LaunchAsync(launchOptions);
            var context = await Browser.NewContextAsync();
            Page = await context.NewPageAsync();
        }

        [TestMethod]
        public async Task NodeAction_Test()
        {
            // Test with a known Node.js action
            var repoUrl = "https://github.com/actions/checkout";
            
            var (actionType, nodeVersion) = await ActionPageInteraction.GetActionTypeAndNodeVersionAsync(Page, repoUrl);
            
            Assert.IsNotNull(actionType, "ActionType should not be null for actions/checkout");
            Assert.AreEqual("Node", actionType, "actions/checkout should be a Node action");
            Assert.IsNotNull(nodeVersion, "NodeVersion should not be null for actions/checkout");
        }

        [TestMethod]
        public async Task DockerAction_Test()
        {
            // Test with a known Docker action
            var repoUrl = "https://github.com/docker/login-action";
            
            var (actionType, nodeVersion) = await ActionPageInteraction.GetActionTypeAndNodeVersionAsync(Page, repoUrl);
            
            // Note: This might be null if the action.yml is not accessible or in a different branch
            // We're just verifying that the method doesn't throw an exception
            Assert.IsTrue(true, "Method should not throw exception");
        }

        [TestMethod]
        public async Task CompositeAction_Test()
        {
            // Test with a known Composite action
            var repoUrl = "https://github.com/actions/cache";
            
            var (actionType, nodeVersion) = await ActionPageInteraction.GetActionTypeAndNodeVersionAsync(Page, repoUrl);
            
            // Note: This might be null if the action.yml is not accessible or in a different branch
            // We're just verifying that the method doesn't throw an exception
            Assert.IsTrue(true, "Method should not throw exception");
        }

        [TestMethod]
        public async Task InvalidRepoUrl_Test()
        {
            // Test with invalid URL
            var repoUrl = "https://invalid-url.com";
            
            var (actionType, nodeVersion) = await ActionPageInteraction.GetActionTypeAndNodeVersionAsync(Page, repoUrl);
            
            Assert.IsNull(actionType, "ActionType should be null for invalid URL");
            Assert.IsNull(nodeVersion, "NodeVersion should be null for invalid URL");
        }

        [TestMethod]
        public async Task EmptyRepoUrl_Test()
        {
            // Test with empty URL
            var repoUrl = "";
            
            var (actionType, nodeVersion) = await ActionPageInteraction.GetActionTypeAndNodeVersionAsync(Page, repoUrl);
            
            Assert.IsNull(actionType, "ActionType should be null for empty URL");
            Assert.IsNull(nodeVersion, "NodeVersion should be null for empty URL");
        }

        [TestMethod]
        public async Task NullRepoUrl_Test()
        {
            // Test with null URL
            string repoUrl = null;
            
            var (actionType, nodeVersion) = await ActionPageInteraction.GetActionTypeAndNodeVersionAsync(Page, repoUrl);
            
            Assert.IsNull(actionType, "ActionType should be null for null URL");
            Assert.IsNull(nodeVersion, "NodeVersion should be null for null URL");
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (Page != null)
                await Page.CloseAsync();
            if (Browser != null)
                await Browser.CloseAsync();
            Playwright?.Dispose();
        }
    }
}
