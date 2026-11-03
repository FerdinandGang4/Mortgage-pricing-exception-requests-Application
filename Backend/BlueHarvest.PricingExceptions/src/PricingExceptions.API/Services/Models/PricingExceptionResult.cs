using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.Services.Models;

public sealed record PricingExceptionResult(
    Guid Id, Guid MortgageApplicationId, Guid CreatedByUserId,
    RequestStatus Status, int RevisionNumber, long Version,
    int DiscountBasisPoints, string Reason, Guid? ReviewerId, DateTimeOffset? DecidedAtUtc);

public sealed record ApprovedDiscountResult(
    Guid RequestId, Guid MortgageApplicationId, int DiscountBasisPoints,
    int RevisionNumber, Guid ReviewerId, DateTimeOffset DecidedAtUtc);
