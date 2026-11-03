using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Services.Models;

namespace PricingExceptions.API.DTOs.Responses;

public sealed record PricingExceptionResponseDto(
    Guid Id, Guid MortgageApplicationId, Guid CreatedByUserId,
    RequestStatus Status, int RevisionNumber, long Version,
    int DiscountBps, string Reason, Guid? ReviewerId, DateTimeOffset? DecidedAtUtc)
{
    public static PricingExceptionResponseDto From(PricingExceptionResult result)
        => new(result.Id, result.MortgageApplicationId, result.CreatedByUserId, result.Status,
            result.RevisionNumber, result.Version, result.DiscountBasisPoints, result.Reason,
            result.ReviewerId, result.DecidedAtUtc);
}
