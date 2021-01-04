using Regard.Backend.Model;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public interface IPreferencesManager
    {
        public TValue GetGlobal<TValue>(PreferenceDefinition<TValue> pref);

        public TValue GetForUser<TValue>(PreferenceDefinition<TValue> pref, string userId);

        public TValue GetForSubscriptionFolder<TValue>(PreferenceDefinition<TValue> pref, int folderId);

        public TValue GetForSubscription<TValue>(PreferenceDefinition<TValue> pref, int subId);

        public void SetGlobal<TValue>(PreferenceDefinition<TValue> pref, TValue value);

        public void SetForUser<TValue>(PreferenceDefinition<TValue> pref, string userId, TValue value);

        public void SetForSubscriptionFolder<TValue>(PreferenceDefinition<TValue> pref, int folderId, TValue value);

        public void SetForSubscription<TValue>(PreferenceDefinition<TValue> pref, int subId, TValue value);
    }
}