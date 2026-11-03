using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Models.Entities;

namespace PricingExceptions.API.Services.Models;

public sealed record PricingExceptionResult(
    Guid Id, Guid MortgageApplicationId, Guid CreatedByUserId,
    RequestStatus Status, int RevisionNumber, long Version,
    int DiscountBasisPoints, string Reason, Guid? ReviewerId, DateTimeOffset? DecidedAtUtc);

public sealed record ApprovedDiscountResult(
    Guid RequestId, Guid MortgageApplicationId, int DiscountBasisPoints,
    int RevisionNumber, Guid ReviewerId, DateTimeOffset DecidedAtUtc);

public sealed record RequestHistoryResult(
    IReadOnlyList<HistoryEntry> Events,
    IReadOnlyList<RequestRevision> Revisions,
    IReadOnlyList<ReviewDecision> Decisions);
