using System;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Thrown by <see cref="SubscriptionManager.Create"/> when the resolved subscription points to a
    /// channel/playlist the user is already subscribed to and duplicates weren't explicitly allowed.
    /// The controller turns this into a 409 so the UI can warn and offer "create anyway".
    /// </summary>
    public class DuplicateSubscriptionException : Exception
    {
        /// <summary>Name of the existing subscription the new one would duplicate.</summary>
        public string ExistingName { get; }

        public DuplicateSubscriptionException(string existingName)
            : base($"You're already subscribed to \"{existingName}\". Create another anyway?")
        {
            ExistingName = existingName;
        }
    }
}
