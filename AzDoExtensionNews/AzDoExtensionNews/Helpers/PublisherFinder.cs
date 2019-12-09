using AzDoExtensionNews.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AzDoExtensionNews.Helpers
{
    public static class PublisherFinder
    {        
        public static string GetPublisher(Extension extension, List<PublisherHandles> publisherHandles)
        {
            var handle = publisherHandles.FirstOrDefault(item => item.PublisherName.Equals(extension.publisher.publisherName, StringComparison.InvariantCultureIgnoreCase));
            if (handle != null)
            {
                Console.WriteLine($"Could not find publisher handle for [{extension.publisher.publisherName}] in the publisher handles list (Count: {publisherHandles.Count})");
                return handle.TwitterHandle;
            }

            return extension.publisher.displayName;
        }
    }
}
