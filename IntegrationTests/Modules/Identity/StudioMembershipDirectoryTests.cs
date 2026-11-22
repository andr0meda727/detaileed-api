using Identity.Application.Directory;
using Identity.Domain.Studios;
using Identity.Domain.Users;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Tenancy;

namespace IntegrationTests.Modules.Identity;

/// <summary>
/// Other modules ask whether someone works at a studio, e.g. before letting them watch its chat inbox.
/// </summary>
[TestFixture]
public class StudioMembershipDirectoryTests
{
    private TwoStudiosSeed _seed = null!;

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        _seed = new TwoStudiosSeed();
        await _seed.SeedAsync();
    }

    [Test]
    public async Task AnActiveMember_CountsForTheirStudioOnly()
    {
        await using var db = PostgresFixture.CreateIdentityDbContext(new TenantContext());
        var directory = new StudioDirectory(db);

        Assert.That(await directory.IsActiveMemberAsync(_seed.StudioA.StudioId, _seed.StudioA.OwnerUserId), Is.True);
        Assert.That(await directory.IsActiveMemberAsync(_seed.StudioB.StudioId, _seed.StudioA.OwnerUserId), Is.False);
        Assert.That(await directory.IsActiveMemberAsync(_seed.StudioA.StudioId, Guid.NewGuid()), Is.False);
    }

    [Test]
    public async Task ARemovedMember_NoLongerCounts()
    {
        var employee = Guid.NewGuid();
        await using (var db = PostgresFixture.CreateIdentityDbContext(new TenantContext()))
        {
            db.Users.Add(new User(employee, $"{employee:N}@test.pl", "Ewa", "Pracownik"));
            var studio = await db.Studios.Include(s => s.Members).SingleAsync(s => s.Id == _seed.StudioA.StudioId);
            studio.Admit(employee, Roles.Employee);
            await db.SaveChangesAsync();

            Assert.That(await new StudioDirectory(db).IsActiveMemberAsync(_seed.StudioA.StudioId, employee), Is.True);

            Assert.That(studio.RemoveMember(employee, _seed.StudioA.OwnerUserId).Success, Is.True);
            await db.SaveChangesAsync();
        }

        await using var fresh = PostgresFixture.CreateIdentityDbContext(new TenantContext());
        Assert.That(await new StudioDirectory(fresh).IsActiveMemberAsync(_seed.StudioA.StudioId, employee), Is.False);
    }
}
