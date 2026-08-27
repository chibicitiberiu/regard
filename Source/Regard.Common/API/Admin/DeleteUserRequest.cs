namespace Regard.Common.API.Admin
{
    /// <summary>Delete a user account and all of its data (subscriptions, videos, downloaded files).</summary>
    public class DeleteUserRequest
    {
        public string UserId { get; set; }
    }
}
