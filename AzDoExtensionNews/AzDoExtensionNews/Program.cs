using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AzDoExtensionNews.Helpers;
using AzDoExtensionNews.Models;
using News.Library;

namespace AzDoExtensionNews
{
    class Program
    {
        private const bool TestingTweet = false;
        private static readonly string StorageFileName = "Extensions";
        private static Twitter twitter;

        static void Main(string[] args)
        {
            var started = DateTime.Now;
            Configuration.LoadSettings();

            twitter = new Twitter(Configuration.TwitterConsumerAPIKey, 
                                  Configuration.TwitterConsumerAPISecretKey, 
                                  Configuration.TwitterAccessToken, 
                                  Configuration.TwitterAccessTokenSecret);

            CheckForUpdates().GetAwaiter().GetResult();

            Log.Message($"Duration: {(DateTime.Now - started).TotalSeconds:N2} seconds");
        }               

        private static async Task CheckForUpdates()
        {
            List<Extension> previousExtensions = null;
            List<PublisherHandles> publisherHandles = null;
            // load previously saved data
            try
            {
                previousExtensions = Storage.ReadFromJson<Extension>(StorageFileName);
                publisherHandles = LoadPublisherHandles.GetPublisherHandles();
            }
            catch (Exception e)
            {
                Log.Message($"Error loading previous execution information. Will start with a new slate{Environment.NewLine}{e.Message}");
            }

            // get all new data
            var maxPages = 50;
            var pageSize = 250;
            var allExtensions = new List<Extension>();            
            for (int i = 0; i < maxPages; i++)
            {
                var data = await LoadExtensionDataAsync(pageNumber: i, pageSize: pageSize);

                if (data == null || data.results[0].extensions.Length == 0) break;

                LogDataResult(data: data, pageNumber: i, pageSize: pageSize);

                allExtensions.AddRange(data.results[0].extensions);
            }
            
            var uniqueExtensionIds = allExtensions.GroupBy(item => item.extensionId).ToList();
            var uniquePublishers = allExtensions.GroupBy(item => item.publisher.publisherId).ToList();

            Log.Message($"Total Extensions found: {allExtensions.Count}, distinct items by extensionId: {uniqueExtensionIds.Count}. Distinct publishers: {uniquePublishers.Count}");
            // de-duplicate the list
            var extensions = DeduplicateExtensions(allExtensions);
            
            // check with stored data
            (var newExtensions, var updateExtension) = Diff(extensions, previousExtensions);
            // show updates
            Log.Message($"Found {newExtensions.Count} new extension(s) and {updateExtension.Count} updated extension(s)");

            // tweet updates
            if (newExtensions.Any() || updateExtension.Any())
            {
                if (PostUpdates(newExtensions, updateExtension, publisherHandles))
                {
                    // store new data
                    Storage.SaveJson(extensions, StorageFileName);
                }
                else
                {
                    Log.Message("Something went wrong while sending the tweets, not updated the data file!");
                }
            }
            else if (TestingTweet)
            {
                // send out a test tweet for 1 extension
                updateExtension.Add(extensions.First());
                PostUpdates(newExtensions, updateExtension, publisherHandles);
            }

            if (!previousExtensions.Any() && extensions.Any())
            {
                // no previously known extensions but new list? Store the new set for the next run
                // store all data
                Storage.SaveJson(extensions, StorageFileName);
            }
            
            // save the data to CSV if needed
            CSV.SaveCSV(extensions);
        }

        private static bool PostUpdates(List<Extension> newExtensions, List<Extension> updateExtension, List<PublisherHandles> publisherHandles)
        {
            // limit throughput to only a small number of tweets to prevent disabling of the twitter account
            if (newExtensions.Count + updateExtension.Count > 50)
            {
                // something must be wrong, number is way to larger
                Log.Message($"Found {newExtensions.Count + updateExtension.Count} extensions to tweet about, number is way to high. Skipping sending the tweets");
                return true;
            }

            // todo: maybe only store successfully tweeted extensions?
            var success = true;
            foreach (var extension in newExtensions)
            {
                if (!TweetNewExtension(extension, publisherHandles))
                {
                    success = false;
                }
            }

            foreach (var extension in updateExtension)
            {
                if (!TweetUpdateExtension(extension, publisherHandles))
                {
                    success = false;
                }
            }

            return success;
        }

        private static bool TweetUpdateExtension(Extension extension, List<PublisherHandles> publisherHandles)
        {
            var version = GetVersion(extension);
            var hashtags = Tags.GetHashTags(extension);
            var publisher = PublisherFinder.GetPublisher(extension, publisherHandles);
            var tweetText = $"The {GetExtensionText(extension)} \"{extension.displayName}\" from {publisher} has been updated to version {version.version}. Link: {extension.Url} {hashtags}";
            string imageUrl = GetImageUrl(version);

            return twitter.SendTweet(tweetText, imageUrl);
        }
        
        private static bool TweetNewExtension(Extension extension, List<PublisherHandles> publisherHandles)
        {
            var version = GetVersion(extension);
            var hashtags = Tags.GetHashTags(extension);
            var publisher = PublisherFinder.GetPublisher(extension, publisherHandles);
            var tweetText = $"There is a new {GetExtensionText(extension)} from {publisher} available in the Azure DevOps Marketplace! Check out \"{extension.displayName}\". Link: {extension.Url} {hashtags}";
            string imageUrl = GetImageUrl(version);

            return twitter.SendTweet(tweetText, imageUrl);
        }

        private static string GetImageUrl(Models.Version version)
        {
            var asset = version.files.FirstOrDefault(item => item.assetType == "Microsoft.VisualStudio.Services.Icons.Default");
            var imageUrl = asset == null ? "" : asset.source;
            return imageUrl;
        }

        private static Models.Version GetVersion(Extension extension)
        {
            return extension.versions.OrderByDescending(item => item.lastUpdated).FirstOrDefault();
        }

        private static string GetExtensionText(Extension extension)
        {
            return "extension";
        }

        private static (List<Extension> newExtensions, List<Extension> updatedExtensions) Diff(List<Extension> extensions, List<Extension> previousExtensions)
        {
            if (extensions == null) throw new ArgumentNullException(nameof(extensions));
            if (previousExtensions == null) throw new ArgumentNullException(nameof(previousExtensions));

            if (!previousExtensions.Any())
            {
                // no old extensions available, so we cannot infer which are new or updated
                // return empty lists to prevent any actions
                return (new List<Extension>(), new List<Extension>());
            }

            var newExtensionList = new List<Extension>();
            var updatedExtensionList = new List<Extension>();

            // order the list to make sure the first one found is the correct one
            var oldExtensions = previousExtensions.OrderByDescending(item => item.lastUpdated);
            var newExtensions = extensions.OrderByDescending(item => item.lastUpdated);

            foreach (var extension in newExtensions)
            {
                var oldExtension = oldExtensions.FirstOrDefault(item => item.extensionId == extension.extensionId);
                if (oldExtension == null)
                {
                    // new extension
                    newExtensionList.Add(extension);
                }
                else
                {
                    if (oldExtension.lastUpdated < extension.lastUpdated)
                    {
                        // updated extension
                        updatedExtensionList.Add(extension);
                    }
                }
            }

            return (newExtensionList, updatedExtensionList);
        }
        
        private static List<Extension> DeduplicateExtensions(List<Extension> allExtensions)
        {
            if (allExtensions == null) throw new ArgumentNullException(nameof(allExtensions));

            var uniqueList = new List<Extension>();
            var uniqueExtensionIds = allExtensions.GroupBy(item => item.extensionId).ToList();

            foreach (var extensionId in uniqueExtensionIds)
            {
                var extension = allExtensions
                    .Where(item => item.extensionId == extensionId.Key)
                    .OrderByDescending(item => item.lastUpdated)
                    .FirstOrDefault();

                if (extension != null && (extension.flags.Split(", ").FirstOrDefault(item => item.Equals("public")) != null))
                {
                    uniqueList.Add(extension);
                }
            }

            return uniqueList;
        }

        private static void LogDataResult(ExtensionDataResult data, int pageNumber, int pageSize)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var extensions = data.results[0].extensions;
            //Log.Message($"Found {extensions.Length} extensions on page number {pageNumber}");
            //Log.Message("");

            for (var i = 0; i < extensions.Length; i++)
            {
                //Todo: only on verbose?
                //var extension = extensions[i];
                //Log.Message($"{(pageNumber * pageSize + i):D3} {extension.lastUpdated} {extension.displayName}");
            }

            // log the last batch's information
            if (extensions.Length < pageSize)
            {
                // pagingToken: {data.results[0].pagingToken} is always empty
                foreach (var resultMetadata in data.results[0].resultMetadata)
                {
                    var itemText = new StringBuilder();
                    foreach (var items in resultMetadata.metadataItems)
                    {
                        itemText.AppendLine($"name: {items.name} count:{items.count};");
                    }
                    Log.Message($"metadataType: {resultMetadata.metadataType}");
                    Log.Message($"{itemText.ToString()}");
                }
                
                Log.Message("");
            }
        }

        private static HttpClient httpClient = null;

        private static async Task<ExtensionDataResult> LoadExtensionDataAsync(int pageNumber, int pageSize)
        {
            if (httpClient == null)
            {
                httpClient = new HttpClient
                {
                    BaseAddress = new Uri("https://marketplace.visualstudio.com")
                };

                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("accept", "application/json; api-version=5.2-preview.1; excludeUrls=true");
            }

            //var body = JsonConvert.SerializeObject(RequestBody.GetDefault(pageNumber, pageSize));
            var body = RequestBody.GetRawBody(pageNumber, pageSize);
            var stringContent = new StringContent(body, Encoding.ASCII, "application/json");

            Log.Message($"Loading data from the Azure DevOps Marketplace. PageSize: {pageSize} PageNumber: {pageNumber}");
            try
            {
                var response = await httpClient.PostAsync("_apis/public/gallery/extensionquery", stringContent);

                response.EnsureSuccessStatusCode();

                var stringResponse = await response.Content.ReadAsStringAsync();
                var dataResult = JsonConvert.DeserializeObject<ExtensionDataResult>(stringResponse);
                
                return dataResult;
            }
            catch (Exception e)
            {
                Log.Message($"Error loading data: {e.Message}{e.InnerException?.Message}");
                return null;
            }
        }
    }
}