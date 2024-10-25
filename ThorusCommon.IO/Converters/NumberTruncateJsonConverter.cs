using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThorusCommon.IO.Converters
{
    public class NumberTruncateJsonConverter<T> : JsonConverter<T>
    {
        public const int DecimalPlaces = 2;

        private readonly int _decimalPlaces;

        public NumberTruncateJsonConverter() : this(DecimalPlaces)
        {
        }

        public NumberTruncateJsonConverter(int decimalPlaces)
        {
            _decimalPlaces = Math.Min(8, Math.Max(0, decimalPlaces));
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                return (T)(object)reader.GetDouble();
            }
            catch
            {
                // Ignore the exception
            }

            return default;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var val = (double)(object)value;
            writer.WriteRawValue((val).ToString($"F{_decimalPlaces}", CultureInfo.InvariantCulture));
        }
    }
}
