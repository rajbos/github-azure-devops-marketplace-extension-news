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

        private static string _GitHubApiToken;

        /// <summary>
        /// Optional token used to authenticate GitHub REST API calls (releases/tags lookups).
        /// Falls back to unauthenticated calls (60 requests/hour) when not set, so it is not required.
        /// </summary>
        public static string GitHubApiToken
        {
            get
            {
                if (!_settingsLoaded)
                {
                    LoadSettings();
                }

                return _GitHubApiToken;
            }

            set
            {
                _GitHubApiToken = value;
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
            var gitHubApiToken = NormalizeOptionalSetting(config["GitHubApiToken"], nameof(GitHubApiToken));
            if (string.IsNullOrWhiteSpace(gitHubApiToken))
            {
                // GITHUB_TOKEN is provided automatically in GitHub Actions workflows
                gitHubApiToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            }

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
            _GitHubApiToken = gitHubApiToken;

            if (string.IsNullOrWhiteSpace(_GitHubApiToken))
            {
                Log.Message($"{nameof(GitHubApiToken)} not configured. GitHub API version lookups will be unauthenticated (60 requests/hour) or skipped once that limit is reached.");
            }

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

        // Guards against the appsettings.json placeholder value (e.g. "GitHubApiToken") being
        // treated as a real, configured value when variable substitution did not run.
        private static string NormalizeOptionalSetting(string value, string settingName)
        {
            if (string.IsNullOrWhiteSpace(value) || value == settingName)
            {
                return null;
            }

            return value;
        }
    }
}
