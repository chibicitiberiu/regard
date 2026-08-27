using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Services;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Modals
{
    public partial class ImportModal
    {
        private const long MaxFileSize = 5 * 1024 * 1024;

        private Modal modal;
        private string fileInfo;

        [Inject] protected BackendService Backend { get; set; }

        [Inject] protected NotificationsService Notifications { get; set; }

        protected SubscriptionImportRequest Request { get; set; } = new SubscriptionImportRequest();

        protected bool Submitting { get; set; }

        protected string ErrorMessage { get; set; }

        protected bool SubmitEnabled => !Submitting;

        private void Reset()
        {
            Request = new SubscriptionImportRequest();
            Submitting = false;
            ErrorMessage = null;
            fileInfo = null;
        }

        // Read the chosen OPML/XML file's text client-side into the Content box (no server multipart).
        private async Task OnFile(InputFileChangeEventArgs e)
        {
            try
            {
                var file = e.File;
                if (file == null)
                    return;

                using var reader = new StreamReader(file.OpenReadStream(MaxFileSize));
                Request.Content = await reader.ReadToEndAsync();
                fileInfo = $"{file.Name} ({Request.Content.Length} chars loaded)";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Could not read the file: " + ex.Message;
            }
            StateHasChanged();
        }

        private async Task OnSubmit()
        {
            if (string.IsNullOrWhiteSpace(Request.Content))
            {
                ErrorMessage = "Paste some URLs or choose an OPML file first.";
                return;
            }

            Submitting = true;
            ErrorMessage = null;
            StateHasChanged();

            try
            {
                var (resp, httpResp) = await Backend.SubscriptionImport(Request);
                if (httpResp.IsSuccessStatusCode)
                {
                    int count = resp?.Data?.Count ?? 0;
                    Notifications.ShowInfo($"Importing {count} subscription(s)… watch the bell for progress.");
                    await Dismiss();
                    return;
                }

                ErrorMessage = string.IsNullOrWhiteSpace(resp?.Message)
                    ? "Import failed. Check the input and try again."
                    : resp.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Import failed: " + ex.Message;
            }
            finally
            {
                Submitting = false;
                StateHasChanged();
            }
        }

        public async Task Show()
        {
            Reset();
            await modal.Show();
        }

        public async Task Dismiss()
        {
            await modal.Close();
        }
    }
}
