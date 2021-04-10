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
        public RgSimpleInputSelect()
        {
            KeyFunc = x => x;
            DisplayTextFunc = KeyToString; 
            SetDefaultItemsSource();
        }

        private string KeyToString(TKey key)
        {
            if (key == null)
                return "(unset)";

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
            }
            else if (typeof(TKey) == typeof(bool?))
            {
                ItemsSource = new bool?[] { null, true, false }.Cast<TKey>();
            }
        }
    }
}
