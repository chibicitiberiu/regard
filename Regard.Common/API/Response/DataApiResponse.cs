using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Response
{
    public enum ApiStatus
    {
        Success,
        Error
    }

    public class ApiResponse
    {
        public ApiStatus Status { get; set; }

        public string Message { get; set; }

        public string DebugMessage { get; set; }

        public ApiResponse()
        {
        }

        public ApiResponse(ApiStatus status, string message = null, string debugMessage = null)
        {
            this.Status = status;
            this.Message = message;
            this.DebugMessage = debugMessage;
        }

        public static ApiResponse Success(string message = null, string debugMessage = null)
        {
            return new ApiResponse(ApiStatus.Success, message, debugMessage);
        }

        public static ApiResponse Error(string message, string debugMessage = null)
        {
            return new ApiResponse(ApiStatus.Error, message, debugMessage);
        }
    }


    public class DataApiResponse<TData> : ApiResponse
    {
        public TData Data { get; set; }

        public DataApiResponse() 
            : base()
        {
        }

        public DataApiResponse(ApiStatus status, TData data, string message = null, string debugMessage = null) 
            : base(status, message, debugMessage)
        {
            this.Data = data;
        }

        public static DataApiResponse<TData> Success(TData data, string message = null, string debugMessage = null)
        {
            return new DataApiResponse<TData>(ApiStatus.Success, data, message, debugMessage);
        }

        public static DataApiResponse<TData> Error(string message, string debugMessage = null, TData data = default)
        {
            return new DataApiResponse<TData>(ApiStatus.Error, data, message, debugMessage);
        }
    }
}
