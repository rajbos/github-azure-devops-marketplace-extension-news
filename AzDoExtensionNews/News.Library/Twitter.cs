using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;

namespace News.Library
{
    public class Twitter
    {
        public Twitter(string oauth_consumer_key, string oauth_consumer_secret, string oauth_token, string oauth_token_secret)
        {
            OauthConsumerKey = oauth_consumer_key;
            OauthConsumerSecret = oauth_consumer_secret;
            OauthToken = oauth_token;
            OauthTokenSecret = oauth_token_secret;
        }

        private DateTime LastTweeted = DateTime.UtcNow - TimeSpan.FromMinutes(10);
        private const int RateLimitDurationInSeconds = 10;

        public string OauthConsumerKey { get; }
        public string OauthConsumerSecret { get; }
        public string OauthToken { get; }
        public string OauthTokenSecret { get; }

        public bool SendTweet(string tweetText, string imageUrl, string additionalLogInfo = null)
        {
            var lastTweetedDuration = DateTime.UtcNow - LastTweeted;
            if (lastTweetedDuration.TotalSeconds < RateLimitDurationInSeconds)
            {
                // back off a little in an attempt to not hit any twitter policy violations
                Log.Message($"Last tweet was {lastTweetedDuration.TotalMilliseconds} milliseconds ago. Backing off for {RateLimitDurationInSeconds} seconds.");
                Thread.Sleep(TimeSpan.FromSeconds(RateLimitDurationInSeconds));
            }

            Log.Message($"Sending tweet: {tweetText}. Tweet.Length: {tweetText.Length} {additionalLogInfo}");
            // update last tweeted
            LastTweeted = DateTime.UtcNow;
            //return TweetWithNuget(tweetText, imageUrl);
            // return TweetWithHttp(tweetText);
            return true;
        }

        private bool TweetWithHttp(string tweetText)
        {
            try
            {
                string twitterURL = "https://api.twitter.com/1.1/statuses/update.json";

                // set the oauth version and signature method
                string oauth_version = "1.0";
                string oauth_signature_method = "HMAC-SHA1";

                // create unique request details
                string oauth_nonce = Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));
                System.TimeSpan timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
                string oauth_timestamp = Convert.ToInt64(timeSpan.TotalSeconds).ToString();

                // create oauth signature
                string baseFormat = "oauth_consumer_key={0}&oauth_nonce={1}&oauth_signature_method={2}" + "&oauth_timestamp={3}&oauth_token={4}&oauth_version={5}&status={6}";

                string baseString = string.Format(
                    baseFormat,
                    OauthConsumerKey,
                    oauth_nonce,
                    oauth_signature_method,
                    oauth_timestamp, OauthToken,
                    oauth_version,
                    Uri.EscapeDataString(tweetText)
                );

                string oauth_signature = null;
                using (HMACSHA1 hasher = new HMACSHA1(ASCIIEncoding.ASCII.GetBytes(Uri.EscapeDataString(OauthConsumerSecret) + "&" + Uri.EscapeDataString(OauthTokenSecret))))
                {
                    oauth_signature = Convert.ToBase64String(hasher.ComputeHash(ASCIIEncoding.ASCII.GetBytes("POST&" + Uri.EscapeDataString(twitterURL) + "&" + Uri.EscapeDataString(baseString))));
                }

                // create the request header
                string authorizationFormat = "OAuth oauth_consumer_key=\"{0}\", oauth_nonce=\"{1}\", " + "oauth_signature=\"{2}\", oauth_signature_method=\"{3}\", " + "oauth_timestamp=\"{4}\", oauth_token=\"{5}\", " + "oauth_version=\"{6}\"";

                string authorizationHeader = string.Format(
                    authorizationFormat,
                    Uri.EscapeDataString(OauthConsumerKey),
                    Uri.EscapeDataString(oauth_nonce),
                    Uri.EscapeDataString(oauth_signature),
                    Uri.EscapeDataString(oauth_signature_method),
                    Uri.EscapeDataString(oauth_timestamp),
                    Uri.EscapeDataString(OauthToken),
                    Uri.EscapeDataString(oauth_version)
                );

                HttpWebRequest objHttpWebRequest = (HttpWebRequest)WebRequest.Create(twitterURL);
                objHttpWebRequest.Headers.Add("Authorization", authorizationHeader);
                objHttpWebRequest.Method = "POST";
                objHttpWebRequest.ContentType = "application/x-www-form-urlencoded";
                using (System.IO.Stream objStream = objHttpWebRequest.GetRequestStream())
                {
                    byte[] content = ASCIIEncoding.ASCII.GetBytes("status=" + Uri.EscapeDataString(tweetText));
                    objStream.Write(content, 0, content.Length);
                }

                var responseResult = "";

                try
                {
                    //success posting
                    WebResponse objWebResponse = objHttpWebRequest.GetResponse();
                    StreamReader objStreamReader = new StreamReader(objWebResponse.GetResponseStream());
                    //responseResult = objStreamReader.ReadToEnd().ToString();
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("(403) Forbidden")) // probably a duplicate tweet
                    {
                        responseResult = "Twitter Post Error: " + ex.Message.ToString() + ", authHeader: " + authorizationHeader;
                        Log.Message(responseResult);
                        throw;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TweetWithNuget(string tweetText, string imageUrl)
        {
            try
            {
                // setup authentication with Twitter
                var appCredentials = new TwitterCredentials(
                    consumerKey: OauthConsumerKey,
                    consumerSecret: OauthConsumerSecret,
                    accessToken: OauthToken,
                    accessTokenSecret: OauthTokenSecret);

                var userClient = new TwitterClient(appCredentials);

                if (tweetText.Length > 280)
                {
                    tweetText = tweetText.Substring(0, 280);
                }

                // download image URL in memory
                var mediaFile = DownloadImageAsync(imageUrl).GetAwaiter().GetResult();
                if (mediaFile != null)
                {
                    // upload it to twitter

                    //store the file
                    var filePath = Guid.NewGuid().ToString(); // + ".png"; // not sure if this is needed
                    File.WriteAllBytes(filePath, mediaFile);

                    // upload image to add to the tweet
                    IMedia uploadedImage = null;

                    try
                    {
                        //var tweetinviLogoBinary = File.ReadAllBytes(filePath);
                        //uploadedImage = userClient.Upload.UploadTweetImageAsync(new UploadTweetImageParameters(tweetinviLogoBinary)
                        //{
                        //    MediaCategory = MediaCategory.Image,
                        //    MediaType = MediaType.Media,
                        //    WaitForTwitterProcessing = true,
                        //}).GetAwaiter().GetResult();
                    }
                    catch (Exception e)
                    {
                        Log.Message($"Error uploading media to Twitter: {Environment.NewLine}{e.Message}");
                    }

                    if (uploadedImage != null)
                    {
                        if (!uploadedImage.IsReadyToBeUsed || !uploadedImage.HasBeenUploaded)
                        {
                            // give more time for the tweet media to be ready
                            Thread.Sleep(3000);
                        }

                        try
                        {
                            // publish the tweet with the media
                            var tweet2 = userClient.Tweets.PublishTweetAsync(new PublishTweetParameters
                            {
                                Text = tweetText,
                                MediaIds = new List<long> { uploadedImage.Id.Value }, // does Ids work or do we need the Medias list?
                            }
                            ).GetAwaiter().GetResult();

                            if (tweet2 != null)
                            {
                                Log.Message($"Tweet came back with creation timestamp: [{tweet2.CreatedAt}]");
                            }
                            else
                            {
                                Log.Message($"Error sending tweet, result was null");
                            }
                        }
                        catch
                        {
                            // just publish the tweet without the media
                            var tweet3 = userClient.Tweets.PublishTweetAsync(tweetText).GetAwaiter().GetResult();
                            if (tweet3 != null)
                            {
                                Log.Message($"Tweet came back with creation timestamp: [{tweet3.CreatedAt}]");
                            }
                            else
                            {
                                Log.Message($"Error sending tweet, result was null");
                            }
                        }

                        return true;
                    }
                }

                // just publish the tweet without the media
                var tweet = userClient.Tweets.PublishTweetAsync(tweetText).GetAwaiter().GetResult();
                if (tweet != null)
                {
                    Log.Message($"Tweet came back with creation timestamp: [{tweet.CreatedAt}]");
                }
                else
                {
                    Log.Message($"Error sending tweet, result was null");
                }

                return true;

            }
            catch (Exception e)
            {
                Log.Message($"Error tweeting: {e.Message}");
                // throw; // throwing will stop the run, NOT store the update and will send out the same tweet next time again.
                return false;
            }
        }

        private async Task<byte[]> DownloadImageAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) { return null; }

            try
            {
                using (var client = new HttpClient())
                {

                    using (var result = await client.GetAsync(imageUrl))
                    {
                        if (result.IsSuccessStatusCode)
                        {
                            return await result.Content.ReadAsByteArrayAsync();
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Log.Message($"Error downloading the image from [{imageUrl}]: {e.Message}");
            }

            return null;
        }
    }
}
