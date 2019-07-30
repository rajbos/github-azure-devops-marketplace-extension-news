using System;
using System.Collections.Generic;
using System.Text;

namespace AzDoExtensionNews
{
    public class ExtensionDataResult
    {
        public Result[] results { get; set; }
    }

    public class Result
    { 
        public Extension[] extensions { get; set; }
        public object pagingToken { get; set; }
        public Resultmetadata[] resultMetadata { get; set; }
    }

    public class Extension
    {
        public Publisher publisher { get; set; }
        public string extensionId { get; set; }
        public string extensionName { get; set; }
        public string displayName { get; set; }
        public string flags { get; set; }
        public DateTime lastUpdated { get; set; }
        public DateTime publishedDate { get; set; }
        public DateTime releaseDate { get; set; }
        public string shortDescription { get; set; }
        public Version[] versions { get; set; }
        public string[] categories { get; set; }
        public string[] tags { get; set; }
        public Statistic[] statistics { get; set; }
        public Installationtarget[] installationTargets { get; set; }
        public int deploymentType { get; set; }

        public string Url
        {
            get
            {
                return $"https://marketplace.visualstudio.com/items?itemName={publisher?.publisherName}.{extensionName}";
            }
        }
    }

    public class Publisher
    {
        public string publisherId { get; set; }
        public string publisherName { get; set; }
        public string displayName { get; set; }
        public string flags { get; set; }
    }

    public class Version
    {
        public string version { get; set; }
        public string flags { get; set; }
        public DateTime lastUpdated { get; set; }
        public File[] files { get; set; }
        public Property1[] properties { get; set; }
        public string assetUri { get; set; }
        public string fallbackAssetUri { get; set; }
    }

    public class File
    {
        public string assetType { get; set; }
        public string source { get; set; }
    }

    public class Property1
    {
        public string key { get; set; }
        public string value { get; set; }
    }

    public class Statistic
    {
        public string statisticName { get; set; }
        public float value { get; set; }
    }

    public class Installationtarget
    {
        public string target { get; set; }
        public string targetVersion { get; set; }
    }

    public class Resultmetadata
    {
        public string metadataType { get; set; }
        public Metadataitem[] metadataItems { get; set; }
    }

    public class Metadataitem
    {
        public string name { get; set; }
        public int count { get; set; }
    }

}
