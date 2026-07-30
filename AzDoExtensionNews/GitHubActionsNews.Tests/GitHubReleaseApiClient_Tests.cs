using GitHubActionsNews;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GitHubActionsNews.Tests
{
    // Routes requests to a queue of canned responses, keyed by the requested url, so the API
    // client can be tested without making real network calls.
    internal class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(string UrlContains, HttpResponseMessage Response)> _responses = new();
        public List<string> RequestedUrls { get; } = new();

        public void Enqueue(string urlContains, HttpResponseMessage response)
        {
            _responses.Enqueue((urlContains, response));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri.ToString());

            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var (urlContains, response) = _responses.Dequeue();
            if (!string.IsNullOrEmpty(urlContains) && !request.RequestUri.ToString().Contains(urlContains))
            {
                throw new InvalidOperationException($"Unexpected request to [{request.RequestUri}], expected it to contain [{urlContains}]");
            }

            return Task.FromResult(response);
        }
    }

    [TestClass]
    public class GitHubReleaseApiClient_Tests
    {
        private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body, int? rateLimitRemaining = 5000)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body ?? string.Empty)
            };
            if (rateLimitRemaining.HasValue)
            {
                response.Headers.Add("X-RateLimit-Remaining", rateLimitRemaining.Value.ToString());
            }
            return response;
        }

        [TestMethod]
        public async Task GetLatestVersionAsync_ReturnsTagFromReleasesLatest()
        {
            var handler = new FakeHttpMessageHandler();
            handler.Enqueue("/releases/latest", JsonResponse(HttpStatusCode.OK, "{\"tag_name\":\"v4.2.0\"}"));
            var client = new GitHubReleaseApiClient(new HttpClient(handler), "token");

            var result = await client.GetLatestVersionAsync("https://github.com/actions/checkout");

            Assert.IsTrue(result.Success);
            Assert.AreEqual("v4.2.0", result.Version);
            Assert.IsFalse(result.RateLimited);
            Assert.AreEqual(1, handler.RequestedUrls.Count, "Should only need a single API call when releases/latest succeeds");
        }

        [TestMethod]
        public async Task GetLatestVersionAsync_FallsBackToTags_WhenNoReleaseExists()
        {
            var handler = new FakeHttpMessageHandler();
            handler.Enqueue("/releases/latest", JsonResponse(HttpStatusCode.NotFound, null));
            handler.Enqueue("/tags", JsonResponse(HttpStatusCode.OK, "[{\"name\":\"v1.0.0\"}]"));
            var client = new GitHubReleaseApiClient(new HttpClient(handler), "token");

            var result = await client.GetLatestVersionAsync("https://github.com/owner/repo-with-only-tags");

            Assert.IsTrue(result.Success);
            Assert.AreEqual("v1.0.0", result.Version);
            Assert.AreEqual(2, handler.RequestedUrls.Count);
        }

        [TestMethod]
        public async Task GetLatestVersionAsync_ReturnsUnsuccessful_WhenNoReleasesOrTagsFound()
        {
            var handler = new FakeHttpMessageHandler();
            handler.Enqueue("/releases/latest", JsonResponse(HttpStatusCode.NotFound, null));
            handler.Enqueue("/tags", JsonResponse(HttpStatusCode.OK, "[]"));
            var client = new GitHubReleaseApiClient(new HttpClient(handler), "token");

            var result = await client.GetLatestVersionAsync("https://github.com/owner/empty-repo");

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.RateLimited);
        }

        [TestMethod]
        public async Task GetLatestVersionAsync_ReturnsNotSuccessful_ForInvalidRepoUrl()
        {
            var handler = new FakeHttpMessageHandler();
            var client = new GitHubReleaseApiClient(new HttpClient(handler), "token");

            var result = await client.GetLatestVersionAsync("not-a-url");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, handler.RequestedUrls.Count, "An invalid repo url should never trigger an API call");
        }

        [TestMethod]
        public async Task GetLatestVersionAsync_MarksRateLimited_OnForbiddenResponse()
        {
            var handler = new FakeHttpMessageHandler();
            var forbidden = new HttpResponseMessage(HttpStatusCode.Forbidden);
            forbidden.Headers.Add("Retry-After", "30");
            handler.Enqueue("/releases/latest", forbidden);
            var client = new GitHubReleaseApiClient(new HttpClient(handler), "token");

            var result = await client.GetLatestVersionAsync("https://github.com/owner/repo");

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.RateLimited);
        }

        [TestMethod]
        public async Task GetLatestVersionAsync_StopsCallingApi_OnceRateLimitRemainingIsLow()
        {
            var handler = new FakeHttpMessageHandler();
            handler.Enqueue("/releases/latest", JsonResponse(HttpStatusCode.OK, "{\"tag_name\":\"v1.0.0\"}", rateLimitRemaining: 10));
            var client = new GitHubReleaseApiClient(new HttpClient(handler), "token");

            var first = await client.GetLatestVersionAsync("https://github.com/owner/repo");
            Assert.IsTrue(first.Success);
            Assert.IsTrue(client.IsRateLimited, "Remaining quota of 10 is below the safety threshold and should already flag as rate limited");

            // second call should short-circuit: rate limit remaining (10) is below the safety threshold
            var second = await client.GetLatestVersionAsync("https://github.com/owner/repo2");

            Assert.IsFalse(second.Success);
            Assert.IsTrue(second.RateLimited);
            Assert.AreEqual(1, handler.RequestedUrls.Count, "No further HTTP calls should be made once the remaining quota is below the safety threshold");
        }

        [TestMethod]
        public void ParseOwnerRepo_ExtractsOwnerAndRepo_FromRepoUrl()
        {
            var (owner, repo) = GitHubReleaseApiClient.ParseOwnerRepo("https://github.com/actions/checkout");

            Assert.AreEqual("actions", owner);
            Assert.AreEqual("checkout", repo);
        }

        [TestMethod]
        public void ParseOwnerRepo_ExtractsOwnerAndRepo_IgnoringTrailingSegments()
        {
            var (owner, repo) = GitHubReleaseApiClient.ParseOwnerRepo("https://github.com/actions/checkout/tree/main");

            Assert.AreEqual("actions", owner);
            Assert.AreEqual("checkout", repo);
        }

        [TestMethod]
        public void ParseOwnerRepo_ReturnsNulls_ForMalformedUrl()
        {
            var (owner, repo) = GitHubReleaseApiClient.ParseOwnerRepo("https://github.com/justowner");

            Assert.IsNull(owner);
            Assert.IsNull(repo);
        }
    }
}
