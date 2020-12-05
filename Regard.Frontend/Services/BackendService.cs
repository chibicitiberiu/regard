using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Regard.Api;
using Regard.Common.API;
using Regard.Common.API.Request;
using Regard.Common.API.Response;
using RegardBackend.Common.API.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Regard.Services
{
    public class BackendService
    {
        private readonly BackendHttpClient client;
        private readonly ILocalStorageService localStorage;

        class Empty { }

        private static readonly Empty EmptyRequest = new Empty();

        public BackendService(BackendHttpClient client, ILocalStorageService localStorage)
        {
            this.client = client;
            this.localStorage = localStorage;
        }

        private async Task UpdateAuthorization()
        {
            var token = await localStorage.GetItemAsync<string>(AuthenticationService.StorageAuthTokenKey);
            client.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token) ? null : new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", token);
        }

        private Task<DataApiResponse<TResponse>> Get<TResponse>(string uri)
        {
            return Get<Empty, TResponse>(uri, null);
        }

        private async Task<DataApiResponse<TResponse>> Get<TRequest, TResponse>(string uri, TRequest data)
        {
            await UpdateAuthorization();

            uri = GenerateQueryString(uri, data);
            var dataResponse = await client.GetFromJsonAsync<DataApiResponse<TResponse>>(uri);
            LogDebugMessage(dataResponse);

            return dataResponse;
        }

        private async Task<(ApiResponse, HttpResponseMessage)> Post<TRequest>(string uri, TRequest data)
        {
            await UpdateAuthorization();
            var response = await client.PostAsJsonAsync(uri, data);
            var dataResponse = await response.Content.ReadFromJsonAsync<ApiResponse>();
            LogDebugMessage(dataResponse);

            return (dataResponse, response);
        }

        private async Task<(DataApiResponse<TResponse>, HttpResponseMessage)> Post<TRequest, TResponse>(string uri, TRequest data)
        {
            await UpdateAuthorization();
            var response = await client.PostAsJsonAsync(uri, data);
            var dataResponse = await response.Content.ReadFromJsonAsync<DataApiResponse<TResponse>>();
            LogDebugMessage(dataResponse);

            return (dataResponse, response);
        }

        private static void LogDebugMessage(ApiResponse response)
        {
            if (!string.IsNullOrEmpty(response.DebugMessage))
            {
                Console.Error.WriteLine("Message from backend: ");
                Console.Error.WriteLine(response.DebugMessage);
            }
        }

        private static string GenerateQueryString<TRequest>(string uri, TRequest data)
        {
            if (data != null)
            {
                var values = new Dictionary<string, string>();
                foreach (var property in data.GetType().GetProperties())
                {
                    var value = property.GetValue(data, null);
                    if (value != null)
                        values.Add(property.Name, value.ToString());
                }

                uri = QueryHelpers.AddQueryString(uri, values);
            }

            return uri;
        }

        #region Setup

        public Task<DataApiResponse<ServerStatus>> SetupServerStatus() 
            => Get<ServerStatus>("api/setup/server_status");

        public Task<(ApiResponse, HttpResponseMessage)> SetupInitialize()
            => Post<Empty>("api/setup/initialize", EmptyRequest);

        #endregion

        #region Auth

        public Task<(DataApiResponse<AuthResult>, HttpResponseMessage)> AuthRegister(UserRegister data)
            => Post<UserRegister, AuthResult>("api/auth/register", data);

        public Task<(DataApiResponse<AuthResult>, HttpResponseMessage)> AuthLogin(UserLogin data)
            => Post<UserLogin, AuthResult>("api/auth/login", data);

        public Task<(DataApiResponse<AuthResult>, HttpResponseMessage)> AuthPromote(UserPromote data)
            => Post<UserPromote, AuthResult>("api/auth/promote", data);

        public Task<DataApiResponse<UserDetails>> AuthMe()
            => Get<UserDetails>("api/auth/me");

        #endregion

        #region Subscriptions

        public Task<(DataApiResponse<SubscriptionValidateResult>, HttpResponseMessage)> SubscriptionValidateUrl(SubscriptionValidate data)
            => Post<SubscriptionValidate, SubscriptionValidateResult>("api/subscription/validate", data);

        public Task<(DataApiResponse<SubscriptionResponse>, HttpResponseMessage)> SubscriptionCreate(SubscriptionCreate data)
            => Post<SubscriptionCreate, SubscriptionResponse>("api/subscription/create", data);

        public Task<DataApiResponse<SubscriptionResponse[]>> SubscriptionList(SubscriptionList data)
            => Get<SubscriptionList, SubscriptionResponse[]>("api/subscription/list", data);

        #endregion
    }
}
