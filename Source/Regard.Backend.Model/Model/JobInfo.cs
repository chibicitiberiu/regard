using Newtonsoft.Json;
using Regard.Backend.Common.Utils;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Regard.Backend.Common.Model
{
    public enum JobState
    {
        Created,
        Scheduled,
        Running,
        Completed,
        Failed
    }

    public class JobInfo
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        #region Job metadata

        public UserAccount User { get; set; }

        public string UserId { get; set; }

        public string Name { get; set; }

        public bool TrackWhenScheduled { get; set; } = false;

        #endregion

        #region Execution parameters

        [NotMapped]
        public Dictionary<string, object> JobData { get; set; } = new Dictionary<string, object>();

        public string JobDataJson
        {
            get => JsonUtils.Serialize(JobData);
            set => JobData = JsonUtils.Deserialize<Dictionary<string, object>>(value);
        }

        public int RetryCount { get; set; }

        public int RetryInterval { get; set; }

        #endregion

        #region Current state

        public JobState State { get; set; } = JobState.Created;

        public DateTimeOffset Created { get; set; }

        public DateTimeOffset? Started { get; set; }

        public DateTimeOffset? Completed { get; set; }

        public DateTimeOffset? NextRun { get; set; }

        [NotMapped]
        public float? Progress { get; set; }
        public string Key { get; set; }

        #endregion
    }
}
