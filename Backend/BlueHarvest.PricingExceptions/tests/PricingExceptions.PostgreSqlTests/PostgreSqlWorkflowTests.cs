using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PricingExceptions.API.Data;
using PricingExceptions.API.Data.Seed;
using PricingExceptions.API.Exceptions;
using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Services;
using PricingExceptions.API.Services.Interfaces;
using PricingExceptions.API.Services.Models;

namespace PricingExceptions.PostgreSqlTests;

[Collection("PostgreSQL")]
public sealed class PostgreSqlWorkflowTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly Guid Manager = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Reviewer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MortgageProcess = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private string connectionString = "";
    private ServiceProvider provider = null!;

    public async Task InitializeAsync()
    {
        connectionString = await fixture.CreateDatabaseAsync();
        var services = new ServiceCollection();
        services.AddDbContext<PricingExceptionsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<InMemoryWriteLock>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPricingExceptionService, PricingExceptionService>();
        services.AddScoped<IApprovedDiscountService, ApprovedDiscountService>();
        provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        await database.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(database);
    }

    // 1. Persistence and history across independent database contexts

    [Fact]
    public async Task RequestHistoryAndRetryResultsSurviveNewDatabaseContexts()
    {
        // Arrange
        var application = Guid.NewGuid();
        var created = await CreateRequest(application);

        // Act: each operation opens an independent scope and connection.
        var approved = await Review(created, DecisionOutcome.Approved);
        var revised = await Revise(approved);
        var declined = await Review(revised, DecisionOutcome.Declined);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var loaded = await service.GetAsync(created.Id, Manager);
        var history = await service.GetHistorySnapshotAsync(created.Id, Manager);
        var retried = await service.CreateAsync(application, 50, "Competitive mortgage offer", Reviewer, Manager, "create");

        // Assert
        Assert.Equal(declined with { DecidedAtUtc = PostgreSqlTimestamp(declined.DecidedAtUtc) }, loaded);
        Assert.Equal(created, retried);
        Assert.Equal(new[] { 50, 75 }, history.Revisions.Select(x => x.DiscountBasisPoints));
        Assert.Equal(new[] { DecisionOutcome.Approved, DecisionOutcome.Declined }, history.Decisions.Select(x => x.Outcome));
        Assert.All(history.Decisions, x => Assert.Equal(Reviewer, x.ReviewedByUserId));
        Assert.All(history.Decisions, x => Assert.Equal("David Miller (Reviewer)", x.ReviewerName));
        Assert.Equal(new[] { PostgreSqlTimestamp(approved.DecidedAtUtc), PostgreSqlTimestamp(declined.DecidedAtUtc) },
            history.Decisions.Select(x => (DateTimeOffset?)x.DecidedAtUtc));
        Assert.Equal(new[] { "Created", "Approved", "Revised", "Declined" }, history.Events.Select(x => x.Action));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IApprovedDiscountService>().GetAsync(application, MortgageProcess));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>().Database.GetPendingMigrationsAsync());
    }

    // 2. Relational constraints and duplicate prevention

    [Fact]
    public async Task DatabaseRejectsDuplicateRevisionNumber()
    {
        // Arrange
        var created = await CreateRequest();
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        database.RequestRevisions.Add(new RequestRevision
        {
            RequestId = created.Id, RevisionNumber = 1, DiscountBasisPoints = 60,
            Reason = "Duplicate submitted revision", SubmittedByUserId = Manager, SubmittedAtUtc = DateTimeOffset.UtcNow
        });

        // Act and Assert: a real PostgreSQL unique violation, not a service validation.
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        Assert.Single(await database.RequestRevisions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task DatabaseAllowsOnlyOneDecisionPerRevision()
    {
        // Arrange
        var created = await CreateRequest();
        await Review(created, DecisionOutcome.Approved);
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        database.ReviewDecisions.Add(NewDecision(created.Id, DecisionOutcome.Declined));

        // Act and Assert
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        Assert.Single(await database.ReviewDecisions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task DatabaseRejectsRevisionWithUnknownManager()
    {
        // Arrange
        var created = await CreateRequest();
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        database.RequestRevisions.Add(new RequestRevision
        {
            RequestId = created.Id, RevisionNumber = 2, DiscountBasisPoints = 75,
            Reason = "Unknown submitting manager", SubmittedByUserId = Guid.NewGuid(), SubmittedAtUtc = DateTimeOffset.UtcNow
        });

        // Act and Assert
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        Assert.Single(await database.RequestRevisions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task DuplicateApplicationIsRejectedAcrossIndependentContexts()
    {
        // Arrange
        var application = Guid.NewGuid();
        var created = await CreateRequest(application);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();

        // Act and Assert: one request per application is currently a service rule.
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(application, 50, "Competitive mortgage offer", Reviewer, Manager, "another-create"));
        var database = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Single(await database.PricingExceptionRequests.ToListAsync());
        Assert.Single((await service.GetHistorySnapshotAsync(created.Id, Manager)).Events);
    }

    // 3. Transactions and consistency

    [Fact]
    public async Task FailedSaveRollsBackRequestDecisionHistoryAndRetryRecord()
    {
        // Arrange
        var created = await CreateRequest();
        using (var scope = provider.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
            var request = await database.PricingExceptionRequests.SingleAsync();
            request.Status = RequestStatus.Approved;
            request.Version = 2;
            database.ReviewDecisions.Add(NewDecision(created.Id, DecisionOutcome.Approved));
            database.HistoryEntries.Add(new HistoryEntry
            {
                Id = Guid.NewGuid(), RequestId = created.Id, RevisionNumber = 1, Sequence = 2,
                Action = "Approved", ActorUserId = Reviewer, OccurredAtUtc = DateTimeOffset.UtcNow
            });
            database.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Id = Guid.NewGuid(), RequestId = created.Id, ActorUserId = Guid.NewGuid(),
                OperationScope = "review:" + created.Id, Key = "failed-review", PayloadHash = "test", ResponseJson = "{}", ResponseStatusCode = 200
            });

            // Act: the invalid retry actor causes a foreign-key failure in the same save.
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }

        // Assert from a new context: no partial writes survived the transaction.
        using var check = provider.CreateScope();
        var stored = check.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var original = await stored.PricingExceptionRequests.SingleAsync();
        Assert.Equal(RequestStatus.Pending, original.Status);
        Assert.Equal(1, original.Version);
        Assert.Empty(await stored.ReviewDecisions.ToListAsync());
        Assert.Single(await stored.RequestRevisions.ToListAsync());
        Assert.Single(await stored.HistoryEntries.ToListAsync());
        Assert.Single(await stored.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task DatabaseRejectsUpdateFromAnOutdatedContext()
    {
        // Arrange: two contexts load version 1 before either writes.
        await CreateRequest();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var firstDatabase = first.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var secondDatabase = second.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var firstRequest = await firstDatabase.PricingExceptionRequests.SingleAsync();
        var secondRequest = await secondDatabase.PricingExceptionRequests.SingleAsync();
        firstRequest.Version++;
        secondRequest.Version++;
        await firstDatabase.SaveChangesAsync();

        // Act and Assert: EF includes the original version in the PostgreSQL update.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDatabase.SaveChangesAsync());
        using var check = provider.CreateScope();
        Assert.Equal(2, (await check.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>().PricingExceptionRequests.SingleAsync()).Version);
    }

    // 4. Concurrent workflow operations within the supported single API instance

    [Fact]
    public async Task ConcurrentCreationRetriesPersistOneRequest()
    {
        // Arrange
        var application = Guid.NewGuid();

        // Act
        var results = await Task.WhenAll(Task.Run(() => CreateRequest(application)), Task.Run(() => CreateRequest(application)))
            .WaitAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.Equal(results[0], results[1]);
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        Assert.Single(await database.PricingExceptionRequests.ToListAsync());
        Assert.Single(await database.HistoryEntries.ToListAsync());
        Assert.Single(await database.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentApprovalAndDeclinePersistOneDecision()
    {
        // Arrange
        var created = await CreateRequest();

        // Act
        var outcomes = await Task.WhenAll(
            Task.Run(() => TryChange(() => Review(created, DecisionOutcome.Approved))),
            Task.Run(() => TryChange(() => Review(created, DecisionOutcome.Declined))))
            .WaitAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.Single(outcomes, success => success);
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PricingExceptionsDbContext>();
        var decision = await database.ReviewDecisions.SingleAsync();
        var request = await database.PricingExceptionRequests.SingleAsync();
        Assert.Equal(decision.Outcome == DecisionOutcome.Approved ? RequestStatus.Approved : RequestStatus.Declined, request.Status);
        Assert.Equal(2, request.Version);
        Assert.Equal(2, await database.HistoryEntries.CountAsync());
        Assert.Equal(2, await database.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task ConcurrentReviewAndRevisionPersistOneChange()
    {
        // Arrange
        var created = await CreateRequest();

        // Act
        var outcomes = await Task.WhenAll(
            Task.Run(() => TryChange(() => Review(created, DecisionOutcome.Approved))),
            Task.Run(() => TryChange(() => Revise(created))))
            .WaitAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.Single(outcomes, success => success);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPricingExceptionService>();
        var history = await service.GetHistorySnapshotAsync(created.Id, Manager);
        Assert.Equal(2, history.Events.Count);
        Assert.Equal(2, history.Revisions.Count + history.Decisions.Count);
        var current = await service.GetAsync(created.Id, Manager);
        Assert.Equal(2, current.Version);
        Assert.Equal(history.Decisions.Count == 1 ? RequestStatus.Approved : RequestStatus.Pending, current.Status);
        Assert.Equal(history.Revisions.Count, current.RevisionNumber);
    }

    private async Task<PricingExceptionResult> CreateRequest(Guid? application = null)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPricingExceptionService>()
            .CreateAsync(application ?? Guid.NewGuid(), 50, "Competitive mortgage offer", Reviewer, Manager, "create");
    }

    private async Task<PricingExceptionResult> Review(PricingExceptionResult request, DecisionOutcome outcome)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPricingExceptionService>()
            .ReviewAsync(request.Id, outcome, "Independent mortgage review", request.Version, request.RevisionNumber, Reviewer, "review-" + outcome + request.RevisionNumber);
    }

    private async Task<PricingExceptionResult> Revise(PricingExceptionResult request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPricingExceptionService>()
            .SubmitRevisionAsync(request.Id, 75, "Updated competitive offer", request.Version, request.RevisionNumber, Manager, "revise");
    }

    private static ReviewDecision NewDecision(Guid requestId, DecisionOutcome outcome) => new()
    {
        Id = Guid.NewGuid(), RequestId = requestId, RevisionNumber = 1, Outcome = outcome,
        ReviewReason = "Independent mortgage review", ReviewedByUserId = Reviewer,
        ReviewerName = "David Miller (Reviewer)", DecidedAtUtc = DateTimeOffset.UtcNow
    };

    private static async Task<bool> TryChange(Func<Task<PricingExceptionResult>> operation)
    {
        try { await operation(); return true; }
        catch (ConflictException) { return false; }
    }

    // PostgreSQL stores microseconds; .NET DateTimeOffset also represents 100 ns ticks.
    // JSON retry records retain the exact original response, including its finer precision.
    private static DateTimeOffset? PostgreSqlTimestamp(DateTimeOffset? value)
        => value?.AddTicks(-(value.Value.Ticks % 10));

    public async Task DisposeAsync()
    {
        if (provider is not null) await provider.DisposeAsync();
        if (connectionString.Length > 0) await fixture.DropDatabaseAsync(connectionString);
    }
}
