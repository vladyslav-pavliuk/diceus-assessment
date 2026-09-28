namespace ClaimsModule.Domain.Common;

/// <summary>
/// Maximum lengths of the text the FRS §9 entities store (NVARCHAR(n)). One source for the EF column
/// sizes and the request validators, so an over-long value is a 422 with a message, never a SQL
/// truncation error (500).
/// </summary>
public static class FieldLengths
{
    /// <summary>FRS §15.1 "Short codes": status, type and cause codes.</summary>
    public const int ShortCode = 50;

    /// <summary>FRS §15.1 "Names": display, client, company and file names.</summary>
    public const int Name = 255;

    /// <summary>FRS §9.3 FirstName / LastName.</summary>
    public const int PersonName = 100;

    /// <summary>FRS §9.3 Phone.</summary>
    public const int Phone = 50;

    /// <summary>FRS §9.3 Email.</summary>
    public const int Email = 255;

    /// <summary>FRS §9.2 LossLocation.</summary>
    public const int LossLocation = 500;

    /// <summary>FRS §9.2 PoliceReportNumber.</summary>
    public const int PoliceReportNumber = 100;

    /// <summary>FRS §9.4 AssetDescription.</summary>
    public const int AssetDescription = 500;

    /// <summary>FRS §9.4 AssetReference.</summary>
    public const int AssetReference = 255;

    /// <summary>Reasons and notes stored in NVARCHAR(500): closure reason, change reason (FRS §9.6), issue resolution note, override reason.</summary>
    public const int Reason = 500;
}
