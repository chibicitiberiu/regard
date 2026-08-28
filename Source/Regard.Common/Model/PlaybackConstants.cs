namespace Regard.Model
{
    public static class PlaybackConstants
    {
        /// <summary>
        /// A video isn't considered "in progress" (no resume point, no progress bar, not in the "Started"
        /// filter) until at least this many seconds have been watched — a few seconds of accidental
        /// playback shouldn't leave a resume marker. Absolute (not a percentage) on purpose: on a multi-hour
        /// VOD a percentage would ignore many minutes of watch time.
        /// </summary>
        public const int MinInProgressSeconds = 30;

        /// <summary>On resume, start this many seconds before the saved point so the viewer can re-orient.</summary>
        public const int ResumeRewindSeconds = 15;
    }
}
