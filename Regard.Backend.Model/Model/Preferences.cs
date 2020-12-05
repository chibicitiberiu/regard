namespace Regard.Backend.Services
{
    public static class Preferences
    {
        // Server basics
        public static readonly PreferenceDefinition<bool> Server_Initialized = new PreferenceDefinition<bool>("server.initialized", false);
        public static readonly PreferenceDefinition<bool> Server_AllowRegistrations = new PreferenceDefinition<bool>("server.allow_registrations", true);
        public static readonly PreferenceDefinition<bool> Server_Debug = new PreferenceDefinition<bool>("server.debug", false);

        // Download manager
        public static readonly PreferenceDefinition<bool> Download_AutoDownload = new PreferenceDefinition<bool>("download.auto_download", true, PreferenceFlags.User);
    }
}
