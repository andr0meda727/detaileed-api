using Crm.Api.Controllers;
using Crm.Application.Clients;
using Crm.Application.Directory;
using Crm.Contracts;
using Crm.Infrastructure;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api;

/// <summary>
/// Composition root of the CRM module: the host only calls <see cref="AddCrmModule"/>.
/// Other modules see CRM only through <see cref="IClientDirectory"/> and the events in Crm.Contracts.
/// </summary>
public static class CrmModule
{
    public static IServiceCollection AddCrmModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IClientDirectory, ClientDirectory>();

        services.AddValidatorsFromAssemblyContaining<ClientRequestValidator>();

        services.AddCrmInfrastructure(configuration);

        services.AddControllers().AddApplicationPart(typeof(ClientsController).Assembly);

        return services;
    }
}
