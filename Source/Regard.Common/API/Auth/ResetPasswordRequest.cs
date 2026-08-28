using System.ComponentModel.DataAnnotations;

namespace Regard.Common.API.Auth
{
    public class ResetPasswordRequest
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required!")]
        public string Username { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Reset token is required!")]
        public string Token { get; set; }

        [Required(ErrorMessage = "Password is required!")]
        [StringLength(250, MinimumLength = 8, ErrorMessage = "Password must have at least 8 characters!")]
        public string Password1 { get; set; }

        [Required(ErrorMessage = "Password verification is required!")]
        [Compare("Password1", ErrorMessage = "Passwords do not match!")]
        public string Password2 { get; set; }
    }
}
