using System.ComponentModel.DataAnnotations;

namespace Regard.Common.API.Auth
{
    public class ForgotPasswordRequest
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required!")]
        public string Username { get; set; }
    }
}
