using News.Library;
using Microsoft.Playwright;
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
            RunTestAsync().GetAwaiter().GetResult();
        }

        private static async Task RunTestAsync()
        {
            using var playwright = await Playwright.CreateAsync();
            var browser = await GetBrowser(playwright);
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            
            try
            {
                // configure for testing either a single action or a search page
                var runSingleActionTest = false;
                if (runSingleActionTest)
                {
                    // run for a single action page
                    await page.GotoAsync("https://github.com/marketplace/actions/glo-parse-card-links");
                    var version = await ActionPageInteraction.GetVersionFromAction(page);
                    var url = await ActionPageInteraction.GetRepoFromAction(page);
                    Log.Message($"Found version [{version}] and url [${url}]");
                }
                else
                {
                    // run fo a search page
                    var twoLetterQuery = "ca";
                    var queriedGitHubMarketplaceUrl = $"{GitHubMarketplaceUrl}&query={twoLetterQuery}";
                    var actions = await GetAllActionsAsync(queriedGitHubMarketplaceUrl);
                }
            }
            finally
            {
                await page.CloseAsync();
                await context.CloseAsync();
                await browser.CloseAsync();
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
            return GetActionsForSearchQueryAsync(query).GetAwaiter().GetResult();
        }

        private static async Task<List<GitHubAction>> GetActionsForSearchQueryAsync(string query)
        {
            var actions = new List<GitHubAction>();
            var started = DateTime.Now;
            // check if query is a single letter, but is not a number
            if (query.Length == 1 && !int.TryParse(query, out var a))
            {
                // running the search for individual letters is to slow and has to much results (pagination stops at 1000 results)
                // run for all two letter combinations instead
                var letters = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
                // some letter combos still return over a 1000 results
                var skipLetterCombo = new List<string>() { "ba", "be", "bo", "bu", "by", "ci", "ch", "cl", "co", "gi", "ma", "up", "of", "or", "pa", "pr", "pu", "py", "re", "sa", "se", "to", "te", "lo", "le", "ng", "ns", "on", "or", "ta", "te", "th", "ti", "ub", "up", "et", "el", "er", "en", "ct", "cu", "we", "wi", "wo", };
                foreach (var letter in letters)
                {
                    var twoLetterQuery = $"{query}{letter}";
                    if (!skipLetterCombo.Contains(twoLetterQuery))
                    {
                        Log.Message($"Loading latest states for all actions starting with [{twoLetterQuery}]");
                        var queriedGitHubMarketplaceUrl = $"{GitHubMarketplaceUrl}&query={twoLetterQuery}";
                        actions.AddRange(await GetAllActionsAsync(queriedGitHubMarketplaceUrl));
                    }
                }
            }
            else
            {
                Log.Message($"Loading latest states for all actions starting with [{query}]");

                var queriedGitHubMarketplaceUrl = $"{GitHubMarketplaceUrl}&query={query}";
                actions.AddRange(await GetAllActionsAsync(queriedGitHubMarketplaceUrl));
            }

            if (actions.Count == 0 && 1 == 3) // this is a temporary fix to avoid an exception when running with the new UI refresh
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
                    if (existingAction?.Version != action?.Version && action?.Version?.IndexOf(Constants.ErrorText) == -1)
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
            return GetAllActionsAsync(searchUrl).GetAwaiter().GetResult();
        }

        private static async Task<List<GitHubAction>> GetAllActionsAsync(string searchUrl)
        {
            try
            {
                var actions = await ScrapeGitHubMarketPlaceAsync(searchUrl);

                return actions;
            }
            catch (Exception e)
            {
                Log.Message($"Error getting all actions from GitHub marketplace for searchUrl [{searchUrl}]: {e.Message}");
            }
            return [];
        }

        private static async Task<List<GitHubAction>> ScrapeGitHubMarketPlaceAsync(string searchUrl)
        {
            var started = DateTime.Now;
            Console.WriteLine("Running");
            
            using var playwright = await Playwright.CreateAsync();
            var browser = await GetBrowser(playwright);
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            try
            {
                await page.GotoAsync(searchUrl);
                var sb = new StringBuilder();
                var actionList = await ScrapePageAsync(page, 1, sb);
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
                await page.CloseAsync();
                await context.CloseAsync();
                await browser.CloseAsync();
            }

            return [];
        }

        private static async Task<IBrowser> GetBrowser(IPlaywright playwright)
        {
            var launchOptions = new BrowserTypeLaunchOptions();
            Console.WriteLine("Initializing Playwright Chromium browser");
            Console.WriteLine($"CI environment?: [{Environment.GetEnvironmentVariable("CI")}]");
            if (!Debugger.IsAttached && Environment.GetEnvironmentVariable("CODESPACES") == null || Environment.GetEnvironmentVariable("CI") != "")
            {
                Console.WriteLine("Running in non-debug mode, so using headless browser");
                launchOptions.Headless = true;
            }
            else
            {
                launchOptions.Headless = false;
            }

            launchOptions.Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" };
            
            Console.WriteLine("Creating Chromium browser");
            var browser = await playwright.Chromium.LaunchAsync(launchOptions);
            Console.WriteLine("Chromium browser created");
            return browser;
        }

        private static List<GitHubAction> ScrapePage(IPage page, int pageNumber, StringBuilder logger)
        {
            return ScrapePageAsync(page, pageNumber, logger).GetAwaiter().GetResult();
        }

        private static async Task<List<GitHubAction>> ScrapePageAsync(IPage page, int pageNumber, StringBuilder logger)
        {
            var actionList = new List<GitHubAction>();
            try
            {
                await ScrollPaginatorIntoView(page);

                // Scrape the page
                var anchors = await page.Locator("a").AllAsync();
                var actionAnchors = new List<ILocator>();
                foreach (var anchor in anchors)
                {
                    var href = await anchor.GetAttributeAsync("href");
                    if (href != null && href.StartsWith("https://github.com/marketplace/actions/"))
                    {
                        actionAnchors.Add(anchor);
                    }
                }
                var actionTags = actionAnchors.ToList();

                Log.Message($"Page {pageNumber}: Found {actionTags.Count} actions, current url: {page.Url}", logger);

                foreach (var action in actionTags)
                {
                    var ghAction = await ParseActionAsync(action, page);
                    Thread.Sleep(2000); // try to cut down on ratelimit messages
                    if (ghAction != null)
                    {
                        actionList.Add(ghAction);

                        logger.AppendLine($"\tFound action:{ghAction.Url}, {ghAction.Title}, {ghAction.Publisher}, Version:{ghAction.Version}, RepoUrl: {ghAction.RepoUrl}, Verified: {ghAction.Verified}");
                    }
                }

                // find the 'next' button
                ILocator nextButton = null;
                var nextButtonLocator = page.GetByRole(AriaRole.Link, new() { Name = "Next" });
                try
                {
                    await nextButtonLocator.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
                    nextButton = nextButtonLocator;
                }
                catch
                {
                    // if the element doesn't exist, we are at the last page
                    Log.Message($"Next button not found, current url = [{page.Url}] on pageNumber [{pageNumber}]", logger);
                }

                if (nextButton != null)
                {
                    var nextUrl = await nextButton.GetAttributeAsync("href");
                    // click the next button
                    await nextButton.ClickAsync();
                    await ScrollPaginatorIntoView(page);

                    await nextButtonLocator.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });

                    // wait for the next button to be available again
                    await page.WaitForURLAsync(nextUrl);
                    // the 'next' link is not a link in the last page, so by waiting for it we would miss that page!
                    await page.Locator(".paginate-container").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

                    // scrape the new page again
                    actionList.AddRange(await ScrapePageAsync(page, pageNumber + 1, logger));
                }
            }
            catch (Exception e)
            {
                Log.Message($"Error scraping page {pageNumber}. Exception message: {e.Message}{Environment.NewLine}{e.InnerException?.Message}");
                Log.Message($"Logs for run with url [{page.Url}]:");
                Log.Message(logger.ToString());
                await SaveScreenshot(page, $"Error_Page_{pageNumber}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
            }

            return actionList;
        }

        private static async Task ScrollPaginatorIntoView(IPage page)
        {
            var timeoutDurationSeconds = Debugger.IsAttached ? 10000 : 5000;
            var elementName = "nav";
            var elementAriaLabel = "Pagination";
            var selector = $"{elementName}[aria-label='{elementAriaLabel}']";
            
            try
            {
                var paginator = page.Locator(selector);
                await paginator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeoutDurationSeconds, State = WaitForSelectorState.Visible });
                await paginator.ScrollIntoViewIfNeededAsync();
            }
            catch
            {
                try
                {
                    // wait some time and retry
                    await Task.Delay(1000);
                    var paginator = page.Locator(selector);
                    await paginator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeoutDurationSeconds, State = WaitForSelectorState.Visible });
                    await paginator.ScrollIntoViewIfNeededAsync();
                }
                catch
                {
                    // if we can't find the paginator, just continue
                    Log.Message($"Paginator not found, continuing without scrolling");
                    return;
                }
            }

            try
            {
                var bannerClassName = "hx_cookie-banner";
                var banner = page.Locator($".{bannerClassName}");
                if (await banner.CountAsync() > 0)
                {
                    await banner.EvaluateAsync("el => el.style.display = 'none'");
                }
            }
            catch
            {
                // nom nom nom
            }
        }

        private static GitHubAction ParseAction(ILocator action, IPage page)
        {
            return ParseActionAsync(action, page).GetAwaiter().GetResult();
        }

        private static async Task<GitHubAction> ParseActionAsync(ILocator action, IPage page)
        {
            try
            {
                var url = await action.GetAttributeAsync("href");
                var title = "content";
                var publisher = "";

                // open the url in a new tab by using page context
                var context = page.Context;
                var newPage = await context.NewPageAsync();
                
                var version = "";
                var actionRepoUrl = "";
                var verified = false;
                
                await newPage.GotoAsync(url);
                await Task.Delay(2000);
                
                var pageTitle = await newPage.TitleAsync();
                if (pageTitle.StartsWith("about:blank"))
                {
                    await Task.Delay(2000); // we need more time for the page to load
                    pageTitle = await newPage.TitleAsync();
                }

                if (!pageTitle.StartsWith("Page not found"))
                {
                    // act
                    try
                    {
                        // check if the verified class exists
                        try
                        {
                            var verifiedLocator = newPage.Locator(".octicon-verified");
                            if (await verifiedLocator.CountAsync() > 0)
                            {
                                verified = true;
                            }
                        }
                        catch
                        {
                            // verified class not found, use default value
                        }

                        version = await ActionPageInteraction.GetVersionFromAction(newPage);
                        Log.Message($"Found version [{version}] for url [{url}]");
                        try
                        {
                            actionRepoUrl = await ActionPageInteraction.GetRepoFromAction(newPage);
                            if (string.IsNullOrEmpty(actionRepoUrl))
                                throw new Exception("Did not find action repo url");
                            else
                                Log.Message($"Found repoUrl [{actionRepoUrl}] for url [{url}]");
                        }
                        catch (Exception e)
                        {
                            Log.Message($"Error loading action repo url for action with url [{url}]: {e.Message}, Page title:{pageTitle}, the version we got is: [{version}]");
                            var source = await newPage.ContentAsync();
                            Console.WriteLine(source);
                        }

                        publisher = GetPublisher(actionRepoUrl);

                        title = await ActionPageInteraction.GetTitleFromAction(newPage);
                        Log.Message($"Found title [{title}] for url [{url}]");
                    }
                    catch (Exception e)
                    {
                        Log.Message($"Error loading version for action with url [{url}]: {e.Message}, Page title:{pageTitle}");
                    }
                }
                else
                {
                    Log.Message($"Action detail page 404's with url [{url}], Page title:{pageTitle}");
                }

                // closing the new page
                await newPage.CloseAsync();

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
            var uri = new Uri(url);
            var segments = uri.Segments;
            if (segments.Length > 1)
            {
                return segments[1].TrimEnd('/');
            }

            var firstSlash = url.IndexOf("/");
            // return the first part of the url
            return url.Substring(0, firstSlash);
        }

        private static async Task SaveScreenshot(IPage page, string fileName)
        {
            try
            {
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = fileName });
                Log.Message($"Screenshot saved to {fileName}");
            }
            catch (Exception ex)
            {
                Log.Message($"Error saving screenshot: {ex.Message}");
            }
        }
    }
}
