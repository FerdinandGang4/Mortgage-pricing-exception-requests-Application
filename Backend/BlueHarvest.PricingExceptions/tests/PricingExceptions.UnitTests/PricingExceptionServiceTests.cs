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
    private readonly Guid manager = Guid.NewGuid();
    private readonly Guid reviewer = Guid.NewGuid();
    private readonly Guid mortgageProcess = Guid.NewGuid();
    private readonly Guid otherManager = Guid.NewGuid();

    public PricingExceptionServiceTests()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<PricingExceptionsDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddSingleton(TimeProvider.System);
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

    [Fact]
    public async Task ApprovedTermsBecomeUnavailableAfterRevisionAndHistoryIsPreserved()
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
        Assert.Equal(new[] { 50, 60 }, (await service.GetRevisionsAsync(created.Id, manager)).Select(x => x.DiscountBasisPoints));
        Assert.Single(await service.GetDecisionsAsync(created.Id, manager));
        Assert.Equal(new[] { "Created", "Approved", "Revised" },
            (await service.GetHistoryAsync(created.Id, manager)).Select(x => x.Action));
    }

    [Fact]
    public async Task RetryReplaysOriginalResultEvenAfterLaterChanges()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        var reviewed = await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "review");
        Assert.Equal(created, await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create"));
        Assert.Equal(reviewed, await service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, reviewer, "review"));
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(application, 60, "Offer", reviewer, manager, "create"));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Equal(1, await db.PricingExceptionRequests.CountAsync());
        Assert.Equal(2, await db.HistoryEntries.CountAsync());
        Assert.Equal(2, await db.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task DeclineDoesNotExposeADiscountAndCannotBeDecidedAgain()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        var created = await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        var declined = await service.ReviewAsync(created.Id, DecisionOutcome.Declined, "Not eligible", 1, 1, reviewer, "decline");
        Assert.Equal(RequestStatus.Declined, declined.Status);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IApprovedDiscountService>().GetAsync(application, mortgageProcess));
        await Assert.ThrowsAsync<ConflictException>(() => service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 2, 1, reviewer, "again"));
    }

    [Fact]
    public async Task UnauthorizedAndStaleActionsLeaveHistoryUnchanged()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var created = await service.CreateAsync(Guid.NewGuid(), 50, "Offer", reviewer, manager, "create");
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, manager, "review"));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SubmitRevisionAsync(created.Id, 60, "Offer", 1, 1, otherManager, "revise"));
        await Assert.ThrowsAsync<ConflictException>(() => service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 99, 1, reviewer, "stale"));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetAsync(created.Id, Guid.NewGuid()));
        Assert.Single(await service.GetHistoryAsync(created.Id, manager));
    }

    [Fact]
    public async Task CreatorCannotSelfReviewEvenIfTheirRoleChanges()
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
    [InlineData(0, "Offer")]
    [InlineData(501, "Offer")]
    [InlineData(50, "  ")]
    public async Task InvalidTermsAreRejectedWithoutWrites(int discount, string reason)
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Guid.NewGuid(), discount, reason, reviewer, manager, "create"));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Empty(await db.PricingExceptionRequests.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task DuplicateApplicationAndUnknownRequestAreRejected()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var application = Guid.NewGuid();
        await service.CreateAsync(application, 50, "Offer", reviewer, manager, "create");
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(application, 50, "Offer", reviewer, manager, "different-key"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Guid.NewGuid(), manager));
    }

    [Fact]
    public async Task CompetingDecisionsAcceptOnlyOneTransitionAcrossScopes()
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
            Task.Run(() => Decide(DecisionOutcome.Declined)));
        Assert.Single(outcomes, x => x);
        Assert.Single(await service.GetDecisionsAsync(created.Id, manager));
    }


    [Fact]
    public async Task CompetingCreateRetriesProduceOneRequestAndOneHistoryEntry()
    {
        var applicationId = Guid.NewGuid();
        async Task<PricingExceptions.API.Services.Models.PricingExceptionResult> Create()
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IPricingExceptionService>()
                .CreateAsync(applicationId, 50, "Offer", reviewer, manager, "same-key");
        }
        var results = await Task.WhenAll(Task.Run(Create), Task.Run(Create));
        Assert.Equal(results[0], results[1]);
        using var checkScope = provider.CreateScope();
        var db = checkScope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Single(await db.PricingExceptionRequests.ToListAsync());
        Assert.Single(await db.HistoryEntries.ToListAsync());
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task ReviewAndRevisionRaceAcceptsOnlyOneChange()
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
        var outcomes = await Task.WhenAll(Task.Run(() => Change(true)), Task.Run(() => Change(false)));
        Assert.Single(outcomes, x => x);
        var snapshot = await service.GetHistorySnapshotAsync(created.Id, manager);
        Assert.Equal(2, snapshot.Events.Count);
        Assert.Equal(2, snapshot.Revisions.Count + snapshot.Decisions.Count);
        Assert.Equal(2, (await service.GetAsync(created.Id, manager)).Version);
    }

    [Fact]
    public async Task RevisionRetryPreservesOriginalSnapshotAfterApproval()
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
    public async Task CancelledReadDoesNotPreventLaterReads()
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
        Assert.Single((await reader.GetHistorySnapshotAsync(created.Id, manager)).Events);
    }

    [Fact]
    public async Task SubmittedTermsCannotBeModifiedThroughTheDbContext()
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
    public async Task HistoricalRecordsCannotBeDeleted(string kind)
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


    [Fact]
    public async Task CreationCapturesRequesterAndAssignedReviewerNamesAndPreservesThem()
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
        var revised = await service.SubmitRevisionAsync(created.Id, 60, "New terms", 2, 1, manager, "revise");
        Assert.Equal(reviewer, revised.AssignedReviewerId);
        Assert.Equal("Manager", revised.RequesterName);
        Assert.Null(revised.ReviewerName);
    }

    [Fact]
    public async Task OnlyTheSelectedReviewerCanDecideAndAssignmentIsPartOfRetryInput()
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
            service.ReviewAsync(created.Id, DecisionOutcome.Approved, null, 1, 1, alternate, "wrong-reviewer"));
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(application, 50, "Offer", alternate, manager, "create"));
        Assert.Single((await service.GetHistorySnapshotAsync(created.Id, manager)).Events);
        var declined = await service.ReviewAsync(created.Id, DecisionOutcome.Declined, "Not justified", 1, 1, reviewer, "decline");
        Assert.Equal("Reviewer", declined.ReviewerName);
        Assert.Equal(reviewer, declined.ReviewerId);
        Assert.NotNull(declined.DecidedAtUtc);
    }

    [Fact]
    public async Task UnknownNonReviewerAndSelfAssignmentsAreRejectedWithoutWrites()
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        foreach (var invalid in new[] { Guid.Empty, Guid.NewGuid(), manager, mortgageProcess })
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CreateAsync(Guid.NewGuid(), 50, "Offer", invalid, manager, Guid.NewGuid().ToString()));
        var db = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Empty(await db.PricingExceptionRequests.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    public void Dispose() => provider.Dispose();
}
