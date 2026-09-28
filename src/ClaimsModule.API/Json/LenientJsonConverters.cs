using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaimsModule.API.Json;

// These converters turn an unknown enum name or a malformed date into "undefined", so the validator reports it with
// the FRS §8 wording instead of a serializer message (D-40). Output is unchanged.

/// <summary>Anything but a known name becomes 0, which no domain enum defines, so IsInEnum() rejects it.</summary>
internal sealed class LenientEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(LenientEnumConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class LenientEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                reader.Skip();
                return default;
            }

            var text = reader.GetString();

            // Enum.TryParse also accepts numeric strings ("2"); only names are part of the contract.
            return !string.IsNullOrEmpty(text)
                && !char.IsDigit(text[0]) && text[0] != '-' && text[0] != '+'
                && Enum.TryParse<TEnum>(text, ignoreCase: true, out var value)
                && Enum.IsDefined(value)
                    ? value
                    : default;
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}

/// <summary>An unparseable value becomes null, which the validator reports. A value without an offset is UTC (D-32).</summary>
internal sealed class LenientNullableDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }

        return DateTimeOffset.TryParse(
            reader.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
            out var value)
            ? value
            : null;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is { } dateTime)
        {
            writer.WriteStringValue(dateTime);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
