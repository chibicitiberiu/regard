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
            "/api/video/view"
        };

        public QueryStringAuthMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        private bool IsRequestWhitelisted(HttpRequest request)
        {
            // SignalR request
            if (request.Headers["Connection"] == "Upgrade")
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
