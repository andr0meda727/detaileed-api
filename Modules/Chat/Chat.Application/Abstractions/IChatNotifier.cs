namespace Chat.Application.Abstractions;

/// <summary>
/// Pushes chat changes to the people in a conversation who are online: the customer (in every tab) and the studio's
/// staff watching its inbox. Called after the change is saved. A push that fails is logged and dropped, never
/// thrown: the database is the source of truth, and an app that missed a push catches up by reading again.
/// </summary>
public interface IChatNotifier
{
    Task MessageSentAsync(Guid studioId, Guid customerUserId, MessageDto message, CancellationToken cancellationToken);

    Task ConversationReadAsync(Guid studioId, Guid customerUserId, ConversationReadDto read, CancellationToken cancellationToken);
}
