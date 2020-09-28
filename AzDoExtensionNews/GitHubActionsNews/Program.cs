using News.Library;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GitHubActionsNews
{
    static class Program
    {
        private const string GitHubMarketplaceUrl = "https://github.com/marketplace?type=actions";
        private static readonly string StorageFileName = "Actions";

        static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "all")
            {
                await RunGroupAction();
            }
            else
            {
                // get all actions from the GitHub marketplace for the given letters
                _ = GetAllActionsFromLetters(args);
            }
        }

        private static async Task  RunGroupAction()
        {
            Log.Message("Running group action");
            var allItems = await Storage.DownloadAllFilesThatStartWith<GitHubAction>(StorageFileName);

            var actualList = new List<GitHubAction>();
            foreach (var item in allItems)
            {
                if (!actualList.Any(element => element.Title == item.Title))
                {
                    actualList.Add(item);
                }
            }

            Log.Message($"We currently have {actualList.Count} unique actions stored with their version");
            Log.Message($"");
        }

        private static List<List<GitHubAction>> GetAllActionsFromLetters(string[] args)
        {
            List<List<GitHubAction>> allActions = new List<List<GitHubAction>>();
            // skipping common letters to prevent lots of double searches
            var searchList = args;
            //var searchList = new List<string> { "b", "c", "d" }; 
            //var searchList = new List<string> { "f", "g", "h", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "v", "w", "y", "z" };
            //var searchList = new List<string> { "s", "t", "v", "w", "y", "z" };
            Parallel.ForEach(searchList, item =>
            {
                var actions = GetActionsForSearchQuery(item);
                allActions.Add(actions);
            });
            return allActions;
        }

        private static List<GitHubAction> GetActionsForSearchQuery(string query)
        {
            var storeFileName = $"{StorageFileName}-{query}";

            var queriedGitHubMarketplaceUrl = $"{GitHubMarketplaceUrl}&query={query}";
            var actions = GetAllActions(queriedGitHubMarketplaceUrl);

            // get existing actions for this query:
            var existingActions = Storage.ReadFromJson<GitHubAction>(storeFileName, storeFileName);

            // tweet about updates and new actions:
            foreach (var action in actions)
            {
                var existingAction = existingActions.FirstOrDefault(item => item.Title == action.Title);
                if (existingAction == null)
                {
                    existingActions.Add(action);
                    // tweet
                    // todo
                }
                else
                {
                    // check version number
                    if (existingAction.Version != action.Version)
                    {
                        // update
                        existingAction.Version = action.Version;
                        existingAction.Url = action.Url;
                        existingAction.Publisher = action.Publisher;

                        // tweet
                        // todo
                    }
                }
            }

            Log.Message($"Found [{existingActions.Count}] unique actions");
            // store the new information:
            Storage.SaveJson(actions, storeFileName);

            return actions;
        }

        private static List<GitHubAction> GetAllActions(string searchUrl)
        {
            var actions = ScrapeGitHubMarketPlace(searchUrl);

            return actions;
        }

        private static List<GitHubAction> ScrapeGitHubMarketPlace(string searchUrl)
        {
            var started = DateTime.Now;
            var driver = GetDriver();

            try
            {
                driver.Url = searchUrl;
                var sb = new StringBuilder();
                var actionList = ScrapePage(driver, 1, sb);

                Log.Message($"Found {actionList.Count} actions for search url [{searchUrl}] in {(DateTime.Now - started).TotalMinutes:N2} minutes");
                return actionList;
            }
            catch (Exception e)
            {
                Log.Message($"Error checking the page: {e.Message}");
            }
            finally
            {
                driver.Close();
                driver.Quit();
            }

            return new List<GitHubAction>();
        }

        private static ChromeDriver GetDriver()
        {
            var chromeOptions = new ChromeOptions();
            if (!Debugger.IsAttached)
            {
                chromeOptions.AddArguments("headless");
            }
            var driver = new ChromeDriver(chromeOptions);
            return driver;
        }

        private static List<GitHubAction> ScrapePage(IWebDriver driver, int pageNumber, StringBuilder logger)
        {
            var actionList = new List<GitHubAction>();
            try
            {
                WebDriverWait waitForElement = ScrollPaginatorIntoView(driver);

                // Scrape the page
                var anchors = driver.FindElements(By.TagName("a")).ToList();
                var actionTags = anchors.Where(item => item.GetAttribute("href").StartsWith("https://github.com/marketplace/actions")).ToList();

                Log.Message($"Page {pageNumber}: Found {actionTags.Count} actions, current url: {driver.Url}", logger);

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
                        actionList.AddRange(ScrapePage(driver, pageNumber + 1, logger));
                    }
                }
                catch
                {
                    // if the element doesn't exist, we are at the last page
                    Log.Message($"Next button not found, current url = [{driver.Url}] on pageNumber [{pageNumber}], logger");
                }
            }
            catch (Exception e)
            {   
                Log.Message($"Error scraping page {pageNumber}. Exception message: {e.Message}{Environment.NewLine}{e.InnerException?.Message}");
                Log.Message($"Logs for run with url [{driver.Url}]:");
                Log.Message(logger.ToString());
            }

            return actionList;
        }

        private static WebDriverWait ScrollPaginatorIntoView(IWebDriver driver)
        {
            // scroll the paginator into view
            var actions = new Actions(driver);

            var waitForElement = new OpenQA.Selenium.Support.UI.WebDriverWait(driver, TimeSpan.FromSeconds(1));
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

            return waitForElement;
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
                    version = ActionPageInteraction.GetVersionFromAction(driver);
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
    }

    public class GitHubAction
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Publisher { get; set; }
        public string Version { get; internal set; }
    }
}
