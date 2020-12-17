using Microsoft.Extensions.Configuration;
using Regard.Common.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Backend.Services
{
    public class ApiResponseFactory
    {
        private readonly bool debug;

        public ApiResponseFactory(IConfiguration configuration)
        {
            debug = configuration.GetValue<bool>("Debug");
        }

        public ApiResponse Success(string message = null, string debugMessage = null)
        {
            return ApiResponse.Success(message, debug ? debugMessage : null);
        }

        public ApiResponse<TData> Success<TData>(TData data, string message = null, string debugMessage = null)
        {
            return ApiResponse<TData>.Success(data, message, debug ? debugMessage : null);
        }

        public ApiResponse Error(string message = null, string debugMessage = null)
        {
            return ApiResponse.Error(message, debug ? debugMessage : null);
        }

        public ApiResponse<TData> Error<TData>(string message, string debugMessage = null, TData data = default)
        {
            return ApiResponse<TData>.Error(message, debug ? debugMessage : null, data);
        }
    }
}
