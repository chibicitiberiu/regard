using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Middleware
{
    public class SignalRQueryStringAuthMiddleware
    {
        private readonly RequestDelegate next;

        public SignalRQueryStringAuthMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        // Convert incomming qs auth token to a Authorization header so the rest of the chain
        // can authorize the request correctly
        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Headers["Connection"] == "Upgrade" &&
                !context.Request.Headers.ContainsKey("Authorization") &&
                context.Request.Query.TryGetValue("access_token", out var token))
            {
                context.Request.Headers.Add("Authorization", "Bearer " + token.First());
            }
            await next.Invoke(context);
        }
    }

    public static class SignalRQueryStringAuthExtensions
    {
        public static IApplicationBuilder UseSignalRQueryStringAuth(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SignalRQueryStringAuthMiddleware>();
        }
    }
}
