using Regard.Backend.Model;

namespace Regard.Backend.Services
{
    public interface IPreferencesCache
    {
        void ClearExpired();
        bool Get<TValue>(string key, out TValue value, UserAccount user = null);
        void Set<TValue>(string key, TValue value, UserAccount user = null);
    }
}