using Identity.Application.Abstractions;
using Identity.Domain.Invitations;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Identity.Application.Invitations.DeclineInvitation;

public sealed record DeclineInvitationCommand(Guid Token, Guid UserId) : ICommand<Result>;

public sealed class DeclineInvitationHandler(IIdentityDbContext db, TimeProvider clock)
    : ICommandHandler<DeclineInvitationCommand, Result>
{
    public async ValueTask<Result> Handle(DeclineInvitationCommand command, CancellationToken cancellationToken)
    {
        var (token, userId) = command;
        var invitation = await db.FindInvitationByTokenAsync(token, cancellationToken);
        if (invitation == null) return InvitationErrors.NotFound;

        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, cancellationToken);

        var result = invitation.Decline(user.Email, clock.GetUtcNow().UtcDateTime);

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }
}
