using Regard.Common.API.Model;

namespace Regard.Common.API.Jobs
{
    public class JobListResponse
    {
        public ApiJobInfo[] Jobs { get; set; }

        /// <summary>
        /// Total number of jobs visible to the user (for pagination), independent of skip/take.
        /// </summary>
        public int Total { get; set; }
    }
}
