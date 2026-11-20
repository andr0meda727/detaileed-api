using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Abstractions;
using Scheduling.Domain.Requests;
using Shared.Application.Pagination;
using Shared.Kernel.Pagination;

namespace Scheduling.Application.Requests.GetStudioRequests;

public sealed record GetStudioBookingRequestsQuery(OnlineRequestStatus? Status, PageRequest Paging)
    : IQuery<PagedResponse<BookingRequestDto>>;

public sealed class GetStudioBookingRequestsHandler(ISchedulingDbContext db)
    : IQueryHandler<GetStudioBookingRequestsQuery, PagedResponse<BookingRequestDto>>
{
    /// <summary>
    /// The current studio's requests (tenant-filtered): pending ones first, then the newest.
    /// </summary>
    public async ValueTask<PagedResponse<BookingRequestDto>> Handle(
        GetStudioBookingRequestsQuery request, CancellationToken cancellationToken)
    {
        var (status, page) = request;
        var query = db.BookingRequests.AsNoTracking();
        if (status is { } wanted) query = query.Where(r => r.Status == wanted);

        var requests = await query
            .OrderBy(r => r.Status == OnlineRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .ToPagedResponseAsync(page, cancellationToken);

        return requests.Map(r => BookingRequestDto.From(r));
    }
}
