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
    private const string OtherManager = "11111111-1111-1111-1111-111111111112";
    private const string OtherReviewer = "22222222-2222-2222-2222-222222222223";
    private const string MortgageProcess = "33333333-3333-3333-3333-333333333333";

    public static IEnumerable<object[]> AssignmentCases()
    {
        var managers = new[] { Manager, OtherManager, "11111111-1111-1111-1111-111111111113" };
        var reviewers = new[] { Reviewer, OtherReviewer, "22222222-2222-2222-2222-222222222224", "22222222-2222-2222-2222-222222222225", "22222222-2222-2222-2222-222222222226" };
        foreach (var manager in managers)
            foreach (var reviewer in reviewers)
                foreach (var outcome in new[] { "Approved", "Declined" })
                    yield return new object[] { manager, reviewer, outcome };
    }

    // 1. Request Creation

    [Fact]
    public async Task ManagerCanCreatePricingExceptionRequest()
    {
        // Arrange
        await using var demo = new DemoApi();
        var application = Guid.NewGuid();

        // Act
        using var response = await Post(demo.Manager, Requests, RequestBody(application), "create");
        var created = await Json(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(application, created.GetProperty("mortgageApplicationId").GetGuid());
        Assert.Equal(50, created.GetProperty("discountBps").GetInt32());
        Assert.Equal("Competitive mortgage offer", created.GetProperty("reason").GetString());
        Assert.Equal(Manager, created.GetProperty("requesterId").GetString());
        Assert.Equal("James Wilson (Manager)", created.GetProperty("requesterName").GetString());
        Assert.Equal(Reviewer, created.GetProperty("assignedReviewerId").GetString());
        Assert.Equal("David Miller (Reviewer)", created.GetProperty("assignedReviewerName").GetString());
        Assert.EndsWith(Route(created), response.Headers.Location!.ToString());
    }

    // 2. Request Submission
    // POST creates and submits revision 1 as Pending. There is no separate submit endpoint.

    [Fact]
    public async Task CreatingRequestSubmitsItForAssignedReviewer()
    {
        // Arrange
        await using var demo = new DemoApi();

        // Act
        var created = await demo.CreateRequest();
        var listed = await demo.Reviewer.GetFromJsonAsync<JsonElement>(Requests);

        // Assert
        var pending = Assert.Single(listed.EnumerateArray());
        Assert.Equal(created.GetProperty("id").GetGuid(), pending.GetProperty("id").GetGuid());
        Assert.Equal("Pending", pending.GetProperty("status").GetString());
        Assert.Equal(1, pending.GetProperty("revisionNumber").GetInt32());
        Assert.Equal(1, pending.GetProperty("version").GetInt64());
        Assert.Equal(JsonValueKind.Null, pending.GetProperty("reviewerId").ValueKind);
    }

    // 3. Request Approval

    [Fact]
    public async Task AssignedReviewerCanApprovePendingRequest()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        var before = DateTimeOffset.UtcNow;

        // Act
        using var response = await Post(demo.Reviewer, Route(created) + "/decisions", Decision("Approved", reason: "Competitive offer justified"), "approve");
        var approved = await Json(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.Equal(Reviewer, approved.GetProperty("reviewerId").GetString());
        Assert.Equal("David Miller (Reviewer)", approved.GetProperty("reviewerName").GetString());
        var time = approved.GetProperty("decidedAtUtc").GetDateTimeOffset();
        Assert.InRange(time, before, DateTimeOffset.UtcNow);
        var decision = Assert.Single((await demo.History(created)).GetProperty("decisions").EnumerateArray());
        Assert.Equal("Approved", decision.GetProperty("outcome").GetString());
        Assert.Equal("Competitive offer justified", decision.GetProperty("reason").GetString());
        Assert.Equal(Reviewer, decision.GetProperty("reviewerId").GetString());
        Assert.Equal(time, decision.GetProperty("decidedAtUtc").GetDateTimeOffset());
    }

    // 4. Request Decline

    [Fact]
    public async Task AssignedReviewerCanDeclineRequestWithReason()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();

        // Act
        var declined = await demo.Review(created, "Declined", "Discount exceeds the justified offer");

        // Assert
        Assert.Equal("Declined", declined.GetProperty("status").GetString());
        var history = await demo.History(created);
        var decision = Assert.Single(history.GetProperty("decisions").EnumerateArray());
        Assert.Equal("Declined", decision.GetProperty("outcome").GetString());
        Assert.Equal("Discount exceeds the justified offer", decision.GetProperty("reason").GetString());
        Assert.Equal(Reviewer, decision.GetProperty("reviewerId").GetString());
        Assert.True(DateTimeOffset.TryParse(decision.GetProperty("decidedAtUtc").GetString(), out _));
        Assert.Equal(2, history.GetProperty("events").GetArrayLength());
    }

    // 5. Approved Discount Retrieval

    [Fact]
    public async Task MortgageProcessCanRetrieveApprovedDiscount()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        await demo.Review(created, "Approved");

        // Act
        using var response = await demo.Process.GetAsync(DiscountRoute(created));
        var discount = await Json(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50, discount.GetProperty("discountBps").GetInt32());
        Assert.Equal(Reviewer, discount.GetProperty("reviewerId").GetString());
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Declined")]
    public async Task UnapprovedRequestDoesNotExposeDiscount(string status)
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        if (status == "Declined") await demo.Review(created, "Declined");

        // Act
        using var response = await demo.Process.GetAsync(DiscountRoute(created));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // 6. Request Revision

    [Fact]
    public async Task ManagerCanReviseRequestAndPreserveOriginalRevision()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();

        // Act
        var revised = await demo.Revise(created);

        // Assert
        Assert.Equal(75, revised.GetProperty("discountBps").GetInt32());
        Assert.Equal("Updated competitive offer", revised.GetProperty("reason").GetString());
        Assert.Equal(2, revised.GetProperty("revisionNumber").GetInt32());
        Assert.Equal(2, revised.GetProperty("version").GetInt64());
        var revisions = (await demo.History(created)).GetProperty("revisions");
        Assert.Equal(2, revisions.GetArrayLength());
        Assert.Equal(50, revisions[0].GetProperty("discountBps").GetInt32());
        Assert.Equal("Competitive mortgage offer", revisions[0].GetProperty("reason").GetString());
        Assert.Equal(75, revisions[1].GetProperty("discountBps").GetInt32());
    }

    // 7. Revision Submission and Review
    // POST revisions saves and submits the revision immediately.

    [Fact]
    public async Task SubmittingRevisionRequiresNewApproval()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        var approved = await demo.Review(created, "Approved");

        // Act
        var revised = await demo.Revise(approved);

        // Assert
        Assert.Equal("Pending", revised.GetProperty("status").GetString());
        Assert.Equal(3, revised.GetProperty("version").GetInt64());
        Assert.Equal(JsonValueKind.Null, revised.GetProperty("reviewerId").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await demo.Process.GetAsync(DiscountRoute(created))).StatusCode);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Declined")]
    public async Task AssignedReviewerCanReviewRevisedRequest(string outcome)
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        var approved = await demo.Review(created, "Approved");
        var revised = await demo.Revise(approved);

        // Act
        var reviewed = await demo.Review(revised, outcome);

        // Assert
        Assert.Equal(outcome, reviewed.GetProperty("status").GetString());
        Assert.Equal(2, reviewed.GetProperty("revisionNumber").GetInt32());
        Assert.Equal(Reviewer, reviewed.GetProperty("reviewerId").GetString());
        using var discount = await demo.Process.GetAsync(DiscountRoute(created));
        Assert.Equal(outcome == "Approved" ? HttpStatusCode.OK : HttpStatusCode.NotFound, discount.StatusCode);
        if (outcome == "Approved") Assert.Equal(75, (await Json(discount)).GetProperty("discountBps").GetInt32());
    }

    [Fact]
    public async Task ReviewerListShowsLatestRevisionOfEveryRequest()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var manager = Client(factory, Manager);
        using var reviewer = Client(factory, Reviewer);
        var empty = await manager.GetFromJsonAsync<JsonElement>("/api/v1/pricing-exceptions");
        Assert.Equal(0, empty.GetArrayLength());
        var first = await Post(manager, "/api/v1/pricing-exceptions",
            new { reviewerId = Reviewer, mortgageApplicationId = Guid.NewGuid(), discountBps = 50, reason = "First offer" }, "first");
        var firstId = (await Json(first)).GetProperty("id").GetGuid();
        await Post(manager, "/api/v1/pricing-exceptions",
            new { reviewerId = Reviewer, mortgageApplicationId = Guid.NewGuid(), discountBps = 30, reason = "Second offer" }, "second");
        await Post(reviewer, $"/api/v1/pricing-exceptions/{firstId}/decisions",
            new { outcome = "Approved", expectedVersion = 1, reviewedRevisionNumber = 1 }, "approve");
        await Post(manager, $"/api/v1/pricing-exceptions/{firstId}/revisions",
            new { discountBps = 75, reason = "Updated offer", expectedVersion = 2, expectedRevisionNumber = 1 }, "revise");
        // Act
        var listed = await reviewer.GetFromJsonAsync<JsonElement>("/api/v1/pricing-exceptions");
        // Assert
        Assert.Equal(2, listed.GetArrayLength());
        var current = listed.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == firstId);
        Assert.Equal(75, current.GetProperty("discountBps").GetInt32());
        Assert.Equal(2, current.GetProperty("revisionNumber").GetInt32());
        Assert.Equal("Pending", current.GetProperty("status").GetString());
        Assert.Equal("James Wilson (Manager)", current.GetProperty("requesterName").GetString());
        Assert.Equal("David Miller (Reviewer)", current.GetProperty("assignedReviewerName").GetString());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("reviewerId").ValueKind);
    }

    // 8. Request History

    [Fact]
    public async Task RequestHistoryPreservesRevisionsDecisionsAndBusinessActions()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();

        // Act
        var approved = await demo.Review(created, "Approved", "Original offer accepted");
        var revised = await demo.Revise(approved);
        var declined = await demo.Review(revised, "Declined", "Revised discount not justified");
        var history = await demo.History(created);

        // Assert
        var revisions = history.GetProperty("revisions");
        Assert.Equal(2, revisions.GetArrayLength());
        Assert.Equal(new[] { 50, 75 }, revisions.EnumerateArray().Select(x => x.GetProperty("discountBps").GetInt32()));
        var decisions = history.GetProperty("decisions");
        Assert.Equal(2, decisions.GetArrayLength());
        Assert.Equal(new[] { "Approved", "Declined" }, decisions.EnumerateArray().Select(x => x.GetProperty("outcome").GetString()));
        Assert.Equal(new[] { "Original offer accepted", "Revised discount not justified" }, decisions.EnumerateArray().Select(x => x.GetProperty("reason").GetString()));
        Assert.All(decisions.EnumerateArray(), d => {
            Assert.Equal(Reviewer, d.GetProperty("reviewerId").GetString());
            Assert.Equal("David Miller (Reviewer)", d.GetProperty("reviewerName").GetString());
            Assert.True(DateTimeOffset.TryParse(d.GetProperty("decidedAtUtc").GetString(), out _));
        });
        Assert.Equal(approved.GetProperty("decidedAtUtc").GetDateTimeOffset(), decisions[0].GetProperty("decidedAtUtc").GetDateTimeOffset());
        Assert.Equal(declined.GetProperty("decidedAtUtc").GetDateTimeOffset(), decisions[1].GetProperty("decidedAtUtc").GetDateTimeOffset());
        Assert.Equal(new[] { "Created", "Approved", "Revised", "Declined" }, history.GetProperty("events").EnumerateArray().Select(x => x.GetProperty("action").GetString()));
    }

    // 9. Manager and Reviewer Authorization

    [Theory]
    [MemberData(nameof(AssignmentCases))]
    public async Task ManagersCanAssignRequestsToDifferentReviewers(string managerId, string reviewerId, string outcome)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var manager = Client(factory, managerId);
        using var reviewer = Client(factory, reviewerId);
        var directory = await manager.GetFromJsonAsync<JsonElement>("/api/v1/users/reviewers");
        var selected = directory.EnumerateArray().Single(x => x.GetProperty("id").GetString() == reviewerId);

        // Act
        using var response = await Post(manager, Requests, RequestBody(Guid.NewGuid(), reviewerId: reviewerId), "create");
        var created = await Json(response);
        using var review = await Post(reviewer, Route(created) + "/decisions", Decision(outcome, reason: "Independent review reason"), "review");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(managerId, created.GetProperty("requesterId").GetString());
        Assert.Equal(reviewerId, created.GetProperty("assignedReviewerId").GetString());
        Assert.Equal(selected.GetProperty("displayName").GetString(), created.GetProperty("assignedReviewerName").GetString());
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Equal(outcome, (await Json(review)).GetProperty("status").GetString());
        var history = await manager.GetFromJsonAsync<JsonElement>(Route(created) + "/history");
        Assert.Equal(2, history.GetProperty("events").GetArrayLength());
        var saved = Assert.Single(history.GetProperty("decisions").EnumerateArray());
        Assert.Equal(reviewerId, saved.GetProperty("reviewerId").GetString());
        Assert.Equal(selected.GetProperty("displayName").GetString(), saved.GetProperty("reviewerName").GetString());
        Assert.Equal(outcome, saved.GetProperty("outcome").GetString());
        Assert.Equal("Independent review reason", saved.GetProperty("reason").GetString());
        Assert.True(DateTimeOffset.TryParse(saved.GetProperty("decidedAtUtc").GetString(), out _));
    }

    [Theory]
    [MemberData(nameof(AssignmentCases))]
    public async Task OnlyAssignedReviewerCanDecideRequest(string managerId, string reviewerId, string outcome)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var manager = Client(factory, managerId);
        using var response = await Post(manager, Requests, RequestBody(Guid.NewGuid(), reviewerId: reviewerId), "create");
        var created = await Json(response);
        var reviewers = await manager.GetFromJsonAsync<JsonElement>("/api/v1/users/reviewers");

        // Act and Assert: every unassigned reviewer is checked for all 30 combinations.
        foreach (var user in reviewers.EnumerateArray().Where(x => x.GetProperty("id").GetString() != reviewerId))
        {
            using var other = Client(factory, user.GetProperty("id").GetString());
            using var denied = await Post(other, Route(created) + "/decisions", Decision(outcome), "unassigned-review");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        var history = await manager.GetFromJsonAsync<JsonElement>(Route(created) + "/history");
        Assert.Empty(history.GetProperty("decisions").EnumerateArray());
        Assert.Single(history.GetProperty("events").EnumerateArray());
    }

    [Theory]
    [InlineData(Reviewer)]
    [InlineData(MortgageProcess)]
    public async Task OnlyManagersCanCreatePricingExceptionRequests(string actor)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = Client(factory, actor);

        // Act
        using var response = await Post(client, Requests, RequestBody(Guid.NewGuid()), "create");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Declined")]
    public async Task ManagerCannotApproveOrDeclineOwnRequest(string outcome)
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();

        // Act
        using var response = await Post(demo.Manager, Route(created) + "/decisions", Decision(outcome), "self-review");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtherManagerCannotReviseRequest()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        using var other = Client(demo.Factory, OtherManager);

        // Act
        using var response = await Post(other, Route(created) + "/revisions", Revision(created), "revise");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single((await demo.History(created)).GetProperty("revisions").EnumerateArray());
    }

    [Fact]
    public async Task ManagerCanSelectFromFiveReviewers()
    {
        // Arrange
        await using var demo = new DemoApi();

        // Act
        var reviewers = await demo.Manager.GetFromJsonAsync<JsonElement>("/api/v1/users/reviewers");

        // Assert
        Assert.Equal(5, reviewers.GetArrayLength());
        Assert.All(reviewers.EnumerateArray(), user => Assert.Equal("Reviewer", user.GetProperty("role").GetString()));
        Assert.Equal("David Miller (Reviewer)", reviewers.EnumerateArray().Single(x => x.GetProperty("id").GetString() == Reviewer).GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task ReviewerCannotAccessManagerReviewerDirectory()
    {
        // Arrange
        await using var demo = new DemoApi();

        // Act
        using var response = await demo.Reviewer.GetAsync("/api/v1/users/reviewers");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // 10. Request Validation

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task InvalidDiscountIsRejected(int discount)
    {
        // Arrange
        await using var demo = new DemoApi();

        // Act
        using var response = await Post(demo.Manager, Requests, RequestBody(Guid.NewGuid(), discount: discount), "create");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task MissingBusinessReasonIsRejected(string? reason)
    {
        // Arrange
        await using var demo = new DemoApi();

        // Act
        using var response = await Post(demo.Manager, Requests, RequestBody(Guid.NewGuid(), reason: reason), "create");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData(MortgageProcess)]
    [InlineData("99999999-9999-9999-9999-999999999999")]
    public async Task InvalidReviewerAssignmentIsRejected(string? reviewerId)
    {
        // Arrange
        await using var demo = new DemoApi();

        // Act
        using var response = await Post(demo.Manager, Requests, RequestBody(Guid.NewGuid(), reviewerId: reviewerId), "create");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidMortgageApplicationIdIsRejected(string? applicationId)
    {
        // Arrange
        await using var demo = new DemoApi();
        var body = new { mortgageApplicationId = applicationId, discountBps = 50, reason = "Competitive mortgage offer", reviewerId = Reviewer };

        // Act
        using var response = await Post(demo.Manager, Requests, body, "create");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingIdempotencyKeyIsRejected()
    {
        // Arrange
        await using var demo = new DemoApi();

        // Act
        using var response = await demo.Manager.PostAsJsonAsync(Requests, RequestBody(Guid.NewGuid()));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"expectedVersion\":1,\"reviewedRevisionNumber\":1}")]
    [InlineData("{\"outcome\":0,\"expectedVersion\":1,\"reviewedRevisionNumber\":1}")]
    [InlineData("{\"outcome\":\"Unknown\",\"expectedVersion\":1,\"reviewedRevisionNumber\":1}")]
    public async Task MissingOrInvalidDecisionIsRejected(string json)
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        var body = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        using var response = await Post(demo.Reviewer, Route(created) + "/decisions", body, "review");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await demo.History(created)).GetProperty("decisions").EnumerateArray());
    }

    [Fact]
    public async Task ReviewOfOutdatedRevisionIsRejected()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        var approved = await demo.Review(created, "Approved");
        await demo.Revise(approved);

        // Act
        using var response = await Post(demo.Reviewer, Route(created) + "/decisions", Decision("Approved", 2, 1), "stale-review");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(3, (await demo.History(created)).GetProperty("events").GetArrayLength());
    }

    // 11. Duplicate Prevention and Idempotency

    [Fact]
    public async Task DuplicatePricingExceptionRequestIsRejected()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();

        // Act
        using var response = await Post(demo.Manager, Requests, RequestBody(created.GetProperty("mortgageApplicationId").GetGuid()), "different-key");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single((await demo.History(created)).GetProperty("events").EnumerateArray());
    }

    [Fact]
    public async Task RetryingRequestCreationReturnsOriginalResponse()
    {
        // Arrange
        await using var demo = new DemoApi();
        var body = RequestBody(Guid.NewGuid());
        using var original = await Post(demo.Manager, Requests, body, "create");
        var created = await Json(original);
        var approved = await demo.Review(created, "Approved");
        await demo.Revise(approved);

        // Act
        using var retry = await Post(demo.Manager, Requests, body, "create");

        // Assert
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(await original.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());
        Assert.EndsWith(Route(created), original.Headers.Location!.ToString());
        Assert.Equal(original.Headers.Location, retry.Headers.Location);
        Assert.Equal(3, (await demo.History(created)).GetProperty("events").GetArrayLength());
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Declined")]
    public async Task RetryingReviewDoesNotCreateDuplicateDecision(string outcome)
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        var body = Decision(outcome);
        using var original = await Post(demo.Reviewer, Route(created) + "/decisions", body, "review");

        // Act
        using var retry = await Post(demo.Reviewer, Route(created) + "/decisions", body, "review");

        // Assert
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(await original.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());
        Assert.Single((await demo.History(created)).GetProperty("decisions").EnumerateArray());
        Assert.Equal(2, (await demo.History(created)).GetProperty("events").GetArrayLength());
    }

    [Fact]
    public async Task RetryingRevisionDoesNotCreateDuplicateRevision()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        var body = Revision(created);
        using var original = await Post(demo.Manager, Route(created) + "/revisions", body, "revise");

        // Act
        using var retry = await Post(demo.Manager, Route(created) + "/revisions", body, "revise");

        // Assert
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(await original.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());
        Assert.EndsWith(Route(created), original.Headers.Location!.ToString());
        Assert.Equal(original.Headers.Location, retry.Headers.Location);
        Assert.Equal(2, (await demo.History(created)).GetProperty("revisions").GetArrayLength());
        Assert.Equal(2, (await demo.History(created)).GetProperty("events").GetArrayLength());
    }

    [Theory]
    [InlineData("discount")]
    [InlineData("reviewer")]
    [InlineData("reason")]
    public async Task ReusingIdempotencyKeyWithDifferentDataIsRejected(string field)
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        var application = created.GetProperty("mortgageApplicationId").GetGuid();
        var changed = field == "discount" ? RequestBody(application, discount: 60)
            : field == "reviewer" ? RequestBody(application, reviewerId: OtherReviewer)
            : RequestBody(application, reason: "Changed business reason");

        // Act
        using var response = await Post(demo.Manager, Requests, changed, "create");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single((await demo.History(created)).GetProperty("events").EnumerateArray());
    }

    [Fact]
    public async Task DecidedRevisionCannotReceiveAnotherDecision()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        var approved = await demo.Review(created, "Approved");

        // Act
        using var response = await Post(demo.Reviewer, Route(created) + "/decisions", Decision("Declined", approved.GetProperty("version").GetInt64()), "another-review");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single((await demo.History(created)).GetProperty("decisions").EnumerateArray());
    }

    // 12. API Security and HTTP Responses

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("99999999-9999-9999-9999-999999999999")]
    public async Task MissingInvalidOrUnknownIdentityReturns401(string? actor)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = Client(factory, actor);

        // Act
        using var response = await client.GetAsync(Requests + "/" + Guid.NewGuid());
        using var list = await client.GetAsync(Requests);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/history")]
    public async Task UnknownRequestReturns404(string suffix)
    {
        // Arrange
        await using var demo = new DemoApi();

        // Act
        using var response = await demo.Manager.GetAsync(Requests + "/" + Guid.NewGuid() + suffix);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CurrentUserReflectsAuthenticatedManager()
    {
        // Arrange
        await using var demo = new DemoApi();

        // Act
        var me = await demo.Manager.GetFromJsonAsync<JsonElement>("/api/v1/users/me");

        // Assert
        Assert.Equal(Manager, me.GetProperty("id").GetString());
        Assert.Equal("James Wilson (Manager)", me.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task RequestBodyCannotOverrideAuthenticatedManagerOrReviewerName()
    {
        // Arrange
        await using var demo = new DemoApi();
        var body = new { mortgageApplicationId = Guid.NewGuid(), discountBps = 50, reason = "Competitive mortgage offer", reviewerId = Reviewer,
            requesterId = Reviewer, requesterName = "Spoofed requester", assignedReviewerName = "Spoofed reviewer" };

        // Act
        using var response = await Post(demo.Manager, Requests, body, "create");
        var created = await Json(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(Manager, created.GetProperty("requesterId").GetString());
        Assert.Equal("James Wilson (Manager)", created.GetProperty("requesterName").GetString());
        Assert.Equal(Reviewer, created.GetProperty("assignedReviewerId").GetString());
        Assert.Equal("David Miller (Reviewer)", created.GetProperty("assignedReviewerName").GetString());
    }

    [Fact]
    public async Task DecisionBodyCannotOverrideAuthenticatedReviewer()
    {
        // Arrange
        await using var demo = new DemoApi();
        var created = await demo.CreateRequest();
        var body = new { outcome = "Approved", expectedVersion = 1, reviewedRevisionNumber = 1, reviewerId = Manager, reviewerName = "Spoofed reviewer" };

        // Act
        using var response = await Post(demo.Reviewer, Route(created) + "/decisions", body, "approve");
        var approved = await Json(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Reviewer, approved.GetProperty("reviewerId").GetString());
        Assert.Equal("David Miller (Reviewer)", approved.GetProperty("reviewerName").GetString());
        var decision = Assert.Single((await demo.History(created)).GetProperty("decisions").EnumerateArray());
        Assert.Equal("David Miller (Reviewer)", decision.GetProperty("reviewerName").GetString());
        Assert.False(string.IsNullOrWhiteSpace(decision.GetProperty("decidedAtUtc").GetString()));
    }

    [Fact]
    public async Task DevelopmentSwaggerDescribesEndpointsAndIdentityHeaders()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = Client(factory);
        // Act and Assert
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var endpoints = document.GetProperty("paths").EnumerateObject()
            .Where(path => path.Name.StartsWith("/api/v1/pricing-exceptions", StringComparison.Ordinal))
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => operation.Name + " " + path.Name))
            .OrderBy(endpoint => endpoint).ToArray();
        Assert.Equal(new[]
        {
            "get /api/v1/pricing-exceptions",
            "get /api/v1/pricing-exceptions/{id}",
            "get /api/v1/pricing-exceptions/{id}/history",
            "post /api/v1/pricing-exceptions",
            "post /api/v1/pricing-exceptions/{id}/decisions",
            "post /api/v1/pricing-exceptions/{id}/revisions"
        }.OrderBy(endpoint => endpoint), endpoints);
        var create = document.GetProperty("paths").GetProperty("/api/v1/pricing-exceptions").GetProperty("post");
        Assert.Contains(create.GetProperty("parameters").EnumerateArray(),
            p => p.GetProperty("name").GetString() == "X-User-Id");
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task ProductionRejectsDemoIdentity()
    {
        // Arrange
        await using var factory = CreateFactory("Production");
        using var client = Client(factory, Manager);

        // Act
        using var response = await client.GetAsync(Requests + "/" + Guid.NewGuid());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task ProductionDoesNotExposeDevelopmentDocumentation(string path)
    {
        // Arrange
        await using var factory = CreateFactory("Production");
        using var client = Client(factory);

        // Act
        using var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private const string Requests = "/api/v1/pricing-exceptions";

    private static object RequestBody(Guid application, int discount = 50, string? reason = "Competitive mortgage offer", string? reviewerId = Reviewer)
        => new { mortgageApplicationId = application, discountBps = discount, reason, reviewerId };

    private static object Decision(string outcome, long version = 1, int revision = 1, string reason = "Independent review reason")
        => new { outcome, reason, expectedVersion = version, reviewedRevisionNumber = revision };

    private static object Revision(JsonElement request)
        => new { discountBps = 75, reason = "Updated competitive offer", expectedVersion = request.GetProperty("version").GetInt64(), expectedRevisionNumber = request.GetProperty("revisionNumber").GetInt32() };

    private static string Route(JsonElement request) => Requests + "/" + request.GetProperty("id").GetGuid();
    private static string DiscountRoute(JsonElement request) => "/api/v1/mortgage-applications/" + request.GetProperty("mortgageApplicationId").GetGuid() + "/approved-discount";

    private static WebApplicationFactory<Program> CreateFactory(string environment = "Development")
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Database:Provider", "InMemory");
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

    // Common HTTP setup only. Each test owns a fresh application and InMemory database.
    private sealed class DemoApi : IAsyncDisposable
    {
        public WebApplicationFactory<Program> Factory { get; } = CreateFactory();
        public HttpClient Manager { get; }
        public HttpClient Reviewer { get; }
        public HttpClient Process { get; }

        public DemoApi()
        {
            Manager = Client(Factory, HttpWorkflowTests.Manager);
            Reviewer = Client(Factory, HttpWorkflowTests.Reviewer);
            Process = Client(Factory, MortgageProcess);
        }

        public async Task<JsonElement> CreateRequest()
        {
            using var response = await Post(Manager, Requests, RequestBody(Guid.NewGuid()), "create");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await Json(response);
        }

        public async Task<JsonElement> Review(JsonElement request, string outcome, string reason = "Independent review reason")
        {
            using var response = await Post(Reviewer, Route(request) + "/decisions", Decision(outcome, request.GetProperty("version").GetInt64(), request.GetProperty("revisionNumber").GetInt32(), reason), "review-" + request.GetProperty("revisionNumber"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await Json(response);
        }

        public async Task<JsonElement> Revise(JsonElement request)
        {
            using var response = await Post(Manager, Route(request) + "/revisions", Revision(request), "revise-" + request.GetProperty("revisionNumber"));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await Json(response);
        }

        public Task<JsonElement> History(JsonElement request) => Manager.GetFromJsonAsync<JsonElement>(Route(request) + "/history");

        public async ValueTask DisposeAsync()
        {
            Manager.Dispose();
            Reviewer.Dispose();
            Process.Dispose();
            await Factory.DisposeAsync();
        }
    }
}
