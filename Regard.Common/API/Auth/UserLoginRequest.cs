using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Common.API.Auth
{
    public class UserLoginRequest
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required!")]
        public string Username { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Password is required!")]
        public string Password { get; set; }

        public bool RememberMe { get; set; } = false;
    }
}
