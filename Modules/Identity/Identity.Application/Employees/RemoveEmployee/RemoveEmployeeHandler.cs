using Identity.Application.Abstractions;
using Mediator;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Identity.Application.Employees.RemoveEmployee;

public sealed record RemoveEmployeeCommand(Guid UserId) : ICommand<Result>;

public sealed class RemoveEmployeeHandler(IIdentityDbContext db, ITenantContext tenant)
    : ICommandHandler<RemoveEmployeeCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveEmployeeCommand command, CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        var studio = await db.Studios.LoadCurrentAsync(tenant, cancellationToken);

        var result = studio.RemoveMember(userId, tenant.UserId!.Value);
        if (!result.Success) return result;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
