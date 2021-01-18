using AzDoExtensionNews.Models;
using News.Library;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AzDoExtensionNews.Helpers
{
    public static class PublisherFinder
    {        
        public static string GetPublisher(Extension extension, List<PublisherHandles> publisherHandles, bool useTwitterHandles = false)
        {
            if (useTwitterHandles)
            {
                var handle = publisherHandles.FirstOrDefault(item => item.PublisherName.Equals(extension.publisher.publisherName, StringComparison.InvariantCultureIgnoreCase));
                if (handle != null)
                {
                    return handle.TwitterHandle;
                }

                Log.Message($"Could not find publisher handle for [{extension.publisher.publisherName}] in the publisher handles list (Count: {publisherHandles.Count})");
            }
            return extension.publisher.displayName;
        }
    }
}
