using Microsoft.Extensions.DependencyInjection;
using Regard.Backend.Common.Model;
using Regard.Backend.DB;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Regard.Backend.Logging
{
    /// <summary>
    /// The user logger is not meant to replace the logger, it should be used to log messages that will be visible to 
    /// users. The messages should be targeted to someone who doesn't have knowledge of how the system works.
    /// The normal log is meant to be used by developers or power users to debug issues.
    /// </summary>
    public class UserLogger : IDisposable
    {
        private readonly IServiceScopeFactory scopeFactory;

        private readonly ConcurrentQueue<Message> messageQueue = new();
        private readonly Thread messageThread;
        private readonly object @lock = new object();
        private bool stop = false;

        public event EventHandler<Message> MessageCreated;

        public UserLogger(IServiceScopeFactory scopeFactory)
        {
            this.scopeFactory = scopeFactory;
            this.messageThread = new Thread(RunMessageThread);
            messageThread.Start();
        }

        private void RunMessageThread()
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            while (!stop)
            {
                if (messageQueue.TryDequeue(out Message message))
                {
                    dataContext.Add(message);
                    dataContext.SaveChanges();
                    MessageCreated?.Invoke(this, message);
                }
                else
                {
                    lock (@lock)
                    {
                        Monitor.Wait(@lock);
                    }
                }
            }
        }

        private void LogMessage(string message,
                                string extraDetails,
                                MessageSeverity severity,
                                string userId = null,
                                long? jobId = null)
        {
            messageQueue.Enqueue(new Message()
            {
                Timestamp = DateTimeOffset.UtcNow,
                Content = message,
                Details = extraDetails,
                Severity = severity,
                UserId = userId,
                JobId = jobId
            });

            lock (@lock)
            {
                Monitor.Pulse(@lock);
            }
        }

        public void LogInfo(string message,
                            string extraDetails = null,
                            string userId = null,
                            long? jobId = null)
        {
            LogMessage(message, extraDetails, MessageSeverity.Info, userId, jobId);
        }

        public void LogWarning(string message,
                               string extraDetails = null,
                               string userId = null,
                               long? jobId = null)
        {
            LogMessage(message, extraDetails, MessageSeverity.Warning, userId, jobId);
        }

        public void LogError(string message,
                             string extraDetails = null,
                             string userId = null,
                             long? jobId = null)
        {
            LogMessage(message, extraDetails, MessageSeverity.Error, userId, jobId);
        }

        private void Stop(bool waitForExit = true)
        {
            stop = true;

            if (waitForExit)
                messageThread.Join();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
