using PricingExceptions.API.DTOs.Responses;
using PricingExceptions.API.Services.Models;

namespace PricingExceptions.API.Mappings;

public static class ApprovedDiscountMapper
{
    public static ApprovedDiscountResponseDto ToResponse(ApprovedDiscountResult result)
        => new(result.RequestId, result.MortgageApplicationId, result.DiscountBasisPoints,
            result.RevisionNumber, result.ReviewerId, result.DecidedAtUtc, result.ReviewerName);
}
