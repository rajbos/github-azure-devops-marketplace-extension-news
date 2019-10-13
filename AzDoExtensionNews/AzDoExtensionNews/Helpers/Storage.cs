using AzDoExtensionNews.Models;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AzDoExtensionNews.Helpers
{
    internal static class Storage
    {
        private const string FileName = "Extensions.Json";
        private static string FilePath = "";
        #region LocalStorage

        private static string GetFilePath()
        {
            if (!String.IsNullOrEmpty(FilePath))
            {
                return FilePath;
            }

            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
            return FilePath;
        }

        public static void SaveJson(List<Extension> extensions)
        {
            // rename the old file
            RenameOldFile();

            var text = JsonConvert.SerializeObject(extensions, Formatting.Indented);
            WriteDataToFile(text);

            UploadFileAsync(GetFilePath()).GetAwaiter().GetResult();
        }

        private static void WriteDataToFile(string text)
        {
            System.IO.File.WriteAllText(GetFilePath(), text);
        }

        public static List<Extension> ReadFromJson()
        {
            List<Extension> extensions = null;
            try
            {
                string text = ReadDataFromFile();

                extensions = JsonConvert.DeserializeObject<List<Extension>>(text);
            }
            catch (Exception e)
            {
                // todo:Renaming the file on Storage for backup and analysis and start fresh. 
                Log.Message($"Error loading the file. Will start as if we have a clean slate Error: {e.Message}");
            }

            return extensions ?? new List<Extension>();
        }

        private static string ReadDataFromFile()
        {
            var filePath = GetFilePath();
            DownloadFileAsync(filePath).GetAwaiter().GetResult();
            var text = System.IO.File.ReadAllText(filePath);
            return text;
        }

        private static void RenameOldFile()
        {
            System.IO.File.Move(FileName, $"{GetFileNameTimeStampPrefix()}_{FileName}");
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

            // Get a reference to the blob address, then upload the file to the blob.
            // Use the value of localFileName for the blob name.
            CloudBlockBlob cloudBlockBlob = CloudBlobContainer.GetBlockBlobReference(localFileName);

            // todo: check if the file is newer then the local file, else do not download!
            var cloudLastModified = cloudBlockBlob.Properties.LastModified;
            await cloudBlockBlob.DownloadToFileAsync(filePath, FileMode.OpenOrCreate);
        }

        private static string GetConnectionString()
        {
            return Configuration.BlobStorageConnectionString;
        }
        #endregion
    }
}
