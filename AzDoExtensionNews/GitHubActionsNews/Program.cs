using News.Library;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GitHubActionsNews
{
    static class Program
    {
        private const string GitHubMarketplaceUrl = "https://github.com/marketplace?type=actions";

        static void Main(string[] args)
        {
            var started = DateTime.Now;

            var actions = ScrapeGitHubMarketPlace();

            Log.Message($"Duration: {(DateTime.Now - started).TotalSeconds:N2} seconds");
        }

        private static List<GitHubAction> ScrapeGitHubMarketPlace()
        {
            IWebDriver Driver = new ChromeDriver();
            try
            {
                Driver.Url = GitHubMarketplaceUrl;
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
                var anchors = driver.FindElements(By.TagName("a")).ToList();
                var actionTags = anchors.Where(item => item.GetAttribute("href").StartsWith("https://github.com/marketplace/actions")).ToList();

                Log.Message($"Page {pageNumber}: Found {actionTags.Count} actions, current url: {driver.Url}");

                foreach (var action in actionTags)
                {
                    var ghAction = ParseAction(action);
                    if (ghAction != null)
                    {
                        actionList.Add(ghAction);
                    }
                }

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
                        //Thread.Sleep(5 * 1000);

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

        private static GitHubAction ParseAction(IWebElement action)
        {
            try
            {
                var hydro = action.GetAttribute("data-hydro-click");

                var data = JsonConvert.DeserializeObject(hydro);

                var title = action.FindElement(By.TagName("h3")).Text;
                var url = action.GetAttribute("href") ;

                Log.Message($"\tFound action:{url}, {title}");

                return new GitHubAction
                {
                    Url = url,
                    Title = title,
                };
            }
            catch (Exception e)
            {
                Log.Message($"Error parsing action: {e.Message}");
                return null;
            }
        }
    }

    public class GitHubAction
    {
        public string Url { get; set; }
        public string Title { get; set; }
    }
}
