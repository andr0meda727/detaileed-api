using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Abstractions;
using Shared.Kernel.Results;

namespace Scheduling.Application.Catalog.UpdateService;

public sealed record UpdateServiceCommand(Guid Id, ServiceRequest Request) : ICommand<Result<ServiceResponseDto>>;

public sealed class UpdateServiceHandler(ISchedulingDbContext db, TimeProvider clock)
    : ICommandHandler<UpdateServiceCommand, Result<ServiceResponseDto>>
{
    public async ValueTask<Result<ServiceResponseDto>> Handle(
        UpdateServiceCommand command, CancellationToken cancellationToken)
    {
        var (id, request) = command;
        // Tenant-filtered, so another studio's service is simply not found
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id && s.IsActive, cancellationToken);
        if (service == null) return SchedulingErrors.ServiceNotFound(id);

        // Left out, the category, prices and duration stay; sent, the price list is replaced as a whole ([] clears it)
        var prices = request.Prices == null ? service.Prices.ToList() : request.ToPrices();
        service.Update(request.Name, request.Category ?? service.Category, prices, request.DurationMinutes);
        await db.EnqueueCatalogChangedAsync(service.StudioId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResponseDto.From(service);
    }
}
