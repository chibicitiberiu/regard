using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Backend.Common.Utils
{
    public static class ProviderHelpers
    {
        public static float? CalculateRating(long? likes, long? dislikes)
        {
            if (likes.HasValue && dislikes.HasValue)
            {
                long total = likes.Value + dislikes.Value;
                if (total > 0)
                    return Convert.ToSingle(likes.Value) / Convert.ToSingle(total);
            }

            return null;
        }
    }
}
