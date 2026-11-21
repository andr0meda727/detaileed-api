using Identity.Application.Abstractions;
using Identity.Application.Employees;
using Identity.Contracts;
using Mediator;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Identity.Application.Studios.ChangeStudioDetails;

public sealed record ChangeStudioDetailsCommand(ChangeStudioDetailsRequest Request) : ICommand<Result>;

public sealed class ChangeStudioDetailsHandler(IIdentityDbContext db, ITenantContext tenant, TimeProvider clock)
    : ICommandHandler<ChangeStudioDetailsCommand, Result>
{
    /// <summary>
    /// Changes the current studio's details and tells Marketplace through <see cref="StudioDetailsChanged"/>, saved in
    /// the same transaction, so the public listing follows.
    /// </summary>
    public async ValueTask<Result> Handle(ChangeStudioDetailsCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var studio = await db.Studios.LoadCurrentAsync(tenant, cancellationToken);

        var result = studio.ChangeDetails(
            request.Name, request.Phone, request.Email, request.Address, request.City, request.Latitude, request.Longitude);
        if (!result.Success) return result;

        db.Enqueue(new StudioDetailsChanged(
            studio.Id,
            new StudioDetails(studio.Name, studio.Phone, studio.Address, studio.City, studio.Latitude, studio.Longitude),
            clock.GetUtcNow().UtcDateTime));

        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
