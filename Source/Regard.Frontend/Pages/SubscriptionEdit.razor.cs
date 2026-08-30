using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Shared.Controls;
using Regard.Model;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Pages
{
    public partial class SubscriptionEdit
    {
        [Inject] protected BackendService Backend { get; set; }

        [Inject] protected AppState AppState { get; set; }

        [Parameter] public int SubscriptionId { get; set; }

        public ApiSubscription Subscription { get; set; }

        public SubscriptionEditRequest Request { get; set; } = new SubscriptionEditRequest();

        public string ValidationMessage { get; set; }

        public bool SubmitEnabled { get; set; }

        protected Modal previewModal;

        protected List<FilterPreviewItem> PreviewItems { get; set; } = new();

        protected bool PreviewTruncated { get; set; }

        private string DownloadMaxCountStr 
        {
            get => Request.DownloadMaxCount?.ToString() ?? string.Empty;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Request.DownloadMaxCount = null;
                    ValidationMessage = string.Empty;
                }
                else if (int.TryParse(value, out int valueInt))
                {
                    Request.DownloadMaxCount = valueInt;
                    ValidationMessage = string.Empty;
                }
                else
                {
                    ValidationMessage = "Download maximum count must be a number!";
                }
            }
        }

        private string DeleteGracePeriodStr
        {
            get => Request.DeleteGracePeriod?.ToString() ?? string.Empty;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Request.DeleteGracePeriod = null;
                    ValidationMessage = string.Empty;
                }
                else if (int.TryParse(value, out int valueInt) && valueInt >= 0)
                {
                    Request.DeleteGracePeriod = valueInt;
                    ValidationMessage = string.Empty;
                }
                else
                {
                    ValidationMessage = "Delete grace period must be a non-negative number of minutes!";
                }
            }
        }

        // Tri-state string binds for the subtitle bools. The shared RgSimpleInputSelect bool? control
        // renders its "True" option with an empty value attribute (a Blazor boolean-attribute quirk),
        // so selecting it round-trips to null — hence plain "" / "on" / "off" selects here instead.
        private static string BoolToTri(bool? v) => v == null ? "" : (v.Value ? "on" : "off");
        private static bool? TriToBool(string s) => string.IsNullOrEmpty(s) ? (bool?)null : s == "on";

        public string WriteSubtitlesStr
        {
            get => BoolToTri(Request.WriteSubtitles);
            set => Request.WriteSubtitles = TriToBool(value);
        }

        public string WriteAutoSubStr
        {
            get => BoolToTri(Request.WriteAutoSub);
            set => Request.WriteAutoSub = TriToBool(value);
        }

        public string AllSubsStr
        {
            get => BoolToTri(Request.AllSubs);
            set => Request.AllSubs = TriToBool(value);
        }

        public string IncludeShortsStr
        {
            get => BoolToTri(Request.IncludeShorts);
            set => Request.IncludeShorts = TriToBool(value);
        }

        public string IncludeMembersOnlyStr
        {
            get => BoolToTri(Request.IncludeMembersOnly);
            set => Request.IncludeMembersOnly = TriToBool(value);
        }

        // The wire format is a "yyyy-MM-dd" string, but @bind on <input type="date"> only accepts a date
        // type — bind a DateOnly? and project. Cleared field => null => the option is unset on save.
        private static DateOnly? ToDate(string s) =>
            DateOnly.TryParseExact(s, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d : (DateOnly?)null;
        private static string FromDate(DateOnly? d) => d?.ToString(DateFormat, CultureInfo.InvariantCulture);

        private const string DateFormat = "yyyy-MM-dd";

        public DateOnly? PublishedAfterDate
        {
            get => ToDate(Request.PublishedAfter);
            set => Request.PublishedAfter = FromDate(value);
        }

        public DateOnly? PublishedBeforeDate
        {
            get => ToDate(Request.PublishedBefore);
            set => Request.PublishedBefore = FromDate(value);
        }

        protected string PatternPreview => Shared.PatternPreviewHelper.Render(Request.DownloadPath);

        // The subscription's icon URL, absolutized against the backend origin (relative in dev → :5000 → 404
        // otherwise). Mirrors Watch.razor.cs / SubscriptionTree.FixRelativeUrl (no shared helper exists).
        protected string IconPreviewUrl
        {
            get
            {
                var url = Subscription?.ThumbnailUrl;
                if (url == null) return null;
                return url.IsAbsoluteUri ? url.ToString() : new Uri(AppState.BackendBase, url).ToString();
            }
        }

        private async Task OnIconFile(InputFileChangeEventArgs e)
        {
            var file = e.File;
            if (file == null || Subscription == null)
                return;

            try
            {
                using var ms = new MemoryStream();
                await file.OpenReadStream(5 * 1024 * 1024).CopyToAsync(ms);   // raise the 512 KB default cap
                var (resp, httpResp) = await Backend.SubscriptionSetIcon(new ApiSetSubscriptionIconRequest
                {
                    Id = SubscriptionId,
                    IconBase64 = Convert.ToBase64String(ms.ToArray()),
                    FileName = file.Name,
                });

                if (httpResp.IsSuccessStatusCode && resp.Data != null)
                {
                    // Update only the icon; keep Subscription.Config (the returned ToApi has none), so the
                    // "Default (…)" inherit hints stay intact. The nav tree refreshes via the live push.
                    Subscription.ThumbnailUrl = resp.Data.ThumbnailUrl;
                    ValidationMessage = "Icon updated.";
                }
                else
                {
                    ValidationMessage = "Icon upload failed: " + resp?.Message;
                }
            }
            catch (Exception ex)
            {
                ValidationMessage = "Icon upload failed: " + ex.Message;
            }
        }

        // Friendly text for the inherited default shown on the "Default (…)" inherit option. Null until
        // the config loads, in which case the control falls back to plain "(unset)".
        internal static string OrderText(VideoOrder o) => o switch
        {
            VideoOrder.Newest => "Newest first",
            VideoOrder.Oldest => "Oldest first",
            VideoOrder.Playlist => "Playlist order",
            VideoOrder.ReversePlaylist => "Reverse playlist order",
            VideoOrder.Popularity => "Most popular",
            VideoOrder.Rating => "Highest rated",
            VideoOrder.Name => "Name",
            _ => o.ToString(),
        };
        private static string OnOff(bool v) => v ? "On" : "Off";

        protected string AutoDownloadDefaultText => Subscription?.Config is { } c ? OnOff(c.AutoDownloadDefault) : null;
        protected string DownloadOrderDefaultText => Subscription?.Config is { } c ? OrderText(c.DownloadOrderDefault) : null;
        protected string DeleteWatchedDefaultText => Subscription?.Config is { } c ? OnOff(c.DeleteWatchedDefault) : null;
        protected string MarkDeletedAsWatchedDefaultText => Subscription?.Config is { } c ? OnOff(c.MarkDeletedAsWatchedDefault) : null;
        protected string IncludeShortsDefaultText => Subscription?.Config is { } c ? OnOff(c.IncludeShortsDefault) : null;
        protected string IncludeMembersOnlyDefaultText => Subscription?.Config is { } c ? OnOff(c.IncludeMembersOnlyDefault) : null;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            ValidationMessage = "Loading...";

            var (resp, httpResp) = await Backend.SubscriptionList(new SubscriptionListRequest() 
            {
                Ids = new[] { SubscriptionId },
                Parts = ApiSubscription.Parts.Config
            });

            if (httpResp.IsSuccessStatusCode)
            {
                Subscription = resp.Data.Subscriptions.FirstOrDefault();
                if (Subscription != null)
                {
                    Request.Id = SubscriptionId;
                    Request.Name = Subscription.Name;
                    Request.Description = Subscription.Description;
                    Request.ParentFolderId = Subscription.ParentFolderId;
                    Request.AutoDownload = Subscription.Config.AutoDownload;
                    Request.DownloadMaxCount = Subscription.Config.DownloadMaxCount;
                    Request.DownloadOrder = Subscription.Config.DownloadOrder;
                    Request.DeleteWatched = Subscription.Config.DeleteWatched;
                    Request.DeleteGracePeriod = Subscription.Config.DeleteGracePeriod;
                    Request.MarkDeletedAsWatched = Subscription.Config.MarkDeletedAsWatched;
                    Request.DownloadPath = Subscription.Config.DownloadPath;
                    Request.WriteSubtitles = Subscription.Config.WriteSubtitles;
                    Request.WriteAutoSub = Subscription.Config.WriteAutoSub;
                    Request.AllSubs = Subscription.Config.AllSubs;
                    Request.SubFormat = Subscription.Config.SubFormat;
                    Request.SubLang = Subscription.Config.SubLang;
                    Request.SponsorblockActions = Subscription.Config.SponsorblockActions;
                    Request.IncludeShorts = Subscription.Config.IncludeShorts;
                    Request.IncludeMembersOnly = Subscription.Config.IncludeMembersOnly;
                    Request.PublishedAfter = Subscription.Config.PublishedAfter;
                    Request.PublishedBefore = Subscription.Config.PublishedBefore;
                    Request.Filters = Subscription.Config.Filters?.ToList() ?? new();
                    SubmitEnabled = true;
                    ValidationMessage = string.Empty;
                }
                else
                {
                    ValidationMessage = "An error occurred while getting video details.";
                }
            }
            else
                ValidationMessage = "An error occurred while getting video details: " + resp.Message;
        }

        private async Task OnSubmit()
        {
            var (resp, httpResp) = await Backend.SubscriptionEdit(Request);
            if (httpResp.IsSuccessStatusCode)
            {
                ValidationMessage = "Success!";
            }

            else ValidationMessage = resp.Message;
        }

        private void AddFilter()
        {
            Request.Filters.Add(new ApiSubscriptionFilter { Action = FilterAction.Include, Pattern = "" });
        }

        private async Task ShowPreview()
        {
            var (resp, httpResp) = await Backend.SubscriptionFilterPreview(new SubscriptionFilterPreviewRequest
            {
                SubscriptionId = SubscriptionId,
                Filters = Request.Filters,
                // Send the form's current window (never null, so the server uses these rather than
                // falling back to the saved values) — the preview must reflect what's about to be saved.
                PublishedAfter = Request.PublishedAfter ?? string.Empty,
                PublishedBefore = Request.PublishedBefore ?? string.Empty,
            });
            if (httpResp.IsSuccessStatusCode)
            {
                PreviewItems = resp.Data.Videos;
                PreviewTruncated = resp.Data.Truncated;
                previewModal?.Show();
            }
            else
            {
                ValidationMessage = "Preview failed: " + resp.Message;
            }
        }
    }
}
