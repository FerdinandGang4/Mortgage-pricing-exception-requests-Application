using Microsoft.EntityFrameworkCore;
using PricingExceptions.API.Data;
using PricingExceptions.API.Data.Seed;
using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Services;

namespace PricingExceptions.UnitTests;

public sealed class DevelopmentDataSeederTests
{
    private static PricingExceptionsDbContext Database() => new(
        new DbContextOptionsBuilder<PricingExceptionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task SeedsExactlyThreeManagersFiveReviewersAndExistingMortgageProcess()
    {
        await using var database = Database();
        await DevelopmentDataSeeder.SeedAsync(database);
        var users = await database.Users.AsNoTracking().ToListAsync();
        Assert.Equal(9, users.Count);
        Assert.Equal(9, users.Select(user => user.Id).Distinct().Count());
        Assert.DoesNotContain(users, user => user.Id == Guid.Empty);
        Assert.Equal(new[] { "James Wilson (Manager)", "Michael Brown (Manager)", "Sarah Johnson (Manager)" },
            users.Where(user => user.Role == UserRole.RelationshipManager).Select(user => user.DisplayName).Order());
        Assert.Equal(new[] { "Daniel Thomas (Reviewer)", "David Miller (Reviewer)", "Emily Davis (Reviewer)", "Jennifer Anderson (Reviewer)", "Robert Taylor (Reviewer)" },
            users.Where(user => user.Role == UserRole.Reviewer).Select(user => user.DisplayName).Order());
        var process = Assert.Single(users, user => user.Role == UserRole.MortgageProcess);
        Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), process.Id);
        Assert.Equal("Demo Mortgage Process", process.DisplayName);
    }

    [Fact]
    public async Task RepeatedSeedingWithClearedTrackingPreservesStableIdentitiesWithoutDuplicates()
    {
        await using var database = Database();
        await DevelopmentDataSeeder.SeedAsync(database);
        var original = await database.Users.AsNoTracking().OrderBy(user => user.Id)
            .Select(user => new { user.Id, user.DisplayName, user.Role }).ToListAsync();
        database.ChangeTracker.Clear();
        await DevelopmentDataSeeder.SeedAsync(database);
        database.ChangeTracker.Clear();
        await DevelopmentDataSeeder.SeedAsync(database);
        var repeated = await database.Users.AsNoTracking().OrderBy(user => user.Id)
            .Select(user => new { user.Id, user.DisplayName, user.Role }).ToListAsync();
        Assert.Equal(original, repeated);
    }

    [Fact]
    public async Task RenamesOriginalUsersWithoutChangingExistingWorkflowOrHistoricalNames()
    {
        await using var database = Database();
        var manager = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var reviewer = Guid.Parse("22222222-2222-2222-2222-222222222222");
        database.Users.AddRange(
            new User { Id = manager, DisplayName = "Demo Manager", Role = UserRole.RelationshipManager },
            new User { Id = reviewer, DisplayName = "Demo Reviewer", Role = UserRole.Reviewer });
        await database.SaveChangesAsync();
        using var gate = new InMemoryWriteLock();
        var service = new PricingExceptionService(database, new UserService(database), gate, TimeProvider.System);
        var application = Guid.NewGuid();
        var created = await service.CreatePricingExceptionRequestAsync(application, 50, "Existing request", reviewer, manager, "create");
        await service.ReviewPricingExceptionRequestAsync(created.Id, DecisionOutcome.Approved, "Existing decision", 1, 1, reviewer, "review");

        await DevelopmentDataSeeder.SeedAsync(database);
        await DevelopmentDataSeeder.SeedAsync(database);

        Assert.Equal("James Wilson (Manager)", (await database.Users.FindAsync(manager))!.DisplayName);
        Assert.Equal("David Miller (Reviewer)", (await database.Users.FindAsync(reviewer))!.DisplayName);
        var request = Assert.Single(await database.PricingExceptionRequests.AsNoTracking().ToListAsync());
        Assert.Equal(created.Id, request.Id);
        Assert.Equal(manager, request.CreatedByUserId);
        Assert.Equal(reviewer, request.AssignedReviewerId);
        Assert.Equal(RequestStatus.Approved, request.Status);
        Assert.Equal("Demo Manager", request.RequesterName);
        Assert.Equal("Demo Reviewer", request.AssignedReviewerName);
        Assert.Equal("Demo Reviewer", Assert.Single(await database.ReviewDecisions.AsNoTracking().ToListAsync()).ReviewerName);
        Assert.Single(await database.RequestRevisions.ToListAsync());
        Assert.Equal(2, await database.HistoryEntries.CountAsync());
        Assert.Equal(2, await database.IdempotencyRecords.CountAsync());
        Assert.Equal(created, await service.CreatePricingExceptionRequestAsync(application, 50, "Existing request", reviewer, manager, "create"));
    }
}
