using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Tapeti.Default
{
    /// <summary>
    /// Converts an <see cref="T:System.Enum" /> to and from its name string value. If an unknown string value is encountered
    /// it will translate to 0xDEADBEEF (-559038737) so it can be gracefully handled.
    /// If you copy this value as-is to another message and try to send it, this converter will throw an exception.
    /// This converter is far simpler than the default StringEnumConverter, it assumes both sides use the same
    /// enum and therefore skips the naming strategy.
    /// </summary>
    public class FallbackStringEnumConverter : JsonConverter
    {
        private readonly int invalidEnumValue;


        /// <inheritdoc />
        public FallbackStringEnumConverter()
        {
            unchecked { invalidEnumValue = (int)0xDEADBEEF; }
        }


        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if ((int) value == invalidEnumValue)
                throw new ArgumentException("Enum value was an unknown string value in an incoming message and can not be published in an outgoing message as-is");

            var outputValue = Enum.GetName(value.GetType(), value);
            writer.WriteValue(outputValue);
        }


        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var isNullable = IsNullableType(objectType);

            if (reader.TokenType == JsonToken.Null)
            {
                if (!isNullable)
                    throw new JsonSerializationException($"Cannot convert null value to {objectType}");

                return null;
            }

            var actualType = isNullable ? Nullable.GetUnderlyingType(objectType) : objectType;
            Debug.Assert(actualType != null, nameof(actualType) + " != null");

            if (reader.TokenType != JsonToken.String)
                throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing enum");

            var enumText = reader.Value?.ToString() ?? "";
            if (enumText == string.Empty && isNullable)
                return null;

            try
            {
                return Enum.Parse(actualType, enumText);
            }
            catch (ArgumentException)
            {
                return Enum.ToObject(actualType, invalidEnumValue);
            }
        }


        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            var actualType = IsNullableType(objectType) ? Nullable.GetUnderlyingType(objectType) : objectType;
            return actualType?.IsEnum ?? false;
        }


        private static bool IsNullableType(Type t)
        {
            if (t == null)
                throw new ArgumentNullException(nameof(t));

            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}
