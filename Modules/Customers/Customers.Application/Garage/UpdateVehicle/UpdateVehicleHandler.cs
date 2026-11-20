using Customers.Application.Abstractions;
using Customers.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Customers.Application.Garage.UpdateVehicle;

public sealed record UpdateVehicleCommand(Guid Id, GarageVehicleRequest Request) : ICommand<Result<GarageVehicleDto>>;

public sealed class UpdateVehicleHandler(ICustomersDbContext db)
    : ICommandHandler<UpdateVehicleCommand, Result<GarageVehicleDto>>
{
    public async ValueTask<Result<GarageVehicleDto>> Handle(UpdateVehicleCommand command, CancellationToken cancellationToken)
    {
        var (id, request) = command;
        // Another account's car is filtered out, so it is simply not found
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vehicle == null) return CustomerErrors.VehicleNotFound;

        var plate = CustomerVehicle.NormalizePlate(request.LicensePlate);
        if (await db.Vehicles.AnyAsync(v => v.Id != id && v.LicensePlate == plate, cancellationToken))
        {
            return CustomerErrors.PlateAlreadySaved;
        }

        vehicle.Update(request.LicensePlate, request.Brand, request.Model, request.Size!.Value);
        await db.SaveChangesAsync(cancellationToken);

        return GarageVehicleDto.From(vehicle);
    }
}
