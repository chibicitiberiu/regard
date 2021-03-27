using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace Regard.Services
{
    public class ApiAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ILocalStorageService localStorage;

        public ApiAuthenticationStateProvider(ILocalStorageService localStorage)
        {
            this.localStorage = localStorage;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            ClaimsIdentity claimsIdentity = new ClaimsIdentity();

            var token = await localStorage.GetItemAsync<string>(AuthenticationService.StorageAuthTokenKey);
            if (!string.IsNullOrWhiteSpace(token))
            {
                var keyValuePairs = ParseJwt(token);

                // Check for expired token
                if (keyValuePairs.TryGetValue("exp", out object expirationObj))
                {
                    var expiration = DateTime.UnixEpoch.AddSeconds(((JsonElement)expirationObj).GetInt64());
                    if (expiration > DateTime.UtcNow)
                    {
                        // Valid token
                        claimsIdentity = new ClaimsIdentity(ParseClaimsFromJwt(keyValuePairs), "jwt");
                    }
                }
            }

            Console.WriteLine("Auth state: " + claimsIdentity.IsAuthenticated);

            return new AuthenticationState(new ClaimsPrincipal(claimsIdentity));
        }

        public void UpdateAuthenticationState()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        private Dictionary<string, object> ParseJwt(string jwt)
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
        }

        private IEnumerable<Claim> ParseClaimsFromJwt(Dictionary<string, object> keyValuePairs)
        {
            var claims = new List<Claim>();

            keyValuePairs.TryGetValue(ClaimTypes.Role, out object roles);

            if (roles != null)
            {
                if (roles.ToString().Trim().StartsWith("["))
                {
                    var parsedRoles = JsonSerializer.Deserialize<string[]>(roles.ToString());

                    foreach (var parsedRole in parsedRoles)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, parsedRole));
                    }
                }
                else
                {
                    claims.Add(new Claim(ClaimTypes.Role, roles.ToString()));
                }

                keyValuePairs.Remove(ClaimTypes.Role);
            }

            claims.AddRange(keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString())));

            return claims;
        }

        private byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}
