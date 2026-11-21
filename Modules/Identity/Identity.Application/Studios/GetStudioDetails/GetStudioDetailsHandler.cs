using Identity.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Identity.Application.Studios.GetStudioDetails;

public sealed record GetStudioDetailsQuery : IQuery<StudioDetailsDto>;

public sealed class GetStudioDetailsHandler(IIdentityDbContext db, ITenantContext tenant)
    : IQueryHandler<GetStudioDetailsQuery, StudioDetailsDto>
{
    public async ValueTask<StudioDetailsDto> Handle(GetStudioDetailsQuery query, CancellationToken cancellationToken)
    {
        var studioId = tenant.StudioId ?? throw new InvalidOperationException("This needs a resolved studio.");

        return await db.Studios.AsNoTracking()
            .Where(s => s.Id == studioId)
            .Select(s => new StudioDetailsDto(
                s.Id, s.Name, s.Phone, s.Email, s.Address, s.City, s.Latitude, s.Longitude, s.TimeZone))
            .SingleAsync(cancellationToken);
    }
}
