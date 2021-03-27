using Microsoft.AspNetCore.SignalR;
using Regard.Common;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Hubs
{
    public class MessagingHub : Hub<IMessagingClient>, IMessagingServer
    {
    }
}
