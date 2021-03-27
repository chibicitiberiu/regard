using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API
{
    public enum ApiResult
    {
        Success,
        Error
    }

    public class ApiResponse
    {
        public ApiResult Status { get; set; }

        public string Message { get; set; }

        public string DebugMessage { get; set; }

        public ApiResponse()
        {
        }

        public ApiResponse(ApiResult status, string message = null, string debugMessage = null)
        {
            this.Status = status;
            this.Message = message;
            this.DebugMessage = debugMessage;
        }

        public static ApiResponse Success(string message = null, string debugMessage = null)
        {
            return new ApiResponse(ApiResult.Success, message, debugMessage);
        }

        public static ApiResponse Error(string message, string debugMessage = null)
        {
            return new ApiResponse(ApiResult.Error, message, debugMessage);
        }
    }

    public class ApiResponse<TData> : ApiResponse
    {
        public TData Data { get; set; }

        public ApiResponse() 
            : base()
        {
        }

        public ApiResponse(ApiResult status, TData data, string message = null, string debugMessage = null) 
            : base(status, message, debugMessage)
        {
            this.Data = data;
        }

        public static ApiResponse<TData> Success(TData data, string message = null, string debugMessage = null)
        {
            return new ApiResponse<TData>(ApiResult.Success, data, message, debugMessage);
        }

        public static ApiResponse<TData> Error(string message, string debugMessage = null, TData data = default)
        {
            return new ApiResponse<TData>(ApiResult.Error, data, message, debugMessage);
        }
    }
}
