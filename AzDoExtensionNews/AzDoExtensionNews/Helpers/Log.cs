using System;

namespace AzDoExtensionNews.Helpers
{
    internal static class Log
    {
        private const string Format = "HH:mm:ss";

        public static void Message(string message)
        {
            var timeTag = DateTime.UtcNow.ToString(Format);
            Console.WriteLine($"{timeTag}  {message}");
        }
    }
}
