using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AzDoExtensionNews
{
    class Program
    {
        static void Main(string[] args)
        {
            var started = DateTime.Now;
            CheckForUpdates().GetAwaiter().GetResult();

            Log($"Duration: {(DateTime.Now - started).TotalSeconds:N2} seconds");
        }

        private static async Task CheckForUpdates()
        {
            // load previously saved data
            var previousExtensions = ReadFromJson();

            // get all new data
            var maxPages = 50;
            var pageSize = 100;
            var allExtensions = new List<Extension>();            
            for (int i = 0; i < maxPages; i++)
            {
                var data = await LoadExtensionDataAsync(pageNumber: i, pageSize);

                if (data == null || data.results[0].extensions.Length == 0) break;

                LogDataResult(data, pageNumber: i, pageSize);

                allExtensions.AddRange(data.results[0].extensions);
            }
            
            var uniqueExtensionIds = allExtensions.GroupBy(item => item.extensionId).ToList();

            Log($"Total Extensions found: {allExtensions.Count}, Distinct items by extensionId: {uniqueExtensionIds.Count}");

            var extensions = DeduplicateExtensions(allExtensions);
            
            // check with stored data
            (var newExtensions, var updateExtension) = Diff(extensions, previousExtensions);
            // tweet updates
            PostUpdates(newExtensions, updateExtension);

            // store new data
            SaveCSV(extensions);
            //temp disable SaveJson(extensions);
        }

        private static void PostUpdates(List<Extension> newExtensions, List<Extension> updateExtension)
        {
            Log($"Found {newExtensions.Count} new extension(s) and {updateExtension.Count} updated extension(s)");
            foreach (var extension in newExtensions)
            {
                TweetNewExtension(extension);
            }

            foreach (var extension in updateExtension)
            {
                TweetUpdateExtension(extension);
            }
        }

        private static void TweetUpdateExtension(Extension extension)
        {
            var tweetText = $"Extension has been updated {extension.displayName}"; // include version?
            Tweet(tweetText);
        }
        private static void TweetNewExtension(Extension extension)
        {
            var tweetText = $"A new extension available in the Marketplace! {extension.displayName}";
            Tweet(tweetText);
        }

        private static void Tweet(string tweetText)
        {
            Log($"{tweetText}");
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

        private static void SaveJson(List<Extension> extensions)
        {
            var text = JsonConvert.SerializeObject(extensions);
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions.Json");
            path = "Extensions.Json";
            System.IO.File.WriteAllText(path, text);
        }

        private static List<Extension> ReadFromJson()
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions.Json");
            path = "Extensions.Json";
            
            var text = System.IO.File.ReadAllText(path);
                        
            
            var extensions = JsonConvert.DeserializeObject<List<Extension>>(text);
            return extensions;

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

                if (extension != null)
                {
                    uniqueList.Add(extension);
                }
            }

            return uniqueList;
        }

        #region Save to CSV
        private static void SaveCSV(List<Extension> allExtensions)
        {
            if (1 == 2)
            {
                CreateCSV(allExtensions, "extensions.csv");
            }
        }

        private static void CreateHeader<T>(List<T> list, StreamWriter sw)
        {
            PropertyInfo[] properties = typeof(T).GetProperties();
            for (int i = 0; i < properties.Length - 1; i++)
            {
                sw.Write(properties[i].Name + ",");
            }
            var lastProp = properties[properties.Length - 1].Name;
            sw.Write(lastProp + sw.NewLine);
        }

        private static void CreateRows<T>(List<T> list, StreamWriter sw)
        {
            foreach (var item in list)
            {
                PropertyInfo[] properties = typeof(T).GetProperties();
                for (int i = 0; i < properties.Length - 1; i++)
                {
                    var prop = properties[i];
                    sw.Write(prop.GetValue(item) + ",");
                }
                var lastProp = properties[properties.Length - 1];
                sw.Write(lastProp.GetValue(item) + sw.NewLine);
            }
        }

        public static void CreateCSV<T>(List<T> list, string filePath)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                CreateHeader(list, sw);
                CreateRows(list, sw);
            }
        }
        #endregion

        private static void LogDataResult(ExtensionDataResult data, int pageNumber, int pageSize)
        {
            var extensions = data.results[0].extensions;
            //Log($"Found {extensions.Length} extensions on page number {pageNumber}");
            //Log("");

            for (var i = 0; i < extensions.Length; i++)
            {
                var extension = extensions[i];
                Log($"{(pageNumber * pageSize + i):D3} {extension.lastUpdated} {extension.displayName}");
            }

            if (extensions.Length < pageSize)
            {
                // pagingToken: {data.results[0].pagingToken} is always empty
                foreach (var resultMetadata in data.results[0].resultMetadata)
                {
                    var itemText = new StringBuilder();
                    foreach (var items in resultMetadata.metadataItems)
                    {
                        itemText.AppendLine($"name: {items.name}= count:{items.count};");
                    }
                    Log($"metadataType: {resultMetadata.metadataType} itemText = {itemText.ToString()}");
                }
            }
            Log("");
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
            //var body = RequestBody.RawBody;
            var stringContent = new StringContent(body, Encoding.ASCII, "application/json");

            Log("Loading data from Azure DevOps");
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
                Log($"Error loading data: {e.Message}{e.InnerException?.Message}");
                return null;
            }
        }

        private static void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}