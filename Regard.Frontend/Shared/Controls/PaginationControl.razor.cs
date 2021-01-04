using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public partial class PaginationControl
    {
        private int page;
        private int pageCount;

        [Parameter] public int PageCount 
        {
            get => pageCount;
            set
            {
                if (pageCount != value)
                {
                    pageCount = value;
                    StateHasChanged();
                }
            }
        }

        [Parameter] public int Page 
        {
            get => page;
            set
            {
                if (page != value)
                {
                    page = value;
                    PageChanged.InvokeAsync(value);
                    StateHasChanged();
                }
            }
        }

        [Parameter] public EventCallback<int> PageChanged { get; set; }

        [Parameter] public int CurrentPagesToDisplay { get; set; } = 5;

        private IEnumerable<int?> GetPagesToDisplay()
        {
            int leftmostPage = Math.Max(Page - (CurrentPagesToDisplay / 2), 1);
            int rightmostPage = Math.Min(Page + (CurrentPagesToDisplay / 2), PageCount - 2);

            // First page
            yield return 0;
            
            // Gap
            if (leftmostPage > 1)
                yield return null;

            // Show current pages
            for (int i = leftmostPage; i <= rightmostPage; i++)
                yield return i;

            // Gap
            if (rightmostPage < PageCount - 2)
                yield return null;

            // Last page
            if (PageCount > 1)
                yield return (PageCount - 1);
        }
    }
}
