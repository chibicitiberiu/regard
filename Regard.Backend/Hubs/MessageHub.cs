using Microsoft.AspNetCore.SignalR;
using RegardBackend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Hubs
{
    public class MessageHub : Hub
    {
        public async Task SendToast(string message)
        {
            await Clients.All.SendAsync("ShowToast", message);
        }
    }
}
