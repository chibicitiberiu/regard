using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace YoutubeDLWrapper
{
    class EnumDescriptionConverter<T> : JsonConverter<T> where T : IComparable, IFormattable, IConvertible
    {
        private readonly Dictionary<string, T> ValueDict = new Dictionary<string, T>();

        public EnumDescriptionConverter()
        {
            foreach (var field in typeof(T).GetFields())
            {
                var description = (DescriptionAttribute)field.GetCustomAttribute(typeof(DescriptionAttribute), false);
                if (description != null)
                    ValueDict.Add(description.Description, (T)field.GetValue(default(T)));
            }
        }

        public override T ReadJson(JsonReader reader, Type objectType, [AllowNull] T existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
                return ValueDict[(string)reader.Value];

            throw new Exception("Unsupported token type");
        }

        public override void WriteJson(JsonWriter writer, [AllowNull] T value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
