using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scheduling.Api.Controllers;
using Scheduling.Application.Bookings;
using Scheduling.Application.Planning;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Consumers;

namespace Scheduling.Api;

/// <summary>
/// Composition root of the Scheduling module: the host calls <see cref="AddSchedulingModule"/>
/// and passes <see cref="ConsumersAssembly"/> to the message bus.
/// </summary>
public static class SchedulingModule
{
    public static Assembly ConsumersAssembly => typeof(BookingCopiesConsumer).Assembly;

    public static IServiceCollection AddSchedulingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<BookingPlanner>();

        services.TryAddSingleton(TimeProvider.System);

        services.AddValidatorsFromAssemblyContaining<BookingRequestValidator>();

        services.AddSchedulingInfrastructure(configuration);

        services.AddControllers().AddApplicationPart(typeof(BookingsController).Assembly);

        return services;
    }
}
