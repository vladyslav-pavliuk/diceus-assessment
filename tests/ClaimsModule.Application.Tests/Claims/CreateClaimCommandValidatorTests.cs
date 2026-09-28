using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Claims.Inputs;
using ClaimsModule.Application.Policies;
using ClaimsModule.Application.ReferenceData;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.Domain.Reserves;
using FluentValidation.Results;
using Microsoft.Extensions.Time.Testing;

namespace ClaimsModule.Application.Tests.Claims;

/// <summary>
/// The FNOL validator against in-memory reference data. Messages are asserted word for word against
/// FRS §8 (VAL-01..09); the HTTP tests prove the same messages reach the 422 body.
/// </summary>
public sealed class CreateClaimCommandValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid KnownPolicyId = Guid.NewGuid();

    private readonly CreateClaimCommandValidator _validator =
        new(new FakeTimeProvider(Now), new StubReferenceData(), new StubPolicies());

    [Fact]
    public async Task API_01_A_complete_intake_is_valid()
    {
        (await ValidateAsync(Valid())).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task VAL_01_Loss_date_in_the_future()
    {
        (await MessagesAsync(Valid() with { LossDate = Now.AddTicks(1) }, "LossDate")).ShouldBe(["Loss date cannot be in the future."]);
    }

    [Fact]
    public async Task BR_C_01_Loss_date_equal_to_now_is_accepted()
    {
        (await ValidateAsync(Valid() with { LossDate = Now })).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task VAL_02_Loss_date_missing()
    {
        (await MessagesAsync(Valid() with { LossDate = null }, "LossDate")).ShouldBe(["Loss date is required."]);
        (await MessagesAsync(Valid() with { LossDate = default(DateTimeOffset) }, "LossDate")).ShouldBe(["Loss date is required."]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Nineteen characters")]
    [InlineData("  padded but short   ")]
    public async Task VAL_04_Loss_description_under_20_characters(string? description)
    {
        (await MessagesAsync(Valid() with { LossDescription = description }, "LossDescription"))
            .ShouldBe(["Loss description is required and must be at least 20 characters."]);
    }

    [Fact]
    public async Task BR_C_07_Description_of_exactly_20_characters_is_accepted()
    {
        (await ValidateAsync(Valid() with { LossDescription = "Exactly twenty chars" })).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("COL-NOPE")] // BR_C_05 unknown
    [InlineData(StubReferenceData.InactiveCode)] // BR_C_05 inactive
    [InlineData("")]
    public async Task VAL_05_Cause_of_loss_code_not_recognised_or_inactive(string code)
    {
        (await MessagesAsync(Valid() with { CauseOfLossCode = code }, "CauseOfLossCode"))
            .ShouldBe(["Cause of loss code is not recognised or is inactive."]);
    }

    [Theory]
    [InlineData(ReserveComponentType.Indemnity, 0)]
    [InlineData(ReserveComponentType.Expense, -1)]
    public async Task VAL_08_Initial_reserve_amount_must_be_positive(ReserveComponentType component, decimal amount)
    {
        (await MessagesAsync(Valid() with { InitialReserve = new InitialReserveInput(component, amount, null) }, "InitialReserve.Amount"))
            .ShouldBe(["Reserve amount must be greater than zero."]);
    }

    [Fact]
    public async Task BR_R_01_Subrogation_may_be_negative_but_not_zero()
    {
        (await ValidateAsync(Valid() with { InitialReserve = new InitialReserveInput(ReserveComponentType.SubrogationRecoverable, -10m, null) }))
            .IsValid.ShouldBeTrue();
        (await MessagesAsync(Valid() with { InitialReserve = new InitialReserveInput(ReserveComponentType.SubrogationRecoverable, 0m, null) }, "InitialReserve.Amount"))
            .ShouldBe(["Reserve amount must not be zero."]);
    }

    [Fact]
    public async Task VAL_09_Invalid_reserve_component()
    {
        (await MessagesAsync(Valid() with { InitialReserve = new InitialReserveInput((ReserveComponentType)0, 100m, null) }, "InitialReserve.Component"))
            .ShouldBe(["Invalid reserve component type."]);
    }

    [Fact]
    public async Task BR_C_06_Initial_reserve_without_policy_is_rejected()
    {
        var command = Valid() with { PolicyId = null, InitialReserve = new InitialReserveInput(ReserveComponentType.Indemnity, 100m, null) };

        (await MessagesAsync(command, "PolicyId")).ShouldBe(["No policy linked. Policy must be associated before reserves can be set."]);
    }

    [Fact]
    public async Task D_06_Missing_claimant_and_policy_are_not_validation_errors()
    {
        // They become persisted issues on the Draft instead (D-06).
        (await ValidateAsync(Valid() with { PolicyId = null, Parties = [], RiskObjects = [] })).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task BR_P_02_Unknown_party_role_is_rejected()
    {
        var party = new PartyInput((PartyRole)0, PartyType.Person, "Pat", "Doe", null, null, null, null);

        (await MessagesAsync(Valid() with { Parties = [party] }, "Parties[0].Role")).ShouldBe(["Invalid party role."]);
    }

    [Fact]
    public async Task API_01_Unknown_policy_id_is_rejected()
    {
        (await MessagesAsync(Valid() with { PolicyId = Guid.NewGuid() }, "PolicyId")).ShouldBe(["Policy was not found."]);
    }

    private static CreateClaimCommand Valid() => new(
        KnownPolicyId,
        Now.AddDays(-1),
        "Delivery van rear-ended at a junction.",
        "Junction 4",
        StubReferenceData.ActiveCode,
        1_000m,
        null,
        ClaimSeverity.Standard,
        [new PartyInput(PartyRole.Claimant, PartyType.Person, "Jordan", "Reyes", null, "jordan@example.com", null, null)],
        [new RiskObjectInput(AssetType.Vehicle, "2022 Ford Transit", null, null)],
        InitialReserve: null);

    private Task<ValidationResult> ValidateAsync(CreateClaimCommand command) => _validator.ValidateAsync(command);

    private async Task<IReadOnlyList<string>> MessagesAsync(CreateClaimCommand command, string property) =>
        (await ValidateAsync(command)).Errors.Where(error => error.PropertyName == property).Select(error => error.ErrorMessage).ToList();

    private sealed class StubReferenceData : IReferenceDataQueries
    {
        public const string ActiveCode = "COL-VEH-COL";
        public const string InactiveCode = "COL-RETIRED";

        public Task<bool> IsActiveCauseOfLossCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(code == ActiveCode);

        public Task<IReadOnlyList<CauseOfLossCodeDto>> ListCauseOfLossCodesAsync(PerilCategory? perilCategory, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ClaimStatusTransitionDto>> ListStatusTransitionsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubPolicies : IPolicyQueries
    {
        public Task<PolicyDto?> GetAsync(Guid policyId, CancellationToken cancellationToken) =>
            Task.FromResult(policyId == KnownPolicyId
                ? new PolicyDto
                {
                    Id = policyId,
                    PolicyNumber = "POL-2025-003001",
                    ClientName = "Northwind Logistics Ltd",
                    EffectiveDate = new DateOnly(2025, 1, 1),
                    ExpirationDate = new DateOnly(2030, 12, 31),
                    Status = PolicyStatus.Active,
                    CoverageTypes = ["Vehicle"],
                }
                : null);

        public Task<IReadOnlyList<PolicyDto>> SearchAsync(string term, int maxResults, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
