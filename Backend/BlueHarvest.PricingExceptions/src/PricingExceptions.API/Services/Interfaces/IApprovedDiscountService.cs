using PricingExceptions.API.Services.Models;

namespace PricingExceptions.API.Services.Interfaces;

public interface IApprovedDiscountService
{
    Task<ApprovedDiscountResult?> GetAsync(Guid applicationId, Guid actorId, CancellationToken cancellationToken = default);
}
