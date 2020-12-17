using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Auth
{
    public class AuthResponse
    {
        public string Token { get; set; }

        public DateTime? ValidTo { get; set; }
    }
}
