using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AzDoExtensionNews
{
    class Program
    {
        static void Main(string[] args)
        {
            CheckForUpdates().GetAwaiter().GetResult();

            if (Debugger.IsAttached)
            {
                Log("Hit the return key to close the application");
            }
        }

        private static async Task CheckForUpdates()
        {
            var maxPages = 50;
            var allExtensions = new List<Extension>();
            // get all data
            for (int i = 0; i < maxPages; i++)
            {
                var data = await LoadExtensionDataAsync(pageNumber: i, pageSize: 50);

                if (data == null || data.results[0].extensions.Length == 0) break;

                LogDataResult(data, pageNumber: i);

                allExtensions.AddRange(data.results[0].extensions);
            }

            Log($"{allExtensions.Count}");

            // check with stored data
            // store new data
            // tweet updates
        }

        private static void LogDataResult(ExtensionDataResult data, int pageNumber)
        {
            var extensions = data.results[0].extensions;
            Log($"Found {extensions.Length} extensions on page number {pageNumber}");
            Log("");

            for (var i = 0; i < extensions.Length; i++)
            {
                var extension = extensions[i];
                Log($"{(pageNumber * 50 + i):D3} {extension.lastUpdated} {extension.displayName}");
            }

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
