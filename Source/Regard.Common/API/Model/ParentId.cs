using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Model
{
    public struct ParentId
    {
        public int? Value { get; set; }

        public ParentId(int? value)
        {
            Value = value;
        }

        public static implicit operator ParentId(int? value) => new ParentId(value);
        public static implicit operator int?(ParentId value) => value.Value;
        public static implicit operator bool(ParentId value) => value.Value.HasValue;
    }
}
