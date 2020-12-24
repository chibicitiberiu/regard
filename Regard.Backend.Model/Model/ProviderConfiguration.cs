using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Model
{
    public class ProviderConfiguration
    {
        [Key, MaxLength(60)]
        public string ProviderId { get; set; }

        [NotNull]
        public string Configuration { get; set; }
    }
}
