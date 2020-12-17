using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Regard.Frontend.Services;
using System;
using System.Threading.Tasks;

namespace Regard.Services
{
    public class MainAppController
    {
        readonly AppState appState;
        readonly NavigationManager navigationManager;
        readonly BackendService backend;
        readonly MessagingService messaging;

        public MainAppController(AppState appState, NavigationManager navigationManager, BackendService backend, MessagingService messaging)
        {
            this.appState = appState;
            this.navigationManager = navigationManager;
            this.backend = backend;
            this.messaging = messaging;
        }

        // url, function that evaluates whether the step should be executed
        readonly (string, Func<AppState, bool>)[] SetupSteps = 
        {
            ("/setup/welcome", _ => true),
            ("/setup/step1", appState => !appState.ServerStatus.HaveAdmin),
            ("/setup/finished", _ => true)
        };

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

        public void NavigateToFromUrl()
        {
            var uri = new Uri(navigationManager.Uri);
            var parsedQuery = QueryHelpers.ParseNullableQuery(uri.Query);

            string targetUri = "/";

            if (parsedQuery.TryGetValue("from", out var value))
                targetUri = value.ToString();

            navigationManager.NavigateTo(targetUri);
        }
    }
}
