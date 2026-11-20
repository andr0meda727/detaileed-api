using Crm.Application.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Crm.Application.Clients.DeleteClient;

public sealed record DeleteClientCommand(Guid Id) : ICommand<Result>;

public sealed class DeleteClientHandler(ICrmDbContext db) : ICommandHandler<DeleteClientCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteClientCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client == null) return CrmErrors.ClientNotFound(id);

        client.Delete();
        await db.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
