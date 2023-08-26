using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.Utils
{
    public class IntToBooleanConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is int intValue)
            {
                bool boolValue = intValue == 1;
                serializer.Serialize(writer, boolValue);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return false;
            }

            return Convert.ToBoolean(reader.Value);
        }

        public override bool CanRead => false;
        public override bool CanConvert(Type objectType) => objectType == typeof(int);
    }
}
