using Identity.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Invitations.GetInvitation;

public sealed record GetInvitationQuery(Guid Token) : IQuery<InvitationResponseDto?>;

public sealed class GetInvitationHandler(IIdentityDbContext db, TimeProvider clock)
    : IQueryHandler<GetInvitationQuery, InvitationResponseDto?>
{
    /// <summary>
    /// Returns an invitation that can still be answered, or null. Anyone with the link may look it up.
    /// </summary>
    public async ValueTask<InvitationResponseDto?> Handle(GetInvitationQuery query, CancellationToken cancellationToken)
    {
        var token = query.Token;
        var invitation = await db.FindInvitationByTokenAsync(token, cancellationToken);
        if (invitation == null) return null;

        if (!invitation.IsOpen(clock.GetUtcNow().UtcDateTime))
        {
            if (invitation.ExpireIfDue(clock.GetUtcNow().UtcDateTime)) await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        var studioName = await db.Studios.Where(s => s.Id == invitation.StudioId).Select(s => s.Name)
            .SingleAsync(cancellationToken);
        var roleName = await db.Roles.Where(r => r.Id == invitation.RoleId).Select(r => r.Name)
            .SingleAsync(cancellationToken);

        return new InvitationResponseDto(invitation.Id, studioName, roleName, invitation.Email, invitation.ExpiresAt);
    }
}
