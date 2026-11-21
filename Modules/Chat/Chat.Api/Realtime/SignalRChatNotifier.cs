using Chat.Application;
using Chat.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Chat.Api.Realtime;

/// <summary>
/// Pushes to both sides of the conversation, the sender's too (their other tabs, the rest of the staff). A failed
/// push is only logged: the change is already saved, and the app reads it on its next refresh or reconnect.
/// </summary>
internal sealed class SignalRChatNotifier(IHubContext<ChatHub> hub, ILogger<SignalRChatNotifier> logger) : IChatNotifier
{
    public Task MessageSentAsync(Guid studioId, Guid customerUserId, MessageDto message, CancellationToken cancellationToken) =>
        PushAsync(studioId, customerUserId, ChatHub.MessageReceived, message, message.ConversationId, cancellationToken);

    public Task ConversationReadAsync(Guid studioId, Guid customerUserId, ConversationReadDto read, CancellationToken cancellationToken) =>
        PushAsync(studioId, customerUserId, ChatHub.ConversationRead, read, read.ConversationId, cancellationToken);

    private async Task PushAsync(
        Guid studioId, Guid customerUserId, string method, object payload, Guid conversationId, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.User(customerUserId.ToString()).SendAsync(method, payload, cancellationToken);
            await hub.Clients.Group(ChatHub.StudioGroup(studioId)).SendAsync(method, payload, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Could not push {Method} for conversation {ConversationId}", method, conversationId);
        }
    }
}
