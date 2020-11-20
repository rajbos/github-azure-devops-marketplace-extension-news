using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace News.Library
{
    public static class Storage
    {
        #region LocalStorage

        private static string GetFilePath(string fileName)
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{fileName}.Json");
            return filePath;
        }

        public static void SaveJson<T>(List<T> extensions, string fileName)
        {
            // rename the old file
            RenameOldFile(fileName);

            var text = JsonConvert.SerializeObject(extensions, Formatting.Indented);
            WriteDataToFile(text, fileName);

            UploadFileAsync(GetFilePath(fileName)).GetAwaiter().GetResult();
        }

        private static void WriteDataToFile(string text, string fileName)
        {
            System.IO.File.WriteAllText(GetFilePath(fileName), text);
        }

        public static List<T> ReadFromJson<T>(string fileName, string message = "extensions")
        {
            List<T> extensions = null;
            try
            {
                string text = ReadDataFromFile(fileName);

                extensions = JsonConvert.DeserializeObject<List<T>>(text);
                Log.Message($"Found {extensions.Count} previously known {message}");
            }
            catch (Exception e)
            {
                // todo:Renaming the file on Storage for backup and analysis and start fresh. 
                Log.Message($"Error loading the file [{fileName}]. Will start as if we have a clean slate Error: {e.Message}");
            }

            return extensions ?? new List<T>();
        }

        private static string ReadDataFromFile(string fileName)
        {
            var filePath = GetFilePath(fileName);
            DownloadFileAsync(filePath).GetAwaiter().GetResult();
            var text = File.ReadAllText(filePath);
            return text;
        }

        private static void RenameOldFile(string fileName)
        {
            var fullFileName = $"{fileName}.json";
            if (File.Exists(fullFileName))
            {
               File.Move(fullFileName, $"{GetFileNameTimeStampPrefix()}_{fullFileName}");
            }
        }

        public static string GetFileNameTimeStampPrefix()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd_HHmm");
        }

        #endregion
        #region BlobStorage
        private static CloudBlobContainer CloudBlobContainer = null;
        private static async Task<CloudBlobContainer> GetBlobClientAsync(string storageConnectionString)
        {
            if (CloudBlobContainer != null)
            {
                return CloudBlobContainer;
            }

            CloudStorageAccount storageAccount;
            if (!CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                throw new Exception("Error parsing the blob storage connection string");
            }

            // Create the CloudBlobClient that represents the 
            // Blob storage endpoint for the storage account.
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

            // Create a container called 'data' 
            CloudBlobContainer = cloudBlobClient.GetContainerReference("data");
            await CloudBlobContainer.CreateIfNotExistsAsync();

            return CloudBlobContainer;
        }

        private static async Task UploadFileAsync(string filePath)
        {
            var connectionString = GetConnectionString();
            await GetBlobClientAsync(connectionString);

            string localFileName = Path.GetFileName(filePath);
            
            Console.WriteLine("Uploading to Blob storage as blob '{0}'", localFileName);

            // Get a reference to the blob address, then upload the file to the blob.
            // Use the value of localFileName for the blob name.
            CloudBlockBlob cloudBlockBlob = CloudBlobContainer.GetBlockBlobReference(localFileName);
            await cloudBlockBlob.UploadFromFileAsync(filePath);
        }

        private static async Task DownloadFileAsync(string filePath)
        {
            var connectionString = GetConnectionString();
            await GetBlobClientAsync(connectionString);

            string localFileName = Path.GetFileName(filePath);

            Console.WriteLine("Downloading from Blob storage as blob to '{0}'", filePath);

            // Get a reference to the blob address, then download the file from the blob.
            // Use the value of localFileName for the blob name.
            CloudBlockBlob cloudBlockBlob = CloudBlobContainer.GetBlockBlobReference(localFileName);

            // todo: check if the file is newer then the local file, else do not download!
            var cloudLastModified = cloudBlockBlob.Properties.LastModified;
            await cloudBlockBlob.DownloadToFileAsync(filePath, FileMode.OpenOrCreate);
        }

        public static async Task<List<T>> DownloadAllFilesThatStartWith<T>(string startsWith)
        {
            // get all file refs that start with startsWith
            var connectionString = GetConnectionString();
            await GetBlobClientAsync(connectionString);

            var list = CloudBlobContainer.ListBlobs(prefix: startsWith);

            var allItems = new List<T>();
            foreach (var item in list)
            {
                // download them all
                var splitted = item.StorageUri.PrimaryUri.ToString().Split('/');
                var fileName = Path.GetFileNameWithoutExtension(splitted[splitted.Length - 1]);

                // group the results
                var itemsFromFile = ReadFromJson<T>(fileName);

                allItems.AddRange(itemsFromFile);                
            }

            return allItems;
        }

        private static string GetConnectionString()
        {
            return Configuration.BlobStorageConnectionString;
        }
        #endregion
    }
}
