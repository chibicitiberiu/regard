using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public partial class ListView<Model>
    {
        private IEnumerable<Model> itemsSource = null;

        [Parameter]
        public IEnumerable<Model> ItemsSource 
        {
            get => itemsSource; 
            set
            {
                if (itemsSource is INotifyCollectionChanged oldObsCollection)
                    oldObsCollection.CollectionChanged -= OnCollectionChanged;

                itemsSource = value;

                if (itemsSource is INotifyCollectionChanged newObsCollection)
                    newObsCollection.CollectionChanged += OnCollectionChanged;
            }
        }

        [Parameter]
        public RenderFragment<Model> ItemTemplate { get; set; }

        [Parameter]
        public string Class { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            StateHasChanged();
        }
    }
}
