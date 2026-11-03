using PricingExceptions.API.DTOs.Responses;
using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Services.Models;

namespace PricingExceptions.API.Mappings;

public static class HistoryMapper
{
    public static HistoryResponseDto ToResponse(RequestHistoryResult snapshot)
        => new(
            snapshot.Events.Select(ToHistoryEntry).ToList(),
            snapshot.Revisions.Select(ToRevision).ToList(),
            snapshot.Decisions.Select(ToDecision).ToList());

    private static HistoryEntryResponseDto ToHistoryEntry(HistoryEntry entry)
        => new(entry.Sequence, entry.RevisionNumber, entry.Action, entry.ActorUserId, entry.OccurredAtUtc);

    private static RevisionResponseDto ToRevision(RequestRevision revision)
        => new(revision.RevisionNumber, revision.DiscountBasisPoints, revision.Reason,
            revision.SubmittedByUserId, revision.SubmittedAtUtc);

    private static DecisionResponseDto ToDecision(ReviewDecision decision)
        => new(decision.RevisionNumber, decision.Outcome, decision.ReviewReason,
            decision.ReviewedByUserId, decision.DecidedAtUtc, decision.ReviewerName);
}
