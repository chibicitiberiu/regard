using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.Utils
{
    public static class StringExtensions
    {
        public static string Truncate(this string @this, int maxLength)
        {
            return (@this.Length > maxLength) ? @this.Substring(0, maxLength) : @this;
        }
    }
}
