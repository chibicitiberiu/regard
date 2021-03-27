using Microsoft.AspNetCore.Components;
using Regard.Common.API.Auth;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Forms
{
    public partial class RegisterForm
    {
        [Inject] 
        AuthenticationService Auth { get; set; }

        bool SubmitClicked { get; set; }

        public UserRegisterRequest Request { get; } = new UserRegisterRequest();

        [Parameter]
        public RenderFragment ExtraButtons { get; set; }

        [Parameter]
        public string SubmitText { get; set; } = "Submit";

        [Parameter]
        public EventCallback Registered { get; set; }

        private string loginMessage = null;

        async Task OnSubmit()
        {
            SubmitClicked = true;

            var (response, httpResponse) = await Auth.Register(Request);
            if (httpResponse.IsSuccessStatusCode)
                await Registered.InvokeAsync(null);
            else
                loginMessage = response.Message;
        }
    }
}
