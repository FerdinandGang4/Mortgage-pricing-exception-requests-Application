using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.DTOs.Responses;

public sealed record PricingExceptionResponseDto(
    Guid Id, Guid MortgageApplicationId, Guid CreatedByUserId,
    RequestStatus Status, int RevisionNumber, long Version,
    int DiscountBps, string Reason, Guid? ReviewerId, DateTimeOffset? DecidedAtUtc,
    Guid RequesterId, string RequesterName, Guid AssignedReviewerId, string AssignedReviewerName,
    string? ReviewerName);
