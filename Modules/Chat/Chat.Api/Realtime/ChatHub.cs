using Identity.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Shared.Api.Realtime;

namespace Chat.Api.Realtime;

/// <summary>
/// The live side of the chat. Apps only listen here: messages are sent and marked read over REST (validation, limits
/// and tests work as for every other endpoint), and the server pushes the result to everyone in the conversation.
/// <list type="bullet">
/// <item>A customer is reached as a SignalR user (their account id, every open tab), with nothing to call.</item>
/// <item>Staff call <see cref="WatchStudio"/> for each studio whose inbox they show. Groups belong to a connection, so
/// an app calls it again after reconnecting.</item>
/// </list>
/// Events: <see cref="MessageReceived"/> with a message, <see cref="ConversationRead"/> with a read receipt.
/// </summary>
[Authorize]
public class ChatHub(IStudioDirectory studios) : Hub
{
    // Under HubRoutes.Prefix, where the access token may come in the query string
    public const string Path = HubRoutes.Prefix + "/chat";
    public const string MessageReceived = nameof(MessageReceived);
    public const string ConversationRead = nameof(ConversationRead);

    public static string StudioGroup(Guid studioId) => $"studio:{studioId}";

    /// <summary>
    /// Starts pushing the studio's conversations to this connection; refused unless the caller works there now.
    /// </summary>
    public async Task WatchStudio(Guid studioId)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId)
            || !await studios.IsActiveMemberAsync(studioId, userId, Context.ConnectionAborted))
        {
            throw new HubException("You do not have access to this studio.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, StudioGroup(studioId), Context.ConnectionAborted);
    }
}
