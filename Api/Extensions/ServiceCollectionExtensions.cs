using Crm.Application.Abstractions;
using Customers.Application.Abstractions;
using Identity.Application.Abstractions;
using Inventory.Application.Abstractions;
using Marketplace.Application.Abstractions;
using Shared.Application.Messaging.Behaviors;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

namespace Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Host-wide API behaviour. Each module registers its own validators; this runs them for every request body.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddFluentValidationAutoValidation();

        // Every use case is a command or query sent through ISender. The source generator finds the handlers in the
        // modules' Application assemblies at compile time; they are scoped because they use the tenant DbContexts.
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies =
            [
                typeof(ICrmDbContext),
                typeof(ICustomersDbContext),
                typeof(IIdentityDbContext),
                typeof(IInventoryDbContext),
                typeof(IMarketplaceDbContext)
            ];
            // Outermost first
            options.PipelineBehaviors = [typeof(LoggingBehavior<,>)];
        });

        return services;
    }
}
