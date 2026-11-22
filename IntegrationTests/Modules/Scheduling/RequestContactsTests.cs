using IntegrationTests.Infrastructure;
using Scheduling.Domain.Requests;
using Scheduling.Infrastructure.Requests;
using Shared.Infrastructure.Tenancy;
using Shared.Kernel.Vehicles;

namespace IntegrationTests.Modules.Scheduling;

/// <summary>
/// Another module (the chat) asks who sent a studio a request, so the studio can write back to them.
/// </summary>
[TestFixture]
public class RequestContactsTests
{
    private TwoStudiosSeed _seed = null!;

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        _seed = new TwoStudiosSeed();
        await _seed.SeedAsync();
    }

    [Test]
    public async Task FindsTheCustomer_OnlyWithinTheStudioTheRequestWentTo()
    {
        var anna = Guid.NewGuid();
        var request = OnlineBookingRequest.Submit(
            _seed.StudioA.StudioId, "Detail Studio", new RequestCustomer(anna, "Anna Nowak", "500600700", "anna@test.pl"),
            new RequestVehicle("KR 1", null, null, VehicleSize.Medium),
            [new RequestedService(_seed.StudioA.ServiceId, "Polishing", null)], DateTime.UtcNow.AddDays(2), null);
        await using (var db = PostgresFixture.CreateSchedulingDbContext(new TenantContext()))
        {
            db.BookingRequests.Add(request);
            await db.SaveChangesAsync();
        }

        // No tenant, as when another module calls in
        await using var other = PostgresFixture.CreateSchedulingDbContext(new TenantContext());
        var contacts = new RequestContacts(other);

        var found = await contacts.FindRequestCustomerAsync(_seed.StudioA.StudioId, request.Id);
        var fromAnotherStudio = await contacts.FindRequestCustomerAsync(_seed.StudioB.StudioId, request.Id);
        var unknown = await contacts.FindRequestCustomerAsync(_seed.StudioA.StudioId, Guid.NewGuid());

        Assert.That(found, Is.EqualTo(new global::Scheduling.Contracts.RequestCustomerSummary(anna, "Anna Nowak")));
        Assert.That(fromAnotherStudio, Is.Null);
        Assert.That(unknown, Is.Null);
    }
}
