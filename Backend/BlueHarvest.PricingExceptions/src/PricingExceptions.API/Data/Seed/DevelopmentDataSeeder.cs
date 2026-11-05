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
            new User { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), DisplayName = "James Wilson (Manager)", Role = UserRole.RelationshipManager },
            new User { Id = Guid.Parse("11111111-1111-1111-1111-111111111112"), DisplayName = "Sarah Johnson (Manager)", Role = UserRole.RelationshipManager },
            new User { Id = Guid.Parse("11111111-1111-1111-1111-111111111113"), DisplayName = "Michael Brown (Manager)", Role = UserRole.RelationshipManager },
            new User { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), DisplayName = "David Miller (Reviewer)", Role = UserRole.Reviewer },
            new User { Id = Guid.Parse("22222222-2222-2222-2222-222222222223"), DisplayName = "Emily Davis (Reviewer)", Role = UserRole.Reviewer },
            new User { Id = Guid.Parse("22222222-2222-2222-2222-222222222224"), DisplayName = "Robert Taylor (Reviewer)", Role = UserRole.Reviewer },
            new User { Id = Guid.Parse("22222222-2222-2222-2222-222222222225"), DisplayName = "Jennifer Anderson (Reviewer)", Role = UserRole.Reviewer },
            new User { Id = Guid.Parse("22222222-2222-2222-2222-222222222226"), DisplayName = "Daniel Thomas (Reviewer)", Role = UserRole.Reviewer },
            new User { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), DisplayName = "Demo Mortgage Process", Role = UserRole.MortgageProcess }
        };
        foreach (var user in users)
        {
            var existing = await database.Users.SingleOrDefaultAsync(x => x.Id == user.Id);
            if (existing is null)
                database.Users.Add(user);
            else
            {
                // Update the identity, preserving request references and historical name snapshots.
                existing.DisplayName = user.DisplayName;
                existing.Role = user.Role;
            }
        }
        await database.SaveChangesAsync();
    }
}
