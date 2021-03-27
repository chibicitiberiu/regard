using Regard.Frontend.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Regard.Frontend.Popper
{
    public enum Placement
    {
        [Description("auto")] Auto,
        [Description("auto-start")] AutoStart,
        [Description("auto-end")] AutoEnd,
        [Description("top")] Top,
        [Description("top-start")] TopStart,
        [Description("top-end")] TopEnd,
        [Description("bottom")] Bottom,
        [Description("bottom-start")] BottomStart,
        [Description("bottom-end")] BottomEnd,
        [Description("right")] Right,
        [Description("right-start")] RightStart,
        [Description("right-end")] RightEnd,
        [Description("left")] Left,
        [Description("left-start")] LeftStart,
        [Description("left-end")] LeftEnd
    }

    public enum PositioningStrategy
    {
        [Description("absolute")] Absolute,
        [Description("fixed")] Fixed,
    }

    public class Options
    {
        [JsonConverter(typeof(EnumDescriptionConverter<Placement>))]
        [JsonPropertyName("placement")]
        public Placement Placement { get; set; }

        [JsonConverter(typeof(EnumDescriptionConverter<PositioningStrategy>))]
        [JsonPropertyName("positioningStrategy")]
        public PositioningStrategy PositioningStrategy { get; set; }

        [JsonPropertyName("modifiers")]
        public Modifier[] Modifiers { get; set; }
    }
}
