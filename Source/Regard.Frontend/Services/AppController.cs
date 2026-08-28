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
            ("/setup/prerequisites", _ => true),
            ("/setup/step1", appState => !appState.ServerStatus.HaveAdmin),
            ("/setup/finished", _ => true)
        };

        /// <summary>Total number of setup wizard steps (for "Step N of X" display).</summary>
        public int SetupStepCount => SetupSteps.Length;

        /// <summary>1-based index of the current setup step.</summary>
        public int SetupStepNumber => appState.SetupStep + 1;

        private readonly IConfiguration configuration;
        private readonly AppState appState;
        private readonly NavigationManager navigationManager;
        private readonly MessagingService messaging;
        private readonly IServiceProvider serviceProvider;

        public AppController(IConfiguration configuration,
                             AppState appState,
                             NavigationManager navigationManager,
                             MessagingService messaging,
                             NotificationsService notifications,
                             IServiceProvider serviceProvider)
        {
            this.configuration = configuration;
            this.appState = appState;
            this.navigationManager = navigationManager;
            this.messaging = messaging;
            // Resolved (not otherwise injected) so its MessagingService subscriptions run from startup.
            _ = notifications;
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
            // Advance to the first step that still needs to run, skipping any whose work is already
            // done (e.g. step1 is skipped when an admin already exists). Track the landed step in
            // appState.SetupStep so ContinueSetup() resumes from the right place instead of desyncing.
            for (int i = appState.SetupStep; i < SetupSteps.Length; i++)
            {
                if (SetupSteps[i].Item2(appState))
                {
                    appState.SetupStep = i;
                    string currentUri = "/" + navigationManager.ToBaseRelativePath(navigationManager.Uri);
                    if (currentUri != SetupSteps[i].Item1)
                        navigationManager.NavigateTo(SetupSteps[i].Item1);
                    return;
                }
            }

            // Every remaining step is already done -> complete setup.
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

            // Force a full reload (like the login/logout paths): the token was set on a scoped auth-state
            // provider, not the singleton the app's AuthorizeView subscribes to, so a soft navigation
            // would briefly re-render the login screen until the next reload.
            navigationManager.NavigateTo("/", forceLoad: true);
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
