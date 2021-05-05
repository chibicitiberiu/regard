using System;

namespace Regard.Backend.Configuration
{
    [Flags]
    public enum OptionFlags
    {
        User,
        SubscriptionFolder,
        Subscription,
    }

    public class OptionDefinition<TValue>
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
        public OptionFlags Flags { get; set; }

        public OptionDefinition(TValue defaultValue, string key, string configurationKey, string environmentKey, OptionFlags flags = 0)
        {
            Key = key;
            DefaultValue = defaultValue;
            Flags = flags;
        }
    }
}
