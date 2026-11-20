using Crm.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Crm.Application.Vehicles.DeleteVehicle;

public sealed record DeleteVehicleCommand(Guid Id) : ICommand<Result>;

public sealed class DeleteVehicleHandler(ICrmDbContext db) : ICommandHandler<DeleteVehicleCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteVehicleCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vehicle == null) return CrmErrors.VehicleNotFound(id);

        vehicle.Delete();
        await db.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
