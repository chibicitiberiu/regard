using Regard.Model;

namespace Regard.Backend.Services
{
    public static class Preferences
    {
        // Server basics
        public static readonly PreferenceDefinition<bool> Server_Initialized = new PreferenceDefinition<bool>("server.initialized",
                                                                                                              false);

        public static readonly PreferenceDefinition<bool> Server_AllowRegistrations = new PreferenceDefinition<bool>("server.allow_registrations",
                                                                                                                     true);

        public static readonly PreferenceDefinition<bool> Server_Debug = new PreferenceDefinition<bool>("server.debug",
                                                                                                        false);

        // Download manager
        public static readonly PreferenceDefinition<bool> Download_AutoDownload = new PreferenceDefinition<bool>("download.auto_download",
                                                                                                                 true,
                                                                                                                 PreferenceFlags.User);


        public static readonly PreferenceDefinition<VideoOrder> Download_Order = new PreferenceDefinition<VideoOrder>("download.order",
                                                                                                                      VideoOrder.Newest,
                                                                                                                      PreferenceFlags.User);

        public static readonly PreferenceDefinition<int> Download_DefaultMaxCount = new PreferenceDefinition<int>("download.default_max_count",
                                                                                                                  5,
                                                                                                                  PreferenceFlags.User);

        public static readonly PreferenceDefinition<int> Download_DefaultMaxSize = new PreferenceDefinition<int>("download.default_max_size",
                                                                                                                 -1,
                                                                                                                 PreferenceFlags.User);

        public static readonly PreferenceDefinition<int> Download_GlobalMaxCount = new PreferenceDefinition<int>("download.global_max_count",
                                                                                                                 5,
                                                                                                                 PreferenceFlags.User);

        public static readonly PreferenceDefinition<int> Download_GlobalMaxSize = new PreferenceDefinition<int>("download.global_max_size",
                                                                                                                 -1,
                                                                                                                 PreferenceFlags.User);

        public static readonly PreferenceDefinition<bool> Videos_MarkDeletedAsWatched = new PreferenceDefinition<bool>("videos.mark_deleted_as_watched",
                                                                                                                       true,
                                                                                                                       PreferenceFlags.User);
    }
}
