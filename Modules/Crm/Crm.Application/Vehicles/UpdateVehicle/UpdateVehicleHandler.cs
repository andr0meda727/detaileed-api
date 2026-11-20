using Crm.Application.Abstractions;
using Crm.Contracts;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Crm.Application.Vehicles.UpdateVehicle;

public sealed record UpdateVehicleCommand(Guid Id, VehicleRequest Request) : ICommand<Result<VehicleResponseDto>>;

public sealed class UpdateVehicleHandler(ICrmDbContext db)
    : ICommandHandler<UpdateVehicleCommand, Result<VehicleResponseDto>>
{
    public async ValueTask<Result<VehicleResponseDto>> Handle(UpdateVehicleCommand command, CancellationToken cancellationToken)
    {
        var (id, request) = command;
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vehicle == null) return CrmErrors.VehicleNotFound(id);

        if (request.ClientId != vehicle.ClientId &&
            !await db.Clients.AnyAsync(c => c.Id == request.ClientId, cancellationToken))
        {
            return CrmErrors.UnknownClient(request.ClientId);
        }

        vehicle.Update(request.ClientId, request.LicensePlate, request.Brand, request.Model, request.Size ?? vehicle.Size);

        db.Enqueue(new VehicleDetailsChanged(
            vehicle.StudioId, vehicle.Id, vehicle.ClientId, vehicle.LicensePlate, vehicle.Brand, vehicle.Model, vehicle.Size.ToString()));
        await db.SaveChangesAsync(cancellationToken);

        return VehicleResponseDto.From(vehicle);
    }
}
