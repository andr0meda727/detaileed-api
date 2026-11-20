using Identity.Application.Abstractions;
using Mediator;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Identity.Application.Employees.ChangeEmployeeRole;

public sealed record ChangeEmployeeRoleCommand(ChangeEmployeeRoleRequest Request) : ICommand<Result>;

public sealed class ChangeEmployeeRoleHandler(IIdentityDbContext db, ITenantContext tenant)
    : ICommandHandler<ChangeEmployeeRoleCommand, Result>
{
    public async ValueTask<Result> Handle(ChangeEmployeeRoleCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var studio = await db.Studios.LoadCurrentAsync(tenant, cancellationToken);

        var result = studio.ChangeRole(request.UserId, request.RoleId, tenant.UserId!.Value);
        if (!result.Success) return result;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
