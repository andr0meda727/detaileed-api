using Chat.Infrastructure.Persistence;
using Identity.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infrastructure.Consumers;

/// <summary>
/// Keeps the studio's name in its conversations in step with Identity. Runs outside a request (no tenant), so it
/// scopes by the event's studio itself. One statement renames every conversation still holding an older name, so a
/// redelivered or out-of-order snapshot changes nothing, and it can't clash with a message being saved meanwhile.
/// </summary>
public class ConversationStudioNameConsumer(ChatDbContext db, TimeProvider clock) : IConsumer<StudioDetailsChanged>
{
    public async Task Consume(ConsumeContext<StudioDetailsChanged> context)
    {
        var message = context.Message;
        var name = message.Details.Name.Trim();
        var now = clock.GetUtcNow().UtcDateTime;

        await db.ConversationsOfAnyStudio
            .Where(c => c.StudioId == message.StudioId && c.StudioNameAsOf < message.ChangedAt)
            .ExecuteUpdateAsync(set => set
                    .SetProperty(c => c.StudioName, name)
                    .SetProperty(c => c.StudioNameAsOf, message.ChangedAt)
                    .SetProperty(c => c.UpdatedAt, now),
                context.CancellationToken);
    }
}
