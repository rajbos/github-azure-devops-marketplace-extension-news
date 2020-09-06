using News.Library;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace GitHubActionsNews
{
    static class Program
    {
        private const string GitHubMarketplaceUrl = "https://github.com/marketplace?type=actions";
        private static List<GitHubAction> Actions = new List<GitHubAction>();

        static void Main(string[] args)
        {
            var queriedGitHubMarketplaceUrl = $"{GitHubMarketplaceUrl}&query=a";
            var actions = GetAllActions(queriedGitHubMarketplaceUrl);

            foreach (var action in actions)
            {
                if (!Actions.Any(item => item.Title == action.Title))
                {
                    Actions.Add(action);
                }
            }
        }

        private static List<GitHubAction> GetAllActions(string searchUrl)
        {
            var started = DateTime.Now;

            var actions = ScrapeGitHubMarketPlace(searchUrl);

            Log.Message($"Duration: {(DateTime.Now - started).TotalSeconds:N2} seconds");

            return actions;
        }

        private static List<GitHubAction> ScrapeGitHubMarketPlace(string searchUrl)
        {
            IWebDriver Driver = new ChromeDriver();
            try
            {
                Driver.Url = searchUrl;
                var actionList = ScrapePage(Driver, 1);

                Log.Message($"Found {actionList.Count} actions");
                return actionList;
            }
            catch (Exception e)
            {
                Log.Message($"Error checking the page: {e.Message}");
            }
            finally
            {
                Driver.Close();
                Driver.Quit();
            }

            return new List<GitHubAction>();
        }

        private static List<GitHubAction> ScrapePage(IWebDriver driver, int pageNumber)
        {
            var actionList = new List<GitHubAction>();
            try
            {
                // scroll the paginator into view
                var actions = new Actions(driver);

                var waitForElement = new OpenQA.Selenium.Support.UI.WebDriverWait(driver, TimeSpan.FromSeconds(5));
                try
                {
                    waitForElement.Until(ExpectedConditions.ElementExists(By.ClassName("paginate-container")));
                    waitForElement.Until(ExpectedConditions.ElementIsVisible(By.ClassName("paginate-container")));
                    waitForElement.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("paginate-container")));
                    var paginator = driver.FindElement(By.ClassName("paginate-container"));
                    actions.MoveToElement(paginator);
                    actions.Perform();
                }
                catch
                {
                    // wait some time and retry
                    waitForElement.Until(ExpectedConditions.ElementExists(By.ClassName("paginate-container")));
                    waitForElement.Until(ExpectedConditions.ElementIsVisible(By.ClassName("paginate-container")));
                    waitForElement.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("paginate-container")));
                    var paginator = driver.FindElement(By.ClassName("paginate-container"));
                    actions.MoveToElement(paginator);
                    actions.Perform();
                }

                // Scrape the page
                Log.Message("Finding all anchors");
                var anchors = driver.FindElements(By.TagName("a")).ToList();
                Log.Message("Filtering for the right anchors");
                var actionTags = anchors.Where(item => item.GetAttribute("href").StartsWith("https://github.com/marketplace/actions")).ToList();

                Log.Message($"Page {pageNumber}: Found {actionTags.Count} actions, current url: {driver.Url}");

                var sb = new StringBuilder();
                foreach (var action in actionTags)
                {
                    var ghAction = ParseAction(action, driver);
                    if (ghAction != null)
                    {
                        actionList.Add(ghAction);

                        sb.AppendLine($"\tFound action:{ghAction.Url}, {ghAction.Title}, {ghAction.Publisher}, {ghAction.Version}");
                    }
                }
                Log.Message(sb.ToString());

                // find the 'next' button
                try
                {
                    var nextButton = driver.FindElement(By.LinkText("Next"));
                    if (nextButton != null)
                    {
                        var nextUrl = nextButton.GetAttribute("href");
                        // click the next button
                        nextButton.Click();

                        waitForElement.Until(ExpectedConditions.ElementExists(By.LinkText("Next")));

                        // wait for the next button to be available again
                        waitForElement.Until(ExpectedConditions.UrlToBe(nextUrl));
                        waitForElement.Until(ExpectedConditions.ElementIsVisible(By.LinkText("Next")));
                        waitForElement.Until(ExpectedConditions.ElementIsVisible(By.ClassName("paginate-container")));

                        // scrape the new page again
                        actionList.AddRange(ScrapePage(driver, pageNumber + 1));
                    }
                }
                catch
                {
                    // if the element doesn't exist, we are at the last page
                    Log.Message($"Next button not found, current url = {driver.Url}");
                }
            }
            catch (Exception e)
            {
                Log.Message($"Error scraping page {pageNumber}. Exception message: {e.Message}{Environment.NewLine}{e.InnerException?.Message}");
            }

            return actionList;
        }

        private static GitHubAction ParseAction(IWebElement action, IWebDriver driver)
        {
            try
            {
                var hydro = action.GetAttribute("data-hydro-click");

                var data = JsonConvert.DeserializeObject(hydro);

                var divWithTitle = action.FindElement(By.TagName("h3"));
                var title = divWithTitle.Text;
                var url = action.GetAttribute("href");

                var publisherParent = divWithTitle.FindElement(By.XPath("./..")); // find parent element
                var allChildElements = publisherParent.FindElements(By.XPath(".//*")); // find all child elements                
                var publisher = allChildElements[2].Text;

                // open the url in a new tab
                action.SendKeys(Keys.Shift + "T");
                action.SendKeys(Keys.Control+ Keys.Enter);

                var newTab = driver.WindowHandles.Last();
                driver.SwitchTo().Window(newTab);
                var version = "";
                // act
                try
                {
                    version = GetVersionFromAction(driver);
                }
                catch (Exception e)
                {
                    Log.Message($"Error loading version for action with url [{url}]: {e.Message}");
                }

                // closing the current window and go back to the original tab
                driver.Close();

                var orgTab = driver.WindowHandles.First();
                driver.SwitchTo().Window(orgTab);

                return new GitHubAction
                {
                    Url = url,
                    Title = title,
                    Publisher = publisher,
                    Version = version,
                };
            }
            catch (Exception e)
            {
                Log.Message($"Error parsing action: {e.Message}");
                return null;
            }
        }

        private static string GetVersionFromAction(IWebDriver driver)
        {
            // driver.Navigate().GoToUrl(url);

            var divWithTitle = driver.FindElement(By.XPath("//*[contains(text(),'Latest version')]"));
            //Log.Message($"{divWithTitle.Text} - {divWithTitle.TagName}");
            // "contains(text(), 'Latest version')"); ;

            var publisherParent = divWithTitle.FindElement(By.XPath("./..")); // find parent element
            var allChildElements = publisherParent.FindElements(By.XPath(".//*")); // find all child elements  
            foreach (var el in allChildElements)
            {
                //Log.Message($"{el.Text} - {el.TagName}");
            }

            return allChildElements[2].Text;
        }
    }

    public class GitHubAction
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Publisher { get; set; }
        public string Version { get; internal set; }
    }
}
