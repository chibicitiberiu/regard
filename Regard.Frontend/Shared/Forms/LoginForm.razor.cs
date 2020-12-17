using Microsoft.AspNetCore.Components;
using Regard.Common.API.Auth;
using Regard.Services;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Forms
{
    public partial class LoginForm
    {
        [Inject]
        AuthenticationService Auth { get; set; }

        bool SubmitClicked { get; set; }

        public UserLoginRequest Request { get; } = new UserLoginRequest();

        [Parameter]
        public RenderFragment ExtraButtons { get; set; }

        [Parameter]
        public string SubmitText { get; set; } = "Submit";

        [Parameter]
        public EventCallback LoggedIn { get; set; }

        private string loginMessage = null;


        async Task OnSubmit()
        {
            SubmitClicked = true;
            var (response, httpResponse) = await Auth.Login(Request);

            if (httpResponse.IsSuccessStatusCode)
                await LoggedIn.InvokeAsync(null);
            else
                loginMessage = response.Message;
        }
    }
}
