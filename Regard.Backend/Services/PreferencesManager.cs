using Regard.Backend.Model;
using RegardBackend.DB;
using RegardBackend.Model;
using System.Text.Json;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class PreferencesManager
    {
        private readonly DataContext dataContext;
        private readonly PreferencesCache cache;

        public PreferencesManager(DataContext dataContext, PreferencesCache cache)
        {
            this.dataContext = dataContext;
            this.cache = cache;
        }

        public async Task<TValue> Get<TValue>(PreferenceDefinition<TValue> pref, UserAccount user = null)
        {
            TValue value;

            // Obtain user setting
            if ((pref.Flags & PreferenceFlags.User) != 0 && user != null)
            {
                if (cache.CacheGet(pref.Key, out value, user))
                    return value;

                var dbPref = await dataContext.UserPreferences.FindAsync(pref.Key, user);
                if (dbPref != null)
                {
                    value = JsonSerializer.Deserialize<TValue>(dbPref.Value);
                    cache.CacheSet(pref.Key, value, user);
                    return value;
                }
            }

            // Obtain global setting - try from cache
            if (cache.CacheGet(pref.Key, out value))
                return value;

            // Look in database
            var dbGlobalPref = await dataContext.Preferences.FindAsync(pref.Key);
            if (dbGlobalPref != null)
            {
                value = JsonSerializer.Deserialize<TValue>(dbGlobalPref.Value);
                cache.CacheSet(pref.Key, value);
                return value;
            }

            // Return default value
            return pref.DefaultValue;
        }

        public async Task Set<TValue>(PreferenceDefinition<TValue> pref, TValue value, UserAccount user = null)
        {
            // Do not store user preference for global-only preferences
            if ((pref.Flags & PreferenceFlags.User) == 0)
                user = null;

            cache.CacheSet(pref.Key, value, user);
            
            IPreference dbPref;
            if (user == null)
            {
                dbPref = await dataContext.Preferences.FindAsync(pref.Key);
                if (dbPref == null)
                {
                    dbPref = new Preference() { Key = pref.Key };
                    dataContext.Preferences.Add((Preference)dbPref);
                }
            }
            else
            {
                dbPref = await dataContext.UserPreferences.FindAsync(pref.Key, user);
                if (dbPref == null)
                {
                    dbPref = new UserPreference() { Key = pref.Key, User = user };
                    dataContext.UserPreferences.Add((UserPreference)dbPref);
                }
            }

            dbPref.Value = JsonSerializer.Serialize<TValue>(value);
            await dataContext.SaveChangesAsync();
        }
    }
}
