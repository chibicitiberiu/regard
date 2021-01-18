using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Regard.Common.API.Response;
using Regard.Frontend.Services;
using Regard.Frontend.Shared.Subscription;
using System;
using System.Threading.Tasks;

namespace Regard.Services
{
    public class AppController
    {
        private readonly (string, Func<AppState, bool>)[] SetupSteps =
        {
            ("/setup/welcome", _ => true),
            ("/setup/step1", appState => !appState.ServerStatus.HaveAdmin),
            ("/setup/finished", _ => true)
        };

        private readonly AppState appState;
        private readonly NavigationManager navigationManager;
        private readonly MessagingService messaging;
        private readonly BackendService backend;

        public event EventHandler RefreshRequested;

        public AppController(AppState appState,
                             NavigationManager navigationManager,
                             MessagingService messaging,
                             BackendService backend)
        {
            this.appState = appState;
            this.navigationManager = navigationManager;
            this.messaging = messaging;
            this.backend = backend;
        }

        #region Initialization

        // url, function that evaluates whether the step should be executed


        public async Task OnInitialize()
        {
            await messaging.Initialize();

            // read server status
            if (appState.ServerStatus == null)
                appState.ServerStatus = (await backend.SetupServerStatus()).Data;

            // check if server is initialized
            if (!appState.ServerStatus.Initialized)
                await ResumeSetup();
        }

        private async Task ResumeSetup()
        {
            bool execute = false;
            for (int i = appState.SetupStep; i < SetupSteps.Length && !execute; i++)
            {
                execute = SetupSteps[i].Item2(appState);
                string currentUri = "/" + navigationManager.ToBaseRelativePath(navigationManager.Uri);
                if (execute && currentUri != SetupSteps[i].Item1)
                    navigationManager.NavigateTo(SetupSteps[i].Item1);
            }

            if (!execute)
                await FinishSetup();
        }

        public async Task ContinueSetup()
        {
            appState.SetupStep++;
            await ResumeSetup();
        }

        private async Task FinishSetup()
        {
            // Finish initialization
            var (result, httpResponse) = await backend.SetupInitialize();
            if (!httpResponse.IsSuccessStatusCode)
                throw new Exception("Initialization failed! " + result.Message);

            // Update server status
            appState.ServerStatus = (await backend.SetupServerStatus()).Data;

            navigationManager.NavigateTo("/");
        }

        #endregion

        public void NavigateToFromUrl()
        {
            var uri = new Uri(navigationManager.Uri);
            var parsedQuery = QueryHelpers.ParseNullableQuery(uri.Query);

            string targetUri = "/";

            if (parsedQuery != null && parsedQuery.TryGetValue("from", out var value))
                targetUri = value.ToString();

            navigationManager.NavigateTo(targetUri, true);
        }
    }
}
