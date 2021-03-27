using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Api
{
    public class RegisterRequest
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required!")]
        [StringLength(150, MinimumLength = 4)]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required!")]
        [StringLength(250, MinimumLength = 8, ErrorMessage = "Password must have at least 8 characters!")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Password verification is required!")]
        [Compare("Password", ErrorMessage = "Passwords do not match!")]
        public string PasswordConfirm { get; set; }

        [StringLength(150)]
        public string FirstName { get; set; }

        [StringLength(150)]
        public string LastName { get; set; }

        [EmailAddress]
        public string Email { get; set; }
    }
}
