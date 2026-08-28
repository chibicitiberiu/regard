using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using Regard.Backend.Configuration;
using System;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    /// <summary>
    /// MailKit-backed <see cref="IEmailService"/>. All settings come from the global Server_Smtp*
    /// options (appsettings "Smtp:*" / REGARD_SMTP_* env / DB override), read fresh on each send so a
    /// config change takes effect without a restart.
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IOptionManager optionManager;
        private readonly ILogger<EmailService> logger;

        public EmailService(IOptionManager optionManager, ILogger<EmailService> logger)
        {
            this.optionManager = optionManager;
            this.logger = logger;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(optionManager.GetGlobal(Options.Server_SmtpHost));

        public async Task SendAsync(string toEmail, string subject, string body)
        {
            var host = optionManager.GetGlobal(Options.Server_SmtpHost);
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("SMTP is not configured (Server_SmtpHost is empty).");

            var port = optionManager.GetGlobal(Options.Server_SmtpPort);
            var user = optionManager.GetGlobal(Options.Server_SmtpUser);
            var password = optionManager.GetGlobal(Options.Server_SmtpPassword);
            var from = optionManager.GetGlobal(Options.Server_SmtpFrom);
            var useSsl = optionManager.GetGlobal(Options.Server_SmtpUseSsl);

            var fromAddress = FirstNonEmpty(from, user, "no-reply@regard.local");

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(fromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            // Auto picks implicit TLS for 465 and STARTTLS otherwise; None for a plaintext local relay.
            var socketOptions = useSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, socketOptions);

            if (!string.IsNullOrWhiteSpace(user))
                await client.AuthenticateAsync(user, password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Sent email to {To} via {Host}:{Port}", toEmail, host, port);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            return null;
        }
    }
}
