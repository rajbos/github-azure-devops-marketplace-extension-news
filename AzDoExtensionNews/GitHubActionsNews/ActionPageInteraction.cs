using News.Library;
using OpenQA.Selenium;
using System;
using System.Diagnostics;
using System.Text;
using System.Linq;

namespace GitHubActionsNews
{
    public static class ActionPageInteraction
    {
        public static string GetRepoFromAction(IWebDriver driver)
        {
            var links = driver.FindElements(By.TagName("a"));
            //foreach (var link in links)
            //{
                //Console.WriteLine(link.Text);
            //}

            var foundIssueLink = links.FirstOrDefault(a => a.Text.StartsWith("Report abuse"));
            if (foundIssueLink == null)
            {
                return null;
            }

            // find the div that has Links in the title
            //var linkDiv = driver.FindElement(By.XPath("//*[contains(text(),'Open issues')]"));
            var linkDiv = foundIssueLink;

            var linkDivParent = linkDiv.FindElement(By.XPath("./..")); // find parent element
            //Console.WriteLine($"{linkDivParent.Text}");
            // find first link in this div
             links = linkDivParent.FindElements(By.TagName("a"));
            if (links.Count > 0)
            {
                var link = links[0];
                return link.Text;
            }
            else
            {
                return "";
            }
        }

        public static string GetVersionFromAction(IWebDriver driver)
        {
            IWebElement divWithTitle;
            try
            {
                divWithTitle = driver.FindElement(By.XPath("//*[contains(text(),'Latest version')]"));
            }
            catch (OpenQA.Selenium.NoSuchElementException)
            {
                try
                {
                    divWithTitle = driver.FindElement(By.XPath("//*[contains(text(),'Pre-release')]"));
                }
                catch (OpenQA.Selenium.NoSuchElementException)
                {
                    return $"Error loading version from page [{driver.Url}], cannot find 'Latest version' or 'Pre-release' on this page";
                }
            }

            var sb = new StringBuilder();
            try
            {
                sb.AppendLine($"{divWithTitle.Text} - {divWithTitle.TagName}");
                // "contains(text(), 'Latest version')"); ;

                var publisherParent = divWithTitle.FindElement(By.XPath("./..")); // find parent element
                var allChildElements = publisherParent.FindElements(By.XPath(".//*")); // find all child elements
                sb.AppendLine($"childElements.Count: [{allChildElements.Count}]");

                if (Debugger.IsAttached)
                {
                    foreach (var el in allChildElements)
                    {
                        sb.AppendLine($"{el.Text} - {el.TagName}");
                    }
                    Log.Message(sb.ToString());
                }

                return allChildElements[2].Text;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading version from page [{driver.Url}]: {e.Message}{Environment.NewLine}Log messages: {Environment.NewLine}{sb}");
                throw;
            }
        }

        public static string GetVerifiedPublisherFromAction(IWebDriver driver)
        {
            // only works for "verified" publisher
            try
            {
                // find the a element with a data-hovercard-type="organization and load the publisher from the href
                var publisherLink = driver.FindElement(By.CssSelector("a[data-hovercard-type='organization']"));
                return publisherLink.GetAttribute("href");
            }
            catch (NoSuchElementException ex)
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

        public static string GetTitleFromAction(IWebDriver driver)
        {
            // find the div that has these classes "pl-md-3 lh-default"
            // find the h1 in the div that has the class "f1 text-normal mb-1"
            // read the text
            try
            {
                var titleDiv = driver.FindElement(By.CssSelector(".pl-md-3.lh-default"));
                var title = titleDiv.FindElement(By.XPath("./..")).FindElement(By.XPath(".//h1")).Text;
                return title;
            }
            catch (NoSuchElementException ex)
            {
                Console.WriteLine("Title element not found: " + ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Title: An error occurred: " + ex.Message);
                return null;
            }
        }
    }
}
