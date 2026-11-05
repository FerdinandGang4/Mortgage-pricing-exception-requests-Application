namespace PricingExceptions.API.Models.Entities;

// Immutable submitted terms; DbContext save guards prevent later edits or deletion.
public sealed class RequestRevision
{
    public Guid RequestId { get; init; }
    public int RevisionNumber { get; init; }
    public int DiscountBasisPoints { get; init; }
    public required string Reason { get; init; }
    public Guid SubmittedByUserId { get; init; }
    public DateTimeOffset SubmittedAtUtc { get; init; }
}

