using Customers.Application.Abstractions;
using Customers.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Customers.Application.Garage.DeleteVehicle;

public sealed record DeleteVehicleCommand(Guid Id) : ICommand<Result>;

public sealed class DeleteVehicleHandler(ICustomersDbContext db) : ICommandHandler<DeleteVehicleCommand, Result>
{
    /// <summary>
    /// Removes the car from the garage for good. Bookings keep their own copy of the car, so nothing else changes.
    /// </summary>
    public async ValueTask<Result> Handle(DeleteVehicleCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vehicle == null) return CustomerErrors.VehicleNotFound;

        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
