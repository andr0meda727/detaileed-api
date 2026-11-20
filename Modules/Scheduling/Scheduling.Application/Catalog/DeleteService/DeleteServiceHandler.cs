using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Abstractions;
using Shared.Kernel.Results;

namespace Scheduling.Application.Catalog.DeleteService;

public sealed record DeleteServiceCommand(Guid Id) : ICommand<Result>;

public sealed class DeleteServiceHandler(ISchedulingDbContext db, TimeProvider clock)
    : ICommandHandler<DeleteServiceCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteServiceCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id && s.IsActive, cancellationToken);
        if (service == null) return SchedulingErrors.ServiceNotFound(id);

        // Deactivated rather than deleted: past bookings still show it
        service.Deactivate();
        await db.EnqueueCatalogChangedAsync(service.StudioId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
