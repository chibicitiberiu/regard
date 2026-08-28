using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Regard.Backend.Configuration;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Settings;
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

        public SettingsController(UserManager<UserAccount> userManager,
                                  ApiResponseFactory responseFactory,
                                  IOptionManager optionManager,
                                  UserQuotaService quotaService)
        {
            this.userManager = userManager;
            this.responseFactory = responseFactory;
            this.optionManager = optionManager;
            this.quotaService = quotaService;
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

            // Per field: a non-null value is pinned as a user override; null clears the override so
            // the value inherits again (mirrors SubscriptionController.Edit's set-or-unset).
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
            SetOrUnset(Options.Subscriptions_DownloadPath, user.Id, request.DownloadPath);

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

        private void SetOrUnsetCodecs(OptionDefinition<string> pref, string userId, string[] value)
        {
            if (value != null) optionManager.SetForUser(pref, userId, string.Join(",", value));
            else optionManager.UnsetForUser(pref, userId);
        }
    }
}
