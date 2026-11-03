using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.Models.Entities;

public sealed class PricingExceptionRequest
{
    public Guid Id { get; set; }
    public Guid MortgageApplicationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public int CurrentRevisionNumber { get; set; } = 1;
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<RequestRevision> Revisions { get; set; } = [];
    public ICollection<HistoryEntry> History { get; set; } = [];
}

