using Regard.Common.API.Model;
using Regard.Common.API.Response;
using Regard.Common.Utils;
using Regard.Common.Utils.Collections;
using Regard.Frontend.Shared.Controls;
using Regard.Frontend.Shared.Subscription;
using System;

namespace Regard.Services
{
    public class AppState : NotifyPropertyChangedBase
    {
        /// <summary>
        /// Base address of backend
        /// </summary>
        public Uri BackendBase { get; set; }

        private ServerStatusResponse serverStatus;

        /// <summary>
        /// Server status
        /// </summary>
        public ServerStatusResponse ServerStatus { get => serverStatus; set => SetField(ref serverStatus, value); }
        
        /// <summary>
        /// Current setup step
        /// </summary>
        public int SetupStep { get; set; } = 0;

        /// <summary>
        /// Current user's subscriptions
        /// </summary>
        public ObservableDictionary<int, ApiSubscription> Subscriptions { get; } = new ObservableDictionary<int, ApiSubscription>();

        /// <summary>
        /// Current user's subscription folders
        /// </summary>
        public ObservableDictionary<int, ApiSubscriptionFolder> Folders { get; } = new ObservableDictionary<int, ApiSubscriptionFolder>();

        #region Selected subscription

        private Either<ApiSubscription, ApiSubscriptionFolder> selectedSubscription;

        /// <summary>
        /// Subscription or folder that is currently selected in subscription tree
        /// </summary>
        public Either<ApiSubscription, ApiSubscriptionFolder> SelectedSubscription 
        {
            get => selectedSubscription;
            set => SetField(ref selectedSubscription, value);
        }

        #endregion

        public event EventHandler RefreshRequested;

        public void RequestRefresh()
        {
            RefreshRequested?.Invoke(this, new EventArgs());
        }
    }
}
