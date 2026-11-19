using Microsoft.VisualStudio.TestTools.UnitTesting;
using GitHubActionsNews;
using Microsoft.Playwright;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GitHubActionsNews.Tests
{
    [TestClass]
    public class GetVersionFromAction_Tests
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

        //[TestMethod]
        public async Task OnlyPrerelease_Test()
        {
            // go to url with only a prerelease version:            
            var url = "https://github.com/marketplace/actions/c-c-code-linter-clang-tidy-clang-format-and-cppcheck";
            await Page.GotoAsync(url);

            // get version info
            var version = await ActionPageInteraction.GetVersionFromAction(Page);

            Assert.AreNotEqual("", version);
            Assert.AreNotEqual("latest", version);
        }

        //[TestMethod]
        public async Task LatestVersion_Test()
        {
            // go to url with a latest version:            
            var url = "https://github.com/marketplace/actions/vault-secrets";
            await Page.GotoAsync(url);

            // get version info
            var version = await ActionPageInteraction.GetVersionFromAction(Page);

            Assert.AreNotEqual("", version);
            Assert.AreNotEqual("v2.0.0", version);
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
