using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Response
{
    public class AuthResult
    {
        public string Token { get; set; }

        public DateTime? ValidTo { get; set; }
    }
}
