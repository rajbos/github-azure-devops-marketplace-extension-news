using System;
using System.Text;

namespace News.Library
{
    public static class Log
    {
        private const string Format = "HH:mm:ss";

        public static void Message(string message)
        {
            var timeTag = DateTime.UtcNow.ToString(Format);
            Console.WriteLine($"{timeTag}  {message}");
        }

        public static void Message(string message, StringBuilder logger)
        {
            var timeTag = DateTime.UtcNow.ToString(Format);
            logger.AppendLine($"{timeTag}  {message}");
        }
    }
}
