namespace PricingExceptions.API.DTOs.Responses;

public sealed record ApprovedDiscountResponseDto(
    Guid RequestId, Guid MortgageApplicationId, int DiscountBps,
    int RevisionNumber, Guid ReviewerId, DateTimeOffset DecidedAtUtc);
