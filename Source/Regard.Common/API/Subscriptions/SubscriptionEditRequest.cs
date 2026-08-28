using Regard.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionEditRequest
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int? ParentFolderId { get; set; }

        public bool? AutoDownload { get; set; }

        public int? DownloadMaxCount { get; set; }

        public VideoOrder? DownloadOrder { get; set; }

        public bool? MarkDeletedAsWatched { get; set; }

        public bool? DeleteWatched { get; set; }

        public string DownloadPath { get; set; }

        public bool? WriteSubtitles { get; set; }

        public bool? WriteAutoSub { get; set; }

        public bool? AllSubs { get; set; }

        public string SubFormat { get; set; }

        public string SubLang { get; set; }

        public List<Regard.Common.API.Model.ApiSubscriptionFilter> Filters { get; set; } = new();
    }
}
