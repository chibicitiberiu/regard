using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regard.Common.API.Model;
using Regard.Common.API.Response;
using Regard.Common.Utils;
using Regard.Frontend.Services;
using Regard.Frontend.Shared.Subscription;
using System;
using System.Threading.Tasks;

namespace Regard.Services
{
    public class AppController : IDisposable
    {
        private readonly (string, Func<AppState, bool>)[] SetupSteps =
        {
            ("/setup/welcome", _ => true),
            ("/setup/step1", appState => !appState.ServerStatus.HaveAdmin),
            ("/setup/finished", _ => true)
        };

        private readonly IConfiguration configuration;
        private readonly AppState appState;
        private readonly NavigationManager navigationManager;
        private readonly MessagingService messaging;
        private readonly IServiceProvider serviceProvider;

        public AppController(IConfiguration configuration,
                             AppState appState,
                             NavigationManager navigationManager,
                             MessagingService messaging,
                             IServiceProvider serviceProvider)
        {
            this.configuration = configuration;
            this.appState = appState;
            this.navigationManager = navigationManager;
            this.messaging = messaging;
            this.serviceProvider = serviceProvider;

            appState.PropertyChanged += AppState_PropertyChanged;
            appState.BackendBase = new Uri(configuration["BACKEND_URL"]);
        }

        #region Initialization

        // url, function that evaluates whether the step should be executed


        public async Task OnInitialize()
        {
            await messaging.Initialize();

            // read server status
            if (appState.ServerStatus == null)
            {
                using var scope = serviceProvider.CreateScope();
                var backend = scope.ServiceProvider.GetRequiredService<BackendService>();
                appState.ServerStatus = (await backend.SetupServerStatus()).Data;
            }

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
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();

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

        private void AppState_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SelectedSubscription")
            {
                if (appState.SelectedSubscription == null)
                    navigationManager.NavigateTo("/");
                else if (appState.SelectedSubscription.IsLeft)
                    navigationManager.NavigateTo($"/subscription/{appState.SelectedSubscription.Left.Id}");
                else
                    navigationManager.NavigateTo($"/folder/{appState.SelectedSubscription.Right.Id}");
            }
        }

        public void EditSubscription(Either<ApiSubscription, ApiSubscriptionFolder> subscription)
        {
            appState.SelectedSubscription = subscription;
            if (subscription == null)
                return;

            if (subscription.IsLeft)
                navigationManager.NavigateTo($"/subscription/edit/{appState.SelectedSubscription.Left.Id}");
            else
                navigationManager.NavigateTo($"/folder/edit/{appState.SelectedSubscription.Right.Id}");
        }

        public void Dispose()
        {
            appState.PropertyChanged -= AppState_PropertyChanged;
        }
    }
}
