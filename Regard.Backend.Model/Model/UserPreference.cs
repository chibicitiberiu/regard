using Regard.Backend.Model;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Regard.Backend.Model
{
    public class UserPreference : IPreference
    {
        public string Key { get; set; }

        public string Value { get; set; }

        [NotNull]
        public UserAccount User { get; set; }
        
        [NotNull]
        public string UserId { get; set; }
    }
}
