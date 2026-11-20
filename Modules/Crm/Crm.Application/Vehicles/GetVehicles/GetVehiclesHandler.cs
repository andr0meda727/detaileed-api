using Crm.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Pagination;
using Shared.Kernel.Pagination;

namespace Crm.Application.Vehicles.GetVehicles;

public sealed record GetVehiclesQuery(PageRequest Paging, Guid? ClientId, string? Search)
    : IQuery<PagedResponse<VehicleResponseDto>>;

public sealed class GetVehiclesHandler(ICrmDbContext db)
    : IQueryHandler<GetVehiclesQuery, PagedResponse<VehicleResponseDto>>
{
    public async ValueTask<PagedResponse<VehicleResponseDto>> Handle(
        GetVehiclesQuery request, CancellationToken cancellationToken)
    {
        var (page, clientId, search) = request;
        var query = db.Vehicles.AsNoTracking();

        if (clientId.HasValue)
        {
            query = query.Where(v => v.ClientId == clientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(v =>
                v.LicensePlate.ToLower().Contains(term) ||
                (v.Brand != null && v.Brand.ToLower().Contains(term)) ||
                (v.Model != null && v.Model.ToLower().Contains(term)));
        }

        var vehicles = await query
            .OrderBy(v => v.LicensePlate).ThenBy(v => v.Id)
            .ToPagedResponseAsync(page, cancellationToken);

        return vehicles.Map(VehicleResponseDto.From);
    }
}
