using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Regard.Frontend.Utils
{
    public static class NumberUtils
    {
        public static string ToMetric(this ulong number, int decimals = 0)
        {
            decimal x = Convert.ToDecimal(number);

            if (x > 1000000000)
                return (x / 1000000).ToString($"F{decimals}", CultureInfo.InvariantCulture) + "B";

            if (x > 1000000)
                return (x / 1000000).ToString($"F{decimals}", CultureInfo.InvariantCulture) + "M";

            else if (x > 1000)
                return (x / 1000).ToString($"F{decimals}", CultureInfo.InvariantCulture) + "k";

            else return number.ToString();
        }
    }
}
