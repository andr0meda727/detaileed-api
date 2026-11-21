using Chat.Application.Abstractions;
using Chat.Domain;
using Customers.Contracts;
using Identity.Contracts;
using Marketplace.Contracts;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Chat.Application.Conversations.OpenAsCustomer;

public sealed record OpenConversationCommand(OpenConversationRequest Request) : ICommand<Result<ConversationDto>>;

public sealed class OpenConversationHandler(
    IChatDbContext db, ITenantContext tenant, IPublicStudios publicStudios, ICustomerDirectory customers,
    IStudioDirectory studios, TimeProvider clock)
    : ICommandHandler<OpenConversationCommand, Result<ConversationDto>>
{
    /// <summary>
    /// The signed-in customer's conversation with a studio, started if there is none yet. Starting one needs a
    /// published studio the customer doesn't work at, and the customer's contact details (the studio sees their
    /// name). An existing conversation is returned as it is, even if the studio has been unpublished since.
    /// </summary>
    public async ValueTask<Result<ConversationDto>> Handle(OpenConversationCommand command, CancellationToken cancellationToken)
    {
        var studioId = command.Request.StudioId;
        var userId = tenant.RequireUser();
        var mine = ConversationAccess.Visible(db, tenant, ChatSide.Customer).Where(c => c.StudioId == studioId);

        var existing = await mine.FirstOrDefaultAsync(cancellationToken);
        if (existing != null) return await ConversationAccess.ToDtoAsync(db, existing, ChatSide.Customer, cancellationToken);

        var studio = await publicStudios.FindPublishedAsync(studioId, cancellationToken);
        if (studio == null) return ChatErrors.StudioNotAvailable;

        if (await studios.IsActiveMemberAsync(studioId, userId, cancellationToken)) return ChatErrors.OwnStudio;

        var profile = await customers.FindProfileAsync(userId, cancellationToken);
        if (profile == null) return ChatErrors.ProfileRequired;

        var conversation = Conversation.Open(studio.StudioId, studio.Name, userId, profile.Name, clock.GetUtcNow().UtcDateTime);
        db.Conversations.Add(conversation);
        if (await db.TrySaveChangesAsync(cancellationToken) == SaveOutcome.Saved)
            return ConversationDto.From(conversation, ChatSide.Customer, unreadCount: 0);

        // Another request (a double click, a second tab) opened it a moment earlier
        var winner = await mine.FirstAsync(cancellationToken);
        return await ConversationAccess.ToDtoAsync(db, winner, ChatSide.Customer, cancellationToken);
    }
}
