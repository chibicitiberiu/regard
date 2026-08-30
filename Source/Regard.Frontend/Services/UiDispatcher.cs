using System;
using System.Threading.Tasks;

namespace Regard.Frontend.Services
{
    /// <summary>
    /// Marshals SignalR callbacks onto Blazor's render context.
    ///
    /// Hub callbacks arrive on a background context, and mutating UI state there means the resulting
    /// StateHasChanged doesn't actually repaint. Some consumers wrapped their handler in InvokeAsync and
    /// some didn't, which is why parts of the app updated live and parts didn't. Doing it once here, at
    /// the point where MessagingService raises its events, fixes every consumer at the same time —
    /// including the indirect ones (a push mutates AppState's dictionary, which synchronously drives the
    /// subscription tree's handlers, so those could never be fixed downstream).
    ///
    /// ComponentBase.InvokeAsync is protected, so a root component hands it over via <see cref="Attach"/>.
    /// </summary>
    public class UiDispatcher
    {
        private Func<Func<Task>, Task> invoker;

        /// <summary>
        /// Called by the root component (App) once rendering has started. Deliberately the root and not a
        /// layout: messaging connects before a layout first renders, and a captured InvokeAsync from a
        /// component that is later disposed would throw on every subsequent push.
        /// </summary>
        public void Attach(Func<Func<Task>, Task> invoke) => invoker = invoke;

        public void Post(Action action)
        {
            var invoke = invoker;
            if (invoke == null)
            {
                // Not rendering yet (a push during startup): run inline rather than dropping it.
                action();
                return;
            }

            try
            {
                _ = invoke(() =>
                {
                    action();
                    return Task.CompletedTask;
                });
            }
            catch (ObjectDisposedException)
            {
                // The renderer went away (navigation/teardown); the update is irrelevant now.
            }
        }
    }
}
