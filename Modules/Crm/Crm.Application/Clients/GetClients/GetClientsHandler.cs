using Crm.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Pagination;
using Shared.Kernel.Pagination;

namespace Crm.Application.Clients.GetClients;

public sealed record GetClientsQuery(PageRequest Paging, string? Search) : IQuery<PagedResponse<ClientResponseDto>>;

public sealed class GetClientsHandler(ICrmDbContext db)
    : IQueryHandler<GetClientsQuery, PagedResponse<ClientResponseDto>>
{
    public async ValueTask<PagedResponse<ClientResponseDto>> Handle(GetClientsQuery request, CancellationToken cancellationToken)
    {
        var (page, search) = request;
        var query = db.Clients.Include(c => c.Vehicles).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Case-insensitive substring match on the client and their vehicles
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                c.Phone.Contains(term) ||
                c.Vehicles.Any(v =>
                    v.LicensePlate.ToLower().Contains(term) ||
                    (v.Brand != null && v.Brand.ToLower().Contains(term)) ||
                    (v.Model != null && v.Model.ToLower().Contains(term))));
        }

        var clients = await query
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .ToPagedResponseAsync(page, cancellationToken);

        return clients.Map(ClientResponseDto.From);
    }
}
