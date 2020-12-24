using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    public abstract class JobBase : IJob
    {
        protected readonly ILogger log;
        protected readonly DataContext dataContext;

        public JobBase(ILogger log, DataContext dataContext)
        {
            this.log = log;
            this.dataContext = dataContext;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await ExecuteJob(context);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "{0} failed with exception!", GetType().Name);
            }
        }

        protected abstract Task ExecuteJob(IJobExecutionContext context);
    }
}
