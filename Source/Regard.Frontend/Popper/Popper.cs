using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Popper
{
    public class Popper
    {
        private readonly IJSRuntime jSRuntime;

        public Popper(IJSRuntime jSRuntime)
        {
            this.jSRuntime = jSRuntime;
        }

        public async Task Create(ElementReference @object, ElementReference tooltip, Options options)
        {
            await jSRuntime.InvokeVoidAsync("PopperWrapper", @object, tooltip, options);
        }
    }
}
