using News.Library;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GitHubActionsNews
{
    static class Program
    {
        private const string GitHubMarketplaceUrl = "https://github.com/marketplace?type=actions";
        private static readonly string StorageFileName = "Actions";
        private static Twitter twitter;

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Log.Message("Please add a parameter to the run:");
                Log.Message(" all = run through each action result in the list");
                Log.Message(" one or more comma separated letters = run through each action result that matches the search string");
                Log.Message(" consolidate = download all previous result files and consolidate to 1 file");
                Log.Message(" verify = verify all actions in the storage account for overlap");
                Log.Message(" test = run for a single test action to debug");
            }

            Configuration.LoadSettings();

            twitter = new Twitter(Configuration.TwitterConsumerAPIKey,
                                  Configuration.TwitterConsumerAPISecretKey,
                                  Configuration.TwitterAccessToken,
                                  Configuration.TwitterAccessTokenSecret);

            switch (args[0])
            {
                case "all":
                    await RunGroupAction();
                    break;
                case "consolidate":
                    await RunConsolidate();
                    break;
                case "verify":
                    await RunVerify();
                    break;
                case "test":
                    RunTest();
                    break;
                default:
                    // get all actions from the GitHub marketplace for the given letters
                    _ = GetAllActionsFromLetters(args);
                    break;
            }
        }

        private static void RunTest()
        {
            var driver = GetDriver();
            try
            {
                // configure for testing either a single action or a search page
                var runSingleActionTest = false;
                if (runSingleActionTest) {
                    // run for a single action page
                    driver.Navigate().GoToUrl("https://github.com/marketplace/actions/glo-parse-card-links");
                    var version = ActionPageInteraction.GetVersionFromAction(driver);
                    var url = ActionPageInteraction.GetRepoFromAction(driver);
                    Log.Message($"Found version [{version}] and url [${url}]");
                }
                else {
                    // run fo a search page
                    var twoLetterQuery = "ca";
                    var queriedGitHubMarketplaceUrl = $"{GitHubMarketplaceUrl}&query={twoLetterQuery}";
                    var actions = GetAllActions(queriedGitHubMarketplaceUrl);
                }
            }
            finally
            {
                driver.Close();
                driver.Quit();
            }
        }

        private static async Task RunConsolidate()
        {
            await Consolidate.Run(twitter);
        }

        private static async Task RunVerify()
        {
            await Verify.Run();
        }

        private static async Task RunGroupAction()
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

            // search example
            SearchFor(actualList, "aqua");
            SearchFor(actualList, "Trivy");
            SearchFor(actualList, "Secrets");
        }

        private static void SearchFor(List<GitHubAction> actualList, string startsWith)
        {
            var items = actualList.Where(item => item.Title.Contains(startsWith, StringComparison.InvariantCultureIgnoreCase));
            Log.Message($"Found {items.Count()} items containing '{startsWith}'");
            foreach (var item in items)
            {
                Log.Message($"\t{item.Title}");
            }
        }

        private static List<List<GitHubAction>> GetAllActionsFromLetters(string[] args)
        {
            List<List<GitHubAction>> allActions = new List<List<GitHubAction>>();
            var searchList = args;
            Parallel.ForEach(searchList, item =>
            {
                var actions = GetActionsForSearchQuery(item);
                allActions.Add(actions);
            });
            return allActions;
        }

        private static List<GitHubAction> GetActionsForSearchQuery(string query)
        {
            var actions = new List<GitHubAction>();
            var started = DateTime.Now;
            // check if query is a single letter, but is not a number
            if (query.Length == 1 && !int.TryParse(query, out var a)) {
                // running the search for individual letters is to slow and has to much results (pagination stops at 1000 results)
                // run for all two letter combinations instead
                var letters = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
                // some letter combos still return over a 1000 results
                var skipLetterCombo = new List<string>() { "ba", "be", "bo", "bu", "by", "ci", "ch", "cl", "co", "gi", "ma", "up", "of", "or", "pa", "pr", "pu", "py", "re", "sa", "se", "to", "te", "lo", "le", "ng", "ns", "on", "or", "ta", "te", "th", "ti", "ub", "up", "et", "el", "er", "en", "ct", "cu", "we", "wi", "wo", };
                foreach (var letter in letters)
                {
                    var twoLetterQuery = $"{query}{letter}";
                    if (!skipLetterCombo.Contains(twoLetterQuery)) {
                        Log.Message($"Loading latest states for all actions starting with [{twoLetterQuery}]");
                        var queriedGitHubMarketplaceUrl = $"{GitHubMarketplaceUrl}&query={twoLetterQuery}";
                        actions.AddRange(GetAllActions(queriedGitHubMarketplaceUrl));
                    }
                }
            }
            else
            {
                Log.Message($"Loading latest states for all actions starting with [{query}]");

                var queriedGitHubMarketplaceUrl = $"{GitHubMarketplaceUrl}&query={query}";
                actions.AddRange(GetAllActions(queriedGitHubMarketplaceUrl));
            }

            if (actions.Count() == 0)
            {
                // this is strange, we should have found some actions
                // throw so that a run will fail and e.g. a workflow indicates failure
                throw new Exception($"No actions found for query [{query}]");
            }

            var storeFileName = $"{StorageFileName}-{query}";
            // get existing actions for this query:
            var existingActions = Storage.ReadFromJson<GitHubAction>(storeFileName, storeFileName);

            // tweet about updates and new actions:
            foreach (var action in actions)
            {
                var allActionsWithThisTitle = existingActions
                                        .Where(item => item.Title == action.Title);

                var existingAction = allActionsWithThisTitle
                                        .OrderByDescending(item => item.Updated)
                                        .LastOrDefault();

                if (existingAction == null)
                {
                    Log.Message($"Found a new action: {action.Title}");
                    existingActions.Add(action);
                }
                else
                {
                    // remove any other action from the list that has the same title
                    if (allActionsWithThisTitle.Count() > 1)
                    {
                        var list = allActionsWithThisTitle.ToList();
                        for (var i = 0; i < list.Count(); i++)
                        {
                            var current = list[i];
                            if (current.Title == existingAction.Title && current.Updated != existingAction.Updated)
                            {
                                // delete this one from the list
                                Log.Message($"Removing duplicate entry for [{current.Title}] and [{current.Updated}]");
                                var toRemove = existingActions.First(item => item.Title == current.Title && item.Updated == current.Updated);
                                existingActions.Remove(toRemove);
                            }
                        }
                    }

                    // check version number
                    if (existingAction.Version != action.Version && action.Version.IndexOf(Constants.ErrorText) == -1)
                    {
                        Log.Message($"Found an updated action: {action.Title}: old version [{existingAction.Version}], new version [{action.Version}]");

                        // update
                        existingAction.Version = action.Version;
                        existingAction.Url = action.Url;
                        existingAction.Publisher = action.Publisher;
                        existingAction.Updated = DateTime.UtcNow;
                        existingAction.RepoUrl = action.RepoUrl;
                        existingAction.Verified = action.Verified;
                    }
                    else
                    {
                        // always update the repo url since that is empty in older runs
                        Log.Message($"No further changes, but updating the repoUrl for [{existingAction.Url}] to [{existingAction.RepoUrl}]");
                        existingAction.RepoUrl = action.RepoUrl;
                    }
                }
            }

            var count = existingActions.Where(item => !String.IsNullOrEmpty(item.RepoUrl)).Count();
            Log.Message($"Found [{existingActions.Count}] unique actions with [{count}] repo urls for query [{query}] in {(DateTime.Now - started).TotalMinutes:N2} minutes", logsummary: true);

            // store the new information:
            Storage.SaveJson(existingActions, storeFileName);

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
                var emptyRepoUrl = actionList.Where(x => String.IsNullOrEmpty(x.RepoUrl)).Count();

                Log.Message($"Found {actionList.Count} actions for search url [{searchUrl}] in {(DateTime.Now - started).TotalMinutes:N2} minutes, with [{emptyRepoUrl}] not filled repo urls", logsummary: true);
                if (actionList.Count == 1000)
                {
                    Log.Message($"::error ::Found 1000 actions for searchurl [{searchUrl}], this is the maximum amount of actions that can be found on a single page, so time to split this search query into multiple queries", logsummary: true);
                }
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
            chromeOptions.AddArguments("headless"); // Run Chrome in headless mode
            if (!System.Diagnostics.Debugger.IsAttached && Environment.GetEnvironmentVariable("CODESPACES") == null)
            {
                chromeOptions.AddArguments("headless"); // Run Chrome in headless mode
            }
            chromeOptions.AddArguments("--no-sandbox"); // Bypass OS security model
            chromeOptions.AddArguments("--disable-dev-shm-usage"); // Overcome limited resource problems
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
                var actionAnchors = new List<IWebElement>();
                foreach (var anchor in anchors)
                {
                    var href = anchor.GetAttribute("href");
                    if (href != null && href.StartsWith("https://github.com/marketplace/actions/"))
                    {
                        actionAnchors.Add(anchor);
                    }
                }
                //var actionAnchors = anchors.Where(item => item.GetAttribute("href").StartsWith("/marketplace/actions"));
                var actionTags = actionAnchors.ToList();

                Log.Message($"Page {pageNumber}: Found {actionTags.Count} actions, current url: {driver.Url}", logger);

                foreach (var action in actionTags)
                {
                    var ghAction = ParseAction(action, driver);
                    Thread.Sleep(2000); // try to cut down on ratelimit messages
                    if (ghAction != null)
                    {
                        actionList.Add(ghAction);

                        logger.AppendLine($"\tFound action:{ghAction.Url}, {ghAction.Title}, {ghAction.Publisher}, Version:{ghAction.Version}, RepoUrl: {ghAction.RepoUrl}, Verified: {ghAction.Verified}");
                    }
                }

                // find the 'next' button
                IWebElement nextButton = null;
                try
                {
                    nextButton = driver.FindElement(By.LinkText("Next"));
                }
                catch
                {
                    // if the element doesn't exist, we are at the last page
                    Log.Message($"Next button not found, current url = [{driver.Url}] on pageNumber [{pageNumber}], logger");
                }

                if (nextButton != null)
                {
                    var nextUrl = nextButton.GetAttribute("href");
                    // click the next button
                    nextButton.Click();

                    waitForElement.Until(ExpectedConditions.ElementExists(By.LinkText("Next")));

                    // wait for the next button to be available again
                    waitForElement.Until(ExpectedConditions.UrlToBe(nextUrl));
                    // the 'next' link is not a link in the last page, so by waiting for it we would miss that page!
                    // waitForElement.Until(ExpectedConditions.ElementIsVisible(By.LinkText("Next")));
                    waitForElement.Until(ExpectedConditions.ElementIsVisible(By.ClassName("paginate-container")));

                    // scrape the new page again
                    actionList.AddRange(ScrapePage(driver, pageNumber + 1, logger));
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
            var elementName = "TablePaginationSteps";
            try
            {
                waitForElement.Until(ExpectedConditions.ElementExists(By.ClassName(elementName)));
                waitForElement.Until(ExpectedConditions.ElementIsVisible(By.ClassName(elementName)));
                waitForElement.Until(ExpectedConditions.ElementToBeClickable(By.ClassName(elementName)));
                var paginator = driver.FindElement(By.ClassName(elementName));
                actions.MoveToElement(paginator);
                actions.Perform();
            }
            catch
            {
                // wait some time and retry
                Thread.Sleep(1000);
                waitForElement.Until(ExpectedConditions.ElementExists(By.ClassName(elementName)));
                waitForElement.Until(ExpectedConditions.ElementIsVisible(By.ClassName(elementName)));
                waitForElement.Until(ExpectedConditions.ElementToBeClickable(By.ClassName(elementName)));
                var paginator = driver.FindElement(By.ClassName(elementName));
                actions.MoveToElement(paginator);
                actions.Perform();
            }

            try
            {
                var bannerClassName = "hx_cookie-banner";
                var banner = driver.FindElement(By.ClassName(bannerClassName));

                IJavaScriptExecutor js = driver as IJavaScriptExecutor;
                js.ExecuteScript("arguments[0].style='display: none;'", banner);
            }
            catch
            {
                // nom nom nom
            }

            return waitForElement;
        }

        private static GitHubAction ParseAction(IWebElement action, IWebDriver driver)
        {
            try
            {
                // unfortunately, the hydro info has not enough data so we need to find the missing data points
                //var hydro = action.GetAttribute("data-hydro-click");
                //var data = JsonConvert.DeserializeObject(hydro);

                //var divWithTitle = action.FindElement(By.TagName("h3"));
                //var title = divWithTitle.Text;
                var url = action.GetAttribute("href");
                var divWithTitle = action;
                // find the action title in the ::before property
                // IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                // string script = "return window.getComputedStyle(document.querySelector('selector'),':before').getPropertyValue('content');";
                // string content = (string)js.ExecuteScript(script);
                var title = "content";
                var publisherParent = divWithTitle.FindElement(By.XPath("./..")); // find parent element
                var allChildElements = publisherParent.FindElements(By.XPath(".//*")); // find all child elements
                //var publisher = allChildElements[2].Text; // is empty, find on the detail page
                var publisher = "";

                // open the url in a new tab
                action.SendKeys(Keys.Shift + "T");
                action.SendKeys(Keys.Control + Keys.Enter);

                var newTab = driver.WindowHandles.Last();
                driver.SwitchTo().Window(newTab);
                var version = "";
                var actionRepoUrl = "";
                var verified = false;
                Thread.Sleep(2000);
                if (driver.Title.StartsWith("about:blank"))
                {
                    Thread.Sleep(2000); // we need more time for the page to load
                }

                if (!driver.Title.StartsWith("Page not found"))
                {
                    // act
                    try
                    {
                        // check if the verified class exists
                        try {
                            var el = driver.FindElements(By.ClassName("octicon-verified"));
                            if (el != null && el.Count > 0) {
                                verified = true;
                            }
                        }
                        catch {
                            // verified class not found, use default value
                        }

                        version = ActionPageInteraction.GetVersionFromAction(driver);
                        Log.Message($"Found version [{version}] for url [{url}]");
                        try
                        {
                            actionRepoUrl = ActionPageInteraction.GetRepoFromAction(driver);
                            if (string.IsNullOrEmpty(actionRepoUrl))
                                throw new Exception("Did not find action repo url");
                            else
                                Log.Message($"Found repoUrl [{actionRepoUrl}] for url [{url}]");
                        }
                        catch (Exception e)
                        {
                            Log.Message($"Error loading action repo url for action with url [{url}]: {e.Message}, Page title:{driver.Title}, the version we got is: [{version}]");
                            var source = driver.PageSource.ToString();
                            Console.WriteLine(source);
                        }

                        //publisher = ActionPageInteraction.GetVerifiedPublisherFromAction(driver);
                        publisher = GetPublisher(actionRepoUrl);

                        title = ActionPageInteraction.GetTitleFromAction(driver);
                        Log.Message($"Found title [{title}] for url [{url}]");
                    }
                    catch (Exception e)
                    {
                        Log.Message($"Error loading version for action with url [{url}]: {e.Message}, Page title:{driver.Title}");
                    }
                }
                else
                {
                    Log.Message($"Action detail page 404's with url [{url}], Page title:{driver.Title}");
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
                    Updated = DateTime.UtcNow,
                    RepoUrl = actionRepoUrl,
                    Verified = verified
                };
            }
            catch (Exception e)
            {
                Log.Message($"Error parsing action: {e.Message}");
                return null;
            }
        }

        private static string GetPublisher(string url)
        {
            // cut the string to the first /
            var firstSlash = url.IndexOf("/");
            // return the first part of the url
            return url.Substring(0, firstSlash);
        }
    }
}
