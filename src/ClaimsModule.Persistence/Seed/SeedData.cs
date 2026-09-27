using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.ReferenceData;
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

    /// <summary>
    /// FRS §5.5 exactly (the first five rows), plus three long-dated policies so the demo always has
    /// in-force policies whatever the review date (D-34). Status per D-33.
    /// </summary>
    public static readonly IReadOnlyList<SeedPolicy> Policies =
    [
        new(new Guid("8281fb25-5cd6-4f26-b82e-72695950beb3"), "POL-2024-001001", "Meridian Transport LLC", new(2024, 1, 1), new(2026, 12, 31), PolicyStatus.Active, ["Vehicle", "Cargo"]),
        new(new Guid("ae6abe22-49fa-4c63-950a-0e1022f61882"), "POL-2024-001002", "Harborview Properties Inc", new(2024, 6, 1), new(2026, 5, 31), PolicyStatus.Active, ["Property", "Liability"]),
        new(new Guid("0276497d-199d-4d7a-b166-de5643601ae6"), "POL-2025-002001", "Coastal Builders Group", new(2025, 3, 1), new(2027, 2, 28), PolicyStatus.Active, ["Property", "Equipment"]),
        new(new Guid("8ab0d13d-392d-47d9-91b6-d6c2088f2de0"), "POL-2025-002002", "Stanton Medical Group", new(2025, 1, 1), new(2026, 12, 31), PolicyStatus.Active, ["Liability", "Vehicle"]),
        new(new Guid("5fd883ac-3109-4539-af13-c387cef26942"), "POL-2023-000099", "Archived Corp", new(2020, 1, 1), new(2021, 12, 31), PolicyStatus.Expired, ["Property"]),

        // D-34 additions.
        new(new Guid("07516340-0391-4182-8b0d-4f507fad688b"), "POL-2025-003001", "Northwind Logistics Ltd", new(2025, 1, 1), new(2030, 12, 31), PolicyStatus.Active, ["Vehicle", "Cargo", "Liability"]),
        new(new Guid("95713a24-9905-4b0e-9d48-7518bb34be10"), "POL-2025-003002", "Summit Retail Holdings", new(2025, 1, 1), new(2030, 12, 31), PolicyStatus.Active, ["Property", "Liability"]),
        new(new Guid("07a6b67a-e72d-4535-bd39-d299b55e434f"), "POL-2025-003003", "Keystone Manufacturing Co", new(2025, 1, 1), new(2030, 12, 31), PolicyStatus.Active, ["Property", "Equipment"]),
    ];

    /// <summary>FRS §5.6 exactly, all active, in the FRS order.</summary>
    public static readonly IReadOnlyList<SeedCauseOfLossCode> CauseOfLossCodes =
    [
        new(new Guid("997a4c70-15b9-49b4-9b50-210df0527a32"), "COL-FIRE", "Fire", PerilCategory.Property, "Structure or contents fire"),
        new(new Guid("1a56ed19-8a6f-40f7-9bd4-f0e30d94b90f"), "COL-FLOOD", "Flood", PerilCategory.Weather, "Water intrusion from external source"),
        new(new Guid("f6a7ba5c-0632-4907-bdc7-3d05d5705ee4"), "COL-THEFT", "Theft", PerilCategory.Crime, "Burglary, robbery, larceny"),
        new(new Guid("4091c278-fbd1-4744-964e-910c77ba8430"), "COL-VEH-COL", "Vehicle Collision", PerilCategory.Auto, "Impact with another vehicle or object"),
        new(new Guid("84395762-ac14-4e68-bdad-afe906824b9b"), "COL-VEH-COMP", "Vehicle Comprehensive", PerilCategory.Auto, "Weather, theft, vandalism"),
        new(new Guid("8d4d2ac8-35d2-4a3c-b034-1a690f2f9641"), "COL-LIAB", "Third Party Liability", PerilCategory.Liability, "Bodily injury or property damage to third party"),
        new(new Guid("3a6d297a-8cef-4352-9125-a7067f9244c0"), "COL-EQUIP", "Equipment Breakdown", PerilCategory.Equipment, "Mechanical or electrical failure"),
        new(new Guid("997fa883-2560-4c33-8fb4-53aec3583554"), "COL-WIND", "Wind / Storm", PerilCategory.Weather, "Wind, hail, hurricane"),
        new(new Guid("98e9408f-6ee8-47b5-8319-3a240c46d516"), "COL-INJURY", "Bodily Injury", PerilCategory.Liability, "Personal injury to claimant"),
        new(new Guid("e597f02e-2d9f-4687-91b4-c7a02bcc4ef3"), "COL-OTHER", "Other / Unknown", PerilCategory.General, "Catch-all for uncategorised losses"),
    ];

    /// <summary>
    /// Fixed ids for the FRS §4.2 transition rows. The rows themselves come from
    /// <see cref="ClaimStatusTransition.FrsDefaults"/>, so the table is declared once.
    /// </summary>
    public static readonly IReadOnlyDictionary<(ClaimStatus From, ClaimStatus To), Guid> TransitionIds =
        new Dictionary<(ClaimStatus From, ClaimStatus To), Guid>
        {
            [(ClaimStatus.Draft, ClaimStatus.Open)] = new("38a97a48-271f-4214-9db7-024d2e245127"),
            [(ClaimStatus.Open, ClaimStatus.UnderInvestigation)] = new("b99eafea-c240-4a77-bd3f-7c7e4ad40679"),
            [(ClaimStatus.Open, ClaimStatus.PendingPayment)] = new("761b6cc3-850c-40d7-980e-a6bc8614946a"),
            [(ClaimStatus.Open, ClaimStatus.Closed)] = new("b415c0c6-5358-44e1-9bad-f1d2ff3a43f3"),
            [(ClaimStatus.Open, ClaimStatus.Withdrawn)] = new("ffe17aee-588f-469e-bedb-f4ea8903782f"),
            [(ClaimStatus.UnderInvestigation, ClaimStatus.Open)] = new("2543c201-57c1-4f59-bb00-4a7fcc32f36a"),
            [(ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment)] = new("c030fc17-dcb5-4848-9253-3364e3a7807e"),
            [(ClaimStatus.UnderInvestigation, ClaimStatus.Closed)] = new("2849f2d3-700e-4713-92ad-8f12a457b94c"),
            [(ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn)] = new("f7ff1102-bd41-4064-b11f-778d0181f00a"),
            [(ClaimStatus.PendingPayment, ClaimStatus.Closed)] = new("566f5ccb-82dd-4b28-a2bb-9210ce42b047"),
            [(ClaimStatus.Closed, ClaimStatus.Reopened)] = new("28fbe2ff-696e-4100-a73e-66465a356dd1"),
            [(ClaimStatus.Reopened, ClaimStatus.Open)] = new("8cad141b-381d-4ffd-918c-83306c42a6d3"),
        };

    internal sealed record SeedUser(Guid Id, string Username, string DisplayName, UserRole Role);

    internal sealed record SeedPolicy(
        Guid Id, string PolicyNumber, string ClientName, DateOnly EffectiveDate, DateOnly ExpirationDate, PolicyStatus Status, string[] CoverageTypes);

    internal sealed record SeedCauseOfLossCode(Guid Id, string Code, string Name, PerilCategory PerilCategory, string Notes);
}
