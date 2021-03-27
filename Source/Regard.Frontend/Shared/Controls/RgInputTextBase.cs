using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public enum BindEvent { OnChange, OnInput }

    public class RgInputTextBase : InputBase<string>, IRgField
    {
        [Parameter] public string Id { get; set; }

        [Parameter] public string Label { get; set; }

        [Parameter] public bool Required { get; set; }

        [Parameter] public BindEvent BindOn { get; set; }
        
        [Parameter] public RenderFragment HelpText { get; set; }

        [Parameter] public int LabelWidth { get; set; } = 3;

        [Parameter] public Expression<Func<string>> ValidationFor { get; set; }

        protected string BindOnAsString 
        {
            get
            {
                return BindOn switch
                {
                    BindEvent.OnChange => "onchange",
                    BindEvent.OnInput => "oninput",
                    _ => "onchange",
                };
            } 
        }

        protected override bool TryParseValueFromString(string value, out string result, out string validationErrorMessage)
        {
            result = value;
            validationErrorMessage = null;
            return true;
        }
    }
}
