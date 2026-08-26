using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Security.Cryptography;

namespace Regard.Backend.Configuration
{
    /// <summary>
    /// Holds the resolved JWT signing secret used by both the token validator (Startup) and the
    /// token signer (AuthController). The value comes from configuration (`JWT:Secret` /
    /// `JWT__Secret`); when that is unset or still the insecure default that historically shipped
    /// in appsettings.json, a strong random secret is generated once and persisted under
    /// DataDirectory so it stays stable across restarts.
    /// </summary>
    public class JwtSecretProvider
    {
        // The insecure default that shipped in the repo's appsettings.json. It must never sign tokens.
        private const string ShippedDefault = "ThisIsMySecretuiq34yt089htdlkrgnsope4ht;dgnpo54uin";

        public string Value { get; }

        public JwtSecretProvider(string value)
        {
            Value = value;
        }

        /// <summary>Resolves the secret from configuration, or generates + persists one under DataDirectory.</summary>
        public static string Resolve(IConfiguration configuration)
        {
            var configured = configuration["JWT:Secret"];
            if (!string.IsNullOrWhiteSpace(configured) && configured != ShippedDefault)
                return configured;

            var dataDir = configuration["DataDirectory"] ?? "Data";
            var path = Path.Combine(dataDir, "jwt-secret");

            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(existing))
                    return existing;
            }

            var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(path, secret);
            Console.Error.WriteLine($"[Regard] No JWT secret configured; generated a new one and saved it to {path}.");
            return secret;
        }
    }
}
