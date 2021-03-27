using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Regard.Frontend.Popper
{
    public class OffsetModifier : Modifier
    {
        public override string Name => "offset";

        [JsonIgnore]
        public double Skidding { get; set; }

        [JsonIgnore]
        public double Distance { get; set; }

        public override object Options => new
        {
            Offset = new[] { Skidding, Distance }
        };
    }
}
