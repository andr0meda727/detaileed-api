using Customers.Application.Abstractions;
using Customers.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Customers.Application.Profile.SaveMyProfile;

public sealed record SaveMyProfileCommand(ProfileRequest Request) : ICommand<ProfileDto>;

public sealed class SaveMyProfileHandler(ICustomersDbContext db, ITenantContext tenant)
    : ICommandHandler<SaveMyProfileCommand, ProfileDto>
{
    /// <summary>
    /// Creates the caller's profile or updates it.
    /// </summary>
    public async ValueTask<ProfileDto> Handle(SaveMyProfileCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var userId = tenant.RequireUser();

        var profile = await db.Profiles.SingleOrDefaultAsync(cancellationToken);
        if (profile == null)
        {
            profile = CustomerProfile.Create(userId, request.Name, request.Phone);
            db.Profiles.Add(profile);
        }
        else
        {
            profile.Update(request.Name, request.Phone);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ProfileDto.From(profile);
    }
}
