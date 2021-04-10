using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public partial class RgInputSelect<TModel, TKey>
    {
        private IEnumerable<TModel> itemsSource = null;
        private bool initialized = false;

        [Parameter] public string Id { get; set; }

        [Parameter] public string Label { get; set; }

        [Parameter] public bool Required { get; set; }

        [Parameter] public RenderFragment HelpText { get; set; }

        [Parameter] public int LabelWidth { get; set; } = 3;

        [Parameter] public Expression<Func<TModel>> ValidationFor { get; set; }

        [Parameter] public RenderFragment ChildContent { get; set; }

        [Parameter] public bool ShowDefaultOption { get; set; } = true;

        [Parameter] public IEnumerable<TModel> ItemsSource 
        { 
            get => itemsSource; 
            set
            {
                if (itemsSource != null && itemsSource is INotifyCollectionChanged oldObservableSource)
                    oldObservableSource.CollectionChanged -= ObservableSource_CollectionChanged;

                itemsSource = value;

                if (itemsSource is INotifyCollectionChanged observableSource)
                    observableSource.CollectionChanged += ObservableSource_CollectionChanged;

                if (initialized)
                    StateHasChanged();
            }
        }

        [Parameter] public Func<TModel, TKey> KeyFunc { get; set; }

        [Parameter] public Func<TModel, string> DisplayTextFunc { get; set; }

        private void ObservableSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Task.Run(StateHasChanged);
        }

        protected override bool TryParseValueFromString(string value, out TKey result, out string validationErrorMessage)
        {
            if (typeof(TKey) == typeof(string))
            {
                result = (TKey)(object)value;
                validationErrorMessage = null;

                return true;
            }
            else if (typeof(TKey) == typeof(int))
            {
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue);
                result = (TKey)(object)parsedValue;
                validationErrorMessage = null;

                return true;
            }
            else if (typeof(TKey) == typeof(int?))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
                    result = (TKey)(object)parsedValue;
                else
                    result = default;

                validationErrorMessage = null;
                return true;
            }
            else if (typeof(TKey) == typeof(Guid))
            {
                Guid.TryParse(value, out var parsedValue);
                result = (TKey)(object)parsedValue;
                validationErrorMessage = null;

                return true;
            }
            else if (typeof(TKey).IsEnum)
            {
                // There's no non-generic Enum.TryParse (https://github.com/dotnet/corefx/issues/692)
                try
                {
                    result = (TKey)Enum.Parse(typeof(TKey), value);
                    validationErrorMessage = null;

                    return true;
                }
                catch (ArgumentException)
                {
                    result = default;
                    validationErrorMessage = $"The {FieldIdentifier.FieldName} field is not valid.";

                    return false;
                }
            }
            else if (typeof(TKey) == typeof(bool?))
            {
                if (bool.TryParse(value, out var parsedValue))
                    result = (TKey)(object)parsedValue;
                else
                    result = default;

                validationErrorMessage = null;
                return true;
            }

            throw new InvalidOperationException($"{GetType()} does not support the type '{typeof(TKey)}'.");
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            initialized = true;
        }
    }
}
