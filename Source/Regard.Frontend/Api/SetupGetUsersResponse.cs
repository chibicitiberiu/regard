using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Api
{
    public class SetupGetUsersResponse
    {
        public class User
        {
            public string Username { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }

        public User[] Users { get; set; }
    }
}
