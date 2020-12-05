using System.ComponentModel.DataAnnotations;

namespace RegardBackend.Common.API.Request
{
    public class UserRegister
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required!")]
        [StringLength(150, MinimumLength = 4)]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required!")]
        [StringLength(250, MinimumLength = 8, ErrorMessage = "Password must have at least 8 characters!")]
        public string Password1 { get; set; }

        [Required(ErrorMessage = "Password verification is required!")]
        [Compare("Password1", ErrorMessage = "Passwords do not match!")]
        public string Password2 { get; set; }

        [StringLength(150)]
        public string FirstName { get; set; }

        [StringLength(150)]
        public string LastName { get; set; }

        [EmailAddress]
        public string Email { get; set; }
    }
}
