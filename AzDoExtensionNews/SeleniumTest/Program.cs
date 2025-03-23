using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

internal class Program
{
    private static void Main(string[] args)
    {
        IWebDriver driver = null;
        try
        {
            var chromeOptions = new ChromeOptions();
            Console.WriteLine("Initializing Chrome driver");

            chromeOptions.AddArguments("--no-sandbox"); // Bypass OS security model
            chromeOptions.AddArguments("--disable-dev-shm-usage"); // Overcome limited resource problems
            chromeOptions.AddArguments("--headless");
            var service = ChromeDriverService.CreateDefaultService();
            driver = new ChromeDriver(service, chromeOptions, TimeSpan.FromSeconds(300));

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