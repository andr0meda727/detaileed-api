using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Abstractions;
using Shared.Kernel.Results;

namespace Scheduling.Application.Bookings.CancelBooking;

public sealed record CancelBookingCommand(Guid Id) : ICommand<Result<BookingResponseDto>>;

public sealed class CancelBookingHandler(ISchedulingDbContext db, TimeProvider clock)
    : ICommandHandler<CancelBookingCommand, Result<BookingResponseDto>>
{
    public async ValueTask<Result<BookingResponseDto>> Handle(
        CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (booking == null) return SchedulingErrors.BookingNotFound(id);

        var result = booking.Cancel(clock.GetUtcNow().UtcDateTime);
        if (!result.Success) return result.Error!;

        await db.SaveChangesAsync(cancellationToken);

        return await db.LoadResponseAsync(id, cancellationToken);
    }
}
