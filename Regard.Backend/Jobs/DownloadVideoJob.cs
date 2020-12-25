using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    public class DownloadVideoJob : JobBase
    {
        protected override int RetryCount => 0;

        protected override TimeSpan RetryInterval => TimeSpan.Zero;

        public DownloadVideoJob(ILogger<DownloadVideoJob> logger, DataContext dataContext) : base(logger, dataContext)
        {
        }

        protected override Task ExecuteJob(IJobExecutionContext context)
        {
            throw new NotImplementedException();
        }
    }
}
