using OpenQA.Selenium;
using System;

namespace GitHubActionsNews
{
    public static class ActionPageInteraction
    {
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
                    return $"Error loading version from page [{driver.Url}]";
                }
            }

            try
            {                
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
            catch (Exception e)
            {
                Console.WriteLine($"Error loading version from page [{driver.Url}]: {e.Message}");
                throw;
            }
        }
    }
}
