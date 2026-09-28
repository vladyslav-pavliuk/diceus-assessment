using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaimsModule.API.Json;

// Model binding only binds; FluentValidation in the MediatR pipeline decides what is valid and words the
// message (CLAUDE.md rule 2, FRS §8). If System.Text.Json rejected an unknown enum name or a malformed
// date itself, the client would get a serializer message ("The JSON value could not be converted…")
// instead of "Invalid reserve component type." or "Loss date is required.". These converters turn such
// values into "undefined" instead, and the validator reports them (D-40). Output is unchanged.

/// <summary>
/// Reads enums by name, case-insensitively. Anything else (an unknown name, a number, another token)
/// becomes the value 0, which no domain enum defines (they all start at 1), so IsInEnum() rejects it.
/// Writes the name.
/// </summary>
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

/// <summary>
/// Reads an optional date-time leniently: an empty or unparseable string becomes null, so the validator
/// answers "Loss date is required." (FRS §8 "Must be a valid date"). A value without an offset is taken
/// as UTC (D-32). Writes the standard ISO 8601 form.
/// </summary>
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
