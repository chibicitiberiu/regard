using Microsoft.AspNetCore.Components;
using Regard.Frontend.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public class RgSimpleInputSelect<TKey> : RgInputSelect<TKey, TKey>
    {
        /// <summary>
        /// When set, the "inherit" (null) option reads "Default (&lt;this&gt;)" — the value the field
        /// would resolve to when left unset — instead of the bare "(unset)".
        /// </summary>
        [Parameter] public string DefaultValueText { get; set; }

        public RgSimpleInputSelect()
        {
            KeyFunc = x => x;
            DisplayTextFunc = KeyToString;
            SetDefaultItemsSource();
        }

        private string KeyToString(TKey key)
        {
            if (key == null)
                return string.IsNullOrEmpty(DefaultValueText) ? "(unset)" : $"Default ({DefaultValueText})";

            if (typeof(TKey).IsEnum || typeof(TKey).IsNullableEnum())
                return CamelCaseAddSpaces(key.ToString());

            return key.ToString();
        }

        private string CamelCaseAddSpaces(string str)
        {
            return Regex.Replace(str, @"(\B[A-Z]+?(?=[A-Z][^A-Z])|\B[A-Z]+?(?=[^A-Z]))", " $1");
        }

        private void SetDefaultItemsSource()
        {
            if (typeof(TKey).IsEnum)
            {
                ItemsSource = Enum.GetValues(typeof(TKey)).Cast<TKey>();
            }
            else if (typeof(TKey).IsNullableEnum())
            {
                var enumDataType = typeof(TKey).GetGenericArguments()[0];
                var itemsSource = new List<TKey> { default };

                foreach (var enumValue in Enum.GetValues(enumDataType))
                {
                    var constructor = typeof(TKey).GetConstructor(new[] { enumDataType });
                    var nullableValue = constructor.Invoke(new[] { enumValue });
                    itemsSource.Add((TKey)nullableValue);
                }

                ItemsSource = itemsSource;
                // The null "(unset)" entry is the default option, so the hidden placeholder is redundant.
                ShowDefaultOption = false;
            }
            else if (typeof(TKey) == typeof(bool?))
            {
                ItemsSource = new bool?[] { null, true, false }.Cast<TKey>();
                ShowDefaultOption = false;
            }
        }
    }
}
