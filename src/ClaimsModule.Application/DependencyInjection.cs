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

            // Order matters: the first behaviour added is the outermost.
            // Logging wraps everything (including validation failures); validation runs before the
            // UnitOfWorkBehavior, so an invalid request never opens a transaction.
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

    /// <summary>Registers every handler class of this assembly under each closed handler interface it implements.</summary>
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
