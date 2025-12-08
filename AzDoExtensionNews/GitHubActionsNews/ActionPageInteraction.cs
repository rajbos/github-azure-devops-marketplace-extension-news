using News.Library;
using Microsoft.Playwright;
using System;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace GitHubActionsNews
{
    public static class ActionPageInteraction
    {
        // Regex pattern for parsing YAML "using:" field
        // Matches: using: value, using: "value", using: 'value' (with optional quotes and whitespace)
        // Excludes: whitespace, quotes, comment characters (#), and newlines from the captured value
        private const string YamlUsingPattern = @"runs:\s*\n\s*using:\s*['""]?([^\s'""#\n]+)['""]?";
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
            var timeout = Debugger.IsAttached ? 15000 : 5000;

            // The new marketplace layout renders the version inside a Truncate component
            // next to a small "Latest" label. Capture that first, as it is the fastest path.
            var version = await TryGetVersionFromLatestLabelStackAsync(page, timeout);
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            // Try schema.org microdata first
            version = await TryGetSoftwareVersionAsync(page, timeout);
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            // Fallback to UI label ("Latest version" / "Latest" / "Pre-release")
            version = await TryGetVersionFromLatestSectionAsync(page, timeout);
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            // Last resort: use release links on the page
            version = await TryGetVersionFromReleaseLinksAsync(page);
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            return $"Error loading version from page [{page.Url}], unable to determine latest release";
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

        private static async Task<string> TryGetSoftwareVersionAsync(IPage page, int timeout)
        {
            var locator = page.Locator("[itemprop='softwareVersion']");
            if (!await WaitForLocatorAsync(locator, timeout))
            {
                return null;
            }

            var element = locator.First;
            var fromContent = (await element.GetAttributeAsync("content"))?.Trim();
            if (!string.IsNullOrWhiteSpace(fromContent) && IsLikelyVersion(fromContent))
            {
                return fromContent;
            }

            var fromText = (await element.InnerTextAsync())?.Trim();
            return IsLikelyVersion(fromText) ? fromText : null;
        }

        private static async Task<string> TryGetVersionFromLatestSectionAsync(IPage page, int timeout)
        {
            var labelLocator = page.Locator("text=/Latest version/i");
            if (!await WaitForLocatorAsync(labelLocator, timeout))
            {
                labelLocator = page.Locator("text=/Latest/i");
                if (!await WaitForLocatorAsync(labelLocator, timeout))
                {
                    labelLocator = page.Locator("text=/Pre-release/i");
                    if (!await WaitForLocatorAsync(labelLocator, timeout))
                    {
                        return null;
                    }
                }
            }

            var versionCandidate = await GetFollowingVersionCandidateAsync(labelLocator.First);
            if (IsLikelyVersion(versionCandidate))
            {
                return versionCandidate;
            }

            // If the direct sibling approach fails, inspect nearby release links
            var sectionContainer = labelLocator.First.Locator("xpath=ancestor::*[self::article or self::section or self::div][contains(@class,'d-flex')][1]");
            if (await sectionContainer.CountAsync() > 0)
            {
                var link = sectionContainer.Locator("a[href*='/releases/']").First;
                if (await link.CountAsync() > 0)
                {
                    var text = (await link.InnerTextAsync())?.Trim();
                    if (IsLikelyVersion(text))
                    {
                        return text;
                    }

                    var href = await link.GetAttributeAsync("href");
                    var fromHref = ExtractVersionFromHref(href);
                    if (IsLikelyVersion(fromHref))
                    {
                        return fromHref;
                    }
                }
            }

            return null;
        }

        private static async Task<string> TryGetVersionFromReleaseLinksAsync(IPage page)
        {
            var releaseLinks = page.Locator("a[href*='/releases/tag/'], a[href*='/releases/']");
            var count = await releaseLinks.CountAsync();
            for (var i = 0; i < count; i++)
            {
                var link = releaseLinks.Nth(i);
                var text = (await link.InnerTextAsync())?.Trim();
                if (IsLikelyVersion(text))
                {
                    return text;
                }

                var title = (await link.GetAttributeAsync("title"))?.Trim();
                if (IsLikelyVersion(title))
                {
                    return title;
                }

                var href = await link.GetAttributeAsync("href");
                var fromHref = ExtractVersionFromHref(href);
                if (IsLikelyVersion(fromHref))
                {
                    return fromHref;
                }
            }

            return null;
        }

        private static async Task<bool> WaitForLocatorAsync(ILocator locator, int timeout)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
            while (DateTime.UtcNow < deadline)
            {
                if (await locator.CountAsync() > 0)
                {
                    return true;
                }

                await Task.Delay(200);
            }

            return await locator.CountAsync() > 0;
        }

        private static async Task<string> TryGetVersionFromLatestLabelStackAsync(IPage page, int timeout)
        {
            var latestLabels = page.Locator("span[data-variant='success']:text-is('Latest')");
            if (!await WaitForLocatorAsync(latestLabels, timeout))
            {
                return null;
            }

            var count = await latestLabels.CountAsync();
            for (var i = 0; i < count; i++)
            {
                var label = latestLabels.Nth(i);
                // In the new layout the version is rendered in the sibling div with truncate styling
                var preceding = label.Locator("xpath=preceding-sibling::*[1]");
                if (await preceding.CountAsync() == 0)
                {
                    continue;
                }

                var titleAttr = (await preceding.GetAttributeAsync("title"))?.Trim();
                if (IsLikelyVersion(titleAttr))
                {
                    return titleAttr;
                }

                var spanText = (await preceding.InnerTextAsync())?.Trim();
                if (IsLikelyVersion(spanText))
                {
                    return spanText;
                }

                var innerSpan = preceding.Locator("span").First;
                if (await innerSpan.CountAsync() > 0)
                {
                    var innerText = (await innerSpan.InnerTextAsync())?.Trim();
                    if (IsLikelyVersion(innerText))
                    {
                        return innerText;
                    }
                }
            }

            return null;
        }

        private static async Task<string> GetFollowingVersionCandidateAsync(ILocator label)
        {
            try
            {
                var candidate = label.Locator("xpath=following-sibling::*[self::a or self::span or self::strong][1]");
                if (await candidate.CountAsync() > 0)
                {
                    var text = (await candidate.InnerTextAsync())?.Trim();
                    if (IsLikelyVersion(text))
                    {
                        return text;
                    }

                    var title = (await candidate.GetAttributeAsync("title"))?.Trim();
                    if (IsLikelyVersion(title))
                    {
                        return title;
                    }

                    var href = await candidate.GetAttributeAsync("href");
                    var fromHref = ExtractVersionFromHref(href);
                    if (IsLikelyVersion(fromHref))
                    {
                        return fromHref;
                    }
                }
            }
            catch
            {
                // ignore and fall back to other strategies
            }

            return null;
        }

        private static bool IsLikelyVersion(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            candidate = candidate.Trim();

            if (candidate.Length > 40)
            {
                return false;
            }

            return candidate.Any(char.IsDigit);
        }

        private static string ExtractVersionFromHref(string href)
        {
            if (string.IsNullOrWhiteSpace(href))
            {
                return null;
            }

            try
            {
                var uri = new Uri(href, UriKind.RelativeOrAbsolute);
                var path = uri.IsAbsoluteUri ? uri.AbsolutePath : href;
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                return segments.Length == 0 ? null : segments[^1];
            }
            catch
            {
                return null;
            }
        }

        public static async Task<(string ActionType, string NodeVersion)> GetActionTypeAndNodeVersionAsync(IPage page, string repoUrl)
        {
            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                return (null, null);
            }

            try
            {
                // Navigate to the repository page
                await page.GotoAsync(repoUrl);
                await Task.Delay(1000); // Wait for page to load

                string actionContent = null;
                string fileType = null;

                // Check for files in order: action.yml, action.yaml, Dockerfile
                var filesToCheck = new[] { "action.yml", "action.yaml", "Dockerfile" };
                
                foreach (var fileName in filesToCheck)
                {
                    try
                    {
                        // Look for a link to the file in the repository
                        var fileLink = page.Locator($"a[href*='/{fileName}']").First;
                        
                        if (await fileLink.CountAsync() > 0)
                        {
                            Log.Message($"Found {fileName} in repository [{repoUrl}]");
                            
                            // Click on the file to open it
                            await fileLink.ClickAsync();
                            await Task.Delay(2000); // Wait for file content to load
                            
                            // Get the file content from the code block
                            var codeBlock = page.Locator("table.js-file-line-container, div.blob-code, pre.highlight");
                            if (await codeBlock.CountAsync() > 0)
                            {
                                actionContent = await codeBlock.First.InnerTextAsync();
                                fileType = fileName;
                                Log.Message($"Successfully loaded content from {fileName}");
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Message($"Could not find or open {fileName} in [{repoUrl}]: {ex.Message}");
                        // Try next file
                    }
                }

                if (string.IsNullOrWhiteSpace(actionContent))
                {
                    Log.Message($"No action definition file found in repository [{repoUrl}]");
                    return (null, null);
                }

                // Parse based on file type
                if (fileType == "Dockerfile")
                {
                    return ("Docker", null);
                }
                else
                {
                    return ParseActionYaml(actionContent);
                }
            }
            catch (Exception ex)
            {
                Log.Message($"Error fetching action type and node version from [{repoUrl}]: {ex.Message}");
                return (null, null);
            }
        }

        private static (string ActionType, string NodeVersion) ParseActionYaml(string yamlContent)
        {
            string actionType = null;
            string nodeVersion = null;

            try
            {
                // Check for runs.using to determine action type
                var runsUsingMatch = Regex.Match(yamlContent, YamlUsingPattern, RegexOptions.Multiline);
                if (runsUsingMatch.Success)
                {
                    var usingValue = runsUsingMatch.Groups[1].Value.Trim();
                    
                    if (usingValue.StartsWith("node", StringComparison.OrdinalIgnoreCase))
                    {
                        actionType = "Node";
                        // Extract node version (e.g., "node20", "node16")
                        var nodeVersionMatch = Regex.Match(usingValue, @"node(\d+)", RegexOptions.IgnoreCase);
                        if (nodeVersionMatch.Success)
                        {
                            nodeVersion = nodeVersionMatch.Groups[1].Value;
                        }
                    }
                    else if (usingValue.Equals("docker", StringComparison.OrdinalIgnoreCase))
                    {
                        actionType = "Docker";
                    }
                    else if (usingValue.Equals("composite", StringComparison.OrdinalIgnoreCase))
                    {
                        actionType = "Composite";
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Message($"Error parsing action.yml content: {ex.Message}");
            }

            return (actionType, nodeVersion);
        }
    }
}
