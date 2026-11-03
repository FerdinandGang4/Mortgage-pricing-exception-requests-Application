using PricingExceptions.API.DTOs.Responses;
using PricingExceptions.API.Services.Models;

namespace PricingExceptions.API.Mappings;

public static class PricingExceptionMapper
{
    public static PricingExceptionResponseDto ToResponse(PricingExceptionResult result)
        => new(result.Id, result.MortgageApplicationId, result.CreatedByUserId, result.Status,
            result.RevisionNumber, result.Version, result.DiscountBasisPoints, result.Reason,
            result.ReviewerId, result.DecidedAtUtc);
}
