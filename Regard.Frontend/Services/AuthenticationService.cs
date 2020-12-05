using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Regard.Common.API.Response;
using RegardBackend.Common.API.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Regard.Services
{
    public class AuthenticationService
    {
        public static readonly string StorageAuthTokenKey = "authToken";
        public static readonly string StorageAuthUsernameKey = "authUserName";

        private readonly BackendService backend;
        private readonly ILocalStorageService localStorage;
        private readonly AuthenticationStateProvider authStateProvider;


        public AuthenticationService(BackendService backend, ILocalStorageService localStorage, AuthenticationStateProvider authStateProvider)
        {
            this.backend = backend;
            this.localStorage = localStorage;
            this.authStateProvider = authStateProvider;
        }

        private async Task UpdateToken(DataApiResponse<AuthResult> result, HttpResponseMessage httpResponse, string username = null)
        {
            if (httpResponse.IsSuccessStatusCode)
            {
                await localStorage.SetItemAsync(StorageAuthTokenKey, result.Data.Token);
                if (username != null)
                    await localStorage.SetItemAsync(StorageAuthUsernameKey, username);

                if (authStateProvider is ApiAuthenticationStateProvider apiAuthenticationStateProvider)
                    apiAuthenticationStateProvider.UpdateAuthenticationState();
            }
        }

        public async Task<(DataApiResponse<AuthResult>, HttpResponseMessage)> Register(UserRegister register)
        {
            var (result, httpResponse) = await backend.AuthRegister(register);
            await UpdateToken(result, httpResponse, register.Username);
            return (result, httpResponse);
        }


        public async Task<(DataApiResponse<AuthResult>, HttpResponseMessage)> Login(UserLogin login)
        {
            var (result, httpResponse) = await backend.AuthLogin(login);
            await UpdateToken(result, httpResponse, login.Username);
            return (result, httpResponse);
        }

        public async Task<(DataApiResponse<AuthResult>, HttpResponseMessage)> Promote(UserPromote promote)
        {
            var (result, httpResponse) = await backend.AuthPromote(promote);
            await UpdateToken(result, httpResponse);
            return (result, httpResponse);
        }

        public async Task Logout()
        {
            await localStorage.RemoveItemAsync(StorageAuthTokenKey);
        }

        public async Task<string> GetToken() => await localStorage.GetItemAsync<string>(StorageAuthTokenKey);

        public async Task<string> GetUsername() => await localStorage.GetItemAsync<string>(StorageAuthUsernameKey);
    }
}
