using Customers.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Customers.Application.Garage.GetMyVehicles;

public sealed record GetMyVehiclesQuery : IQuery<List<GarageVehicleDto>>;

public sealed class GetMyVehiclesHandler(ICustomersDbContext db)
    : IQueryHandler<GetMyVehiclesQuery, List<GarageVehicleDto>>
{
    // At most ten cars, so the list is not paged
    public async ValueTask<List<GarageVehicleDto>> Handle(GetMyVehiclesQuery query, CancellationToken cancellationToken)
    {
        var vehicles = await db.Vehicles.AsNoTracking()
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .ToListAsync(cancellationToken);

        return vehicles.Select(GarageVehicleDto.From).ToList();
    }
}
