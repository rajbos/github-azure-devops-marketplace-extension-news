using System;
using Microsoft.Extensions.Configuration;

namespace News.Library
{
    public static class Configuration
    {
        private static string _TwitterConsumerAPIKey;
        public static string TwitterConsumerAPIKey
        {
            get
            {
                if (string.IsNullOrEmpty(_TwitterConsumerAPIKey))
                {
                    LoadSettings();
                }

                return _TwitterConsumerAPIKey;
            }

            set
            {
                _TwitterConsumerAPIKey = value;
            }
        }

        public static string TwitterConsumerAPISecretKey;
        public static string TwitterAccessToken;
        public static string TwitterAccessTokenSecret;

        private static string _BlobStorageConnectionString;

        /// <summary>
        /// Must be a connection string, not a SAS token!
        /// </summary>
        public static string BlobStorageConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_BlobStorageConnectionString))
                {
                    LoadSettings();
                }

                return _BlobStorageConnectionString;
            }

            set
            {
                _BlobStorageConnectionString = value;
            }
        }

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
            BlobStorageConnectionString = config["BlobStorageConnectionString"];

            // check them all
            if (String.IsNullOrEmpty(TwitterConsumerAPIKey)) throw new ConfigurationException($"Error loading value for {nameof(TwitterConsumerAPIKey)}");
            if (String.IsNullOrEmpty(TwitterConsumerAPISecretKey)) throw new ConfigurationException($"Error loading value for {nameof(TwitterConsumerAPISecretKey)}");
            if (String.IsNullOrEmpty(TwitterAccessToken)) throw new ConfigurationException($"Error loading value for {nameof(TwitterAccessToken)}");
            if (String.IsNullOrEmpty(TwitterAccessTokenSecret)) throw new ConfigurationException($"Error loading value for {nameof(TwitterAccessTokenSecret)}");
        }
    }
}
