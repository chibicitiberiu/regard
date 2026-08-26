using Regard.Model;

namespace Regard.Common.API.Model
{
    public class ApiSubscriptionFilter
    {
        public FilterAction Action { get; set; }

        public string Pattern { get; set; }
    }
}
