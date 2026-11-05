using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.Models.Entities;

// An immutable outcome for one specific revision, including the reviewer's name at decision time.
public sealed class ReviewDecision
{
    public Guid Id { get; init; }
    public Guid RequestId { get; init; }
    public int RevisionNumber { get; init; }
    public DecisionOutcome Outcome { get; init; }
    public string? ReviewReason { get; init; }
    public Guid ReviewedByUserId { get; init; }
    public required string ReviewerName { get; init; }
    public DateTimeOffset DecidedAtUtc { get; init; }
}

