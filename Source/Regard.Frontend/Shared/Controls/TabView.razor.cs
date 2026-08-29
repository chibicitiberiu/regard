using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public partial class TabView
    {
        private readonly List<TabPage> tabs = new List<TabPage>();
        private TabPage selectedPage = null;

        public TabPage SelectedPage 
        { 
            get => selectedPage;
            set {
                if (selectedPage != value)
                {
                    selectedPage = value;
                    ActivePageChanged.InvokeAsync(this);
                }
            }
        }

        public IReadOnlyList<TabPage> Tabs => tabs;

        [Parameter]
        public EventCallback ActivePageChanged { get; set; }

        /// <summary>Name of the tab to select initially (matches TabPage.Name); null selects the first tab.</summary>
        [Parameter]
        public string ActiveTab { get; set; }

        [Parameter]
        public RenderFragment ChildContent { get; set; }

        private string GetSelectedClass(TabPage tab)
        {
            return (tab == SelectedPage) ? "tab-header-selected" : "";
        }

        private void OnTabHeaderClicked(TabPage tab)
        {
            SelectedPage = tab;
        }

        public void AddTab(TabPage tab)
        {
            tabs.Add(tab);

            // An explicit ActiveTab match wins over the default (first tab), regardless of registration order.
            if (ActiveTab != null && tab.Name == ActiveTab)
                SelectedPage = tab;
            else if (SelectedPage == null)
                SelectedPage = tab;

            StateHasChanged();
        }
    }
}
