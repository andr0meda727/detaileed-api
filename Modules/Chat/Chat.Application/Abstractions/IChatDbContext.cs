using Chat.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chat.Application.Abstractions;

/// <summary>
/// The module's unit of work. <see cref="Conversations"/> and <see cref="Messages"/> are tenant-filtered (the studio
/// side). The customer side has no tenant, so it reads through the <c>OfAnyStudio</c> sets and must always match
/// the signed-in customer itself.
/// </summary>
public interface IChatDbContext
{
    DbSet<Conversation> Conversations { get; }

    DbSet<Message> Messages { get; }

    IQueryable<Conversation> ConversationsOfAnyStudio { get; }

    IQueryable<Message> MessagesOfAnyStudio { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves, or returns false when another request saved the same conversation (studio and customer) or the same
    /// message (sender and client message id) first. The change tracker is then cleared, so the caller can read the
    /// row that won.
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);
}
