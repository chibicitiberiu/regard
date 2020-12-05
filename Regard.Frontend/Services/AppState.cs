using Regard.Common.API.Response;

namespace Regard.Services
{
    public class AppState
    {
        public ServerStatus ServerStatus { get; set; }
        
        public int SetupStep { get; set; } = 0;
    }
}
