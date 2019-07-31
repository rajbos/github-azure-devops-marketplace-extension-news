using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace AzDoExtensionNews.Helpers
{
    internal static class Configuration
    {
        public static string TwitterConsumerAPIKey;
        public static string TwitterConsumerAPISecretKey;
        public static string TwitterAccessToken;
        public static string TwitterAccessTokenSecret;

        public static void LoadSettings()
        {
            IConfiguration config = new ConfigurationBuilder()
                                            .AddJsonFile("appsettings.json", true, false)
                                            .AddJsonFile("appsettings.secrets.json", true, false)
                                            .Build();

            // load the variables
            TwitterConsumerAPIKey = config["TwitterConsumerAPIKey"];
            TwitterConsumerAPISecretKey = config["TwitterConsumerAPISecretKey"];
            TwitterAccessToken = config["TwitterAccessToken"];
            TwitterAccessTokenSecret = config["TwitterAccessTokenSecret"];

            // check them all
            if (String.IsNullOrEmpty(TwitterConsumerAPIKey)) throw new ConfigurationException($"Error loading value for {nameof(TwitterConsumerAPIKey)}");
            if (String.IsNullOrEmpty(TwitterConsumerAPISecretKey)) throw new ConfigurationException($"Error loading value for {nameof(TwitterConsumerAPISecretKey)}");
            if (String.IsNullOrEmpty(TwitterAccessToken)) throw new ConfigurationException($"Error loading value for {nameof(TwitterAccessToken)}");
            if (String.IsNullOrEmpty(TwitterAccessTokenSecret)) throw new ConfigurationException($"Error loading value for {nameof(TwitterAccessTokenSecret)}");
        }
    }
}
