using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RegardBackend.Common.API.Request
{
    public class UserLogin
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Username is required!")]
        public string Username { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Password is required!")]
        public string Password { get; set; }

        public bool RememberMe { get; set; } = false;
    }
}
