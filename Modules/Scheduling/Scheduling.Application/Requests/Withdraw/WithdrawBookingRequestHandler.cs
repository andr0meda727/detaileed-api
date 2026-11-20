using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Abstractions;
using Scheduling.Domain.Requests;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Scheduling.Application.Requests.Withdraw;

public sealed record WithdrawBookingRequestCommand(Guid Id) : ICommand<Result>;

public sealed class WithdrawBookingRequestHandler(ISchedulingDbContext db, ITenantContext tenant, TimeProvider clock)
    : ICommandHandler<WithdrawBookingRequestCommand, Result>
{
    public async ValueTask<Result> Handle(WithdrawBookingRequestCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var userId = tenant.UserId ?? throw new InvalidOperationException("This needs a signed-in customer.");

        // Another customer's request is simply not found
        var request = await db.BookingRequestsOfAnyStudio
            .FirstOrDefaultAsync(r => r.Id == id && r.CustomerUserId == userId, cancellationToken);
        if (request == null) return RequestErrors.NotFound;

        var result = request.Withdraw(clock.GetUtcNow().UtcDateTime);
        if (!result.Success) return result;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
