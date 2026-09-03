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
        // volatile: the writer is whatever thread calls Stop(), the reader is the message thread's
        // loop condition. Without it the JIT is free to hoist the read out of the loop.
        private volatile bool stop = false;

        public event EventHandler<Message> MessageCreated;

        public UserLogger(IServiceScopeFactory scopeFactory)
        {
            this.scopeFactory = scopeFactory;
            this.messageThread = new Thread(RunMessageThread)
            {
                // Background, so this thread can never be the reason the process refuses to exit.
                // It was a foreground thread, which meant a startup failure -- Program.Main logging a
                // fatal exception and returning -- left the process hanging forever instead of
                // exiting. Nothing here is worth blocking shutdown for: an undelivered user-facing
                // message is less important than the process actually stopping.
                IsBackground = true,
                Name = "UserLogger",
            };
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
                    try
                    {
                        dataContext.Add(message);
                        dataContext.SaveChanges();
                        MessageCreated?.Invoke(this, message);
                    }
                    catch (Exception ex)
                    {
                        // Never let a logging failure crash the process.
                        Console.Error.WriteLine($"UserLogger: failed to persist message: {ex.Message}");
                        dataContext.ChangeTracker.Clear();
                    }
                }
                else
                {
                    lock (@lock)
                    {
                        // Timed wait rather than an indefinite one: Stop() sets the flag and pulses,
                        // but if the pulse is missed (set between the queue check and taking the lock)
                        // an untimed wait would park here forever and Join() below would never return.
                        Monitor.Wait(@lock, TimeSpan.FromSeconds(1));
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

            // Wake the thread if it is parked, so it notices the flag rather than waiting out its
            // timeout.
            lock (@lock)
            {
                Monitor.PulseAll(@lock);
            }

            // Bounded: a logging thread must not be able to hold up shutdown. If it is wedged on a
            // database write we abandon it -- it is a background thread, so the process still exits.
            if (waitForExit)
                messageThread.Join(TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
