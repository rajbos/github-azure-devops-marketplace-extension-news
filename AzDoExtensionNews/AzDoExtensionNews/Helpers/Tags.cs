using AzDoExtensionNews.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AzDoExtensionNews.Helpers
{
    public static class Tags
    {
        /// <summary>
        /// Get the tags from the publisher as twitter hashTags
        /// </summary>
        /// <param name="extension">The extension to load the tags from</param>
        public static string GetHashTags(Extension extension)
        {
            if (extension == null) throw new ArgumentNullException(nameof(extension));

            var hashtagList = new List<string>();
            foreach (var tag in extension.tags)
            {
                // add # and TitleCase the tag
                if (!HideTag(tag))
                {
                    hashtagList.Add($"#{HashtagCasing(tag)}");
                }
            }

            // check for paid extension
            if (IsPaidExtension(extension))
            {
                hashtagList.Add(paidEmoticon);
            }

            var trailPeriod = GetTrailPeriod(extension);
            // check for trial period
            if (trailPeriod > 0)
            {
                hashtagList.Add($"{trailPeriod}Days");
            }

            return string.Join(" ", hashtagList);
        }

        /// <summary>
        /// Check if the extension is paid or not
        /// </summary>
        private static bool IsPaidExtension(Extension extension)
        {
            return extension.tags?.FirstOrDefault(item => item.Equals(IsPaidTag, StringComparison.InvariantCultureIgnoreCase)) != null;
        }

        /// <summary>
        /// Get the trials period in days
        /// </summary>
        /// <param name="extension">The extension to check</param>
        private static int GetTrailPeriod(Extension extension)
        {
            var tag = extension.tags?.FirstOrDefault(item => item.StartsWith(TrialTag, StringComparison.InvariantCultureIgnoreCase));
            if (tag == null)
            {
                return 0;
            }

            var splitted = tag.Split(":");
            if (splitted.Length < 2)
            {
                return 0;
            }

            var dayValue = splitted[1];
            if (int.TryParse(dayValue, out int days))
            {
                return days;
            }

            return 0;
        }

        private const string IsPaidTag = "$ISPAID";
        private const string TrialTag = "__Trialdays";
        private static readonly string[] HiddenTags = new string[3] { "$DONOTDOWNLOAD", IsPaidTag, "__BYOLENFORCED" };
        private static readonly string paidEmoticon = char.ConvertFromUtf32(0x1F4B3); // credit card emoji
        
        /// <summary>
        /// Checks a list of tags to hide that are used by the marketplace and not shown as tags
        /// </summary>
        /// <param name="tag">Text of the tag to inspect</param>
        private static bool HideTag(string tag)
        {
            var hideTag = HiddenTags.FirstOrDefault(item => item.Equals(tag, StringComparison.InvariantCultureIgnoreCase)) != null;
            if (!hideTag)
            {
                // check for "__Trialdays" or just even __
                hideTag = tag.StartsWith(TrialTag) || tag.StartsWith("__");
            }

            return hideTag;
        }

        private static string HashtagCasing(string text)
        {
            // Creates a TextInfo based on the "en-US" culture.
            TextInfo myTI = new CultureInfo("en-US", false).TextInfo;

            var titleCasedText = myTI.ToTitleCase(text).Replace(" ", String.Empty);
            return titleCasedText;
        }

    }
}
