using ClaimsModule.Application.Common.Behaviors;
using ClaimsModule.Application.Common.Events;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);

            // The first behaviour added is the outermost.
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            configuration.AddOpenBehavior(typeof(UnitOfWorkBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddAutoMapper(assembly);

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddDomainEventHandlers(typeof(IBeforeCommitHandler<>));
        services.AddDomainEventHandlers(typeof(IAfterCommitHandler<>));

        return services;
    }

    /// <summary>Assembly scan: handlers such as ClaimAuditTrail are never referenced by name.</summary>
    private static void AddDomainEventHandlers(this IServiceCollection services, Type openHandlerInterface)
    {
        var handlerTypes = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var handlerType in handlerTypes)
        {
            var closedInterfaces = handlerType.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == openHandlerInterface);

            foreach (var closedInterface in closedInterfaces)
            {
                services.AddScoped(closedInterface, handlerType);
            }
        }
    }
}
