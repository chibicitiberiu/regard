using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Utils
{
    public static class CssUtils
    {
        public static string BoolToVisible(bool value)
        {
            return value ? "" : "hidden";
        }

        public static string BoolToClass(bool value, string cssClass)
        {
            return value ? cssClass : string.Empty;
        }
    }
}
