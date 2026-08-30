using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Settings;
using Regard.Model;
using System;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly ApiResponseFactory responseFactory;
        private readonly IOptionManager optionManager;
        private readonly UserQuotaService quotaService;
        private readonly UserCookiesService cookiesService;

        public SettingsController(UserManager<UserAccount> userManager,
                                  ApiResponseFactory responseFactory,
                                  IOptionManager optionManager,
                                  UserQuotaService quotaService,
                                  UserCookiesService cookiesService)
        {
            this.userManager = userManager;
            this.responseFactory = responseFactory;
            this.optionManager = optionManager;
            this.quotaService = quotaService;
            this.cookiesService = cookiesService;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            // Read only the user's explicitly-set values (no fallback): null = "inherits default".
            var settings = new ApiUserSettings
            {
                CookiesConfigured = cookiesService.HasCookies(
                    user.Id, optionManager.GetForUserNoResolve(Options.Server_Ytdl_CookiesFile, user.Id, out string cookiesPath) ? cookiesPath : null),
                AutoDownload = GetOrNull(Options.Subscriptions_AutoDownload, user.Id),
                DownloadOrder = GetOrNull(Options.Subscriptions_DownloadOrder, user.Id),
                DownloadMaxCount = GetOrNull(Options.Subscriptions_MaxCount, user.Id),
                DownloadMaxSize = GetOrNull(Options.Subscriptions_MaxSize, user.Id),
                DeleteWatched = GetOrNull(Options.Subscriptions_DeleteWatched, user.Id),
                MarkDeletedAsWatched = GetOrNull(Options.Subscriptions_MarkDeletedAsWatched, user.Id),
                DeleteGracePeriod = GetOrNull(Options.Subscriptions_DeleteGracePeriod, user.Id),
                IncludeShorts = GetOrNull(Options.Subscriptions_IncludeShorts, user.Id),
                IncludeMembersOnly = GetOrNull(Options.Subscriptions_IncludeMembersOnly, user.Id),
                PublishedAfter = GetOrNull(Options.Subscriptions_PublishedAfter, user.Id),
                PublishedBefore = GetOrNull(Options.Subscriptions_PublishedBefore, user.Id),
                // Effective global defaults (what "inherit" resolves to) for the "Default (…)" labels.
                AutoDownloadDefault = optionManager.GetGlobal(Options.Subscriptions_AutoDownload),
                DownloadOrderDefault = optionManager.GetGlobal(Options.Subscriptions_DownloadOrder),
                DeleteWatchedDefault = optionManager.GetGlobal(Options.Subscriptions_DeleteWatched),
                MarkDeletedAsWatchedDefault = optionManager.GetGlobal(Options.Subscriptions_MarkDeletedAsWatched),
                IncludeShortsDefault = optionManager.GetGlobal(Options.Subscriptions_IncludeShorts),
                IncludeMembersOnlyDefault = optionManager.GetGlobal(Options.Subscriptions_IncludeMembersOnly),
                MaxResolution = GetOrNull(Options.Ytdl_MaxResolution, user.Id),
                ExcludedVideoCodecs = GetCodecsOrNull(Options.Ytdl_ExcludedVideoCodecs, user.Id),
                ExcludedAudioCodecs = GetCodecsOrNull(Options.Ytdl_ExcludedAudioCodecs, user.Id),
                TranscodeVideo = GetOrNull(Options.Ytdl_TranscodeVideo, user.Id),
                TranscodeMode = GetOrNull(Options.Ytdl_TranscodeMode, user.Id),
                RawFormatOverride = GetOrNull(Options.Ytdl_Format, user.Id),
                MergeOutputFormat = GetOrNull(Options.Ytdl_MergeOutputFormat, user.Id),
                AllowEmbedding = GetOrNull(Options.Ui_AllowEmbedding, user.Id),
                WriteSubtitles = GetOrNull(Options.Ytdl_WriteSubtitles, user.Id),
                WriteAutoSub = GetOrNull(Options.Ytdl_WriteAutoSub, user.Id),
                AllSubs = GetOrNull(Options.Ytdl_AllSubs, user.Id),
                SubFormat = GetOrNull(Options.Ytdl_SubFormat, user.Id),
                SubLang = GetOrNull(Options.Ytdl_SubLang, user.Id),
                SponsorblockActions = GetOrNull(Options.Sponsorblock_Actions, user.Id),
                DownloadPath = GetOrNull(Options.Subscriptions_DownloadPath, user.Id),
            };
            return Ok(responseFactory.Success(settings));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Update([FromBody] ApiUserSettings request)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            // Validated before any write, since the SetOrUnset calls below persist eagerly and a late
            // rejection would leave the settings half-applied. Same check the subscription editor runs.
            var dateError = PublishDateFilter.DescribeValidationError(request.PublishedAfter, request.PublishedBefore);
            if (dateError != null)
                return BadRequest(responseFactory.Error(dateError));

            // Per field: a non-null value is pinned as a user override; null clears the override so
            // the value inherits again (mirrors SubscriptionController.Edit's set-or-unset).
            SetOrUnset(Options.Subscriptions_AutoDownload, user.Id, request.AutoDownload);
            SetOrUnset(Options.Subscriptions_DownloadOrder, user.Id, request.DownloadOrder);
            SetOrUnset(Options.Subscriptions_MaxCount, user.Id, request.DownloadMaxCount);
            SetOrUnset(Options.Subscriptions_MaxSize, user.Id, request.DownloadMaxSize);
            SetOrUnset(Options.Subscriptions_DeleteWatched, user.Id, request.DeleteWatched);
            SetOrUnset(Options.Subscriptions_MarkDeletedAsWatched, user.Id, request.MarkDeletedAsWatched);
            SetOrUnset(Options.Subscriptions_DeleteGracePeriod, user.Id, request.DeleteGracePeriod);
            SetOrUnset(Options.Subscriptions_IncludeShorts, user.Id, request.IncludeShorts);
            SetOrUnset(Options.Subscriptions_IncludeMembersOnly, user.Id, request.IncludeMembersOnly);
            SetOrUnset(Options.Subscriptions_PublishedAfter, user.Id, request.PublishedAfter?.Trim());
            SetOrUnset(Options.Subscriptions_PublishedBefore, user.Id, request.PublishedBefore?.Trim());
            SetOrUnset(Options.Ytdl_MaxResolution, user.Id, request.MaxResolution);
            SetOrUnsetCodecs(Options.Ytdl_ExcludedVideoCodecs, user.Id, request.ExcludedVideoCodecs);
            SetOrUnsetCodecs(Options.Ytdl_ExcludedAudioCodecs, user.Id, request.ExcludedAudioCodecs);
            SetOrUnset(Options.Ytdl_TranscodeVideo, user.Id, request.TranscodeVideo);
            SetOrUnset(Options.Ytdl_TranscodeMode, user.Id, request.TranscodeMode);
            SetOrUnset(Options.Ytdl_Format, user.Id, request.RawFormatOverride);
            SetOrUnset(Options.Ytdl_MergeOutputFormat, user.Id, request.MergeOutputFormat);
            SetOrUnset(Options.Ui_AllowEmbedding, user.Id, request.AllowEmbedding);
            SetOrUnset(Options.Ytdl_WriteSubtitles, user.Id, request.WriteSubtitles);
            SetOrUnset(Options.Ytdl_WriteAutoSub, user.Id, request.WriteAutoSub);
            SetOrUnset(Options.Ytdl_AllSubs, user.Id, request.AllSubs);
            SetOrUnset(Options.Ytdl_SubFormat, user.Id, request.SubFormat);
            SetOrUnset(Options.Ytdl_SubLang, user.Id, request.SubLang);
            SetOrUnset(Options.Sponsorblock_Actions, user.Id, request.SponsorblockActions);
            SetOrUnset(Options.Subscriptions_DownloadPath, user.Id, request.DownloadPath);

            // Cookies are handled apart from the SetOrUnset list on purpose. The option holds a
            // filesystem path that is passed to yt-dlp as --cookies, and yt-dlp both reads it and writes
            // the jar back — so routing it through the generic string setter would let any signed-in user
            // read and overwrite arbitrary server files. The request carries content only; the path comes
            // from the authenticated account.
            if (request.CookiesFileContent != null)
            {
                try
                {
                    var stored = await cookiesService.ApplyAsync(user.Id, request.CookiesFileContent);
                    if (stored != null)
                        optionManager.SetForUser(Options.Server_Ytdl_CookiesFile, user.Id, stored);
                }
                catch (Exception ex)
                {
                    return BadRequest(responseFactory.Error(ex.Message));
                }
            }

            return Ok(responseFactory.Success());
        }

        [HttpGet]
        [Route("usage")]
        [Authorize]
        public async Task<IActionResult> GetUsage()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            var usage = quotaService.GetUsage(user.Id);
            var (countQuota, sizeQuotaBytes) = quotaService.GetHardQuota(user.Id);

            return Ok(responseFactory.Success(new ApiUserUsage
            {
                VideoCount = usage.Count,
                UsedBytes = usage.Bytes,
                VideoQuota = countQuota,
                StorageQuotaBytes = sizeQuotaBytes,
            }));
        }

        private int? GetOrNull(OptionDefinition<int> pref, string userId)
            => optionManager.GetForUserNoResolve(pref, userId, out int v) ? v : (int?)null;

        private string GetOrNull(OptionDefinition<string> pref, string userId)
            => optionManager.GetForUserNoResolve(pref, userId, out string v) ? v : null;

        private bool? GetOrNull(OptionDefinition<bool> pref, string userId)
            => optionManager.GetForUserNoResolve(pref, userId, out bool v) ? v : (bool?)null;

        private long? GetOrNull(OptionDefinition<long> pref, string userId)
            => optionManager.GetForUserNoResolve(pref, userId, out long v) ? v : (long?)null;

        private VideoOrder? GetOrNull(OptionDefinition<VideoOrder> pref, string userId)
            => optionManager.GetForUserNoResolve(pref, userId, out VideoOrder v) ? v : (VideoOrder?)null;

        private string[] GetCodecsOrNull(OptionDefinition<string> pref, string userId)
        {
            if (!optionManager.GetForUserNoResolve(pref, userId, out string v))
                return null;
            if (string.IsNullOrEmpty(v))
                return Array.Empty<string>();
            return v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private void SetOrUnset(OptionDefinition<int> pref, string userId, int? value)
        {
            if (value.HasValue) optionManager.SetForUser(pref, userId, value.Value);
            else optionManager.UnsetForUser(pref, userId);
        }

        private void SetOrUnset(OptionDefinition<string> pref, string userId, string value)
        {
            if (value != null) optionManager.SetForUser(pref, userId, value);
            else optionManager.UnsetForUser(pref, userId);
        }

        private void SetOrUnset(OptionDefinition<bool> pref, string userId, bool? value)
        {
            if (value.HasValue) optionManager.SetForUser(pref, userId, value.Value);
            else optionManager.UnsetForUser(pref, userId);
        }

        private void SetOrUnset(OptionDefinition<long> pref, string userId, long? value)
        {
            if (value.HasValue) optionManager.SetForUser(pref, userId, value.Value);
            else optionManager.UnsetForUser(pref, userId);
        }

        private void SetOrUnset(OptionDefinition<VideoOrder> pref, string userId, VideoOrder? value)
        {
            if (value.HasValue) optionManager.SetForUser(pref, userId, value.Value);
            else optionManager.UnsetForUser(pref, userId);
        }

        private void SetOrUnsetCodecs(OptionDefinition<string> pref, string userId, string[] value)
        {
            if (value != null) optionManager.SetForUser(pref, userId, string.Join(",", value));
            else optionManager.UnsetForUser(pref, userId);
        }
    }
}
