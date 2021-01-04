using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Services
{
    public class PreferencesCache<TKey> : IPreferencesCache<TKey>
    {
        struct CacheEntry
        {
            public object Value;
            public DateTime Timestamp;
        }

        private readonly Dictionary<TKey, CacheEntry> cache = new Dictionary<TKey, CacheEntry>();
        private const int CacheExpirationSeconds = 3600 * 24;

        public bool Get<TValue>(TKey key, out TValue value)
        {
            if (cache.TryGetValue(key, out CacheEntry entry))
            {
                if (entry.Timestamp + TimeSpan.FromSeconds(CacheExpirationSeconds) > DateTime.Now)
                {
                    value = (TValue)entry.Value;
                    return true;
                }

                // cache expired
                cache.Remove(key);
            }

            value = default;
            return false;
        }

        public void Set<TValue>(TKey key, TValue value)
        {
            cache[key] = new CacheEntry()
            {
                Timestamp = DateTime.Now,
                Value = value
            };
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

        public void Invalidate()
        {
            cache.Clear();
        }
    }
}
