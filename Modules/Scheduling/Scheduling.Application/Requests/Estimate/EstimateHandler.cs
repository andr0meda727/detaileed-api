using Marketplace.Contracts;
using Mediator;
using Scheduling.Application.Abstractions;
using Shared.Kernel.Results;

namespace Scheduling.Application.Requests.Estimate;

public sealed record EstimateQuery(Guid StudioId, EstimateRequest Request) : IQuery<Result<EstimateDto>>;

public sealed class EstimateHandler(ISchedulingDbContext db, IPublicStudios publicStudios)
    : IQueryHandler<EstimateQuery, Result<EstimateDto>>
{
    /// <summary>
    /// The price of the chosen services for a car of this size at a published studio, before sending a request.
    /// Computed the same way as the request's own estimate.
    /// </summary>
    public async ValueTask<Result<EstimateDto>> Handle(EstimateQuery query, CancellationToken cancellationToken)
    {
        var (studioId, request) = query;
        if (await publicStudios.FindPublishedAsync(studioId, cancellationToken) == null)
        {
            return CustomerRequestErrors.StudioNotAvailable;
        }

        var services = await db.PriceAsync(studioId, request.ServiceIds, request.VehicleSize!.Value, cancellationToken);
        if (services == null) return CustomerRequestErrors.UnknownServices;

        return EstimateDto.From(services);
    }
}
