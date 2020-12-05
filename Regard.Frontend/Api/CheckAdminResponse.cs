using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Api
{
    public class CheckAdminResponse
    {
        public bool HaveUsers { get; set; }
        public bool HaveAdmin { get; set; }
    }
}
