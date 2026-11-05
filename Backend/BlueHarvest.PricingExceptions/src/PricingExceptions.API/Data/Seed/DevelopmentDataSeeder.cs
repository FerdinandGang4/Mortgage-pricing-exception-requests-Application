using Microsoft.EntityFrameworkCore;
using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.Data.Seed;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(PricingExceptionsDbContext database)
    {
        // Stable IDs let Swagger and the frontend select the same demo identities on each start.
        var users = new[]
        {
            new User { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), DisplayName = "Demo Manager", Role = UserRole.RelationshipManager },
            new User { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), DisplayName = "Demo Reviewer", Role = UserRole.Reviewer },
            new User { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), DisplayName = "Demo Mortgage Process", Role = UserRole.MortgageProcess }
        };
        foreach (var user in users)
            if (!await database.Users.AnyAsync(x => x.Id == user.Id))
                database.Users.Add(user);
        await database.SaveChangesAsync();
    }
}
