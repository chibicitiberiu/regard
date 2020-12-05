using System;

namespace Regard.Backend.Services
{
    [Flags]
    public enum PreferenceFlags
    {
        User
    }

    public class PreferenceDefinition<TValue>
    {
        public string Key { get; set; }
        public TValue DefaultValue { get; set; }
        public PreferenceFlags Flags { get; set; }

        public PreferenceDefinition(string key, TValue defaultValue, PreferenceFlags flags = 0)
        {
            Key = key;
            DefaultValue = defaultValue;
            Flags = flags;
        }
    }
}
