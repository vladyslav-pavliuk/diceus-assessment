using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaimsModule.Application.Common.Auditing;

/// <summary>Shared audit formatting: camelCase JSON with enum names, and amounts as "$1,234.50".</summary>
internal static class AuditValues
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string? ToJson(object? value) => value is null ? null : JsonSerializer.Serialize(value, Json);

    public static string Money(decimal amount) => amount.ToString("$#,##0.00;-$#,##0.00", CultureInfo.InvariantCulture);

    public const string ReserveTransactionEntity = "ReserveTransaction";
}
