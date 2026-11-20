using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Abstractions;
using Shared.Kernel.Results;

namespace Scheduling.Application.Bookings.GetBooking;

public sealed record GetBookingQuery(Guid Id) : IQuery<Result<BookingResponseDto>>;

public sealed class GetBookingHandler(ISchedulingDbContext db)
    : IQueryHandler<GetBookingQuery, Result<BookingResponseDto>>
{
    public async ValueTask<Result<BookingResponseDto>> Handle(
        GetBookingQuery query, CancellationToken cancellationToken)
    {
        var id = query.Id;
        var booking = await db.Bookings.WithDetails().AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        return booking == null ? SchedulingErrors.BookingNotFound(id) : BookingResponseDto.From(booking);
    }
}
