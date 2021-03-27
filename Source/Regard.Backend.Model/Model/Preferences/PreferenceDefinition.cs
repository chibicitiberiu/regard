using System;

namespace Regard.Backend.Services
{
    [Flags]
    public enum PreferenceFlags
    {
        User,
        SubscriptionFolder,
        Subscription,
    }

    public class PreferenceDefinition<TValue>
    {
        /// <summary>
        /// Key which is used in database
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Key in appsettings.json file, optional
        /// </summary>
        public string ConfigurationKey { get; set; }

        /// <summary>
        /// Environment variable, optional
        /// </summary>
        public string EnvironmentKey { get; set; }

        /// <summary>
        /// Default value if not present
        /// </summary>
        public TValue DefaultValue { get; set; }
        
        /// <summary>
        /// Flags
        /// </summary>
        public PreferenceFlags Flags { get; set; }

        public PreferenceDefinition(TValue defaultValue, string key, string configurationKey, string environmentKey, PreferenceFlags flags = 0)
        {
            Key = key;
            DefaultValue = defaultValue;
            Flags = flags;
        }
    }
}
