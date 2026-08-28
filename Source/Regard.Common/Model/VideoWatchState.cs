namespace Regard.Model
{
    /// <summary>
    /// Watch-state filter for the video list. "Started" means a resume position was saved but the video
    /// isn't finished; "Unwatched" is a fresh video that was never started.
    /// </summary>
    public enum VideoWatchState
    {
        All,
        Unwatched,
        Started,
        Watched,
    }
}
