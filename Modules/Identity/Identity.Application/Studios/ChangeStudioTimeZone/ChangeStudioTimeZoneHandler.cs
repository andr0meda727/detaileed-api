using Identity.Application.Abstractions;
using Identity.Application.Employees;
using Mediator;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Identity.Application.Studios.ChangeStudioTimeZone;

public sealed record ChangeStudioTimeZoneCommand(ChangeStudioTimeZoneRequest Request) : ICommand<Result>;

public sealed class ChangeStudioTimeZoneHandler(IIdentityDbContext db, ITenantContext tenant)
    : ICommandHandler<ChangeStudioTimeZoneCommand, Result>
{
    public async ValueTask<Result> Handle(ChangeStudioTimeZoneCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var studio = await db.Studios.LoadCurrentAsync(tenant, cancellationToken);

        var result = studio.ChangeTimeZone(request.TimeZone);
        if (!result.Success) return result;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
