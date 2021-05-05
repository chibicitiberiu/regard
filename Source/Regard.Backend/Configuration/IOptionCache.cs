using Regard.Backend.Model;

namespace Regard.Backend.Configuration
{
    public interface IOptionCache<TKey>
    {
        bool Get<TValue>(TKey key, out TValue value);

        void Set<TValue>(TKey key, TValue value);

        void Remove(TKey key);

        void Invalidate();

        void ClearExpired();
    }
}