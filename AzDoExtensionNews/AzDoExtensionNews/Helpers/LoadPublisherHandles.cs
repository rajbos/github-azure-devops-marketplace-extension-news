using AzDoExtensionNews.Models;
using News.Library;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AzDoExtensionNews.Helpers
{
    internal static class LoadPublisherHandles
    {
        public static List<PublisherHandles> GetPublisherHandles()
        {
            string text = ReadDataFromFile();

            var extensions = JsonConvert.DeserializeObject<PublisherHandles[]>(text).ToList();
            Log.Message($"Found {extensions.Count} previously known publishers");
            return extensions;
        }

        private static string ReadDataFromFile()
        {
            var filePath = GetFilePath();
            var text = System.IO.File.ReadAllText(filePath);
            return text;
        }

        private const string FileName = "Publishers.json";
        private static string FilePath;
        private static string GetFilePath()
        {
            if (!String.IsNullOrEmpty(FilePath))
            {
                return FilePath;
            }

            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", FileName);
            return FilePath;
        }
    }
}
