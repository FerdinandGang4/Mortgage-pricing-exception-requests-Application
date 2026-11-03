using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PricingExceptions.IntegrationTests;

public sealed class HttpWorkflowTests
{
    private const string Manager = "11111111-1111-1111-1111-111111111111";
    private const string Reviewer = "22222222-2222-2222-2222-222222222222";
    private const string MortgageProcess = "33333333-3333-3333-3333-333333333333";

    private static WebApplicationFactory<Program> CreateFactory(string environment = "Development")
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Database:Name", Guid.NewGuid().ToString());
        });

    private static HttpClient Client(WebApplicationFactory<Program> factory, string? actor = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        if (actor is not null) client.DefaultRequestHeaders.Add("X-User-Id", actor);
        return client;
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();

    [Fact]
    public async Task CreateApproveReviseAndReapproveRetainsHistoryAndExactRetryResponse()
    {
        await using var factory = CreateFactory();
        using var manager = Client(factory, Manager);
        using var reviewer = Client(factory, Reviewer);
        using var process = Client(factory, MortgageProcess);
        var applicationId = Guid.NewGuid();
        var payload = new { mortgageApplicationId = applicationId, discountBps = 50, reason = "Competitive offer" };
        var created = await Post(manager, "/api/v1/pricing-exceptions", payload, "create");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var snapshot = await Json(created);
        var id = snapshot.GetProperty("id").GetGuid();
        var route = $"/api/v1/pricing-exceptions/{id}";
        Assert.EndsWith(route, created.Headers.Location!.ToString());
        var approval = await Post(reviewer, route + "/decisions",
            new { outcome = "Approved", expectedVersion = 1, reviewedRevisionNumber = 1 }, "approve");
        Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
        Assert.Equal(Reviewer, (await Json(approval)).GetProperty("reviewerId").GetString());
        var discountRoute = $"/api/v1/mortgage-applications/{applicationId}/approved-discount";
        Assert.Equal(50, (await process.GetFromJsonAsync<JsonElement>(discountRoute)).GetProperty("discountBps").GetInt32());
        var replay = await Post(manager, "/api/v1/pricing-exceptions", payload, "create");
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await created.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(created.Headers.Location, replay.Headers.Location);
        var revised = await Post(manager, route + "/revisions",
            new { discountBps = 75, reason = "Updated terms", expectedVersion = 2, expectedRevisionNumber = 1 }, "revise");
        Assert.Equal(HttpStatusCode.Created, revised.StatusCode);
        Assert.Equal("Pending", (await Json(revised)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await process.GetAsync(discountRoute)).StatusCode);
        var stale = await Post(reviewer, route + "/decisions",
            new { outcome = "Approved", expectedVersion = 2, reviewedRevisionNumber = 1 }, "stale");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var reapproval = await Post(reviewer, route + "/decisions",
            new { outcome = "Approved", expectedVersion = 3, reviewedRevisionNumber = 2 }, "reapprove");
        Assert.Equal(HttpStatusCode.OK, reapproval.StatusCode);
        Assert.Equal(75, (await process.GetFromJsonAsync<JsonElement>(discountRoute)).GetProperty("discountBps").GetInt32());
        var history = await manager.GetFromJsonAsync<JsonElement>(route + "/history");
        Assert.Equal(4, history.GetProperty("events").GetArrayLength());
        Assert.Equal(2, history.GetProperty("revisions").GetArrayLength());
        Assert.Equal(2, history.GetProperty("decisions").GetArrayLength());
    }

    [Fact]
    public async Task DeclineAndConflictingRetryDoNotExposeDiscountOrDuplicateHistory()
    {
        await using var factory = CreateFactory();
        using var manager = Client(factory, Manager);
        using var reviewer = Client(factory, Reviewer);
        using var process = Client(factory, MortgageProcess);
        var applicationId = Guid.NewGuid();
        var created = await Post(manager, "/api/v1/pricing-exceptions",
            new { mortgageApplicationId = applicationId, discountBps = 50, reason = "Offer" }, "create");
        var id = (await Json(created)).GetProperty("id").GetGuid();
        var route = $"/api/v1/pricing-exceptions/{id}";
        var conflict = await Post(manager, "/api/v1/pricing-exceptions",
            new { mortgageApplicationId = applicationId, discountBps = 60, reason = "Offer" }, "create");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var declined = await Post(reviewer, route + "/decisions",
            new { outcome = "Declined", reason = "Not eligible", expectedVersion = 1, reviewedRevisionNumber = 1 }, "decline");
        Assert.Equal("Declined", (await Json(declined)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound,
            (await process.GetAsync($"/api/v1/mortgage-applications/{applicationId}/approved-discount")).StatusCode);
        Assert.Equal(2, (await manager.GetFromJsonAsync<JsonElement>(route + "/history")).GetProperty("events").GetArrayLength());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("99999999-9999-9999-9999-999999999999")]
    public async Task MissingInvalidOrUnknownIdentityReturns401(string? actor)
    {
        await using var factory = CreateFactory();
        using var client = Client(factory, actor);
        var response = await client.GetAsync($"/api/v1/pricing-exceptions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task WrongRoleAndMissingResourcesHaveDistinctResponses()
    {
        await using var factory = CreateFactory();
        using var manager = Client(factory, Manager);
        using var reviewer = Client(factory, Reviewer);
        var forbidden = await Post(reviewer, "/api/v1/pricing-exceptions",
            new { mortgageApplicationId = Guid.NewGuid(), discountBps = 50, reason = "Offer" }, "create");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var missing = await manager.GetAsync($"/api/v1/pricing-exceptions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task InvalidAndMissingInputsReturn400()
    {
        await using var factory = CreateFactory();
        using var manager = Client(factory, Manager);
        using var reviewer = Client(factory, Reviewer);
        var badTerms = await Post(manager, "/api/v1/pricing-exceptions",
            new { mortgageApplicationId = Guid.NewGuid(), discountBps = 0, reason = "" }, "invalid");
        Assert.Equal(HttpStatusCode.BadRequest, badTerms.StatusCode);
        var missingKey = await manager.PostAsJsonAsync("/api/v1/pricing-exceptions",
            new { mortgageApplicationId = Guid.NewGuid(), discountBps = 50, reason = "Offer" });
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        var badApplication = await Post(manager, "/api/v1/pricing-exceptions",
            new { mortgageApplicationId = Guid.Empty, discountBps = 50, reason = "Offer" }, "empty-app");
        Assert.Equal(HttpStatusCode.BadRequest, badApplication.StatusCode);
        var missingOutcome = await Post(reviewer, $"/api/v1/pricing-exceptions/{Guid.NewGuid()}/decisions",
            new { expectedVersion = 1, reviewedRevisionNumber = 1 }, "outcome");
        Assert.Equal(HttpStatusCode.BadRequest, missingOutcome.StatusCode);
        var numericOutcome = await Post(reviewer, $"/api/v1/pricing-exceptions/{Guid.NewGuid()}/decisions",
            new { outcome = 0, expectedVersion = 1, reviewedRevisionNumber = 1 }, "numeric");
        Assert.Equal(HttpStatusCode.BadRequest, numericOutcome.StatusCode);
    }

    [Fact]
    public async Task DevelopmentSwaggerDescribesEndpointsAndIdentityHeaders()
    {
        await using var factory = CreateFactory();
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var create = document.GetProperty("paths").GetProperty("/api/v1/pricing-exceptions").GetProperty("post");
        Assert.Contains(create.GetProperty("parameters").EnumerateArray(),
            p => p.GetProperty("name").GetString() == "X-User-Id");
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task ProductionRejectsDemoIdentityAndDoesNotExposeSwagger()
    {
        await using var factory = CreateFactory("Production");
        using var client = Client(factory, Manager);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/api/v1/pricing-exceptions/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/index.html")).StatusCode);
    }
}
