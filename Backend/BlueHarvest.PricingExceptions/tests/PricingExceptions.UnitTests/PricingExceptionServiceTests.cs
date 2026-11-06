using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PricingExceptions.API.Data;
using PricingExceptions.API.Exceptions;
using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Services;
using PricingExceptions.API.Services.Interfaces;

namespace PricingExceptions.UnitTests;

public sealed class PricingExceptionServiceTests : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly TestClock clock = new();
    private readonly Guid manager = Guid.NewGuid();
    private readonly Guid reviewer = Guid.NewGuid();
    private readonly Guid mortgageProcess = Guid.NewGuid();
    private readonly Guid otherManager = Guid.NewGuid();

    public PricingExceptionServiceTests()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<PricingExceptionsDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<InMemoryWriteLock>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPricingExceptionService, PricingExceptionService>();
        services.AddScoped<IApprovedDiscountService, ApprovedDiscountService>();
        provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        db.Users.AddRange(
            new User { Id = manager, DisplayName = "Manager", Role = UserRole.RelationshipManager },
            new User { Id = reviewer, DisplayName = "Reviewer", Role = UserRole.Reviewer },
            new User { Id = mortgageProcess, DisplayName = "Mortgage process", Role = UserRole.MortgageProcess },
            new User { Id = otherManager, DisplayName = "Other manager", Role = UserRole.RelationshipManager });
        db.SaveChanges();
    }

    // 1. Request Creation

    [Fact]
    public async Task CreatingRequestSetsStatusToPending()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();

        var created = await service.CreateAsync(application, 50, "Competitive offer", reviewer, manager, "create");

        Assert.Equal(RequestStatus.Pending, created.Status);
        Assert.Equal(1, created.RevisionNumber);
        Assert.Equal(1, created.Version);
        Assert.Equal(application, created.MortgageApplicationId);
        Assert.Equal(50, created.DiscountBasisPoints);
        Assert.Equal("Competitive offer", created.Reason);
        Assert.Equal(manager, created.CreatedByUserId);
        Assert.Equal("Manager", created.RequesterName);
        Assert.Equal(reviewer, created.AssignedReviewerId);
        Assert.Equal("Reviewer", created.AssignedReviewerName);
        Assert.Null(created.ReviewerId);
        Assert.Null(created.ReviewerName);
        Assert.Null(created.DecidedAtUtc);
        Assert.Equal(created, await service.GetAsync(created.Id, manager));
    }


    // 2. Request Approval

    [Fact]
    public async Task ApprovingRequestSetsStatusToApproved()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");

        var approved = await service.ReviewAsync(created.Id, DecisionOutcome.Approved, "Accepted", 1, 1, reviewer, "approve");

        Assert.Equal(RequestStatus.Approved, approved.Status);
        Assert.Equal(2, approved.Version);
        Assert.Equal(reviewer, approved.ReviewerId);
        Assert.Equal("Reviewer", approved.ReviewerName);
        Assert.NotNull(approved.DecidedAtUtc);
        Assert.Equal(approved, await service.GetAsync(created.Id, manager));
    }

    // 3. Request Decline

    [Fact]
    public async Task DecliningRequestSetsStatusToDeclined()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        var declined = await service.ReviewAsync(created.Id, DecisionOutcome.Declined, "Not eligible", 1, 1, reviewer, "decline");
        Assert.Equal(RequestStatus.Declined, declined.Status);
        Assert.Equal(reviewer, declined.ReviewerId);
        Assert.Equal("Reviewer", declined.ReviewerName);
        Assert.NotNull(declined.DecidedAtUtc);
        Assert.Equal(declined, await service.GetAsync(created.Id, manager));
    }

    // 4. Approved Discount

    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.Declined)]
    public async Task UnapprovedDiscountIsNotAvailableToMortgageProcess(RequestStatus status)
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        if (status == RequestStatus.Declined)
            await service.ReviewAsync(created.Id, DecisionOutcome.Declined, "Not eligible", 1, 1, reviewer, "decline");

        Assert.Null(await scope.ServiceProvider.GetRequiredService<IApprovedDiscountService>().GetAsync(application, mortgageProcess));
    }

    [Fact]
    public async Task ApprovedDiscountIsAvailableToMortgageProcess()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        var approved = await service.ReviewAsync(created.Id, DecisionOutcome.Approved, "Accepted", 1, 1, reviewer, "approve");

        var discount = await scope.ServiceProvider.GetRequiredService<IApprovedDiscountService>().GetAsync(application, mortgageProcess);

        Assert.NotNull(discount);
        Assert.Equal(created.Id, discount.RequestId);
        Assert.Equal(application, discount.MortgageApplicationId);
        Assert.Equal(50, discount.DiscountBasisPoints);
        Assert.Equal(1, discount.RevisionNumber);
        Assert.Equal(reviewer, discount.ReviewerId);
        Assert.Equal(approved.DecidedAtUtc, discount.DecidedAtUtc);
    }

    // 5. Request Revision

    [Fact]
    public async Task RevisingApprovedRequestRequiresNewApproval()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var discounts = scope.ServiceProvider.GetRequiredService<IApprovedDiscountService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Competitive offer", reviewer, manager, "create");
        Assert.Equal(RequestStatus.Pending, created.Status);
        Assert.Null(await discounts.GetAsync(application, mortgageProcess));
        var approved = await service.ReviewAsync(created.Id, DecisionOutcome.Approved, "Accepted",
            1, 1, reviewer, "approve");
        var discount = await discounts.GetAsync(application, mortgageProcess);
        Assert.Equal(50, discount!.DiscountBasisPoints);
        Assert.Equal(reviewer, discount.ReviewerId);
        Assert.NotNull(approved.DecidedAtUtc);
        var revised = await service.SubmitRevisionAsync(created.Id, 60, "Updated offer",
            approved.Version, 1, manager, "revise");
        Assert.Equal(2, revised.RevisionNumber);
        Assert.Equal(3, revised.Version);
        Assert.Equal(RequestStatus.Pending, revised.Status);
        Assert.Null(await discounts.GetAsync(application, mortgageProcess));
        Assert.Null(revised.ReviewerId);
        Assert.Null(revised.ReviewerName);
        Assert.Null(revised.DecidedAtUtc);
    }

    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.Declined)]
    public async Task RevisingRequestCreatesNewPendingRevision(RequestStatus status)
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        if (status == RequestStatus.Declined)
            created = await service.ReviewAsync(created.Id, DecisionOutcome.Declined, "Not eligible", 1, 1, reviewer, "decline");

        var revised = await service.SubmitRevisionAsync(created.Id, 60, "Updated offer", created.Version, 1, manager, "revise");

        Assert.Equal(RequestStatus.Pending, revised.Status);
        Assert.Equal(2, revised.RevisionNumber);
        Assert.Equal(created.Version + 1, revised.Version);
        Assert.Equal(60, revised.DiscountBasisPoints);
        Assert.Null(revised.ReviewerId);
    }

    [Fact]
    public async Task ApprovingRevisedRequestMakesNewDiscountAvailable()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "approve-original");
        var revised = await service.SubmitRevisionAsync(created.Id, 75, "Updated offer", 2, 1, manager, "revise");
        await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, revised.Version, revised.RevisionNumber, reviewer, "approve-revision");

        var discount = await scope.ServiceProvider.GetRequiredService<IApprovedDiscountService>().GetAsync(application, mortgageProcess);

        Assert.NotNull(discount);
        Assert.Equal(75, discount.DiscountBasisPoints);
        Assert.Equal(2, discount.RevisionNumber);
    }

    // 6. Request History

    [Fact]
    public async Task RequestHistoryPreservesNamesAfterUsersAreRenamed()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        Assert.Equal(manager, created.CreatedByUserId);
        Assert.Equal("Manager", created.RequesterName);
        Assert.Equal(reviewer, created.AssignedReviewerId);
        Assert.Equal("Reviewer", created.AssignedReviewerName);
        Assert.Null(created.ReviewerId);
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        (await db.Users.SingleAsync(user => user.Id == manager)).DisplayName = "Renamed Manager";
        (await db.Users.SingleAsync(user => user.Id == reviewer)).DisplayName = "Reviewer at decision time";
        await db.SaveChangesAsync();
        var decided = await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "approve");
        Assert.Equal("Manager", decided.RequesterName);
        Assert.Equal("Reviewer", decided.AssignedReviewerName);
        Assert.Equal("Reviewer at decision time", decided.ReviewerName);
        (await db.Users.SingleAsync(user => user.Id == reviewer)).DisplayName = "Renamed again";
        await db.SaveChangesAsync();
        var history = await service.GetDecisionsAsync(created.Id, manager);
        Assert.Equal("Reviewer at decision time", Assert.Single(history).ReviewerName);
        var revised = await service.SubmitRevisionAsync(created.Id, 60, "Updated offer", 2, 1, manager, "revise");
        Assert.Equal(reviewer, revised.AssignedReviewerId);
        Assert.Equal("Manager", revised.RequesterName);
        Assert.Null(revised.ReviewerName);
    }


    [Fact]
    public async Task RequestHistoryPreservesPreviousRevisionsAndDecisions()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var createdAt = clock.GetUtcNow();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Competitive offer", reviewer, manager, "create");
        clock.Advance(TimeSpan.FromMinutes(5));
        var approvedAt = clock.GetUtcNow();
        await service.ReviewAsync(created.Id, DecisionOutcome.Approved, "Accepted", 1, 1, reviewer, "approve");
        clock.Advance(TimeSpan.FromMinutes(5));
        var revisedAt = clock.GetUtcNow();
        await service.SubmitRevisionAsync(created.Id, 60, "Updated offer", 2, 1, manager, "revise");
        clock.Advance(TimeSpan.FromMinutes(5));
        var declinedAt = clock.GetUtcNow();
        await service.ReviewAsync(created.Id, DecisionOutcome.Declined, "Not justified", 3, 2, reviewer, "decline");

        var snapshot = await service.GetHistorySnapshotAsync(created.Id, manager);

        Assert.Equal(new[] { 50, 60 }, snapshot.Revisions.Select(x => x.DiscountBasisPoints));
        Assert.Equal(new[] { "Competitive offer", "Updated offer" }, snapshot.Revisions.Select(x => x.Reason));
        Assert.All(snapshot.Revisions, x => Assert.Equal(manager, x.SubmittedByUserId));
        Assert.Equal(new[] { createdAt, revisedAt }, snapshot.Revisions.Select(x => x.SubmittedAtUtc));
        Assert.Equal(new[] { DecisionOutcome.Approved, DecisionOutcome.Declined }, snapshot.Decisions.Select(x => x.Outcome));
        Assert.Equal(new[] { "Accepted", "Not justified" }, snapshot.Decisions.Select(x => x.ReviewReason));
        Assert.All(snapshot.Decisions, x => Assert.Equal(reviewer, x.ReviewedByUserId));
        Assert.All(snapshot.Decisions, x => Assert.Equal("Reviewer", x.ReviewerName));
        Assert.Equal(new[] { approvedAt, declinedAt }, snapshot.Decisions.Select(x => x.DecidedAtUtc));
        Assert.Equal(new[] { "Created", "Approved", "Revised", "Declined" }, snapshot.Events.Select(x => x.Action));
        Assert.Equal(new long[] { 1, 2, 3, 4 }, snapshot.Events.Select(x => x.Sequence));
        Assert.Equal(new[] { 1, 1, 2, 2 }, snapshot.Events.Select(x => x.RevisionNumber));
        Assert.Equal(new[] { manager, reviewer, manager, reviewer }, snapshot.Events.Select(x => x.ActorUserId));
        Assert.Equal(new[] { createdAt, approvedAt, revisedAt, declinedAt }, snapshot.Events.Select(x => x.OccurredAtUtc));
    }

    [Fact]
    public async Task SubmittedRequestRevisionCannotBeChanged()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Original", reviewer, manager, "create");
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var revision = await db.RequestRevisions.SingleAsync(x => x.RequestId == created.Id);
        db.Entry(revision).Property(x => x.Reason).CurrentValue = "Overwritten";
        await Assert.ThrowsAsync<BusinessRuleException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal("Original", (await service.GetAsync(created.Id, manager)).Reason);
    }

    [Theory]
    [InlineData("decision")]
    [InlineData("history")]
    [InlineData("retry")]
    public async Task RequestHistoryAndRetryRecordsCannotBeDeleted(string kind)
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "approve");
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        if (kind == "decision") db.ReviewDecisions.Remove(await db.ReviewDecisions.SingleAsync());
        else if (kind == "history") db.HistoryEntries.Remove(await db.HistoryEntries.FirstAsync());
        else db.IdempotencyRecords.Remove(await db.IdempotencyRecords.FirstAsync());
        Assert.Throws<BusinessRuleException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        Assert.Equal(2, (await service.GetHistorySnapshotAsync(created.Id, manager)).Events.Count);
    }

    // 7. Validation and Authorization

    [Theory]
    [InlineData(0, "Offer")]
    [InlineData(501, "Offer")]
    [InlineData(50, "  ")]
    public async Task InvalidDiscountOrReasonDoesNotCreateRequest(int discount, string reason)
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Guid.NewGuid(), discount, reason, reviewer, manager, "create"));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Empty(await db.PricingExceptionRequests.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    [Theory]
    [InlineData(99, 1)]
    [InlineData(1, 99)]
    public async Task StaleReviewDoesNotChangeRequestHistory(long version, int revision)
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        await Assert.ThrowsAsync<ConflictException>(() => service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, version, revision, reviewer, "stale"));
        Assert.Equal(created, await service.GetAsync(created.Id, manager));
        Assert.Single(await service.GetHistoryAsync(created.Id, manager));
    }

    [Fact]
    public async Task ManagerCannotApproveRequest()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, manager, "review"));
        Assert.Equal(created, await service.GetAsync(created.Id, manager));
        Assert.Single(await service.GetHistoryAsync(created.Id, manager));
    }

    [Fact]
    public async Task OtherManagerCannotReviseRequest()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");

        await Assert.ThrowsAsync<ForbiddenException>(() => service.SubmitRevisionAsync(created.Id, 60, "Offer", 1, 1, otherManager, "revise"));
        Assert.Equal(created, await service.GetAsync(created.Id, manager));
        Assert.Single(await service.GetHistoryAsync(created.Id, manager));
    }

    [Fact]
    public async Task UnknownUserCannotReadRequest()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetAsync(created.Id, Guid.NewGuid()));
        Assert.Single(await service.GetHistoryAsync(created.Id, manager));
    }

    [Fact]
    public async Task RequestCreatorCannotApproveAfterChangingRole()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == manager);
        user.Role = UserRole.Reviewer;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, manager, "self-review"));
    }

    [Theory]
    [InlineData(DecisionOutcome.Approved)]
    [InlineData(DecisionOutcome.Declined)]
    public async Task OnlyAssignedReviewerCanDecideRequest(DecisionOutcome outcome)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var alternate = Guid.NewGuid();
        db.Users.Add(new User { Id = alternate, DisplayName = "Alternate reviewer", Role = UserRole.Reviewer });
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ReviewAsync(created.Id, outcome, "Decision", 1, 1, alternate, "wrong-reviewer"));
        Assert.Single((await service.GetHistorySnapshotAsync(created.Id, manager)).Events);
        Assert.Empty(await service.GetDecisionsAsync(created.Id, manager));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("manager")]
    [InlineData("mortgage process")]
    public async Task InvalidReviewerAssignmentDoesNotCreateRequest(string assignment)
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var invalid = assignment switch
        {
            "empty" => Guid.Empty,
            "manager" => manager,
            "mortgage process" => mortgageProcess,
            _ => Guid.NewGuid()
        };
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(Guid.NewGuid(), 50, "Offer", invalid, manager, "create"));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Empty(await db.PricingExceptionRequests.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task UnknownRequestCannotBeRetrieved()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Guid.NewGuid(), manager));
    }

    // 8. Duplicate Prevention and Reliability

    [Theory]
    [InlineData(DecisionOutcome.Approved)]
    [InlineData(DecisionOutcome.Declined)]
    public async Task DecidedRequestCannotBeReviewedAgain(DecisionOutcome outcome)
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        var decided = await service.ReviewAsync(created.Id, outcome, "Decision", 1, 1, reviewer, "first");

        await Assert.ThrowsAsync<ConflictException>(() => service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 2, 1, reviewer, "again"));
        Assert.Equal(decided, await service.GetAsync(created.Id, manager));
        Assert.Single(await service.GetDecisionsAsync(created.Id, manager));
    }

    [Fact]
    public async Task ReusingRequestKeyWithDifferentReviewerIsRejected()
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var alternate = Guid.NewGuid();
        db.Users.Add(new User { Id = alternate, DisplayName = "Alternate reviewer", Role = UserRole.Reviewer });
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(application, 50, "Offer", alternate, manager, "create"));
        Assert.Equal(created, await service.GetAsync(created.Id, manager));
        Assert.Single(await service.GetHistoryAsync(created.Id, manager));
    }

    [Fact]
    public async Task DuplicateRequestIsRejected()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(application, 50, "Offer", reviewer, manager, "different-key"));
        Assert.Single(await scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>().PricingExceptionRequests.ToListAsync());
    }

    [Fact]
    public async Task RetryingCreationAfterApprovalReturnsOriginalRequest()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        var reviewed = await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "review");
        Assert.Equal(created, await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create"));
        Assert.Equal(reviewed, await service.GetAsync(created.Id, manager));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Equal(1, await db.PricingExceptionRequests.CountAsync());
        Assert.Equal(2, await db.HistoryEntries.CountAsync());
        Assert.Equal(2, await db.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task RetryingRequestDoesNotCreateDuplicate()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");

        Assert.Equal(created, await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create"));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Single(await db.PricingExceptionRequests.ToListAsync());
        Assert.Single(await db.HistoryEntries.ToListAsync());
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task RetryingApprovalDoesNotCreateDuplicateDecision()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        var reviewed = await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "review");

        Assert.Equal(reviewed, await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "review"));
        Assert.Single(await service.GetDecisionsAsync(created.Id, manager));
        Assert.Equal(2, (await service.GetHistoryAsync(created.Id, manager)).Count);
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>().IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task ReusingRequestKeyWithDifferentDiscountIsRejected()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        var approved = await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "review");

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(application, 60, "Offer", reviewer, manager, "create"));
        Assert.Equal(approved, await service.GetAsync(created.Id, manager));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Single(await db.PricingExceptionRequests.ToListAsync());
        Assert.Equal(2, await db.HistoryEntries.CountAsync());
        Assert.Equal(2, await db.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task RetryingRevisionAfterApprovalReturnsSavedRevision()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        var revised = await service.SubmitRevisionAsync(created.Id, 60, "Changed", 1, 1, manager, "revise");
        await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 2, 2, reviewer, "approve");
        Assert.Equal(revised, await service.SubmitRevisionAsync(created.Id, 60, "Changed", 1, 1, manager, "revise"));
        Assert.Equal(3, (await service.GetHistorySnapshotAsync(created.Id, manager)).Events.Count);
    }

    [Fact]
    public async Task ConcurrentRetriesCreateOneRequest()
    {
        var applicationId = Guid.NewGuid();
        async Task<PricingExceptions.API.Services.Models.PricingExceptionResult> Create()
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IPricingExceptionService>()
                .CreateAsync(applicationId, 50, "Offer", reviewer, manager, "same-key");
        }
        var results = await Task.WhenAll(Task.Run(Create), Task.Run(Create)).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(results[0], results[1]);
        using var checkScope = provider.CreateScope();
        var db = checkScope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Single(await db.PricingExceptionRequests.ToListAsync());
        Assert.Single(await db.HistoryEntries.ToListAsync());
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentReviewsAllowOnlyOneDecision()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        async Task<bool> Decide(DecisionOutcome outcome)
        {
            using var decisionScope = provider.CreateScope();
            try
            {
                await decisionScope.ServiceProvider.GetRequiredService<IPricingExceptionService>()
                    .ReviewAsync(created.Id, outcome, null, 1, 1, reviewer, outcome.ToString());
                return true;
            }
            catch (ConflictException) { return false; }
        }
        var outcomes = await Task.WhenAll(
            Task.Run(() => Decide(DecisionOutcome.Approved)),
            Task.Run(() => Decide(DecisionOutcome.Declined))).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(outcomes, x => x);
        Assert.Single(await service.GetDecisionsAsync(created.Id, manager));
    }

    [Fact]
    public async Task ConcurrentReviewAndRevisionAllowOnlyOneChange()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        async Task<bool> Change(bool revise)
        {
            using var changeScope = provider.CreateScope();
            var workflow = changeScope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
            try
            {
                if (revise)
                    await workflow.SubmitRevisionAsync(created.Id, 60, "New offer", 1, 1, manager, "revise");
                else
                    await workflow.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "approve");
                return true;
            }
            catch (ConflictException) { return false; }
        }
        var outcomes = await Task.WhenAll(Task.Run(() => Change(true)), Task.Run(() => Change(false))).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(outcomes, x => x);
        var snapshot = await service.GetHistorySnapshotAsync(created.Id, manager);
        Assert.Equal(2, snapshot.Events.Count);
        Assert.Equal(2, snapshot.Revisions.Count + snapshot.Decisions.Count);
        Assert.Equal(2, (await service.GetAsync(created.Id, manager)).Version);
    }

    [Fact]
    public async Task CancelledHistoryReadDoesNotBlockLaterReads()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        using var readScope = provider.CreateScope();
        var reader = readScope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.GetHistorySnapshotAsync(created.Id, manager, cancellation.Token));
        // Cancellation must not leave the shared gate locked.
        Assert.Single((await reader.GetHistorySnapshotAsync(created.Id, manager).WaitAsync(TimeSpan.FromSeconds(10))).Events);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now += duration;
    }

    public void Dispose() => provider.Dispose();
}
