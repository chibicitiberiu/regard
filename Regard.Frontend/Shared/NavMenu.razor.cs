using Microsoft.AspNetCore.Components.Web;
using Regard.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Shared
{
    public partial class NavMenu
    {
        private readonly static string NotificationsGlyph = "notifications";
        private readonly static string NotificationsNoneGlyph = "notifications_none";

        // TODO
        private bool CanRegister { get; set; } = true;

        // TODO
        private bool HaveNotifications { get; set; } = true;

        private bool NotificationsPanelVisible { get; set; } = false;

        private string HaveNotificationsIcon => HaveNotifications ? NotificationsGlyph : NotificationsNoneGlyph;


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
