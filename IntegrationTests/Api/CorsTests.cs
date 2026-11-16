namespace IntegrationTests.Api;

/// <summary>The "Frontend" CORS policy allows the origins listed in Cors:AllowedOrigins and nothing else.</summary>
[TestFixture]
public class CorsTests
{
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _factory = new ApiFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestCase("https://app.detaileed.com")]
    [TestCase("https://detaileed.com")]
    [TestCase("https://www.detaileed.com")]
    public async Task Preflight_FromConfiguredOrigin_IsAllowed(string origin)
    {
        var response = await _client.SendAsync(Preflight(origin));

        Assert.That(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed), Is.True);
        Assert.That(allowed, Is.EqualTo(new[] { origin }));
    }

    [Test]
    public async Task Preflight_FromOtherOrigin_IsNotAllowed()
    {
        var response = await _client.SendAsync(Preflight("https://evil.example"));

        Assert.That(response.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/bookings");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }
}
