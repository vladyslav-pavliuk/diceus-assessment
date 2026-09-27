namespace ClaimsModule.Domain.Documents;

/// <summary>FRS §9.7 lists these as examples; they are taken as the complete list (D-28, ASSUMPTION).</summary>
public enum DocumentType
{
    PoliceReport = 1,
    MedicalReport,
    Invoice,
    Other,
}
