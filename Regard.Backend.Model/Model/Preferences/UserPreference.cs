using System.ComponentModel.DataAnnotations;

namespace Regard.Backend.Model
{
    public class UserPreference : IPreference
    {
        public string Key { get; set; }

        public string Value { get; set; }

        [Required]
        public UserAccount User { get; set; }
        
        [Required]
        public string UserId { get; set; }
    }
}
