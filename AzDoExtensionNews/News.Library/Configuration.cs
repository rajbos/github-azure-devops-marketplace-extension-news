using System;
using Microsoft.Extensions.Configuration;

namespace News.Library
{
    public static class Configuration
    {
        private static bool _settingsLoaded;
        private static string _TwitterConsumerAPIKey;
        public static string TwitterConsumerAPIKey
        {
            get
            {
                if (!_settingsLoaded || string.IsNullOrEmpty(_TwitterConsumerAPIKey))
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
                if (!_settingsLoaded)
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

        public static bool IsBlobStorageConfigured
        {
            get
            {
                if (!_settingsLoaded)
                {
                    LoadSettings();
                }

                return !string.IsNullOrWhiteSpace(_BlobStorageConnectionString);
            }
        }

        public static void LoadSettings()
        {
            IConfiguration config = new ConfigurationBuilder()
                                            .AddJsonFile("appsettings.json", true, false)
                                            .AddJsonFile("appsettings.secrets.json", true, false)
                                            .AddEnvironmentVariables()
                                            .Build();

            var twitterConsumerApiKey = config["TwitterConsumerAPIKey"];
            var twitterConsumerApiSecret = config["TwitterConsumerAPISecretKey"];
            var twitterAccessToken = config["TwitterAccessToken"];
            var twitterAccessTokenSecret = config["TwitterAccessTokenSecret"];
            var rawConnectionString = config["BlobStorageConnectionString"];
            var normalizedBlobConnectionString = NormalizeBlobStorageConnectionString(rawConnectionString);

            // check them all
            if (String.IsNullOrEmpty(twitterConsumerApiKey)) throw new ConfigurationException($"Error loading value for {nameof(TwitterConsumerAPIKey)}");
            if (String.IsNullOrEmpty(twitterConsumerApiSecret)) throw new ConfigurationException($"Error loading value for {nameof(TwitterConsumerAPISecretKey)}");
            if (String.IsNullOrEmpty(twitterAccessToken)) throw new ConfigurationException($"Error loading value for {nameof(TwitterAccessToken)}");
            if (String.IsNullOrEmpty(twitterAccessTokenSecret)) throw new ConfigurationException($"Error loading value for {nameof(TwitterAccessTokenSecret)}");

            // assign the validated values
            _TwitterConsumerAPIKey = twitterConsumerApiKey;
            TwitterConsumerAPISecretKey = twitterConsumerApiSecret;
            TwitterAccessToken = twitterAccessToken;
            TwitterAccessTokenSecret = twitterAccessTokenSecret;
            _BlobStorageConnectionString = normalizedBlobConnectionString;

            var hasBlobStorageConfiguration = !string.IsNullOrWhiteSpace(_BlobStorageConnectionString);

            if (!hasBlobStorageConfiguration)
            {
                var runningInCi = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI"));
                if (runningInCi)
                {
                    throw new ConfigurationException($"Error loading value for {nameof(BlobStorageConnectionString)}. Please set a valid Azure Storage connection string.");
                }

                Log.Message("Blob storage connection string not configured. Azure upload is disabled for this run.");
            }

            _settingsLoaded = true;
        }

        private static string NormalizeBlobStorageConnectionString(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == nameof(BlobStorageConnectionString))
            {
                return null;
            }

            return value;
        }
    }
}
