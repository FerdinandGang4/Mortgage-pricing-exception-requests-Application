using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PricingExceptions.API.Data;
using PricingExceptions.API.Exceptions;
using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Services;
using PricingExceptions.API.Services.Interfaces;
using PricingExceptions.API.Services.Models;

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
    public async Task ManagerCanCreateRequestWithApplicationDiscountReasonAndReviewer()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();

        // Act
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Competitive offer", reviewer, manager, "create");

        // Assert
        Assert.Equal(1, created.RevisionNumber);
        Assert.Equal(1, created.Version);
        Assert.Equal(application, created.MortgageApplicationId);
        Assert.Equal(50, created.DiscountBasisPoints);
        Assert.Equal("Competitive offer", created.Reason);
        Assert.Equal(manager, created.CreatedByUserId);
        Assert.Equal("Manager", created.RequesterName);
        Assert.Equal(reviewer, created.AssignedReviewerId);
        Assert.Equal("Reviewer", created.AssignedReviewerName);
        Assert.Equal(created, await service.GetPricingExceptionRequestAsync(created.Id, manager));
    }

    // 2. Request Submission
    // CreatePricingExceptionRequestAsync creates revision 1 and submits it immediately as Pending; there is no draft/submit operation.

    [Fact]
    public async Task CreatingRequestSubmitsItForReviewWithPendingStatus()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();

        // Act
        var created = await CreateRequestAsync(service);

        // Assert
        var availableForReview = Assert.Single(await service.ListPricingExceptionRequestsAsync(reviewer));
        Assert.Equal(created, availableForReview);
        Assert.Equal(RequestStatus.Pending, availableForReview.Status);
        Assert.Equal(reviewer, availableForReview.AssignedReviewerId);
        Assert.Null(availableForReview.ReviewerId);
        Assert.Null(availableForReview.ReviewerName);
        Assert.Null(availableForReview.DecidedAtUtc);
    }

    // 3. Request Approval

    [Fact]
    public async Task ApprovingRequestSetsStatusToApproved()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);

        clock.Advance(TimeSpan.FromMinutes(5));
        var decidedAt = clock.GetUtcNow();

        // Act
        var approved = await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, "Accepted", 1, 1, reviewer, "approve");

        // Assert
        Assert.Equal(RequestStatus.Approved, approved.Status);
        Assert.Equal(2, approved.Version);
        Assert.Equal(reviewer, approved.ReviewerId);
        Assert.Equal("Reviewer", approved.ReviewerName);
        Assert.Equal(decidedAt, approved.DecidedAtUtc);
        Assert.Equal(approved, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        var decision = Assert.Single(await service.GetReviewDecisionsAsync(created.Id, manager));
        Assert.Equal(DecisionOutcome.Approved, decision.Outcome);
        Assert.Equal("Accepted", decision.ReviewReason);
        Assert.Equal(reviewer, decision.ReviewedByUserId);
        Assert.Equal(decidedAt, decision.DecidedAtUtc);
    }

    // 4. Request Decline

    [Fact]
    public async Task DecliningRequestSetsStatusToDeclined()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create");
        clock.Advance(TimeSpan.FromMinutes(5));
        var decidedAt = clock.GetUtcNow();

        // Act
        var declined = await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Declined, "Not eligible", 1, 1, reviewer, "decline");

        // Assert
        Assert.Equal(RequestStatus.Declined, declined.Status);
        Assert.Equal(reviewer, declined.ReviewerId);
        Assert.Equal("Reviewer", declined.ReviewerName);
        Assert.Equal(decidedAt, declined.DecidedAtUtc);
        Assert.Equal(declined, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        var decision = Assert.Single(await service.GetReviewDecisionsAsync(created.Id, manager));
        Assert.Equal(DecisionOutcome.Declined, decision.Outcome);
        Assert.Equal("Not eligible", decision.ReviewReason);
        Assert.Equal(reviewer, decision.ReviewedByUserId);
        Assert.Equal(decidedAt, decision.DecidedAtUtc);
    }

    // 5. Approved Discount Retrieval

    [Fact]
    public async Task ApprovedDiscountIsAvailableToMortgageProcess()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create");
        var approved = await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, "Accepted", 1, 1, reviewer, "approve");

        // Act
        var discount = await scope.ServiceProvider.GetRequiredService<IApprovedDiscountService>().GetAsync(application, mortgageProcess);

        // Assert
        Assert.NotNull(discount);
        Assert.Equal(created.Id, discount.RequestId);
        Assert.Equal(application, discount.MortgageApplicationId);
        Assert.Equal(50, discount.DiscountBasisPoints);
        Assert.Equal(1, discount.RevisionNumber);
        Assert.Equal(reviewer, discount.ReviewerId);
        Assert.Equal(approved.DecidedAtUtc, discount.DecidedAtUtc);
    }

    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.Declined)]
    public async Task UnapprovedDiscountIsNotAvailableToMortgageProcess(RequestStatus status)
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create");
        if (status == RequestStatus.Declined)
            await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Declined, "Not eligible", 1, 1, reviewer, "decline");

        // Act and Assert
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IApprovedDiscountService>().GetAsync(application, mortgageProcess));
    }

    // 6. Request Revision

    [Theory]
    [InlineData(RequestStatus.Pending)]
    [InlineData(RequestStatus.Declined)]
    public async Task RevisingRequestCreatesNewPendingRevision(RequestStatus status)
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        if (status == RequestStatus.Declined)
            created = await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Declined, "Not eligible", 1, 1, reviewer, "decline");

        // Act
        var revised = await service.SubmitRequestRevisionAsync(created.Id, 60, "Updated offer", created.Version, 1, manager, "revise");

        // Assert
        Assert.Equal(RequestStatus.Pending, revised.Status);
        Assert.Equal(2, revised.RevisionNumber);
        Assert.Equal(created.Version + 1, revised.Version);
        Assert.Equal(60, revised.DiscountBasisPoints);
        Assert.Equal("Updated offer", revised.Reason);
        Assert.Equal(manager, revised.CreatedByUserId);
        Assert.Equal(revised, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        Assert.Null(revised.ReviewerId);
    }

    [Fact]
    public async Task RevisingApprovedRequestRequiresNewApproval()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var discounts = scope.ServiceProvider.GetRequiredService<IApprovedDiscountService>();
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Competitive offer", reviewer, manager, "create");
        var pendingDiscount = await discounts.GetAsync(application, mortgageProcess);
        var approved = await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, "Accepted",
            1, 1, reviewer, "approve");
        var discount = await discounts.GetAsync(application, mortgageProcess);

        // Act
        var revised = await service.SubmitRequestRevisionAsync(created.Id, 60, "Updated offer",
            approved.Version, 1, manager, "revise");

        // Assert
        Assert.Equal(RequestStatus.Pending, created.Status);
        Assert.Null(pendingDiscount);
        Assert.Equal(50, discount!.DiscountBasisPoints);
        Assert.Equal(reviewer, discount.ReviewerId);
        Assert.NotNull(approved.DecidedAtUtc);
        Assert.Equal(2, revised.RevisionNumber);
        Assert.Equal(3, revised.Version);
        Assert.Equal(RequestStatus.Pending, revised.Status);
        Assert.Null(await discounts.GetAsync(application, mortgageProcess));
        Assert.Null(revised.ReviewerId);
        Assert.Null(revised.ReviewerName);
        Assert.Null(revised.DecidedAtUtc);
    }

    // 7. Revision Submission and Review
    // SubmitRequestRevisionAsync saves and submits the new revision in one operation.

    [Fact]
    public async Task SubmittingRevisionMakesItAvailableForReview()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);

        // Act
        var revised = await service.SubmitRequestRevisionAsync(created.Id, 65, "Updated offer", 1, 1, manager, "revise");

        // Assert
        var availableForReview = Assert.Single(await service.ListPricingExceptionRequestsAsync(reviewer));
        Assert.Equal(revised, availableForReview);
        Assert.Equal(RequestStatus.Pending, availableForReview.Status);
        Assert.Equal(2, availableForReview.RevisionNumber);
        Assert.Equal(reviewer, availableForReview.AssignedReviewerId);
        Assert.Null(availableForReview.DecidedAtUtc);
    }

    [Theory]
    [InlineData(DecisionOutcome.Approved)]
    [InlineData(DecisionOutcome.Declined)]
    public async Task ReviewingSubmittedRevisionExposesOnlyItsApprovedDiscount(DecisionOutcome outcome)
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create");
        await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "approve-original");
        var revised = await service.SubmitRequestRevisionAsync(created.Id, 75, "Updated offer", 2, 1, manager, "revise");

        // Act
        var reviewed = await service.ReviewPricingExceptionRequestAsync(created.Id, outcome, "Revised request reviewed", revised.Version, revised.RevisionNumber, reviewer, "review-revision");

        var discount = await scope.ServiceProvider.GetRequiredService<IApprovedDiscountService>().GetAsync(application, mortgageProcess);

        // Assert
        Assert.Equal(outcome == DecisionOutcome.Approved ? RequestStatus.Approved : RequestStatus.Declined, reviewed.Status);
        Assert.Equal(reviewer, reviewed.ReviewerId);
        var decision = (await service.GetReviewDecisionsAsync(created.Id, manager)).Single(x => x.RevisionNumber == 2);
        Assert.Equal(outcome, decision.Outcome);
        Assert.Equal("Revised request reviewed", decision.ReviewReason);
        Assert.Equal(reviewed.DecidedAtUtc, decision.DecidedAtUtc);
        if (outcome == DecisionOutcome.Approved)
        {
            Assert.NotNull(discount);
            Assert.Equal(75, discount.DiscountBasisPoints);
            Assert.Equal(2, discount.RevisionNumber);
        }
        else
        {
            Assert.Null(discount);
        }
    }

    // 8. Request History

    [Fact]
    public async Task RequestHistoryPreservesNamesAfterUsersAreRenamed()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();

        // Act
        (await db.Users.SingleAsync(user => user.Id == manager)).DisplayName = "Renamed Manager";
        (await db.Users.SingleAsync(user => user.Id == reviewer)).DisplayName = "Reviewer at decision time";
        await db.SaveChangesAsync();
        var decided = await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "approve");

        (await db.Users.SingleAsync(user => user.Id == reviewer)).DisplayName = "Renamed again";
        await db.SaveChangesAsync();
        var history = await service.GetReviewDecisionsAsync(created.Id, manager);
        var revised = await service.SubmitRequestRevisionAsync(created.Id, 60, "Updated offer", 2, 1, manager, "revise");

        // Assert
        Assert.Equal(manager, created.CreatedByUserId);
        Assert.Equal("Manager", created.RequesterName);
        Assert.Equal(reviewer, created.AssignedReviewerId);
        Assert.Equal("Reviewer", created.AssignedReviewerName);
        Assert.Null(created.ReviewerId);
        Assert.Equal("Manager", decided.RequesterName);
        Assert.Equal("Reviewer", decided.AssignedReviewerName);
        Assert.Equal("Reviewer at decision time", decided.ReviewerName);
        Assert.Equal("Reviewer at decision time", Assert.Single(history).ReviewerName);
        Assert.Equal(reviewer, revised.AssignedReviewerId);
        Assert.Equal("Manager", revised.RequesterName);
        Assert.Null(revised.ReviewerName);
    }

    [Fact]
    public async Task RequestHistoryPreservesPreviousRevisionsAndDecisions()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var createdAt = clock.GetUtcNow();

        // Act
        var created = await service.CreatePricingExceptionRequestAsync(Guid.NewGuid(), 50, "Competitive offer", reviewer, manager, "create");
        clock.Advance(TimeSpan.FromMinutes(5));
        var approvedAt = clock.GetUtcNow();
        await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, "Accepted", 1, 1, reviewer, "approve");
        clock.Advance(TimeSpan.FromMinutes(5));
        var revisedAt = clock.GetUtcNow();
        await service.SubmitRequestRevisionAsync(created.Id, 60, "Updated offer", 2, 1, manager, "revise");
        clock.Advance(TimeSpan.FromMinutes(5));
        var declinedAt = clock.GetUtcNow();
        await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Declined, "Not justified", 3, 2, reviewer, "decline");

        var snapshot = await service.GetRequestHistorySnapshotAsync(created.Id, manager);

        // Assert
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
    public async Task SubmittedRevisionCannotBeModified()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreatePricingExceptionRequestAsync(Guid.NewGuid(), 50, "Original", reviewer, manager, "create");
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var revision = await db.RequestRevisions.SingleAsync(x => x.RequestId == created.Id);

        // Act
        db.Entry(revision).Property(x => x.Reason).CurrentValue = "Overwritten";
        await Assert.ThrowsAsync<BusinessRuleException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // Assert
        Assert.Equal("Original", (await service.GetPricingExceptionRequestAsync(created.Id, manager)).Reason);
    }

    [Theory]
    [InlineData("decision")]
    [InlineData("history")]
    [InlineData("retry")]
    public async Task RequestHistoryAndRetryRecordsCannotBeDeleted(string kind)
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "approve");
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();

        // Act
        if (kind == "decision") db.ReviewDecisions.Remove(await db.ReviewDecisions.SingleAsync());
        else if (kind == "history") db.HistoryEntries.Remove(await db.HistoryEntries.FirstAsync());
        else db.IdempotencyRecords.Remove(await db.IdempotencyRecords.FirstAsync());

        // Assert
        Assert.Throws<BusinessRuleException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        Assert.Equal(2, (await service.GetRequestHistorySnapshotAsync(created.Id, manager)).Events.Count);
    }

    // 9. Validation and Authorization

    [Theory]
    [InlineData(0, "Offer")]
    [InlineData(501, "Offer")]
    [InlineData(50, "  ")]
    public async Task InvalidDiscountOrReasonDoesNotCreateRequest(int discount, string reason)
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();

        // Act and Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreatePricingExceptionRequestAsync(Guid.NewGuid(), discount, reason, reviewer, manager, "create"));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Empty(await db.PricingExceptionRequests.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("manager")]
    [InlineData("mortgage process")]
    public async Task InvalidReviewerAssignmentDoesNotCreateRequest(string assignment)
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var invalid = assignment switch
        {
            "empty" => Guid.Empty,
            "manager" => manager,
            "mortgage process" => mortgageProcess,
            _ => Guid.NewGuid()
        };

        // Act and Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreatePricingExceptionRequestAsync(Guid.NewGuid(), 50, "Offer", invalid, manager, "create"));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Empty(await db.PricingExceptionRequests.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task OtherManagerCannotReviseRequest()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);

        // Act and Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SubmitRequestRevisionAsync(created.Id, 60, "Offer", 1, 1, otherManager, "revise"));
        Assert.Equal(created, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        Assert.Single(await service.GetRequestHistoryAsync(created.Id, manager));
    }

    [Fact]
    public async Task ManagerCannotApproveRequest()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);

        // Act and Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, manager, "review"));
        Assert.Equal(created, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        Assert.Single(await service.GetRequestHistoryAsync(created.Id, manager));
    }

    [Theory]
    [InlineData(DecisionOutcome.Approved)]
    [InlineData(DecisionOutcome.Declined)]
    public async Task OnlyAssignedReviewerCanDecideRequest(DecisionOutcome outcome)
    {
        // Arrange
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var alternate = Guid.NewGuid();
        db.Users.Add(new User { Id = alternate, DisplayName = "Alternate reviewer", Role = UserRole.Reviewer });
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create");

        // Act and Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ReviewPricingExceptionRequestAsync(created.Id, outcome, "Decision", 1, 1, alternate, "wrong-reviewer"));
        Assert.Single((await service.GetRequestHistorySnapshotAsync(created.Id, manager)).Events);
        Assert.Empty(await service.GetReviewDecisionsAsync(created.Id, manager));
    }

    [Fact]
    public async Task RequestCreatorCannotApproveAfterChangingRole()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == manager);
        user.Role = UserRole.Reviewer;
        await db.SaveChangesAsync();

        // Act and Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, manager, "self-review"));
    }

    [Fact]
    public async Task UnknownUserCannotReadRequest()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);

        // Act and Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetPricingExceptionRequestAsync(created.Id, Guid.NewGuid()));
        Assert.Single(await service.GetRequestHistoryAsync(created.Id, manager));
    }

    [Fact]
    public async Task UnknownRequestCannotBeRetrieved()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();

        // Act and Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetPricingExceptionRequestAsync(Guid.NewGuid(), manager));
    }

    [Theory]
    [InlineData(99, 1)]
    [InlineData(1, 99)]
    public async Task ReviewWithOutdatedVersionOrRevisionIsRejected(long version, int revision)
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);

        // Act and Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, version, revision, reviewer, "stale"));
        Assert.Equal(created, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        Assert.Single(await service.GetRequestHistoryAsync(created.Id, manager));
    }

    [Theory]
    [InlineData(99, 1)]
    [InlineData(1, 99)]
    public async Task RevisionWithOutdatedVersionOrRevisionIsRejected(long version, int revision)
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);

        // Act and Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.SubmitRequestRevisionAsync(created.Id, 60, "Updated offer", version, revision, manager, "stale"));

        Assert.Equal(created, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        Assert.Single(await service.GetRequestHistoryAsync(created.Id, manager));
        Assert.Single(await service.GetRequestRevisionsAsync(created.Id, manager));
    }

    // 10. Duplicate Prevention and Idempotency

    [Fact]
    public async Task DuplicateRequestIsRejected()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create");

        // Act and Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "different-key"));
        Assert.Single(await scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>().PricingExceptionRequests.ToListAsync());
    }

    [Fact]
    public async Task RetryingRequestDoesNotCreateDuplicate()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create");

        // Act and Assert
        Assert.Equal(created, await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create"));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Single(await db.PricingExceptionRequests.ToListAsync());
        Assert.Single(await db.HistoryEntries.ToListAsync());
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task RetryingRequestCreationReturnsOriginalResult()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create");
        var reviewed = await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "review");

        // Act and Assert
        Assert.Equal(created, await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create"));
        Assert.Equal(reviewed, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Equal(1, await db.PricingExceptionRequests.CountAsync());
        Assert.Equal(2, await db.HistoryEntries.CountAsync());
        Assert.Equal(2, await db.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task RetryingApprovalDoesNotCreateDuplicateDecision()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        var reviewed = await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "review");

        // Act and Assert
        Assert.Equal(reviewed, await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "review"));
        Assert.Single(await service.GetReviewDecisionsAsync(created.Id, manager));
        Assert.Equal(2, (await service.GetRequestHistoryAsync(created.Id, manager)).Count);
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>().IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task RetryingRevisionDoesNotCreateDuplicateRevision()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        var revised = await service.SubmitRequestRevisionAsync(created.Id, 60, "Updated offer", 1, 1, manager, "revise");

        // Act
        var retried = await service.SubmitRequestRevisionAsync(created.Id, 60, "Updated offer", 1, 1, manager, "revise");

        // Assert
        Assert.Equal(revised, retried);
        Assert.Equal(revised, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        Assert.Equal(2, (await service.GetRequestRevisionsAsync(created.Id, manager)).Count);
        Assert.Equal(2, (await service.GetRequestHistoryAsync(created.Id, manager)).Count);
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>().IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task RetryingRevisionAfterApprovalReturnsSavedRevision()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        var revised = await service.SubmitRequestRevisionAsync(created.Id, 60, "Changed", 1, 1, manager, "revise");
        await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 2, 2, reviewer, "approve");

        // Act and Assert
        Assert.Equal(revised, await service.SubmitRequestRevisionAsync(created.Id, 60, "Changed", 1, 1, manager, "revise"));
        Assert.Equal(3, (await service.GetRequestHistorySnapshotAsync(created.Id, manager)).Events.Count);
    }

    [Fact]
    public async Task ReusingRequestKeyWithDifferentDiscountIsRejected()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create");
        var approved = await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "review");

        // Act and Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.CreatePricingExceptionRequestAsync(application, 60, "Offer", reviewer, manager, "create"));
        Assert.Equal(approved, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Single(await db.PricingExceptionRequests.ToListAsync());
        Assert.Equal(2, await db.HistoryEntries.CountAsync());
        Assert.Equal(2, await db.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task ReusingRequestKeyWithDifferentReviewerIsRejected()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var alternate = Guid.NewGuid();
        db.Users.Add(new User { Id = alternate, DisplayName = "Alternate reviewer", Role = UserRole.Reviewer });
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Offer", reviewer, manager, "create");

        // Act and Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.CreatePricingExceptionRequestAsync(application, 50, "Offer", alternate, manager, "create"));
        Assert.Equal(created, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        Assert.Single(await service.GetRequestHistoryAsync(created.Id, manager));
    }

    [Fact]
    public async Task ReusingReviewKeyWithDifferentDecisionIsRejected()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        var approved = await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, "Accepted", 1, 1, reviewer, "review");

        // Act and Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Declined, "Accepted", 1, 1, reviewer, "review"));

        Assert.Equal(approved, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        Assert.Single(await service.GetReviewDecisionsAsync(created.Id, manager));
        Assert.Equal(2, (await service.GetRequestHistoryAsync(created.Id, manager)).Count);
    }

    [Fact]
    public async Task ReusingRevisionKeyWithDifferentDiscountIsRejected()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        var revised = await service.SubmitRequestRevisionAsync(created.Id, 60, "Updated offer", 1, 1, manager, "revise");

        // Act and Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.SubmitRequestRevisionAsync(created.Id, 70, "Updated offer", 1, 1, manager, "revise"));

        Assert.Equal(revised, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        Assert.Equal(2, (await service.GetRequestRevisionsAsync(created.Id, manager)).Count);
        Assert.Equal(2, (await service.GetRequestHistoryAsync(created.Id, manager)).Count);
    }

    [Theory]
    [InlineData(DecisionOutcome.Approved)]
    [InlineData(DecisionOutcome.Declined)]
    public async Task DecidedRequestCannotBeReviewedAgain(DecisionOutcome outcome)
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        var decided = await service.ReviewPricingExceptionRequestAsync(created.Id, outcome, "Decision", 1, 1, reviewer, "first");

        // Act and Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 2, 1, reviewer, "again"));
        Assert.Equal(decided, await service.GetPricingExceptionRequestAsync(created.Id, manager));
        Assert.Single(await service.GetReviewDecisionsAsync(created.Id, manager));
    }

    // 11. Concurrency and Reliability

    [Fact]
    public async Task ConcurrentRetriesCreateOneRequest()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        async Task<PricingExceptionResult> Create()
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IPricingExceptionService>()
                .CreatePricingExceptionRequestAsync(applicationId, 50, "Offer", reviewer, manager, "same-key");
        }

        // Act
        var results = await Task.WhenAll(Task.Run(Create), Task.Run(Create)).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
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
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        async Task<bool> Decide(DecisionOutcome outcome)
        {
            using var decisionScope = provider.CreateScope();
            try
            {
                await decisionScope.ServiceProvider.GetRequiredService<IPricingExceptionService>()
                    .ReviewPricingExceptionRequestAsync(created.Id, outcome, null, 1, 1, reviewer, outcome.ToString());
                return true;
            }
            catch (ConflictException) { return false; }
        }

        // Act
        var outcomes = await Task.WhenAll(
            Task.Run(() => Decide(DecisionOutcome.Approved)),
            Task.Run(() => Decide(DecisionOutcome.Declined))).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Single(outcomes, x => x);
        var decision = Assert.Single(await service.GetReviewDecisionsAsync(created.Id, manager));
        var current = await service.GetPricingExceptionRequestAsync(created.Id, manager);
        Assert.Equal(decision.Outcome == DecisionOutcome.Approved ? RequestStatus.Approved : RequestStatus.Declined, current.Status);
        Assert.Equal(2, current.Version);
        Assert.Equal(2, (await service.GetRequestHistoryAsync(created.Id, manager)).Count);
    }

    [Fact]
    public async Task ConcurrentReviewAndRevisionAllowOnlyOneChange()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        async Task<bool> Change(bool revise)
        {
            using var changeScope = provider.CreateScope();
            var workflow = changeScope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
            try
            {
                if (revise)
                    await workflow.SubmitRequestRevisionAsync(created.Id, 60, "New offer", 1, 1, manager, "revise");
                else
                    await workflow.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "approve");
                return true;
            }
            catch (ConflictException) { return false; }
        }

        // Act
        var outcomes = await Task.WhenAll(Task.Run(() => Change(true)), Task.Run(() => Change(false))).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Single(outcomes, x => x);
        var snapshot = await service.GetRequestHistorySnapshotAsync(created.Id, manager);
        Assert.Equal(2, snapshot.Events.Count);
        Assert.Equal(2, snapshot.Revisions.Count + snapshot.Decisions.Count);
        Assert.Equal(2, (await service.GetPricingExceptionRequestAsync(created.Id, manager)).Version);
    }

    [Fact]
    public async Task CancelledHistoryReadDoesNotBlockLaterReads()
    {
        // Arrange
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await CreateRequestAsync(service);
        using var readScope = provider.CreateScope();
        var reader = readScope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.GetRequestHistorySnapshotAsync(created.Id, manager, cancellation.Token));
        // Cancellation must not leave the shared gate locked.
        Assert.Single((await reader.GetRequestHistorySnapshotAsync(created.Id, manager).WaitAsync(TimeSpan.FromSeconds(10))).Events);
    }

    // Default valid submission for scenarios that do not vary the original request data.
    private Task<PricingExceptionResult> CreateRequestAsync(IPricingExceptionService service)
        => service.CreatePricingExceptionRequestAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now += duration;
    }

    public void Dispose() => provider.Dispose();
}
