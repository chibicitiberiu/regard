using Regard.Backend.Model;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public interface IPreferencesManager
    {
        Task<TValue> Get<TValue>(PreferenceDefinition<TValue> pref, UserAccount user = null);
        Task Set<TValue>(PreferenceDefinition<TValue> pref, TValue value, UserAccount user = null);
    }
}