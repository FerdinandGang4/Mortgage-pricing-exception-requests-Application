namespace PricingExceptions.API.Models.Entities;

public sealed class HistoryEntry
{
    public Guid Id { get; init; }
    public Guid RequestId { get; init; }
    public int RevisionNumber { get; init; }
    public long Sequence { get; init; }
    public required string Action { get; init; }
    public Guid ActorUserId { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
}

