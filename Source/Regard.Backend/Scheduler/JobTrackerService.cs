using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class JobInfo
    {
        public int Id { get; set; }

        public string UserId { get; set; }

        public string Name { get; set; }
    }

    public class JobTrackerService
    {
        private readonly Dictionary<int, JobInfo> jobs = new Dictionary<int, JobInfo>();
        private int nextId = 0;

        public int CreateJob(string name, string userName)
        {
            jobs.Add(nextId, new JobInfo()
            {

            });
            return nextId++;
        }
    }
}
