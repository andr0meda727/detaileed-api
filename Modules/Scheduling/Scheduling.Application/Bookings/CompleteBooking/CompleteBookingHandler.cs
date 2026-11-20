using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Abstractions;
using Shared.Kernel.Results;

namespace Scheduling.Application.Bookings.CompleteBooking;

public sealed record CompleteBookingCommand(Guid Id) : ICommand<Result<BookingResponseDto>>;

public sealed class CompleteBookingHandler(ISchedulingDbContext db, TimeProvider clock)
    : ICommandHandler<CompleteBookingCommand, Result<BookingResponseDto>>
{
    public async ValueTask<Result<BookingResponseDto>> Handle(
        CompleteBookingCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        // The service lines go into BookingCompleted, so Inventory knows what the visit used
        var booking = await db.Bookings.Include(b => b.Services).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (booking == null) return SchedulingErrors.BookingNotFound(id);

        var result = booking.Complete(clock.GetUtcNow().UtcDateTime);
        if (!result.Success) return result.Error!;

        db.EnqueueIfCompleted(booking, booking.StudioId, wasCompleted: false);

        await db.SaveChangesAsync(cancellationToken);

        return await db.LoadResponseAsync(id, cancellationToken);
    }
}
