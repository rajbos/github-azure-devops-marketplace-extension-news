using Microsoft.VisualStudio.TestTools.UnitTesting;
using GitHubActionsNews;
using Microsoft.Playwright;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GitHubActionsNews.Tests
{
    [TestClass]
    public class VerifiedCreator_Tests
    {
        private IPlaywright Playwright = null;
        private IBrowser Browser = null;
        private IPage Page = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = true
            };
            Browser = await Playwright.Chromium.LaunchAsync(launchOptions);
            var context = await Browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true
            });
            Page = await context.NewPageAsync();
        }

        [TestMethod]
        public async Task VerifiedCreator_ActionsCheckout_IsVerified()
        {
            // Test with a known verified action
            var url = "https://github.com/marketplace/actions/checkout";
            await Page.GotoAsync(url);
            await Task.Delay(2000);

            var verifiedCreatorLocator = Page.Locator("text=Verified creator");
            var isVerified = await verifiedCreatorLocator.CountAsync() > 0;

            Assert.IsTrue(isVerified, "actions/checkout should be a verified creator");
        }

        [TestMethod]
        public async Task VerifiedCreator_SetupGoTask_IsNotVerified()
        {
            // Test with the action mentioned in the issue that should NOT be verified
            var url = "https://github.com/marketplace/actions/setup-go-task-task-taskfile";
            await Page.GotoAsync(url);
            await Task.Delay(2000);

            var verifiedCreatorLocator = Page.Locator("text=Verified creator");
            var isVerified = await verifiedCreatorLocator.CountAsync() > 0;

            Assert.IsFalse(isVerified, "setup-go-task should NOT be a verified creator");
        }

        [TestMethod]
        public async Task VerifiedCreator_SuperLinter_IsVerified()
        {
            // Test with another known verified action
            var url = "https://github.com/marketplace/actions/super-linter";
            await Page.GotoAsync(url);
            await Task.Delay(2000);

            var verifiedCreatorLocator = Page.Locator("text=Verified creator");
            var isVerified = await verifiedCreatorLocator.CountAsync() > 0;

            Assert.IsTrue(isVerified, "super-linter should be a verified creator");
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
