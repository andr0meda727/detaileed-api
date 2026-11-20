using Identity.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Studios.GetMyStudios;

public sealed record GetMyStudiosQuery(Guid UserId) : IQuery<List<UserStudioDto>>;

public sealed class GetMyStudiosHandler(IIdentityDbContext db) : IQueryHandler<GetMyStudiosQuery, List<UserStudioDto>>
{
    // Only studios the user still works in: a removed member can't open the studio, so it isn't offered
    public async ValueTask<List<UserStudioDto>> Handle(GetMyStudiosQuery query, CancellationToken cancellationToken)
    {
        var userId = query.UserId;
        return await (
                from member in db.Members.AsNoTracking()
                join studio in db.Studios on member.StudioId equals studio.Id
                join role in db.Roles on member.RoleId equals role.Id
                where member.UserId == userId && member.IsActive
                orderby studio.Name
                select new UserStudioDto(studio.Id, studio.Name, role.Name, studio.Address, studio.TimeZone))
            .ToListAsync(cancellationToken);
    }
}
