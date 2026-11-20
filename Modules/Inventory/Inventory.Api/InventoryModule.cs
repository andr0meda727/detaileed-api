using FluentValidation;
using Inventory.Api.Controllers;
using System.Reflection;
using Inventory.Application.Consumption;
using Inventory.Application.Items;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Consumers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Api;

/// <summary>
/// Composition root of the Inventory module: the host calls <see cref="AddInventoryModule"/> and passes
/// <see cref="ConsumersAssembly"/> to the message bus. Command and query handlers are registered by the host's
/// mediator, which finds them at compile time.
/// </summary>
public static class InventoryModule
{
    public static Assembly ConsumersAssembly => typeof(BookingConsumptionConsumer).Assembly;

    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ConsumptionRecorder>();

        services.AddValidatorsFromAssemblyContaining<InventoryCreateDtoValidator>();

        services.AddInventoryInfrastructure(configuration);

        services.AddControllers().AddApplicationPart(typeof(InventoryController).Assembly);

        return services;
    }
}
