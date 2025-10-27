using News.Library;
using Microsoft.Playwright;
using System;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace GitHubActionsNews
{
    public static class ActionPageInteraction
    {
        public static async Task<string> GetRepoFromAction(IPage page)
        {
            var links = await page.Locator("a").AllAsync();

            ILocator foundIssueLink = null;
            foreach (var link in links)
            {
                var text = await link.TextContentAsync();
                if (text != null && text.StartsWith("View source code"))
                {
                    foundIssueLink = link;
                    break;
                }
            }

            if (foundIssueLink == null)
            {
                return null;
            }

            var linkDiv = foundIssueLink;
            var linkDivParent = linkDiv.Locator("..");
            var firstLink = linkDivParent.Locator("a").First;
            
            if (await firstLink.CountAsync() > 0)
            {
                return await firstLink.GetAttributeAsync("href");
            }
            else
            {
                return "";
            }
        }

        public static async Task<string> GetVersionFromAction(IPage page)
        {
            ILocator divWithTitle;
            try
            {
                divWithTitle = page.Locator("//*[contains(text(),'Latest')]").First;
                await divWithTitle.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
            }
            catch (TimeoutException)
            {
                try
                {
                    divWithTitle = page.Locator("//*[contains(text(),'Pre-release')]").First;
                    await divWithTitle.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
                }
                catch (TimeoutException)
                {
                    return $"Error loading version from page [{page.Url}], cannot find 'Latest' or 'Pre-release' on this page";
                }
            }

            var sb = new StringBuilder();
            try
            {
                if (Debugger.IsAttached)
                {
                    var text = await divWithTitle.TextContentAsync();
                    var tagName = await divWithTitle.EvaluateAsync<string>("el => el.tagName");
                    var title = await divWithTitle.GetAttributeAsync("Title");
                    sb.AppendLine($"{text} - {tagName} - {title}");
                    Console.WriteLine($"divWithTitle.Text: {text}, divWithTitle.TagName: {tagName}, divWithTitle.GetAttribute(\"Title\"): {title}");
                }

                var publisherParent = divWithTitle.Locator("..");
                var allChildElements = await publisherParent.Locator("*").AllAsync();
                sb.AppendLine($"childElements.Count: [{allChildElements.Count}]");

                if (Debugger.IsAttached)
                {
                    for (int i = 0; i < allChildElements.Count; i++)
                    {
                        var el = allChildElements[i];
                        var text = await el.TextContentAsync();
                        var tagName = await el.EvaluateAsync<string>("el => el.tagName");
                        var title = await el.GetAttributeAsync("Title");
                        sb.AppendLine($"{i}: {text} - {tagName} - {title}");
                    }
                    Log.Message(sb.ToString());
                }

                return await allChildElements[0].GetAttributeAsync("Title");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading version from page [{page.Url}]: {e.Message}{Environment.NewLine}Log messages: {Environment.NewLine}{sb}");
                throw;
            }
        }

        public static async Task<string> GetVerifiedPublisherFromAction(IPage page)
        {
            try
            {
                var publisherLink = page.Locator("a[data-hovercard-type='organization']").First;
                return await publisherLink.GetAttributeAsync("href");
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine("Publisher element not found: " + ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Publisher: An error occurred: " + ex.Message);
                return null;
            }
        }

        public static async Task<string> GetTitleFromAction(IPage page)
        {
            try
            {
                var title = await page.TitleAsync();
                return title.Split('·')[0].Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Title: An error occurred: " + ex.Message);
                return null;
            }
        }
    }
}
