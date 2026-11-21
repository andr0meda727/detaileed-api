using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chat.Infrastructure;

public static class ChatInfrastructureExtensions
{
    // The DbContext is registered here once the module has one
    public static IServiceCollection AddChatInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services;
}
