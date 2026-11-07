using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.Services.Interfaces;

public interface IUserService
{
    Task<User> GetUserOrThrowAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<User> GetUserWithRequiredRoleAsync(Guid actorId, UserRole role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetReviewersAsync(Guid actorId, CancellationToken cancellationToken = default);
}
