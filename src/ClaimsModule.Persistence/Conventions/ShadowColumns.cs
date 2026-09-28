namespace ClaimsModule.Persistence.Conventions;

/// <summary>Convention columns kept as EF Core shadow properties, out of the domain classes.</summary>
public static class ShadowColumns
{
    public const string CreatedAt = nameof(CreatedAt);
    public const string UpdatedAt = nameof(UpdatedAt);
    public const string UserCreated = nameof(UserCreated);
    public const string UserModified = nameof(UserModified);
    public const string IsDeleted = nameof(IsDeleted);
    public const string DeletedAt = nameof(DeletedAt);

    /// <summary>A real property on User; a shadow property elsewhere.</summary>
    public const string OrganisationId = nameof(OrganisationId);

    /// <summary>On Claims and ClaimReserveComponents only.</summary>
    public const string RowVer = nameof(RowVer);
}
