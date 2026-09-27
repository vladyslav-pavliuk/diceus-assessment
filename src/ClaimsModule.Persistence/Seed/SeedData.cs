using ClaimsModule.Domain.Users;

namespace ClaimsModule.Persistence.Seed;

/// <summary>
/// Fixed identifiers and values for HasData seeding (FRS §15.4: seed data via HasData or
/// migrations, never startup code). GUID literals are allowed here and nowhere in application
/// logic (FRS §15.4).
/// </summary>
internal static class SeedData
{
    /// <summary>Constant CreatedAt for seeded rows, so the migration is deterministic.</summary>
    public static readonly DateTimeOffset SeededAt = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);

    public static readonly Guid OrganisationId = new("b49c0515-77a9-49ff-be76-2a1fcfdc144f");

    public const string OrganisationName = "Demo Insurance Company";

    /// <summary>Two users per role, so self-approval and cross-approval can be demonstrated for every tier (D-16).</summary>
    public static readonly IReadOnlyList<SeedUser> Users =
    [
        new(new Guid("415b998a-b77b-4d44-9a25-8f506dfd17e4"), "handler.alex", "Alex Carter", UserRole.Handler),
        new(new Guid("6b870c3c-4333-441b-8c57-88f277f61fca"), "handler.blake", "Blake Jordan", UserRole.Handler),
        new(new Guid("7b2e441e-dc0b-42fc-8a8d-0b7e1308e1b8"), "supervisor.casey", "Casey Morgan", UserRole.Supervisor),
        new(new Guid("cbbca2e1-4bd9-4714-9f79-735500e13c5d"), "supervisor.drew", "Drew Taylor", UserRole.Supervisor),
        new(new Guid("c65a96eb-f89d-4a31-be62-eb79ea9343cf"), "manager.emery", "Emery Brooks", UserRole.Manager),
        new(new Guid("3a97300c-8566-47a8-8e24-87ce3dae8759"), "manager.finley", "Finley Hayes", UserRole.Manager),
    ];

    internal sealed record SeedUser(Guid Id, string Username, string DisplayName, UserRole Role);
}
