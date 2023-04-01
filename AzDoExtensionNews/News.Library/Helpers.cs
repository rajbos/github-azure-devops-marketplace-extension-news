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
         
        public static void Message(string message, bool logsummary)
        {
            // log to both the console and the GITHUB_STEP_SUMMARY environment variable
            var timeTag = DateTime.UtcNow.ToString(Format);
            Console.WriteLine($"{timeTag}  {message}");
            if (logsummary) {
                // get environment variable
                var summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
                if (summary != null) {
                    // append to the file in that location
                    System.IO.File.AppendAllText(summary, $"{timeTag}  {message} {Environment.NewLine}");
                }
            }
        }
    }
}
