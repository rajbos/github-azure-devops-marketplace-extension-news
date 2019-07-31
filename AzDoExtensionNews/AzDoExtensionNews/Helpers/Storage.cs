using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace AzDoExtensionNews.Helpers
{
    internal static class Storage
    {
        public static void SaveJson(List<Extension> extensions)
        {
            // rename the old file
            System.IO.File.Move("Extensions.Json", $"{GetFileNameTimeStampPrefix()}_Extensions.Json");

            var text = JsonConvert.SerializeObject(extensions);
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions.Json");
            path = "Extensions.Json";
            System.IO.File.WriteAllText(path, text);
        }

        public static string GetFileNameTimeStampPrefix()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd_HHmm");
        }

        public static List<Extension> ReadFromJson()
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions.Json");
            path = "Extensions.Json";

            var text = System.IO.File.ReadAllText(path);


            var extensions = JsonConvert.DeserializeObject<List<Extension>>(text);
            return extensions;

        }

    }
}
