using Chat.Application;
using Chat.Application.Abstractions;

namespace Chat.Api;

// Until the SignalR hub exists: changes are saved, and apps see them when they read again
internal sealed class NullChatNotifier : IChatNotifier
{
    public Task MessageSentAsync(Guid studioId, Guid customerUserId, MessageDto message, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task ConversationReadAsync(Guid studioId, Guid customerUserId, ConversationReadDto read, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
