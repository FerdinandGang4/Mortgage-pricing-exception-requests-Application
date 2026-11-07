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
        await GetUserWithRequiredRoleAsync(actorId, UserRole.RelationshipManager, cancellationToken);
        // Only independent reviewers are offered for assignment by a relationship manager.
        return await database.Users.AsNoTracking()
            .Where(internalUser => internalUser.Role == UserRole.Reviewer && internalUser.Id != actorId)
            .OrderBy(internalUser => internalUser.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<User> GetUserOrThrowAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        var internalUser = await database.Users.AsNoTracking().SingleOrDefaultAsync(storedUser => storedUser.Id == actorId, cancellationToken);
        if (internalUser is null || !Enum.IsDefined(internalUser.Role))
            throw new ForbiddenException("A known internal user is required.");
        return internalUser;
    }

    public async Task<User> GetUserWithRequiredRoleAsync(Guid actorId, UserRole role, CancellationToken cancellationToken = default)
    {
        var internalUser = await GetUserOrThrowAsync(actorId, cancellationToken);
        if (internalUser.Role != role)
            throw new ForbiddenException($"The {role} role is required.");
        return internalUser;
    }
}
