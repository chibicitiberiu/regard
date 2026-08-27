namespace Regard.Common.API.Admin
{
    /// <summary>Grant or revoke the admin role for a user.</summary>
    public class SetUserRoleRequest
    {
        public string UserId { get; set; }

        /// <summary>True to make the user an admin, false to demote to a regular user.</summary>
        public bool IsAdmin { get; set; }
    }
}
