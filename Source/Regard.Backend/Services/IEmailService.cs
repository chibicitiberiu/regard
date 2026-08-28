using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Sends outgoing mail (currently just the password-reset link) via the configured SMTP server.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// True when an SMTP host is configured. When false, callers should fall back to another
        /// delivery channel (e.g. writing the reset link to the server log).
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Sends a plain-text email. Throws if SMTP is not configured or the send fails.
        /// </summary>
        Task SendAsync(string toEmail, string subject, string body);
    }
}
