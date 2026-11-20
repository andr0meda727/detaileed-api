using Mediator;
using Scheduling.Application.Abstractions;
using Scheduling.Contracts;
using Scheduling.Domain.Catalog;
using Shared.Kernel.Tenancy;

namespace Scheduling.Application.Catalog.CreateService;

public sealed record CreateServiceCommand(ServiceRequest Request) : ICommand<ServiceResponseDto>;

public sealed class CreateServiceHandler(ISchedulingDbContext db, ITenantContext tenant, TimeProvider clock)
    : ICommandHandler<CreateServiceCommand, ServiceResponseDto>
{
    public async ValueTask<ServiceResponseDto> Handle(CreateServiceCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var studioId = tenant.StudioId ?? throw new InvalidOperationException("Creating a service needs a resolved studio.");
        var service = Service.Create(
            request.Name, request.Category ?? ServiceCategory.Other, request.ToPrices(),
            request.DurationMinutes ?? Service.DefaultDurationMinutes);

        db.Services.Add(service);
        await db.EnqueueCatalogChangedAsync(studioId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResponseDto.From(service);
    }
}
