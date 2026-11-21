namespace Chat.Domain;

/// <summary>
/// The two parties of a conversation: the customer, or the studio's staff answering as one shared inbox.
/// </summary>
public enum ChatSide
{
    Customer,
    Studio
}
