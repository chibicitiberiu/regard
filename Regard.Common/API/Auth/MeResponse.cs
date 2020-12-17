using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Auth
{
    public class MeResponse
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
