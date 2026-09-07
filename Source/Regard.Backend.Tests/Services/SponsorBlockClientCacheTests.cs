using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Services;
using Regard.Common.SponsorBlock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.Tests.Services
{
    /// <summary>
    /// The watch page fetches SponsorBlock live, which meant a request to sponsor.ajay.app on every load
    /// of the same video. GetSkipSegmentsCached puts a short in-memory cache in front of that. These
    /// pin down the two things that make the cache safe: it actually collapses repeat calls into one HTTP
    /// request, and it never hands back the list it holds (callers mutate Skip, which must not leak back).
    /// </summary>
    [TestClass]
    public class SponsorBlockClientCacheTests
    {
        // Canned response: one sponsor segment. actionType=skip so the client keeps it.
        private const string OneSponsorSegment =
            "[{\"segment\":[10.0,20.0],\"category\":\"sponsor\",\"actionType\":\"skip\"}]";

        // Minimal counting handler — no mocking library in this project.
        private sealed class CountingHandler : HttpMessageHandler
        {
            private readonly string body;
            private readonly HttpStatusCode status;
            public int Calls { get; private set; }

            public CountingHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
            {
                this.body = body;
                this.status = status;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Calls++;
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(body),
                });
            }
        }

        private static SponsorBlockClient MakeClient(CountingHandler handler, out IMemoryCache cache)
        {
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://sponsor.ajay.app") };
            cache = new MemoryCache(new MemoryCacheOptions());
            return new SponsorBlockClient(http, cache, NullLogger<SponsorBlockClient>.Instance);
        }

        private static readonly List<string> AllCats = SponsorBlockActions.Categories.ToList();

        [TestMethod]
        public async Task Second_lookup_of_the_same_video_is_served_from_cache()
        {
            var handler = new CountingHandler(OneSponsorSegment);
            var client = MakeClient(handler, out _);

            var first = await client.GetSkipSegmentsCached("vid1", AllCats);
            var second = await client.GetSkipSegmentsCached("vid1", AllCats);

            Assert.AreEqual(1, handler.Calls, "the second lookup should not hit the network");
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(1, second.Count);
            Assert.AreEqual("sponsor", second[0].Category);
        }

        [TestMethod]
        public async Task A_different_video_is_fetched_separately()
        {
            var handler = new CountingHandler(OneSponsorSegment);
            var client = MakeClient(handler, out _);

            await client.GetSkipSegmentsCached("vid1", AllCats);
            await client.GetSkipSegmentsCached("vid2", AllCats);

            Assert.AreEqual(2, handler.Calls, "a distinct video id must not share the cache entry");
        }

        [TestMethod]
        public async Task Callers_get_independent_copies_so_mutating_Skip_cannot_poison_the_cache()
        {
            var handler = new CountingHandler(OneSponsorSegment);
            var client = MakeClient(handler, out _);

            var first = await client.GetSkipSegmentsCached("vid1", AllCats);
            // Simulate what VideoController does: mark the segment to skip for this request's config.
            first[0].Skip = true;

            var second = await client.GetSkipSegmentsCached("vid1", AllCats);

            Assert.IsFalse(second[0].Skip, "the cached segment's Skip must not carry over between callers");
            Assert.AreNotSame(first[0], second[0], "each call should return fresh segment objects");
            Assert.AreEqual(1, handler.Calls);
        }

        [TestMethod]
        public async Task An_empty_result_is_cached_too_so_the_common_no_segments_case_stops_refetching()
        {
            // 404 is SponsorBlock's documented "no segments" response -> empty list, and it should cache.
            var handler = new CountingHandler("", HttpStatusCode.NotFound);
            var client = MakeClient(handler, out _);

            var first = await client.GetSkipSegmentsCached("vid1", AllCats);
            var second = await client.GetSkipSegmentsCached("vid1", AllCats);

            Assert.AreEqual(0, first.Count);
            Assert.AreEqual(0, second.Count);
            Assert.AreEqual(1, handler.Calls, "a no-segments result should also be cached");
        }

        [TestMethod]
        public async Task Empty_video_or_no_categories_never_touches_the_network()
        {
            var handler = new CountingHandler(OneSponsorSegment);
            var client = MakeClient(handler, out _);

            var noVid = await client.GetSkipSegmentsCached("", AllCats);
            var noCats = await client.GetSkipSegmentsCached("vid1", new List<string>());

            Assert.AreEqual(0, noVid.Count);
            Assert.AreEqual(0, noCats.Count);
            Assert.AreEqual(0, handler.Calls);
        }
    }
}
