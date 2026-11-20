using Mediator;
using Shared.Kernel.Vehicles;
using Crm.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Crm.Application.Vehicles.CreateVehicle;

public sealed record CreateVehicleCommand(VehicleRequest Request) : ICommand<Result<VehicleResponseDto>>;

public sealed class CreateVehicleHandler(ICrmDbContext db)
    : ICommandHandler<CreateVehicleCommand, Result<VehicleResponseDto>>
{
    public async ValueTask<Result<VehicleResponseDto>> Handle(
        CreateVehicleCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        // Tenant-filtered, so another studio's client is unknown here
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken);
        if (client == null) return CrmErrors.UnknownClient(request.ClientId);

        var vehicle = client.AddVehicle(request.LicensePlate, request.Brand, request.Model, request.Size ?? VehicleSize.Medium);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(cancellationToken);

        return VehicleResponseDto.From(vehicle);
    }
}
