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

        /// <summary>
        /// Formats a duration in seconds as a compact "m:ss" (or "h:mm:ss" when at least one hour).
        /// </summary>
        public static string ToDurationString(this int seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1
                ? ((int)t.TotalHours).ToString() + t.ToString(@"\:mm\:ss", CultureInfo.InvariantCulture)
                : ((int)t.TotalMinutes).ToString() + t.ToString(@"\:ss", CultureInfo.InvariantCulture);
        }
    }
}
