using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Domain.Tests;

/// <summary>Builders for claims in a known state, so each test states only what it is about.</summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    public static readonly Actor Handler = new(Guid.NewGuid(), UserRole.Handler);
    public static readonly Actor OtherHandler = new(Guid.NewGuid(), UserRole.Handler);
    public static readonly Actor Supervisor = new(Guid.NewGuid(), UserRole.Supervisor);
    public static readonly Actor OtherSupervisor = new(Guid.NewGuid(), UserRole.Supervisor);
    public static readonly Actor Manager = new(Guid.NewGuid(), UserRole.Manager);
    public static readonly Actor OtherManager = new(Guid.NewGuid(), UserRole.Manager);

    public static readonly StatusTransitionTable Transitions = new(ClaimStatusTransition.FrsDefaults());

    public static Policy InForcePolicy() =>
        Policy.Create("POL-TEST-000001", "Test Client Ltd", new DateOnly(2025, 1, 1), new DateOnly(2027, 12, 31), PolicyStatus.Active, ["Property"]);

    public static Policy PolicyCovering(DateOnly effective, DateOnly expiration) =>
        Policy.Create("POL-TEST-000002", "Other Client Ltd", effective, expiration, PolicyStatus.Active, ["Vehicle"]);

    public static LossEventDetails Loss(DateTimeOffset? lossDate = null, string description = "Rear-end collision at a junction on the A1.") =>
        new(lossDate ?? Now.AddDays(-2), description, "A1, junction 5", "COL-VEH-COL", 12_500m, null);

    public static PartyDetails Claimant(string firstName = "Jane") =>
        new(PartyRole.Claimant, PartyType.Person, firstName, "Doe", null, "jane.doe@example.com", "+44 20 7946 0000", null);

    public static PartyDetails Witness() =>
        new(PartyRole.Witness, PartyType.Person, "John", "Smith", null, null, null, null);

    public static RiskObjectDetails Vehicle(bool isPrimary = false) =>
        new(AssetType.Vehicle, "2022 Ford Transit van", "Rear bumper and tailgate", "WF0XXXTTGXKA12345", isPrimary);

    /// <summary>A Draft claim. By default it has an in-force policy, one claimant and one risk object.</summary>
    public static Claim DraftClaim(
        bool withPolicy = true,
        Policy? policy = null,
        bool withClaimant = true,
        bool withRiskObject = true,
        LossEventDetails? loss = null,
        Actor? creator = null)
    {
        var claim = Claim.Create(
            ClaimNumber.Create(2026, 1),
            withPolicy ? policy ?? InForcePolicy() : null,
            loss ?? Loss(),
            ClaimSeverity.Standard,
            withClaimant ? [Claimant()] : [],
            withRiskObject ? [Vehicle()] : [],
            creator ?? Handler,
            Now);

        claim.ClearDomainEvents();
        return claim;
    }

    public static Claim OpenClaim(bool withPolicy = true)
    {
        var claim = DraftClaim(withPolicy);
        claim.ChangeStatus(ClaimStatus.Open, null, null, Handler, Transitions, Now);
        claim.ClearDomainEvents();
        return claim;
    }

    /// <summary>A claim in any status reachable through the FRS §4.2 table (Reopened is never at rest).</summary>
    public static Claim ClaimIn(ClaimStatus status)
    {
        switch (status)
        {
            case ClaimStatus.Draft:
                return DraftClaim();
            case ClaimStatus.Open:
                return OpenClaim();
        }

        var claim = OpenClaim();
        switch (status)
        {
            case ClaimStatus.UnderInvestigation:
                claim.ChangeStatus(ClaimStatus.UnderInvestigation, null, null, Handler, Transitions, Now);
                break;
            case ClaimStatus.PendingPayment:
                claim.SubmitReserveTransaction(ReserveComponentType.Expense, null, 1_000m, "Assessor fee", Handler, Now);
                claim.ChangeStatus(ClaimStatus.PendingPayment, null, null, Handler, Transitions, Now);
                break;
            case ClaimStatus.Closed:
                claim.ChangeStatus(ClaimStatus.Closed, "Settled", null, Handler, Transitions, Now);
                break;
            case ClaimStatus.Withdrawn:
                claim.ChangeStatus(ClaimStatus.Withdrawn, "Claimant withdrew", null, Handler, Transitions, Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Not a resting status.");
        }

        claim.ClearDomainEvents();
        return claim;
    }

    public static ReserveTransaction Submit(Claim claim, ReserveComponentType component, decimal? amount, Actor? submitter = null, ReserveTransactionType? type = null) =>
        claim.SubmitReserveTransaction(component, type, amount, "Reserve review", submitter ?? Handler, Now).Transaction;

    /// <summary>Asserts that the action fails with a 422-style violation and returns its errors.</summary>
    public static IReadOnlyDictionary<string, string[]> ShouldViolate(Action action) =>
        Should.Throw<BusinessRuleViolationException>(action).Errors;

    public static T SingleEvent<T>(this Claim claim) where T : IDomainEvent =>
        claim.DomainEvents.OfType<T>().ShouldHaveSingleItem();
}

internal static class TestUsers
{
    public static User Active(UserRole role) =>
        User.Create(Guid.NewGuid(), $"{role.ToCode()}.{Guid.NewGuid():N}", $"Test {role}", role);
}
