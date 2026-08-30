using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Middleware
{
    public class QueryStringAuthMiddleware
    {
        private readonly RequestDelegate next;

        private readonly HashSet<string> WhitelistedPaths = new HashSet<string>()
        {
            "/api/video/view",
            // Subtitles are fetched by the browser's own <track> loader, which sends no headers. The
            // endpoint still authorizes normally and is owner-scoped; this only moves the bearer token
            // from a header to the query string, as for the video stream above.
            "/api/video/subtitle"
        };

        public QueryStringAuthMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        private bool IsRequestWhitelisted(HttpRequest request)
        {
            // SignalR WebSocket upgrade (some agents send "keep-alive, Upgrade", so match loosely).
            if (request.Headers["Connection"].ToString().Contains("Upgrade", StringComparison.OrdinalIgnoreCase))
                return true;

            // The SignalR hub itself, so non-WebSocket transports (SSE / long-polling negotiate)
            // also authenticate — otherwise Clients.User(...) would silently deliver to nobody.
            if (request.Path.StartsWithSegments("/api/message_hub"))
                return true;

            if (WhitelistedPaths.Contains(request.Path.Value))
                return true;

            return false;
        }

        // Convert incomming qs auth token to a Authorization header so the rest of the chain
        // can authorize the request correctly
        public async Task Invoke(HttpContext context)
        {
            if (!context.Request.Headers.ContainsKey("Authorization") 
                && context.Request.Query.TryGetValue("access_token", out var token) 
                && IsRequestWhitelisted(context.Request))
            {
                context.Request.Headers.Add("Authorization", "Bearer " + token.First());
            }
            await next.Invoke(context);
        }
    }

    public static class QueryStringAuthExtensions
    {
        public static IApplicationBuilder UseSignalRQueryStringAuth(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<QueryStringAuthMiddleware>();
        }
    }
}
