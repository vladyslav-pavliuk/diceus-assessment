using ClaimsModule.Application.Claims.Commands.AdjustReserve;
using ClaimsModule.Application.Claims.Commands.RejectReserveTransaction;
using ClaimsModule.Application.Claims.Commands.SetReserveLimitOverride;
using ClaimsModule.Application.Claims.Commands.SubmitReserveTransaction;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Tests.Claims;

/// <summary>
/// Request-shape rules of the reserve commands (FRS §8 ReserveAmount / ReserveComponent, D-05). Keys are the FRS §8
/// field names, the same keys the aggregate uses for the rules only it can check (D-41).
/// </summary>
public sealed class ReserveCommandValidatorTests
{
    private static readonly SubmitReserveTransactionCommand Valid =
        new(Guid.NewGuid(), ReserveComponentType.Indemnity, null, 2_500m, "Surveyor estimate.");

    [Fact]
    public void API_10_A_complete_submission_is_valid()
    {
        new SubmitReserveTransactionCommandValidator().Validate(Valid).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void VAL_09_Invalid_reserve_component()
    {
        Messages(Valid with { Component = null }, "ReserveComponent").ShouldBe(["Invalid reserve component type."]);
        Messages(Valid with { Component = (ReserveComponentType)99 }, "ReserveComponent").ShouldBe(["Invalid reserve component type."]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void VAL_08_Explicit_add_amount_must_be_positive(decimal amount)
    {
        Messages(Valid with { TransactionType = ReserveTransactionType.Add, Amount = amount }, "ReserveAmount")
            .ShouldBe(["Reserve amount must be greater than zero."]);
    }

    [Fact]
    public void BR_R_01_Explicit_add_of_negative_subrogation_is_valid_but_zero_is_not()
    {
        var subrogation = Valid with { Component = ReserveComponentType.SubrogationRecoverable, TransactionType = ReserveTransactionType.Add };

        new SubmitReserveTransactionCommandValidator().Validate(subrogation with { Amount = -8_000m }).IsValid.ShouldBeTrue();
        Messages(subrogation with { Amount = 0m }, "ReserveAmount").ShouldBe(["Reserve amount must not be zero."]);
    }

    [Fact]
    public void D_05_Adjust_needs_a_non_zero_signed_amount()
    {
        var adjust = Valid with { TransactionType = ReserveTransactionType.Adjust };

        new SubmitReserveTransactionCommandValidator().Validate(adjust with { Amount = -1_000m }).IsValid.ShouldBeTrue();
        Messages(adjust with { Amount = 0m }, "ReserveAmount").ShouldBe(["Adjustment amount must not be zero."]);
        Messages(adjust with { Amount = null }, "ReserveAmount").ShouldBe(["Adjustment amount must not be zero."]);
    }

    [Fact]
    public void D_05_Reverse_takes_no_amount()
    {
        var reverse = Valid with { TransactionType = ReserveTransactionType.Reverse };

        new SubmitReserveTransactionCommandValidator().Validate(reverse with { Amount = null }).IsValid.ShouldBeTrue();
        Messages(reverse with { Amount = 100m }, "ReserveAmount")
            .ShouldBe(["The amount of a Reverse transaction is computed by the system; omit it."]);
    }

    [Fact]
    public void D_05_Omitted_type_needs_an_amount_and_leaves_the_sign_to_the_aggregate()
    {
        Messages(Valid with { Amount = null }, "ReserveAmount").ShouldBe(["Reserve amount must be greater than zero."]);

        // Add or Adjust depends on whether the component exists, which only the aggregate knows.
        new SubmitReserveTransactionCommandValidator().Validate(Valid with { Amount = -500m }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void FRS_15_1_Amounts_with_more_than_four_decimals_are_rejected()
    {
        Messages(Valid with { Amount = 1.00001m }, "ReserveAmount").ShouldBe(["Reserve amount must have at most 4 decimal places."]);
    }

    [Fact]
    public void API_10_Change_reason_is_required_and_bounded()
    {
        Messages(Valid with { ChangeReason = "  " }, "ChangeReason").ShouldBe(["A change reason is required."]);
        Messages(Valid with { ChangeReason = new string('x', 501) }, "ChangeReason").ShouldBe(["Change reason must not exceed 500 characters."]);
    }

    [Fact]
    public void API_11_Adjust_to_a_new_amount_needs_the_amount()
    {
        var validator = new AdjustReserveCommandValidator();

        validator.Validate(new AdjustReserveCommand(Guid.NewGuid(), Guid.NewGuid(), 0m, "Settled for less.")).IsValid.ShouldBeTrue();
        validator.Validate(new AdjustReserveCommand(Guid.NewGuid(), Guid.NewGuid(), null, "Estimate."))
            .Errors.Single().ErrorMessage.ShouldBe("The new reserve amount is required.");
    }

    [Fact]
    public void API_14_Reject_requires_reason()
    {
        new RejectReserveTransactionCommandValidator().Validate(new RejectReserveTransactionCommand(Guid.NewGuid(), Guid.NewGuid(), ""))
            .Errors.Single().ErrorMessage.ShouldBe("A rejection reason is required.");
    }

    [Fact]
    public void API_24_Override_needs_enabled_and_a_reason()
    {
        var errors = new SetReserveLimitOverrideCommandValidator().Validate(new SetReserveLimitOverrideCommand(Guid.NewGuid(), null, null)).Errors;

        errors.Select(error => error.ErrorMessage)
            .ShouldBe(["Enabled is required.", "A reason is required to change the reserve limit override."], ignoreOrder: true);
    }

    private static IReadOnlyList<string> Messages(SubmitReserveTransactionCommand command, string key) =>
        new SubmitReserveTransactionCommandValidator().Validate(command).Errors
            .Where(error => error.PropertyName == key)
            .Select(error => error.ErrorMessage)
            .ToList();
}
