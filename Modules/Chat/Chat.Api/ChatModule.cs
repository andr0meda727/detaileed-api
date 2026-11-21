using Chat.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chat.Api;

/// <summary>
/// Composition root of the Chat module: conversations between a customer and a studio, stored in the database and
/// pushed live over SignalR (see <c>docs/chat-module-plan.md</c>). The host calls <see cref="AddChatModule"/>.
/// </summary>
public static class ChatModule
{
    public static IServiceCollection AddChatModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddChatInfrastructure(configuration);

        return services;
    }
}
