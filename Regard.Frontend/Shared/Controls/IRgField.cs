using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public interface IRgField
    {
        string Id { get; set; }

        string Label { get; set; }

        bool Required { get; set; }

        RenderFragment HelpText { get; set; }

        int LabelWidth { get; set; }
    }
}
