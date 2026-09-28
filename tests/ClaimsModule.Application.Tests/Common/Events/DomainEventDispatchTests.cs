using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Behaviors;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaimsModule.Application.Tests.Common.Events;

/// <summary>The two-phase domain-event dispatch and the unit-of-work behaviour (CLAUDE.md rules 4 and 5).</summary>
public sealed class DomainEventDispatchTests
{
    [Fact]
    public void AUD_I1_Every_domain_event_has_a_before_commit_audit_handler()
    {
        using var provider = new ServiceCollection().AddLogging().AddApplication()
            .AddScoped<IAuditLogService, NoAuditLog>()
            .BuildServiceProvider();
        using var scope = provider.CreateScope();

        var eventTypes = typeof(IDomainEvent).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(type))
            .ToList();

        eventTypes.ShouldNotBeEmpty();
        eventTypes
            .Where(type => !scope.ServiceProvider.GetServices(typeof(IBeforeCommitHandler<>).MakeGenericType(type)).Any())
            .Select(type => type.Name)
            .ShouldBeEmpty(); // BR-A-02: every significant action is audited
    }

    [Fact]
    public async Task CONV_15_Before_commit_handler_exceptions_are_not_wrapped()
    {
        var dispatcher = Dispatcher(services => services.AddScoped<IBeforeCommitHandler<SampleEvent>, ThrowingHandler>());

        // A 422 must stay a 422: no TargetInvocationException around the handler's exception.
        await Should.ThrowAsync<BusinessRuleViolationException>(
            () => dispatcher.DispatchBeforeCommitAsync([new SampleEvent()], CancellationToken.None));
    }

    [Fact]
    public async Task CONV_15_After_commit_failures_are_logged_not_thrown()
    {
        var recorder = new List<string>();
        var dispatcher = Dispatcher(services => services
            .AddSingleton(recorder)
            .AddScoped<IAfterCommitHandler<SampleEvent>, ThrowingAfterCommitHandler>()
            .AddScoped<IAfterCommitHandler<SampleEvent>, RecordingAfterCommitHandler>());

        await dispatcher.DispatchAfterCommitAsync([new SampleEvent()], CancellationToken.None);

        recorder.ShouldBe(["recorded"]); // the commit already happened; the request still succeeds
    }

    [Fact]
    public async Task CONV_15_Queries_bypass_the_unit_of_work_and_commands_use_it()
    {
        var unitOfWork = new CountingUnitOfWork();

        await new UnitOfWorkBehavior<SampleQuery, int>(unitOfWork).Handle(new SampleQuery(), _ => Task.FromResult(1), CancellationToken.None);
        unitOfWork.Calls.ShouldBe(0);

        await new UnitOfWorkBehavior<SampleCommand, int>(unitOfWork).Handle(new SampleCommand(), _ => Task.FromResult(1), CancellationToken.None);
        unitOfWork.Calls.ShouldBe(1);
    }

    private static IDomainEventDispatcher Dispatcher(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return new DomainEventDispatcher(services.BuildServiceProvider(), NullLogger<DomainEventDispatcher>.Instance);
    }

    public sealed record SampleEvent : IDomainEvent;

    public sealed record SampleQuery : IQuery<int>;

    public sealed record SampleCommand : ICommand<int>;

    private sealed class ThrowingHandler : IBeforeCommitHandler<SampleEvent>
    {
        public Task HandleAsync(SampleEvent domainEvent, CancellationToken cancellationToken) =>
            throw new BusinessRuleViolationException("Key", "Rule failed.");
    }

    private sealed class ThrowingAfterCommitHandler : IAfterCommitHandler<SampleEvent>
    {
        public Task HandleAsync(SampleEvent domainEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Queue unavailable.");
    }

    private sealed class RecordingAfterCommitHandler(List<string> recorder) : IAfterCommitHandler<SampleEvent>
    {
        public Task HandleAsync(SampleEvent domainEvent, CancellationToken cancellationToken)
        {
            recorder.Add("recorded");
            return Task.CompletedTask;
        }
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int Calls { get; private set; }

        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
        {
            Calls++;
            return operation(cancellationToken);
        }
    }

    private sealed class NoAuditLog : IAuditLogService
    {
        public void Record(AuditEntry entry)
        {
        }
    }
}
