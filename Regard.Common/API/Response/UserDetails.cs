using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API
{
    public class UserDetails
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
