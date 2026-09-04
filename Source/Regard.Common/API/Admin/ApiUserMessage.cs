using System;

namespace Regard.Common.API.Admin
{
    /// <summary>
    /// A row from the Messages table — the user-facing counterpart to the developer log. UserLogger's
    /// own description: "targeted to someone who doesn't have knowledge of how the system works. The
    /// normal log is meant to be used by developers or power users."
    ///
    /// Named ApiUserMessage rather than ApiMessage because that name is already taken by the SignalR
    /// notification payload, which is unrelated.
    /// </summary>
    public class ApiUserMessage
    {
        public int Id { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        /// <summary>The one-line summary, e.g. "Fetch thumbnails: cancelled".</summary>
        public string Content { get; set; }

        /// <summary>Longer explanation when there was one. Collapsed in the UI.</summary>
        public string Details { get; set; }

        /// <summary>0 = Info, 1 = Warning, 2 = Error (matches MessageSeverity).</summary>
        public int Severity { get; set; }

        /// <summary>The job this came from, if any. Messages cascade away when their job is pruned.</summary>
        public long? JobId { get; set; }

        /// <summary>Name of that job, resolved for display so the row means something on its own.</summary>
        public string JobName { get; set; }

        /// <summary>Account the message was addressed to; null for system-wide ones.</summary>
        public string UserName { get; set; }
    }

    public class ApiUserMessagePage
    {
        public ApiUserMessage[] Messages { get; set; } = Array.Empty<ApiUserMessage>();
        public int TotalCount { get; set; }
    }
}
