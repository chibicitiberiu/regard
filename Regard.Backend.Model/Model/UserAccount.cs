using Microsoft.AspNetCore.Identity;

namespace RegardBackend.Model
{
    public class UserAccount : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
