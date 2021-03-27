using System;
using System.ComponentModel;
using System.Reflection;

namespace Regard.Frontend.Utils
{
    public static class EnumExtensions
    {
        public static string GetDescription<TEnum>(this TEnum @this) where TEnum : Enum
        {
            string name = Enum.GetName(typeof(TEnum), @this);
            if (name == null)
                return null;

            FieldInfo fi = typeof(TEnum).GetField(name);
            if (fi == null)
                return null;

            var description = (DescriptionAttribute)fi.GetCustomAttribute(typeof(DescriptionAttribute), false);
            return description?.Description;
        }
    }
}
