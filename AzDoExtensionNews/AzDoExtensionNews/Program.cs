using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.FileExtensions;
using Microsoft.Extensions.Configuration.Json;

namespace AzDoExtensionNews
{
    class Program
    {
        static void Main(string[] args)
        {
            var started = DateTime.Now;
            GetSettings();
            CheckForUpdates().GetAwaiter().GetResult();

            Log($"Duration: {(DateTime.Now - started).TotalSeconds:N2} seconds");
        }

        private static string TWConsumerAPIKey = "";
        private static string TWConsumerAPISecretKey = "";
        private static string TWAccessToken = "";
        private static string TWAccessTokenSecret = "";

        private static void GetSettings()
        {
            IConfiguration config = new ConfigurationBuilder()
                                            .AddJsonFile("appsettings.json", true, false)
                                            .AddJsonFile("appsettings.secrets.json", true, false)
                                            .Build();

            // load the variables
            TWConsumerAPIKey = config["TWConsumerAPIKey"];
            TWConsumerAPISecretKey = config["TWConsumerAPISecretKey"];
            TWAccessToken = config["TWAccessToken"];
            TWAccessTokenSecret = config["TWAccessTokenSecret"];

            // check them all
            if (String.IsNullOrEmpty(TWConsumerAPIKey)) throw new Exception($"Error loading value for {nameof(TWConsumerAPIKey)}");
            if (String.IsNullOrEmpty(TWConsumerAPISecretKey)) throw new Exception($"Error loading value for {nameof(TWConsumerAPISecretKey)}");
            if (String.IsNullOrEmpty(TWAccessToken)) throw new Exception($"Error loading value for {nameof(TWAccessToken)}");
            if (String.IsNullOrEmpty(TWAccessTokenSecret)) throw new Exception($"Error loading value for {nameof(TWAccessTokenSecret)}");
        }

        private static async Task CheckForUpdates()
        {
            // load previously saved data
            var previousExtensions = ReadFromJson();

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

            Log($"Total Extensions found: {allExtensions.Count}, Distinct items by extensionId: {uniqueExtensionIds.Count}");
            // deduplicate the list
            var extensions = DeduplicateExtensions(allExtensions);
            
            // check with stored data
            (var newExtensions, var updateExtension) = Diff(extensions, previousExtensions);
            // show updates
            Log($"Found {newExtensions.Count} new extension(s) and {updateExtension.Count} updated extension(s)");

            // tweet updates
            if (newExtensions.Any() && PostUpdates(newExtensions, updateExtension))
            {
                // store new data
                //SaveCSV(extensions);
                SaveJson(extensions);
            }
        }

        private static bool PostUpdates(List<Extension> newExtensions, List<Extension> updateExtension)
        {
            // todo: maybe only store successfully tweeted extensions?
            var success = true;
            foreach (var extension in newExtensions)
            {
                if (!TweetNewExtension(extension))
                {
                    success = false;
                }
            }

            foreach (var extension in updateExtension)
            {
                if (TweetUpdateExtension(extension))
                {
                    success = false;
                }
            }

            return success;
        }

        private static bool TweetUpdateExtension(Extension extension)
        {
            var version = extension.versions.OrderByDescending(item => item.lastUpdated).FirstOrDefault().version;
            var tweetText = $"Extension has been updated {extension.displayName} to version {version}. Link: {extension.Url}"; // include version?
            return Tweet(tweetText);
        }
        private static bool TweetNewExtension(Extension extension)
        {
            var tweetText = $"There is a new extension available in the Azure DevOps Marketplace! {extension.displayName}. Link: {extension.Url}";
            return Tweet(tweetText);
        }

        private static bool Tweet(string tweetText)
        {
            Log(tweetText);
            try
            {
                string twitterURL = "https://api.twitter.com/1.1/statuses/update.json";

                string oauth_consumer_key = TWConsumerAPIKey; //  GlobalConstants.TWConsumerAPIKey;
                string oauth_consumer_secret = TWConsumerAPISecretKey; //GlobalConstants.TWConsumerAPISecretKey;
                string oauth_token = TWAccessToken;  //GlobalConstants.TWAccessToken;
                string oauth_token_secret = TWAccessTokenSecret; //GlobalConstants.TWAccessTokenSecret;

                // set the oauth version and signature method
                string oauth_version = "1.0";
                string oauth_signature_method = "HMAC-SHA1";

                // create unique request details
                string oauth_nonce = Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));
                System.TimeSpan timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
                string oauth_timestamp = Convert.ToInt64(timeSpan.TotalSeconds).ToString();

                // create oauth signature
                string baseFormat = "oauth_consumer_key={0}&oauth_nonce={1}&oauth_signature_method={2}" + "&oauth_timestamp={3}&oauth_token={4}&oauth_version={5}&status={6}";

                string baseString = string.Format(
                    baseFormat,
                    oauth_consumer_key,
                    oauth_nonce,
                    oauth_signature_method,
                    oauth_timestamp, oauth_token,
                    oauth_version,
                    Uri.EscapeDataString(tweetText)
                );

                string oauth_signature = null;
                using (HMACSHA1 hasher = new HMACSHA1(ASCIIEncoding.ASCII.GetBytes(Uri.EscapeDataString(oauth_consumer_secret) + "&" + Uri.EscapeDataString(oauth_token_secret))))
                {
                    oauth_signature = Convert.ToBase64String(hasher.ComputeHash(ASCIIEncoding.ASCII.GetBytes("POST&" + Uri.EscapeDataString(twitterURL) + "&" + Uri.EscapeDataString(baseString))));
                }

                // create the request header
                string authorizationFormat = "OAuth oauth_consumer_key=\"{0}\", oauth_nonce=\"{1}\", " + "oauth_signature=\"{2}\", oauth_signature_method=\"{3}\", " + "oauth_timestamp=\"{4}\", oauth_token=\"{5}\", " + "oauth_version=\"{6}\"";

                string authorizationHeader = string.Format(
                    authorizationFormat,
                    Uri.EscapeDataString(oauth_consumer_key),
                    Uri.EscapeDataString(oauth_nonce),
                    Uri.EscapeDataString(oauth_signature),
                    Uri.EscapeDataString(oauth_signature_method),
                    Uri.EscapeDataString(oauth_timestamp),
                    Uri.EscapeDataString(oauth_token),
                    Uri.EscapeDataString(oauth_version)
                );

                HttpWebRequest objHttpWebRequest = (HttpWebRequest)WebRequest.Create(twitterURL);
                objHttpWebRequest.Headers.Add("Authorization", authorizationHeader);
                objHttpWebRequest.Method = "POST";
                objHttpWebRequest.ContentType = "application/x-www-form-urlencoded";
                using (Stream objStream = objHttpWebRequest.GetRequestStream())
                {
                    byte[] content = ASCIIEncoding.ASCII.GetBytes("status=" + Uri.EscapeDataString(tweetText));
                    objStream.Write(content, 0, content.Length);
                }

                var responseResult = "";

                try
                {
                    //success posting
                    WebResponse objWebResponse = objHttpWebRequest.GetResponse();
                    StreamReader objStreamReader = new StreamReader(objWebResponse.GetResponseStream());
                    responseResult = objStreamReader.ReadToEnd().ToString();
                }
                catch (Exception ex)
                {
                    responseResult = "Twitter Post Error: " + ex.Message.ToString() + ", authHeader: " + authorizationHeader;
                    Log(responseResult);
                    throw;
                }           

                return true;
            }
            catch
            {
                return false;
            }
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
            // rename the old file
            System.IO.File.Move("Extensions.Json", $"{GetFileNameTimeStampPrefix()}_Extensions.Json");

            var text = JsonConvert.SerializeObject(extensions);
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions.Json");
            path = "Extensions.Json";
            System.IO.File.WriteAllText(path, text);
        }

        private static string GetFileNameTimeStampPrefix()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd_HHmm");
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

                if (extension != null && (extension.flags.Split(", ").FirstOrDefault(item => item.Equals("public")) != null))
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