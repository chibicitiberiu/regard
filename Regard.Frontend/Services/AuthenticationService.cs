using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Regard.Common.API;
using Regard.Common.API.Auth;
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

        private readonly IServiceProvider serviceProvider;

        public event EventHandler AuthenticationStateChanged;

        public AuthenticationService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        private async Task UpdateToken(IServiceScope scope, ApiResponse<AuthResponse> result, HttpResponseMessage httpResponse, string username = null)
        {
            var localStorage = scope.ServiceProvider.GetRequiredService<ILocalStorageService>();
            var authStateProvider = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();

            if (httpResponse.IsSuccessStatusCode)
            {
                await localStorage.SetItemAsync(StorageAuthTokenKey, result.Data.Token);
                if (username != null)
                    await localStorage.SetItemAsync(StorageAuthUsernameKey, username);

                if (authStateProvider is ApiAuthenticationStateProvider apiAuthenticationStateProvider)
                    apiAuthenticationStateProvider.UpdateAuthenticationState();
                
                AuthenticationStateChanged?.Invoke(this, new EventArgs());
            }
        }

        public async Task<(ApiResponse<AuthResponse>, HttpResponseMessage)> Register(UserRegisterRequest register)
        {
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();
            var (result, httpResponse) = await backend.AuthRegister(register);

            await UpdateToken(scope, result, httpResponse, register.Username);
            return (result, httpResponse);
        }

        public async Task<(ApiResponse<AuthResponse>, HttpResponseMessage)> Login(UserLoginRequest login)
        {
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();
            var (result, httpResponse) = await backend.AuthLogin(login);

            await UpdateToken(scope, result, httpResponse, login.Username);
            return (result, httpResponse);
        }

        public async Task<(ApiResponse<AuthResponse>, HttpResponseMessage)> Promote(UserPromoteRequest promote)
        {
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();
            var (result, httpResponse) = await backend.AuthPromote(promote);

            await UpdateToken(scope, result, httpResponse);
            return (result, httpResponse);
        }

        public async Task Logout()
        {
            using var scope = serviceProvider.CreateScope();
            var localStorage = scope.ServiceProvider.GetRequiredService<ILocalStorageService>();
            var authStateProvider = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();

            await localStorage.RemoveItemAsync(StorageAuthTokenKey);
            if (authStateProvider is ApiAuthenticationStateProvider apiAuthenticationStateProvider)
                apiAuthenticationStateProvider.UpdateAuthenticationState();

            AuthenticationStateChanged?.Invoke(this, new EventArgs());
        }

        public async Task<string> GetToken()
        {
            using var scope = serviceProvider.CreateScope();
            var localStorage = scope.ServiceProvider.GetRequiredService<ILocalStorageService>();

            return await localStorage.GetItemAsync<string>(StorageAuthTokenKey);
        }

        public async Task<string> GetUsername()
        {
            using var scope = serviceProvider.CreateScope();
            var localStorage = scope.ServiceProvider.GetRequiredService<ILocalStorageService>();

            return await localStorage.GetItemAsync<string>(StorageAuthUsernameKey);
        }
    }
}
