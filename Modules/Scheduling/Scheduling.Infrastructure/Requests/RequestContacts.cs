using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Requests;

public class RequestContacts(SchedulingDbContext db) : IRequestContacts
{
    public async Task<RequestCustomerSummary?> FindRequestCustomerAsync(
        Guid studioId, Guid requestId, CancellationToken cancellationToken = default)
    {
        // The caller may be another module working without this studio as the tenant, so match the studio here
        return await db.BookingRequestsOfAnyStudio
            .AsNoTracking()
            .Where(r => r.Id == requestId && r.StudioId == studioId)
            .Select(r => new RequestCustomerSummary(r.CustomerUserId, r.CustomerName))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
