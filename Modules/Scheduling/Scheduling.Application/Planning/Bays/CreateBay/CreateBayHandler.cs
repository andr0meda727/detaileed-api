using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Abstractions;
using Scheduling.Domain.Planning;
using Shared.Kernel.Results;

namespace Scheduling.Application.Planning.Bays.CreateBay;

public sealed record CreateBayCommand(BayRequest Request) : ICommand<Result<BayDto>>;

public sealed class CreateBayHandler(ISchedulingDbContext db) : ICommandHandler<CreateBayCommand, Result<BayDto>>
{
    public async ValueTask<Result<BayDto>> Handle(CreateBayCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var active = await db.Bays.CountAsync(b => b.IsActive, cancellationToken);
        if (active >= Bay.MaxActivePerStudio) return BayErrors.LimitReached;

        var bay = Bay.Create(request.Name);
        db.Bays.Add(bay);
        await db.SaveChangesAsync(cancellationToken);

        return BayDto.From(bay);
    }
}
