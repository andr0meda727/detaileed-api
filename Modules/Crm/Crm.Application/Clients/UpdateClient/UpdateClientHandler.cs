using Crm.Application.Abstractions;
using Crm.Contracts;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Crm.Application.Clients.UpdateClient;

public sealed record UpdateClientCommand(Guid Id, ClientRequest Request) : ICommand<Result<ClientResponseDto>>;

public sealed class UpdateClientHandler(ICrmDbContext db)
    : ICommandHandler<UpdateClientCommand, Result<ClientResponseDto>>
{
    public async ValueTask<Result<ClientResponseDto>> Handle(UpdateClientCommand command, CancellationToken cancellationToken)
    {
        var (id, request) = command;
        var client = await db.Clients.Include(c => c.Vehicles).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client == null) return CrmErrors.ClientNotFound(id);

        if (await db.IsTakenAsync(request.Phone, id, cancellationToken)) return CrmErrors.PhoneTaken(request.Phone);

        // Vehicles are managed through their own endpoints, so request.Vehicle is ignored here
        client.UpdateDetails(request.Name, request.Phone, request.Email, request.Nip);

        // Other modules keep copies of these details (bookings, for example); saved together with the change
        db.Enqueue(new ClientDetailsChanged(client.StudioId, client.Id, client.Name, client.Phone, client.Email));
        await db.SaveChangesAsync(cancellationToken);

        return ClientResponseDto.From(client);
    }
}
