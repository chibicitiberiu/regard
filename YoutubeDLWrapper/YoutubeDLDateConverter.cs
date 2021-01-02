using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace YoutubeDLWrapper
{
    internal class YoutubeDLDateConverter : IsoDateTimeConverter
    {
        public YoutubeDLDateConverter()
        {
            DateTimeFormat = "yyyyMMdd";
        }
    }
}
