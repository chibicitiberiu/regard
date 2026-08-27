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

        public SettingsController(UserManager<UserAccount> userManager,
                                  ApiResponseFactory responseFactory,
                                  IOptionManager optionManager)
        {
            this.userManager = userManager;
            this.responseFactory = responseFactory;
            this.optionManager = optionManager;
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

            return Ok(responseFactory.Success());
        }

        private int? GetOrNull(OptionDefinition<int> pref, string userId)
            => optionManager.GetForUserNoResolve(pref, userId, out int v) ? v : (int?)null;

        private string GetOrNull(OptionDefinition<string> pref, string userId)
            => optionManager.GetForUserNoResolve(pref, userId, out string v) ? v : null;

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

        private void SetOrUnsetCodecs(OptionDefinition<string> pref, string userId, string[] value)
        {
            if (value != null) optionManager.SetForUser(pref, userId, string.Join(",", value));
            else optionManager.UnsetForUser(pref, userId);
        }
    }
}
