using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.Utils
{
    public static class ObjectExtensions
    {
        public static void VerifyNotNull(this object @this, string messageIfNull = null)
        {
            if (@this == null)
                throw new ArgumentNullException(messageIfNull ?? "Object was null.");
        }
    }
}
