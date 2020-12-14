using Microsoft.AspNetCore.Components;
using Regard.Services;
using RegardBackend.Common.API.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Forms
{
    public partial class LoginForm
    {
        [Inject]
        AuthenticationService Auth { get; set; }

        bool SubmitClicked { get; set; }

        public UserLogin Request { get; } = new UserLogin();

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
