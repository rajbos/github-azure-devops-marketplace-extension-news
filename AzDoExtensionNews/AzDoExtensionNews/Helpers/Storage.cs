using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace AzDoExtensionNews.Helpers
{
    internal static class Storage
    {
        private const string FileName = "Extensions.Json";

        public static void SaveJson(List<Extension> extensions)
        {
            // rename the old file
            RenameOldFile();

            var text = JsonConvert.SerializeObject(extensions);
            WriteDataToFile(text);
        }

        private static void WriteDataToFile(string text)
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
            path = FileName;
            System.IO.File.WriteAllText(path, text);
        }

        public static List<Extension> ReadFromJson()
        {
            string text = ReadDataFromFile();

            var extensions = JsonConvert.DeserializeObject<List<Extension>>(text);
            return extensions;
        }

        private static string ReadDataFromFile()
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
            path = FileName;

            var text = System.IO.File.ReadAllText(path);
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
    }
}
