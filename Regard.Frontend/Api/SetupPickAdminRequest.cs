using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Api
{
    public class SetupPickAdminRequest
    {
        [Required]
        public string UserName;
    }
}
