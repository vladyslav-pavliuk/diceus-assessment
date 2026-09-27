namespace ClaimsModule.Persistence.Conventions;

/// <summary>
/// Names of the FRS §15.1 convention columns. They are EF Core shadow properties: present in the
/// tables and in queries, absent from the domain classes (D-38 item 4).
/// </summary>
public static class ShadowColumns
{
    public const string CreatedAt = nameof(CreatedAt);
    public const string UpdatedAt = nameof(UpdatedAt);
    public const string UserCreated = nameof(UserCreated);
    public const string UserModified = nameof(UserModified);
    public const string IsDeleted = nameof(IsDeleted);
    public const string DeletedAt = nameof(DeletedAt);

    /// <summary>Tenant column (FRS §15.1, D-12). A real property on User; a shadow property elsewhere.</summary>
    public const string OrganisationId = nameof(OrganisationId);

    /// <summary>ROWVERSION on the aggregate roots Claims and ClaimReserveComponents (FRS §15.1).</summary>
    public const string RowVer = nameof(RowVer);
}
