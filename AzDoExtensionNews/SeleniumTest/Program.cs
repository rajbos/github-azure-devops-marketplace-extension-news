using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Diagnostics;

using OpenQA.Selenium;
using OpenQA.Selenium.BiDi.Modules.Session;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

internal static class Program
{
    private static ChromeDriver GetDriver()
    {
        var chromeOptions = new ChromeOptions();
        Console.WriteLine("Initializing Chrome driver");
        Console.WriteLine($"CI environment?: [{Environment.GetEnvironmentVariable("CI")}]");
        if (!Debugger.IsAttached && Environment.GetEnvironmentVariable("CODESPACES") == null || Environment.GetEnvironmentVariable("CI") != "")
        {
            Console.WriteLine("Running in non-debug mode, so using headless Chrome");
            chromeOptions.AddArguments("headless"); // Run Chrome in headless mode
        }

        // test for the env var CHROME_BIN
        var variableName = "CHROMEWEBDRIVER";
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variableName)))
        {
            //Console.WriteLine($"Using [{variableName}] from env var: [{Environment.GetEnvironmentVariable(variableName)}]");
            //chromeOptions.BinaryLocation = Environment.GetEnvironmentVariable(variableName);
        }
        chromeOptions.AddArguments("--no-sandbox"); // Bypass OS security model
        chromeOptions.AddArguments("--disable-dev-shm-usage"); // Overcome limited resource problems
        Console.WriteLine("Creating default service");
        var service = ChromeDriverService.CreateDefaultService();
        Console.WriteLine("Creating Chrome driver");
        var driver = new ChromeDriver(service, chromeOptions, TimeSpan.FromSeconds(300));
        Console.WriteLine("Chrome driver created");
        return driver;
    }

    private static async Task Main(string[] args)
    {
        IWebDriver driver = GetDriver();
        try
        {
            Console.WriteLine("Starting the driver");
            // go to google.com with the driver
            driver.Navigate().GoToUrl("https://www.google.com");
            Console.WriteLine("Navigated to Google");
        }
        finally
        {
            if (driver != null)
            {
                Console.WriteLine("Closing the driver");
                driver.Close();
                driver.Quit();
                Console.WriteLine("Driver closed successfully");
            }
        }
    }
}