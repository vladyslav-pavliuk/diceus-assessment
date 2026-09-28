using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaimsModule.Application.Common.Auditing;

/// <summary>
/// How audit rows format their values, shared by every writer of ClaimAuditLog: OldValue/NewValue are
/// camelCase JSON objects with enum names (FRS §9.8, D-40 item 14), and amounts in descriptions read "$1,234.50".
/// </summary>
internal static class AuditValues
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string? ToJson(object? value) => value is null ? null : JsonSerializer.Serialize(value, Json);

    public static string Money(decimal amount) => amount.ToString("$#,##0.00;-$#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>Audit RelatedEntityType of a ReserveHistory row.</summary>
    public const string ReserveTransactionEntity = "ReserveTransaction";
}
