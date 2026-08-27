namespace Regard.Common.API.Admin
{
    /// <summary>Enable or disable (lock out) a user account.</summary>
    public class SetUserEnabledRequest
    {
        public string UserId { get; set; }

        /// <summary>True to allow login, false to disable the account.</summary>
        public bool Enabled { get; set; }
    }
}
