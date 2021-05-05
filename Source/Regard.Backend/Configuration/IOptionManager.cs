using Regard.Backend.Model;
using System.Threading.Tasks;

namespace Regard.Backend.Configuration
{
    public interface IOptionManager
    {
        public TValue GetGlobal<TValue>(OptionDefinition<TValue> pref);

        public TValue GetForUser<TValue>(OptionDefinition<TValue> pref, string userId);

        public TValue GetForSubscriptionFolder<TValue>(OptionDefinition<TValue> pref, int folderId);

        public bool GetForSubscriptionFolderNoResolve<TValue>(OptionDefinition<TValue> pref, int subId, out TValue value);

        public TValue GetForSubscription<TValue>(OptionDefinition<TValue> pref, int subId);

        public bool GetForSubscriptionNoResolve<TValue>(OptionDefinition<TValue> pref, int subId, out TValue value);

        public void SetGlobal<TValue>(OptionDefinition<TValue> pref, TValue value);

        public void SetForUser<TValue>(OptionDefinition<TValue> pref, string userId, TValue value);

        public void SetForSubscriptionFolder<TValue>(OptionDefinition<TValue> pref, int folderId, TValue value);

        public void SetForSubscription<TValue>(OptionDefinition<TValue> pref, int subId, TValue value);

        public void UnsetForSubscription<TValue>(OptionDefinition<TValue> pref, int subId);

        public void UnsetForSubscriptionFolder<TValue>(OptionDefinition<TValue> pref, int subId);
    }
}