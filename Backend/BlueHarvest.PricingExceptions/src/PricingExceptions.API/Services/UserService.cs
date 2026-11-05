using Microsoft.EntityFrameworkCore;
using PricingExceptions.API.Data;
using PricingExceptions.API.Exceptions;
using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Services.Interfaces;

namespace PricingExceptions.API.Services;

// Resolve identities and enforce stored roles even when called outside an HTTP controller.
public sealed class UserService(PricingExceptionsDbContext database) : IUserService
{
    public async Task<IReadOnlyList<User>> GetReviewersAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        await RequireRoleAsync(actorId, UserRole.RelationshipManager, cancellationToken);
        // Only independent reviewers are offered for assignment by a relationship manager.
        return await database.Users.AsNoTracking()
            .Where(user => user.Role == UserRole.Reviewer && user.Id != actorId)
            .OrderBy(user => user.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<User> GetRequiredAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, cancellationToken);
        if (user is null || !Enum.IsDefined(user.Role))
            throw new ForbiddenException("A known internal user is required.");
        return user;
    }

    public async Task<User> RequireRoleAsync(Guid actorId, UserRole role, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredAsync(actorId, cancellationToken);
        if (user.Role != role)
            throw new ForbiddenException($"The {role} role is required.");
        return user;
    }
}
