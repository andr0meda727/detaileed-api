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
    /// Saves, or reports why another request got there first. On anything but <see cref="SaveOutcome.Saved"/> the
    /// change tracker is cleared, so the caller can read the current rows and decide again.
    /// </summary>
    Task<SaveOutcome> TrySaveChangesAsync(CancellationToken cancellationToken = default);
}

public enum SaveOutcome
{
    Saved,

    // The same conversation (studio and customer) or the same message (sender and client message id) was saved first
    AlreadyExists,

    // The conversation was changed by someone else since it was read (its row version moved)
    Conflict
}
