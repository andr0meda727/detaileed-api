using Crm.Application.Abstractions;
using Customers.Application.Abstractions;
using Identity.Application.Abstractions;
using Inventory.Application.Abstractions;
using Marketplace.Application.Abstractions;
using Scheduling.Application.Abstractions;
using Shared.Application.Messaging.Behaviors;

namespace Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Host-wide API behaviour. Every use case is a command or query sent through ISender; each module registers
    /// its own validators, and the validation behavior runs them before the handler.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // The source generator finds the handlers in the modules' Application assemblies at compile time;
        // they are scoped because they use the tenant DbContexts.
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies =
            [
                typeof(ICrmDbContext),
                typeof(ICustomersDbContext),
                typeof(IIdentityDbContext),
                typeof(IInventoryDbContext),
                typeof(IMarketplaceDbContext),
                typeof(ISchedulingDbContext)
            ];
            // Outermost first, so failed validation is logged too
            options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
        });

        return services;
    }
}
