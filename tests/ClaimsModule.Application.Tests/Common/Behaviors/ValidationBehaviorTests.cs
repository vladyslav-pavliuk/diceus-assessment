using ClaimsModule.Application.Common.Behaviors;
using ClaimsModule.Application.Common.Messaging;
using FluentValidation;
using MediatR;
using ValidationException = ClaimsModule.Application.Common.Exceptions.ValidationException;

namespace ClaimsModule.Application.Tests.Common.Behaviors;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task CONV_15_Validation_behavior_short_circuits_handler()
    {
        var handlerCalled = false;
        var behavior = CreateBehavior(new NameValidator());

        var exception = await Should.ThrowAsync<ValidationException>(() => behavior.Handle(
            new SampleCommand(Name: "", Quantity: 1),
            Next(() => handlerCalled = true),
            CancellationToken.None));

        handlerCalled.ShouldBeFalse();
        exception.Errors.ShouldContainKey(nameof(SampleCommand.Name));
    }

    [Fact]
    public async Task CONV_15_Errors_from_all_validators_are_grouped_by_property()
    {
        var behavior = CreateBehavior(new NameValidator(), new QuantityValidator());

        var exception = await Should.ThrowAsync<ValidationException>(() => behavior.Handle(
            new SampleCommand(Name: "", Quantity: 0),
            Next(() => { }),
            CancellationToken.None));

        exception.Errors.Keys.ShouldBe([nameof(SampleCommand.Name), nameof(SampleCommand.Quantity)], ignoreOrder: true);
        exception.Errors[nameof(SampleCommand.Name)].ShouldBe(["Name is required.", "Name is too short."]);
        exception.Errors[nameof(SampleCommand.Quantity)].ShouldBe(["Quantity must be positive."]);
    }

    [Fact]
    public async Task CONV_15_Valid_request_reaches_the_handler()
    {
        var handlerCalled = false;
        var behavior = CreateBehavior(new NameValidator(), new QuantityValidator());

        var response = await behavior.Handle(
            new SampleCommand(Name: "Valid name", Quantity: 1),
            Next(() => handlerCalled = true),
            CancellationToken.None);

        handlerCalled.ShouldBeTrue();
        response.ShouldBe("handled");
    }

    [Fact]
    public async Task CONV_15_Request_without_validators_reaches_the_handler()
    {
        var behavior = CreateBehavior();

        var response = await behavior.Handle(new SampleCommand(Name: null, Quantity: 0), Next(() => { }), CancellationToken.None);

        response.ShouldBe("handled");
    }

    private static ValidationBehavior<SampleCommand, string> CreateBehavior(params IValidator<SampleCommand>[] validators) =>
        new(validators);

    private static RequestHandlerDelegate<string> Next(Action onCalled) => _ =>
    {
        onCalled();
        return Task.FromResult("handled");
    };

    public sealed record SampleCommand(string? Name, int Quantity) : ICommand<string>;

    private sealed class NameValidator : AbstractValidator<SampleCommand>
    {
        public NameValidator()
        {
            RuleFor(command => command.Name).NotEmpty().WithMessage("Name is required.");
        }
    }

    private sealed class QuantityValidator : AbstractValidator<SampleCommand>
    {
        public QuantityValidator()
        {
            RuleFor(command => command.Quantity).GreaterThan(0).WithMessage("Quantity must be positive.");
            RuleFor(command => command.Name).MinimumLength(3).WithMessage("Name is too short.");
        }
    }
}
