using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Model
{
    public class ApiJobInfo
    {
        /// <summary>
        /// Name of job
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Detail (i.e. current step)
        /// </summary>
        public string Detail { get; set; }

        /// <summary>
        /// Value between 0 and 1 indicating progress
        /// </summary>
        public float Progress { get; set; }
    }
}
