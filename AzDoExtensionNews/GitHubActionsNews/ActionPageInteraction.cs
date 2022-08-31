using News.Library;
using OpenQA.Selenium;
using System;
using System.Diagnostics;
using System.Text;

namespace GitHubActionsNews
{
    public static class ActionPageInteraction
    {
        public static string GetRepoFromAction(IWebDriver driver) 
        {
            // find the div that has Links in the title
            var linkDiv = driver.FindElement(By.XPath("//*[contains(text(),'Links')]"));
            var linkDivParent = linkDiv.FindElement(By.XPath("./..")); // find parent element
            Console.WriteLine($"{linkDivParent.Text}");
            // find first link in this div
            var links = linkDivParent.FindElements(By.TagName("a"));
            var link = links[0];
            return link.Text;
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
                foreach (var el in allChildElements)
                {
                    sb.AppendLine($"{el.Text} - {el.TagName}");                   
                }

                if (Debugger.IsAttached)
                {
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
    }
}
