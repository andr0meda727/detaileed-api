using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Abstractions;

namespace Scheduling.Application.Catalog.GetServices;

public sealed record GetServicesQuery : IQuery<List<ServiceResponseDto>>;

public sealed class GetServicesHandler(ISchedulingDbContext db)
    : IQueryHandler<GetServicesQuery, List<ServiceResponseDto>>
{
    // A studio has a handful of services, so the list is not paged
    public async ValueTask<List<ServiceResponseDto>> Handle(GetServicesQuery query, CancellationToken cancellationToken)
    {
        var services = await db.Services
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return services.Select(ServiceResponseDto.From).ToList();
    }
}
