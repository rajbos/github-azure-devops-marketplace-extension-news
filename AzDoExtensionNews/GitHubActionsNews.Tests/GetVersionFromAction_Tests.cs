using Microsoft.VisualStudio.TestTools.UnitTesting;
using GitHubActionsNews;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;

namespace GitHubActionsNews.Tests
{
    [TestClass]
    public class GetVersionFromAction_Tests
    {
        private IWebDriver Driver = null;

        [TestInitialize] 
        public void TestInitialize()
        {
            var chromeOptions = new ChromeOptions();
            if (Debugger.IsAttached)
            {
                chromeOptions.AddArguments("headless");
            }
            Driver = new ChromeDriver(chromeOptions);
        }

        //[TestMethod]
        public void OnlyPrerelease_Test()
        {
            // go to url with only a prerelease version:            
            var url = "https://github.com/marketplace/actions/c-c-code-linter-clang-tidy-clang-format-and-cppcheck";
            Driver.Navigate().GoToUrl(url);

            // get version info
            var version = ActionPageInteraction.GetVersionFromAction(Driver);

            Assert.AreNotEqual("", version);
            Assert.AreNotEqual("latest", version);
        }

        //[TestMethod]
        public void LatestVersion_Test()
        {
            // go to url with a latest version:            
            var url = "https://github.com/marketplace/actions/vault-secrets";
            Driver.Navigate().GoToUrl(url);

            // get version info
            var version = ActionPageInteraction.GetVersionFromAction(Driver);

            Assert.AreNotEqual("", version);
            Assert.AreNotEqual("v2.0.0", version);
        }        

        [TestCleanup]
        public void TestCleanup()
        {
            Driver.Close();
            Driver.Quit();
        }
    }
}
