using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Api
{
    public class CheckServerConfigResponse
    {
        public string[] Errors { get; set; }
        public string[] Warnings { get; set; }
    }
}
