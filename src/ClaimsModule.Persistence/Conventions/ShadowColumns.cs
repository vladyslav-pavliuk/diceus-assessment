namespace ClaimsModule.Persistence.Conventions;

/// <summary>
/// Names of the FRS §15.1 audit and soft-delete columns. They are EF Core shadow properties:
/// present in every table and in queries, absent from the domain classes.
/// </summary>
public static class ShadowColumns
{
    public const string CreatedAt = nameof(CreatedAt);
    public const string UpdatedAt = nameof(UpdatedAt);
    public const string UserCreated = nameof(UserCreated);
    public const string UserModified = nameof(UserModified);
    public const string IsDeleted = nameof(IsDeleted);
    public const string DeletedAt = nameof(DeletedAt);
}
