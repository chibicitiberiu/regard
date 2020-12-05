using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Regard.Services
{
    public class BackendHttpClient : HttpClient
    {
        public BackendHttpClient(IConfiguration configuration) : base() 
        {
            BaseAddress = new Uri(configuration["BACKEND_URL"]);
        }

        public BackendHttpClient(IConfiguration configuration, HttpMessageHandler handler) : base(handler)
        {
            BaseAddress = new Uri(configuration["BACKEND_URL"]);
        }

        public BackendHttpClient(IConfiguration configuration, HttpMessageHandler handler, bool disposeHandler) : base(handler, disposeHandler) 
        {
            BaseAddress = new Uri(configuration["BACKEND_URL"]);
        }
    }
}
