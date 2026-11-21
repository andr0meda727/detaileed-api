using System.Text.Json.Serialization;
using Chat.Api.Controllers;
using Chat.Api.Realtime;
using Chat.Application;
using Chat.Application.Abstractions;
using Chat.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Api.Tenancy;

namespace Chat.Api;

/// <summary>
/// Composition root of the Chat module: conversations between a customer and a studio, stored in the database and
/// pushed live over SignalR. The host calls <see cref="AddChatModule"/> and, after its controllers,
/// <see cref="MapChatHub"/>.
/// </summary>
public static class ChatModule
{
    public static IServiceCollection AddChatModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssemblyContaining<SendMessageRequestValidator>();

        services.AddChatInfrastructure(configuration);

        // One instance, no backplane: pushes reach connections on this server only. Enums go out as names, as in
        // the REST answers (SignalR has its own JSON options)
        services.AddSignalR()
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSingleton<IUserIdProvider, ChatUserIdProvider>();
        services.AddScoped<IChatNotifier, SignalRChatNotifier>();

        services.AddControllers().AddApplicationPart(typeof(ConversationsController).Assembly);

        return services;
    }

    /// <summary>
    /// Maps <see cref="ChatHub"/>. It works without a resolved studio: a browser can't send <c>X-Studio-Id</c> with
    /// a WebSocket, and staff of several studios pick theirs with <see cref="ChatHub.WatchStudio"/> instead.
    /// </summary>
    public static IEndpointConventionBuilder MapChatHub(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHub<ChatHub>(ChatHub.Path).WithMetadata(new AllowWithoutStudioAttribute());
}
