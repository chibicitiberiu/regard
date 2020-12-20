using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public enum Icons
    {
        Close,
        Subscription,
        SubscriptionNew,
        Folder,
        FolderNew,
        Import,
        NotificationsFull,
        NotificationsEmpty,
        Profile,
        Settings
    }

    public partial class Icon
    {
        private static Dictionary<Icons, string> mapping = new Dictionary<Icons, string>()
        {
            { Icons.Close, "close" },
            { Icons.Subscription, "subject" },
            { Icons.SubscriptionNew, "playlist_add" },
            { Icons.Folder, "folder" },
            { Icons.FolderNew, "create_new_folder" },
            { Icons.Import, "publish" },
            { Icons.NotificationsFull, "notifications" },
            { Icons.NotificationsEmpty, "notifications_none" },
            { Icons.Profile, "person" },
            { Icons.Settings, "settings" },
        };

        private string Map(Icons icon)
        {
            if (mapping.TryGetValue(icon, out string value))
                return value;

            throw new NotImplementedException();
        }
    }
}
