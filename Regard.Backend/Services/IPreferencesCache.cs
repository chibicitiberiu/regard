using Regard.Backend.Model;

namespace Regard.Backend.Services
{
    public interface IPreferencesCache<TKey>
    {
        bool Get<TValue>(TKey key, out TValue value);

        void Set<TValue>(TKey key, TValue value);

        void Invalidate();

        void ClearExpired();
    }
}