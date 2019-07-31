using System;

namespace AzDoExtensionNews.Helpers
{
    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(message)
        {
        }
    }
}
