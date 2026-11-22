using Chat.Application.Abstractions;
using Chat.Domain;
using Identity.Contracts;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Chat.Application.Conversations.OpenAsStudio;

public sealed record OpenStudioConversationCommand(OpenStudioConversationRequest Request) : ICommand<Result<ConversationDto>>;

public sealed class OpenStudioConversationHandler(
    IChatDbContext db, ITenantContext tenant, IRequestContacts contacts, IStudioDirectory studios, TimeProvider clock)
    : ICommandHandler<OpenStudioConversationCommand, Result<ConversationDto>>
{
    /// <summary>
    /// The current studio's conversation with the customer who sent it a booking request, started if there is none
    /// yet. A studio can only write first to someone who contacted it.
    /// </summary>
    public async ValueTask<Result<ConversationDto>> Handle(
        OpenStudioConversationCommand command, CancellationToken cancellationToken)
    {
        var studioId = tenant.StudioId ?? throw new InvalidOperationException("Opening a conversation needs a resolved studio.");

        var customer = await contacts.FindRequestCustomerAsync(studioId, command.Request.BookingRequestId, cancellationToken);
        if (customer == null) return ChatErrors.NoContact;

        // The tenant filter keeps this to the current studio
        var theirs = db.Conversations.Where(c => c.CustomerUserId == customer.UserId);

        var existing = await theirs.FirstOrDefaultAsync(cancellationToken);
        if (existing != null) return await ConversationAccess.ToDtoAsync(db, existing, ChatSide.Studio, cancellationToken);

        var details = await studios.FindDetailsAsync(studioId, cancellationToken)
                      ?? throw new InvalidOperationException($"Studio {studioId} has no details.");

        var conversation = Conversation.Open(studioId, details.Name, customer.UserId, customer.Name, clock.GetUtcNow().UtcDateTime);
        db.Conversations.Add(conversation);
        if (await db.TrySaveChangesAsync(cancellationToken) == SaveOutcome.Saved)
            return ConversationDto.From(conversation, ChatSide.Studio, unreadCount: 0);

        // The customer, or another staff member, opened it a moment earlier
        var winner = await theirs.FirstAsync(cancellationToken);
        return await ConversationAccess.ToDtoAsync(db, winner, ChatSide.Studio, cancellationToken);
    }
}
