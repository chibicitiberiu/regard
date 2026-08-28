using Microsoft.AspNetCore.Components;
using Regard.Common.API.Auth;
using Regard.Services;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Forms
{
    public partial class ForgotPasswordForm
    {
        [Inject]
        BackendService Backend { get; set; }

        bool SubmitClicked { get; set; }

        public ForgotPasswordRequest Request { get; } = new ForgotPasswordRequest();

        [Parameter]
        public string SubmitText { get; set; } = "Send reset link";

        private string message = null;

        async Task OnSubmit()
        {
            SubmitClicked = true;

            // The endpoint always returns a generic success (no account enumeration), so show its
            // message regardless. Re-enable the button so the user can retry if nothing arrives.
            var (response, _) = await Backend.AuthForgotPassword(Request);
            message = response.Message
                ?? "If an account with that username exists, password reset instructions have been sent.";
            SubmitClicked = false;
        }
    }
}
