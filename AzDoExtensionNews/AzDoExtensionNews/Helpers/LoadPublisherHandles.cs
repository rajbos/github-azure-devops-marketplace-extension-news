using AzDoExtensionNews.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AzDoExtensionNews.Helpers
{
    internal static class LoadPublisherHandles
    {
        public static List<PublisherHandles> GetPublisherHandles()
        {
            string text = ReadDataFromFile();

            var extensions = JsonConvert.DeserializeObject<PublisherHandles[]>(text);
            return extensions.ToList();
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
