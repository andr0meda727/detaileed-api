using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Abstractions;
using Shared.Kernel.Results;

namespace Scheduling.Application.Planning.Bays.UpdateBay;

public sealed record UpdateBayCommand(Guid Id, BayRequest Request) : ICommand<Result<BayDto>>;

public sealed class UpdateBayHandler(ISchedulingDbContext db) : ICommandHandler<UpdateBayCommand, Result<BayDto>>
{
    public async ValueTask<Result<BayDto>> Handle(UpdateBayCommand command, CancellationToken cancellationToken)
    {
        var (id, request) = command;
        // Tenant-filtered, so another studio's bay is simply not found
        var bay = await db.Bays.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bay == null) return BayErrors.NotFound;

        bay.Rename(request.Name);
        await db.SaveChangesAsync(cancellationToken);

        return BayDto.From(bay);
    }
}
