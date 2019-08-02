using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AzDoExtensionNews.Helpers;
using System.Globalization;
using AzDoExtensionNews.Models;

namespace AzDoExtensionNews
{
    class Program
    {
        static void Main(string[] args)
        {
            var started = DateTime.Now;
            Configuration.LoadSettings();
            CheckForUpdates().GetAwaiter().GetResult();

            Log.Message($"Duration: {(DateTime.Now - started).TotalSeconds:N2} seconds");
        }               

        private static async Task CheckForUpdates()
        {
            // load previously saved data
            var previousExtensions = Storage.ReadFromJson();
            var publisherHandles = LoadPublisherHandles.GetPublisherHandles();

            // get all new data
            var maxPages = 50;
            var pageSize = 250;
            var allExtensions = new List<Extension>();            
            for (int i = 0; i < maxPages; i++)
            {
                var data = await LoadExtensionDataAsync(pageNumber: i, pageSize);

                if (data == null || data.results[0].extensions.Length == 0) break;

                LogDataResult(data, pageNumber: i, pageSize);

                allExtensions.AddRange(data.results[0].extensions);
            }
            
            var uniqueExtensionIds = allExtensions.GroupBy(item => item.extensionId).ToList();

            Log.Message($"Total Extensions found: {allExtensions.Count}, Distinct items by extensionId: {uniqueExtensionIds.Count}");
            // deduplicate the list
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
                    CSV.SaveCSV(extensions);
                    Storage.SaveJson(extensions);
                }
                else
                {
                    Log.Message("Something went wrong while sending the tweets, not updated the data file!");
                }
            }
        }

        private static bool PostUpdates(List<Extension> newExtensions, List<Extension> updateExtension, List<PublisherHandles> publisherHandles)
        {
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

        private static string GetPublisher(Extension extension, List<PublisherHandles> publisherHandles)
        {
            var handle = publisherHandles.FirstOrDefault(item => item.PublisherName == extension.publisher.publisherName);
            if (handle != null)
            {
                return handle.TwitterHandle;
            }

            return extension.publisher.displayName;
        }

        private static bool TweetUpdateExtension(Extension extension, List<PublisherHandles> publisherHandles)
        {
            var version = extension.versions.OrderByDescending(item => item.lastUpdated).FirstOrDefault().version;
            var hashtags = GetHashTags(extension);
            var publisher = GetPublisher(extension, publisherHandles);
            var tweetText = $"This extension from {publisher} has been updated: \"{extension.displayName}\" to version {version}. Link: {extension.Url} {hashtags}";
            return Twitter.Tweet(tweetText);
        }

        private static bool TweetNewExtension(Extension extension, List<PublisherHandles> publisherHandles)
        {
            var hashtags = GetHashTags(extension);
            var publisher = GetPublisher(extension, publisherHandles);
            var tweetText = $"There is a new extension from {publisher} available in the Azure DevOps Marketplace! Check out \"{extension.displayName}\". Link: {extension.Url} {hashtags}";
            return Twitter.Tweet(tweetText);
        }

        private static string GetHashTags(Extension extension)
        {
            var hashtagList = new List<string>();
            foreach (var tag in extension.tags)
            {
                // add # and TitleCase the tag
                hashtagList.Add($"#{HashtagCasing(tag)}");
            }
            return string.Join(" ", hashtagList);
        }

        private static string HashtagCasing(string text)
        {
            // Creates a TextInfo based on the "en-US" culture.
            TextInfo myTI = new CultureInfo("en-US", false).TextInfo;

            var titleCasedText = myTI.ToTitleCase(text).Replace(" ", String.Empty);
            return titleCasedText;
        }

        private static (List<Extension> newExtensions, List<Extension> updatedExtensions) Diff(List<Extension> extensions, List<Extension> previousExtensions)
        {
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
            var extensions = data.results[0].extensions;
            //Log.Message($"Found {extensions.Length} extensions on page number {pageNumber}");
            //Log.Message("");

            for (var i = 0; i < extensions.Length; i++)
            {
                var extension = extensions[i];
                //only on verbose?
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
                    Log.Message($"metadataType: {resultMetadata.metadataType} itemText = {itemText.ToString()}");
                }
            }
            Log.Message("");
        }

        private static async Task<ExtensionDataResult> LoadExtensionDataAsync(int pageNumber, int pageSize)
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://marketplace.visualstudio.com")
            };

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("accept", "application/json; api-version=5.2-preview.1; excludeUrls=true");

            //var body = JsonConvert.SerializeObject(RequestBody.GetDefault(pageNumber, pageSize));
            var body = RequestBody.GetRawBody(pageNumber, pageSize);
            var stringContent = new StringContent(body, Encoding.ASCII, "application/json");

            Log.Message("Loading data from the Azure DevOps Marketplace");
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