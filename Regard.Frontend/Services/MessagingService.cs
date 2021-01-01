using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Regard.Common;
using Regard.Common.API.Model;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Services
{
    public class MessagingService
    {
        private readonly IConfiguration configuration;
        private readonly AuthenticationService authService;
        private HubConnection hubConnection;

        public event EventHandler<ApiSubscription> SubscriptionCreated;
        public event EventHandler<ApiSubscription> SubscriptionUpdated;
        public event EventHandler<int[]> SubscriptionsDeleted;
        public event EventHandler<ApiSubscriptionFolder> SubscriptionFolderCreated;
        public event EventHandler<ApiSubscriptionFolder> SubscriptionFolderUpdated;
        public event EventHandler<int[]> SubscriptionFoldersDeleted;

        public MessagingService(IConfiguration configuration, AuthenticationService authService)
        {
            this.configuration = configuration;
            this.authService = authService;
            authService.AuthenticationStateChanged += AuthService_AuthenticationStateChanged;
        }

        private async void AuthService_AuthenticationStateChanged(object sender, EventArgs e)
        {
            // Reinitialize with new token
            if (hubConnection != null)
                await hubConnection.DisposeAsync();
            hubConnection = null;
            await Initialize();
        }

        public async Task Initialize()
        {
            // Already initialized
            if (hubConnection != null)
                return;

            var baseAddress = new Uri(configuration["BACKEND_URL"]);
            var messageHub = new Uri(baseAddress, "/api/message_hub");

            var authToken = await authService.GetToken();
            
            hubConnection = new HubConnectionBuilder()
                .WithUrl(messageHub, opts =>
                {
                    opts.AccessTokenProvider = () => authService.GetToken();
                    //if (!string.IsNullOrEmpty(authToken)) 
                    //    opts.Headers.Add("Authorization", $"bearer {authToken}");
                })
                .WithAutomaticReconnect()
                .Build();

            hubConnection.Reconnected += HubConnection_Reconnected;
            hubConnection.Reconnecting += HubConnection_Reconnecting;
            hubConnection.Closed += HubConnection_Closed;

            hubConnection.On<string>("ShowToast", ShowToast);
            hubConnection.On<ApiSubscription>("NotifySubscriptionCreated", NotifySubscriptionCreated);
            hubConnection.On<ApiSubscription>("NotifySubscriptionUpdated", NotifySubscriptionUpdated);
            hubConnection.On<int[]>("NotifySubscriptionsDeleted", NotifySubscriptionsDeleted);
            hubConnection.On<ApiSubscriptionFolder>("NotifySubscriptionFolderCreated", NotifySubscriptionFolderCreated);
            hubConnection.On<ApiSubscriptionFolder>("NotifySubscriptionFolderUpdated", NotifySubscriptionFolderUpdated);
            hubConnection.On<int[]>("NotifySubscriptionFoldersDeleted", NotifySubscriptionFoldersDeleted);

            await hubConnection.StartAsync();
        }

        private async Task HubConnection_Closed(Exception arg)
        {
            Console.WriteLine("Hub closed: " + arg);
        }

        private async Task HubConnection_Reconnecting(Exception arg)
        {
            Console.WriteLine("Hub reconnecting: " + arg);
        }

        private async Task HubConnection_Reconnected(string arg)
        {
            Console.WriteLine("Hub reconnected: " + arg);
        }

        private void ShowToast(string toast)
        {
            Console.WriteLine("Toast: " + toast);
        }

        private void NotifySubscriptionCreated(ApiSubscription subscription)
        {
            SubscriptionCreated?.Invoke(this, subscription);
        }

        private void NotifySubscriptionUpdated(ApiSubscription subscription)
        {
            SubscriptionUpdated?.Invoke(this, subscription);
        }

        private void NotifySubscriptionsDeleted(int[] ids)
        {
            SubscriptionsDeleted?.Invoke(this, ids);
        }

        private void NotifySubscriptionFolderCreated(ApiSubscriptionFolder folder)
        {
            SubscriptionFolderCreated?.Invoke(this, folder);
        }

        private void NotifySubscriptionFolderUpdated(ApiSubscriptionFolder folder)
        {
            SubscriptionFolderUpdated?.Invoke(this, folder);
        }

        private void NotifySubscriptionFoldersDeleted(int[] ids)
        {
            SubscriptionFoldersDeleted?.Invoke(this, ids);
        }
    }
}
