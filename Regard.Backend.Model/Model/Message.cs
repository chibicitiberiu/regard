using Regard.Backend.Model;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Regard.Backend.Common.Model
{
    public enum MessageSeverity
    {
        Info,
        Notice,
        Warning,
        Error
    }

    public class Message
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        [NotNull]
        public string Content { get; set; }

        public string Details { get; set; }

        public MessageSeverity Severity { get; set; }

        public UserAccount User { get; set; }

        public string UserId { get; set; }
    }
}
