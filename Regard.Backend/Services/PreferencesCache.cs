using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Services
{
    public class PreferencesCache : IPreferencesCache
    {
        struct CacheEntry
        {
            public object Value;
            public DateTime Timestamp;
        }

        private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
        private const int CacheExpirationSeconds = 3600 * 24;

        private string CacheKey(string key, UserAccount user = null)
        {
            if (user != null)
                return $"{key}.{user.Id}";
            return key;
        }

        public bool Get<TValue>(string key, out TValue value, UserAccount user = null)
        {
            string cacheKey = CacheKey(key, user);
            if (cache.TryGetValue(CacheKey(key, user), out CacheEntry entry))
            {
                if (entry.Timestamp + TimeSpan.FromSeconds(CacheExpirationSeconds) > DateTime.Now)
                {
                    value = (TValue)entry.Value;
                    return true;
                }

                // cache expired
                cache.Remove(cacheKey);
            }

            value = default;
            return false;
        }

        public void Set<TValue>(string key, TValue value, UserAccount user = null)
        {
            var cacheEntry = new CacheEntry()
            {
                Timestamp = DateTime.Now,
                Value = value
            };
            cache[CacheKey(key, user)] = cacheEntry;
        }

        public void ClearExpired()
        {
            var expiredKeys = cache
                .Where(x => x.Value.Timestamp + TimeSpan.FromSeconds(CacheExpirationSeconds) < DateTime.Now)
                .Select(x => x.Key)
                .ToArray();

            foreach (var key in expiredKeys)
                cache.Remove(key);
        }
    }
}
