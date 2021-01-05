using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.Utils
{
    public static class IListExtensions
    {
        public static void AddRange(this IList list, IEnumerable items)
        {
            if (list == null)
                throw new ArgumentNullException("list");
            
            if (items == null)
                throw new ArgumentNullException("items");

            foreach (var item in items)
                list.Add(item);
        }
    }
}
