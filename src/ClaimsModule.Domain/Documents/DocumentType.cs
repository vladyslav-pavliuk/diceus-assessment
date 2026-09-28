namespace ClaimsModule.Domain.Documents;

/// <summary>FRS §9.7's examples, taken as the complete list (D-28).</summary>
public enum DocumentType
{
    PoliceReport = 1,
    MedicalReport,
    Invoice,
    Other,
}
