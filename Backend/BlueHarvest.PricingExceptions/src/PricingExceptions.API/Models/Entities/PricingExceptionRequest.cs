using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.Models.Entities;

// Mutable workflow state points to the current terms; older terms live in RequestRevision.
public sealed class PricingExceptionRequest
{
    public Guid Id { get; set; }
    public Guid MortgageApplicationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public required string RequesterName { get; init; }
    public Guid AssignedReviewerId { get; init; }
    public required string AssignedReviewerName { get; init; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public int CurrentRevisionNumber { get; set; } = 1;
    // Incremented for revisions and decisions so callers can detect stale state.
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<RequestRevision> Revisions { get; set; } = [];
    public ICollection<HistoryEntry> History { get; set; } = [];
}

