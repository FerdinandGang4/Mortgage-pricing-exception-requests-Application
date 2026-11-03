using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.DTOs.Responses;

public sealed record HistoryResponseDto(
    IReadOnlyList<HistoryEntryResponseDto> Events,
    IReadOnlyList<RevisionResponseDto> Revisions,
    IReadOnlyList<DecisionResponseDto> Decisions);

public sealed record HistoryEntryResponseDto(
    long Sequence, int RevisionNumber, string Action, Guid ActorUserId, DateTimeOffset OccurredAtUtc);

public sealed record RevisionResponseDto(
    int RevisionNumber, int DiscountBps, string Reason, Guid SubmittedByUserId, DateTimeOffset SubmittedAtUtc);

public sealed record DecisionResponseDto(
    int RevisionNumber, DecisionOutcome Outcome, string? Reason, Guid ReviewerId, DateTimeOffset DecidedAtUtc);
