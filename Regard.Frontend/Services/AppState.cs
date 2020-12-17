using Regard.Common.API.Response;

namespace Regard.Services
{
    public class AppState
    {
        public ServerStatusResponse ServerStatus { get; set; }
        
        public int SetupStep { get; set; } = 0;

        
    }
}
