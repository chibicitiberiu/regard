using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public partial class TabPage
    {
        [Parameter]
        public RenderFragment Title { get; set; }

        /// <summary>Optional stable key so a tab can be selected via TabView.ActiveTab (e.g. a ?tab= link).</summary>
        [Parameter]
        public string Name { get; set; }

        [Parameter]
        public RenderFragment Content { get; set; }

        [CascadingParameter]
        public TabView Parent { get; set; }

        protected override void OnInitialized()
        {
            if (Parent == null)
                throw new ArgumentNullException(nameof(Parent), "TabPage must exist within a TabView");
            Parent.AddTab(this);
            base.OnInitialized();
        }
    }
}
