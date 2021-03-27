using Microsoft.AspNetCore.Components;
using Regard.Common.API.Auth;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Pages.Setup
{
    public partial class Step1Admin
    {
        [Inject]
        protected BackendService Backend { get; set; }
        
        [Inject]
        protected AppState AppState { get; set; }
        
        [Inject] 
        protected AuthenticationService Auth { get; set; }
        
        [Inject] 
        protected AppController AppCtrl { get; set; }

        bool pickUser = false;

        private async Task OnFormCompleted()
        {
            // Promote user
            string username = await Auth.GetUsername();
            var (_, httpResponse) = await Auth.Promote(new UserPromoteRequest() { Username = username });
            httpResponse.EnsureSuccessStatusCode();

            await AppCtrl.ContinueSetup();
        }

        void OnTogglePickCreate()
        {
            pickUser = !pickUser;
        }
    }
}
