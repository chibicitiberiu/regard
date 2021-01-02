using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    public class FetchThumbnailsJob : JobBase
    {
        protected override int RetryCount => throw new NotImplementedException();

        protected override TimeSpan RetryInterval => throw new NotImplementedException();

        protected override Task ExecuteJob(IJobExecutionContext context)
        {
            throw new NotImplementedException();
        }

        public FetchThumbnailsJob(ILogger<FetchThumbnailsJob> logger, DataContext dataContext)
            : base(logger, dataContext)
        {
        }
    }
}
