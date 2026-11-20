using Customers.Application.Abstractions;
using Customers.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Customers.Application.Profile.GetMyProfile;

public sealed record GetMyProfileQuery : IQuery<Result<ProfileDto>>;

public sealed class GetMyProfileHandler(ICustomersDbContext db) : IQueryHandler<GetMyProfileQuery, Result<ProfileDto>>
{
    // The Owner filter limits the set to the caller, so there is at most one row
    public async ValueTask<Result<ProfileDto>> Handle(GetMyProfileQuery query, CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(cancellationToken);

        return profile == null ? CustomerErrors.ProfileNotFound : ProfileDto.From(profile);
    }
}
