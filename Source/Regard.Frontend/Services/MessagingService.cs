using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Regard.Common;
using Regard.Common.API.Model;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Frontend.Services
{
    public class MessagingService
    {
        private readonly IConfiguration configuration;
        private readonly AuthenticationService authService;
        private HubConnection hubConnection;

        // Serializes (re)connects. Auth-state changes can fire several times in quick
        // succession; without this lock, one firing disposes the connection while another's
        // StartAsync is still in flight, canceling it (OperationCanceled) and leaving the app
        // with no live hub -- so pushes like NotifySubscriptionCreated silently go nowhere.
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(1, 1);
        private string currentToken;

        public event EventHandler<ApiSubscription> SubscriptionCreated;
        public event EventHandler<ApiSubscription> SubscriptionUpdated;
        public event EventHandler<int[]> SubscriptionsDeleted;
        public event EventHandler<ApiSubscriptionFolder> SubscriptionFolderCreated;
        public event EventHandler<ApiSubscriptionFolder> SubscriptionFolderUpdated;
        public event EventHandler<int[]> SubscriptionFoldersDeleted;
        public event EventHandler<ApiVideo> VideoUpdated;
        public event EventHandler<ApiJobInfo> JobUpdated;
        public event EventHandler<ApiMessage> MessageReceived;

        public MessagingService(IConfiguration configuration, AuthenticationService authService)
        {
            this.configuration = configuration;
            this.authService = authService;
            authService.AuthenticationStateChanged += AuthService_AuthenticationStateChanged;
        }

        private async void AuthService_AuthenticationStateChanged(object sender, EventArgs e)
        {
            // async void event handler: an unhandled exception here terminates the whole WASM
            // runtime, so everything below must be guarded.
            try
            {
                await EnsureConnected();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to reinitialize messaging: " + ex.Message);
            }
        }

        public Task Initialize() => EnsureConnected();

        /// <summary>
        /// Ensures a hub connection exists for the current auth token. Safe to call
        /// concurrently and repeatedly: a lock serializes (re)connects so overlapping
        /// auth-state changes can't cancel each other's StartAsync, and an already-connected
        /// hub on the same token is left untouched.
        /// </summary>
        private async Task EnsureConnected()
        {
            var token = await authService.GetToken();

            await connectionLock.WaitAsync();
            try
            {
                // Keep a healthy connection when the token hasn't changed.
                if (hubConnection != null
                    && hubConnection.State == HubConnectionState.Connected
                    && token == currentToken)
                    return;

                // Otherwise tear down whatever we had and rebuild.
                if (hubConnection != null)
                {
                    try { await hubConnection.DisposeAsync(); } catch { }
                    hubConnection = null;
                }

                currentToken = token;

                // Not signed in -> nothing to connect to. A later auth change reconnects.
                if (string.IsNullOrEmpty(token))
                    return;

                var connection = BuildConnection();
                try
                {
                    await connection.StartAsync();
                    hubConnection = connection;
                }
                catch (Exception ex)
                {
                    // A failed/canceled initial connect must not crash the app. Drop it so a
                    // later auth change / refresh can retry (WithAutomaticReconnect only recovers
                    // drops after a successful start, not the initial StartAsync).
                    Console.Error.WriteLine("Failed to start message hub: " + ex.Message);
                    try { await connection.DisposeAsync(); } catch { }
                    hubConnection = null;
                }
            }
            finally
            {
                connectionLock.Release();
            }
        }

        private HubConnection BuildConnection()
        {
            var baseAddress = new Uri(configuration["BACKEND_URL"]);
            var messageHub = new Uri(baseAddress, "/api/message_hub");

            var connection = new HubConnectionBuilder()
                .WithUrl(messageHub, opts =>
                {
                    opts.AccessTokenProvider = () => authService.GetToken();
                    // Pin the transport to WebSockets: the query-string JWT only authenticates the
                    // WS upgrade, so a fallback to SSE/long-polling would leave the hub unauthenticated
                    // and Clients.User(...) pushes would silently vanish.
                    opts.Transports = HttpTransportType.WebSockets;
                })
                .WithAutomaticReconnect()
                .Build();

            connection.Reconnected += HubConnection_Reconnected;
            connection.Reconnecting += HubConnection_Reconnecting;
            connection.Closed += HubConnection_Closed;

            connection.On<string>("ShowToast", ShowToast);
            connection.On<ApiSubscription>("NotifySubscriptionCreated", NotifySubscriptionCreated);
            connection.On<ApiSubscription>("NotifySubscriptionUpdated", NotifySubscriptionUpdated);
            connection.On<int[]>("NotifySubscriptionsDeleted", NotifySubscriptionsDeleted);
            connection.On<ApiSubscriptionFolder>("NotifySubscriptionFolderCreated", NotifySubscriptionFolderCreated);
            connection.On<ApiSubscriptionFolder>("NotifySubscriptionFolderUpdated", NotifySubscriptionFolderUpdated);
            connection.On<int[]>("NotifySubscriptionFoldersDeleted", NotifySubscriptionFoldersDeleted);
            connection.On<ApiVideo>("NotifyVideoUpdated", NotifyVideoUpdated);
            connection.On<ApiJobInfo>("NotifyJobUpdated", NotifyJobUpdated);
            connection.On<ApiMessage>("NotifyMessage", NotifyMessage);

            return connection;
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

        private void NotifyVideoUpdated(ApiVideo video)
        {
            VideoUpdated?.Invoke(this, video);
        }

        private void NotifyJobUpdated(ApiJobInfo job)
        {
            JobUpdated?.Invoke(this, job);
        }

        private void NotifyMessage(ApiMessage message)
        {
            MessageReceived?.Invoke(this, message);
        }
    }
}
