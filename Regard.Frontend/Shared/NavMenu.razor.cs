using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Regard.Frontend.Shared.Controls;
using Regard.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared
{
    public partial class NavMenu
    {
        private ElementReference notificationsLink;
        private ElementReference userLink;

        // TODO
        private bool CanRegister { get; set; } = true;

        // TODO
        private bool HaveNotifications { get; set; } = true;

        private bool NotificationsPanelVisible { get; set; } = false;

        private bool UserPanelVisible { get; set; } = false;

        private void HideAllPanels()
        {
            NotificationsPanelVisible = false;
            UserPanelVisible = false;
        }

        private void ToggleNotificationsPanel()
        {
            bool visible = NotificationsPanelVisible;
            HideAllPanels();
            if (!visible)
                NotificationsPanelVisible = true;
        }

        private void ToggleUserPanel()
        {
            bool visible = UserPanelVisible;
            HideAllPanels();
            if (!visible)
                UserPanelVisible = true;
        }

    }
}
