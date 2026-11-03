using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.Services.Interfaces;

public interface IUserService
{
    Task<User> GetRequiredAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<User> RequireRoleAsync(Guid actorId, UserRole role, CancellationToken cancellationToken = default);
}
