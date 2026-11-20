using Mediator;
using Shared.Kernel.Vehicles;
using Crm.Application.Abstractions;
using Crm.Domain;
using Shared.Kernel.Results;

namespace Crm.Application.Clients.CreateClient;

public sealed record CreateClientCommand(ClientRequest Request) : ICommand<Result<ClientResponseDto>>;

public sealed class CreateClientHandler(ICrmDbContext db)
    : ICommandHandler<CreateClientCommand, Result<ClientResponseDto>>
{
    public async ValueTask<Result<ClientResponseDto>> Handle(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (await db.IsTakenAsync(request.Phone, null, cancellationToken)) return CrmErrors.PhoneTaken(request.Phone);

        var client = Client.Create(request.Name, request.Phone, request.Email, request.Nip);
        if (request.Vehicle is { } vehicle)
        {
            client.AddVehicle(vehicle.LicensePlate, vehicle.Brand, vehicle.Model, vehicle.Size ?? VehicleSize.Medium);
        }

        // Client and vehicle are saved together
        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);

        return ClientResponseDto.From(client);
    }
}
