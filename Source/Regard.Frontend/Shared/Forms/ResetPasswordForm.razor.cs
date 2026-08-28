using Microsoft.AspNetCore.Components;
using Regard.Common.API.Auth;
using Regard.Services;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Forms
{
    public partial class ResetPasswordForm
    {
        [Inject]
        BackendService Backend { get; set; }

        bool SubmitClicked { get; set; }

        public ResetPasswordRequest Request { get; } = new ResetPasswordRequest();

        /// <summary>Username carried in the reset link's query string.</summary>
        [Parameter]
        public string Username { get; set; }

        /// <summary>Reset token carried in the reset link's query string (already URL-decoded once).</summary>
        [Parameter]
        public string Token { get; set; }

        [Parameter]
        public string SubmitText { get; set; } = "Set new password";

        private string message = null;
        private bool succeeded = false;

        // Populate the required Username/Token on the model up front so form validation (which runs
        // before OnValidSubmit) sees them — the user only fills the two password fields.
        protected override void OnParametersSet()
        {
            Request.Username = Username;
            Request.Token = Token;
        }

        async Task OnSubmit()
        {
            SubmitClicked = true;
            succeeded = false;

            var (response, httpResponse) = await Backend.AuthResetPassword(Request);
            if (httpResponse.IsSuccessStatusCode)
            {
                succeeded = true;
                message = response.Message ?? "Your password has been reset. You can now log in.";
            }
            else
            {
                message = response.Message ?? "This reset link is invalid or has expired.";
                SubmitClicked = false;
            }
        }
    }
}
