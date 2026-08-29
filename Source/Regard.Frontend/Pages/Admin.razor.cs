using Microsoft.AspNetCore.Components;
using Regard.Common.API.Admin;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Regard.Frontend.Pages
{
    public partial class Admin
    {
        [Inject] protected BackendService Backend { get; set; }
        [Inject] protected AuthenticationService Auth { get; set; }

        protected bool loadingServer = true;
        protected bool loadingUsers = true;
        protected bool savingServer = false;
        protected bool serverSaved = false;
        protected string serverStatus = string.Empty;

        // Server settings form. Quota strings are blank = unlimited.
        protected bool AllowRegistrations { get; set; }
        protected string DefaultVideoQuotaStr { get; set; } = string.Empty;
        protected string DefaultStorageQuotaStr { get; set; } = string.Empty;
        protected int JobHistoryRetentionDays { get; set; }

        // Throttling / anti-bot
        protected bool ThrottleEnabled { get; set; }
        protected int SleepRequests { get; set; }
        protected int SleepInterval { get; set; }
        protected int MaxSleepInterval { get; set; }
        protected string LimitRate { get; set; } = string.Empty;
        protected int DownloadMinSeconds { get; set; }
        protected int DownloadMaxSeconds { get; set; }
        protected int ExtractMinSeconds { get; set; }
        protected int ExtractMaxSeconds { get; set; }
        protected int MaxPerHour { get; set; }
        protected int MaxPerDay { get; set; }
        protected int PerHostConcurrency { get; set; }
        protected int MaxParallelJobs { get; set; }
        protected bool CookiesConfigured { get; set; }
        private string cookiesFileContent = null;   // null = unchanged; "" = remove; non-empty = replace
        protected string cookiesNote = string.Empty;

        // Users
        protected List<ApiAdminUser> users = new();
        protected string currentUsername;
        protected string usersError = string.Empty;

        // Set-quota modal
        protected Modal quotaModal;
        protected ApiAdminUser quotaTarget;
        protected string quotaVideoStr = string.Empty;
        protected string quotaStorageStr = string.Empty;

        // Delete-confirm modal
        protected Modal deleteModal;
        protected ApiAdminUser deleteTarget;

        protected override async Task OnInitializedAsync()
        {
            currentUsername = await Auth.GetUsername();
            await LoadServer();
            await LoadUsers();
        }

        private async Task LoadServer()
        {
            loadingServer = true;
            var s = (await Backend.GetServerSettings())?.Data;
            if (s != null)
            {
                AllowRegistrations = s.AllowRegistrations;
                DefaultVideoQuotaStr = s.DefaultVideoQuota?.ToString() ?? string.Empty;
                DefaultStorageQuotaStr = s.DefaultStorageQuotaGb?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                JobHistoryRetentionDays = s.JobHistoryRetentionDays;
                ThrottleEnabled = s.ThrottleEnabled;
                SleepRequests = s.SleepRequests;
                SleepInterval = s.SleepInterval;
                MaxSleepInterval = s.MaxSleepInterval;
                LimitRate = s.LimitRate ?? string.Empty;
                DownloadMinSeconds = s.DownloadMinSeconds;
                DownloadMaxSeconds = s.DownloadMaxSeconds;
                ExtractMinSeconds = s.ExtractMinSeconds;
                ExtractMaxSeconds = s.ExtractMaxSeconds;
                MaxPerHour = s.MaxPerHour;
                MaxPerDay = s.MaxPerDay;
                PerHostConcurrency = s.PerHostConcurrency;
                MaxParallelJobs = s.MaxParallelJobs;
                CookiesConfigured = s.CookiesConfigured;
                cookiesFileContent = null;
                cookiesNote = string.Empty;
            }
            loadingServer = false;
        }

        private async Task LoadUsers()
        {
            loadingUsers = true;
            var resp = await Backend.GetAdminUsers();
            users = resp?.Data ?? new List<ApiAdminUser>();
            loadingUsers = false;
        }

        protected bool IsSelf(ApiAdminUser u) => string.Equals(u.UserName, currentUsername, StringComparison.Ordinal);

        protected async Task OnSaveServer()
        {
            savingServer = true;
            serverStatus = string.Empty;

            var request = new ApiServerSettings
            {
                AllowRegistrations = AllowRegistrations,
                DefaultVideoQuota = ParseIntOrNull(DefaultVideoQuotaStr),
                DefaultStorageQuotaGb = ParseDoubleOrNull(DefaultStorageQuotaStr),
                JobHistoryRetentionDays = JobHistoryRetentionDays,
                ThrottleEnabled = ThrottleEnabled,
                SleepRequests = SleepRequests,
                SleepInterval = SleepInterval,
                MaxSleepInterval = MaxSleepInterval,
                LimitRate = LimitRate,
                DownloadMinSeconds = DownloadMinSeconds,
                DownloadMaxSeconds = DownloadMaxSeconds,
                ExtractMinSeconds = ExtractMinSeconds,
                ExtractMaxSeconds = ExtractMaxSeconds,
                MaxPerHour = MaxPerHour,
                MaxPerDay = MaxPerDay,
                PerHostConcurrency = PerHostConcurrency,
                CookiesFileContent = cookiesFileContent,
            };
            var (resp, httpResp) = await Backend.SaveServerSettings(request);
            savingServer = false;
            serverSaved = httpResp.IsSuccessStatusCode;
            serverStatus = serverSaved ? "Saved." : ("Save failed: " + resp?.Message);
            if (serverSaved)
                await LoadServer();   // refresh cookies-configured indicator + clear the pending upload
        }

        protected async Task OnCookiesFile(Microsoft.AspNetCore.Components.Forms.InputFileChangeEventArgs e)
        {
            try
            {
                using var reader = new System.IO.StreamReader(e.File.OpenReadStream(5 * 1024 * 1024));
                cookiesFileContent = await reader.ReadToEndAsync();
                cookiesNote = $"{e.File.Name} ready — click Save to apply";
            }
            catch (Exception ex)
            {
                cookiesFileContent = null;
                cookiesNote = "Could not read file: " + ex.Message;
            }
        }

        protected void OnClearCookies()
        {
            cookiesFileContent = string.Empty;   // empty string = remove on Save
            cookiesNote = "cookies will be removed on Save";
        }

        protected async Task ToggleAdmin(ApiAdminUser u)
        {
            var (resp, http) = await Backend.SetUserRole(new SetUserRoleRequest { UserId = u.Id, IsAdmin = !u.IsAdmin });
            usersError = http.IsSuccessStatusCode ? string.Empty : resp?.Message;
            await LoadUsers();
        }

        protected async Task ToggleEnabled(ApiAdminUser u)
        {
            var (resp, http) = await Backend.SetUserEnabled(new SetUserEnabledRequest { UserId = u.Id, Enabled = u.IsDisabled });
            usersError = http.IsSuccessStatusCode ? string.Empty : resp?.Message;
            await LoadUsers();
        }

        protected async Task OpenQuota(ApiAdminUser u)
        {
            quotaTarget = u;
            quotaVideoStr = u.VideoQuotaOverride?.ToString() ?? string.Empty;
            quotaStorageStr = u.StorageQuotaOverrideGb?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            await quotaModal.Show();
        }

        protected async Task SaveQuota()
        {
            await Backend.SetUserQuota(new SetUserQuotaRequest
            {
                UserId = quotaTarget.Id,
                VideoQuota = ParseIntOrNull(quotaVideoStr),
                StorageQuotaGb = ParseDoubleOrNull(quotaStorageStr),
            });
            await quotaModal.Close();
            await LoadUsers();
        }

        protected async Task OpenDelete(ApiAdminUser u)
        {
            deleteTarget = u;
            await deleteModal.Show();
        }

        protected async Task ConfirmDelete()
        {
            var (resp, http) = await Backend.DeleteUser(new DeleteUserRequest { UserId = deleteTarget.Id });
            usersError = http.IsSuccessStatusCode ? string.Empty : resp?.Message;
            await deleteModal.Close();
            await LoadUsers();
        }

        protected static string FormatSize(long bytes)
        {
            const double gb = 1024d * 1024 * 1024, mb = 1024d * 1024, kb = 1024d;
            if (bytes >= gb) return $"{bytes / gb:0.0} GB";
            if (bytes >= mb) return $"{bytes / mb:0.0} MB";
            if (bytes >= kb) return $"{bytes / kb:0.0} KB";
            return $"{bytes} B";
        }

        protected static string QuotaLabel(ApiAdminUser u)
        {
            string videos = u.VideoQuotaOverride.HasValue ? $"{u.VideoQuotaOverride.Value} videos" : "videos: default";
            string size = u.StorageQuotaOverrideGb.HasValue ? $"{u.StorageQuotaOverrideGb.Value:0.#} GB" : "size: default";
            return $"{videos}; {size}";
        }

        private static int? ParseIntOrNull(string s)
            => int.TryParse(s?.Trim(), out var v) ? v : (int?)null;

        private static double? ParseDoubleOrNull(string s)
            => double.TryParse(s?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
    }
}
