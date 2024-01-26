using AzDoExtensionNews.Helpers;
using AzDoExtensionNews.Models;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Tests.Helpers
{
    public class TagsUnitTests
    {
        [Test]
        public void TrialdaysHashtag()
        {
            // Arrange
            var extension = new Extension
            {
                tags = new string[1] { "__Trialdays:18" }
            };

            // Act
            var hashTags = Tags.GetHashTags(extension, 100);

            // Assert
            ClassicAssert.AreEqual("Trial: 18 Days", hashTags);
        }

        [Test]
        public void IsPaidExtension()
        {
            // Arrange
            var extension = new Extension
            {
                tags = new string[1] { "$ISPAID" }
            };

            // Act
            var hashTags = Tags.GetHashTags(extension, 100);

            // Assert
            ClassicAssert.AreEqual(char.ConvertFromUtf32(0x1F4B3), hashTags);
        }
    }
}