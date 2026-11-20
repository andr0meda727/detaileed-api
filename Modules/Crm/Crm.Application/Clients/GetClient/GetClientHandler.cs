using Crm.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Crm.Application.Clients.GetClient;

public sealed record GetClientQuery(Guid Id) : IQuery<Result<ClientResponseDto>>;

public sealed class GetClientHandler(ICrmDbContext db) : IQueryHandler<GetClientQuery, Result<ClientResponseDto>>
{
    public async ValueTask<Result<ClientResponseDto>> Handle(GetClientQuery query, CancellationToken cancellationToken)
    {
        var id = query.Id;
        var client = await db.Clients
            .Include(c => c.Vehicles)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return client == null ? CrmErrors.ClientNotFound(id) : ClientResponseDto.From(client);
    }
}
