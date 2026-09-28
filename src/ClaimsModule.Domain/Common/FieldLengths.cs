namespace ClaimsModule.Domain.Common;

/// <summary>
/// NVARCHAR lengths from FRS §9, shared by the EF columns and the validators, so an over-long value
/// is a 422 rather than a SQL truncation error.
/// </summary>
public static class FieldLengths
{
    /// <summary>Status, type and cause codes.</summary>
    public const int ShortCode = 50;

    /// <summary>Display, client, company and file names.</summary>
    public const int Name = 255;

    public const int PersonName = 100;

    public const int Phone = 50;

    public const int Email = 255;

    public const int LossLocation = 500;

    public const int PoliceReportNumber = 100;

    public const int AssetDescription = 500;

    public const int AssetReference = 255;

    /// <summary>Closure, change and override reasons, and issue resolution notes.</summary>
    public const int Reason = 500;
}
