using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Regard.Backend.Common.Utils
{
    public static class JsonUtils
    {
        private static readonly Lazy<JsonSerializer> Serializer = new Lazy<JsonSerializer>(JsonSerializer.CreateDefault);

        public static string Serialize(object value)
        {
            using var writer = new StringWriter();
            Serializer.Value.Serialize(writer, value);
            return writer.ToString();
        }

        public static T Deserialize<T>(string json)
        {
            using var reader = new StringReader(json);
            using var jsonReader = new JsonTextReader(reader);
            return Serializer.Value.Deserialize<T>(jsonReader);
        }
    }
}
