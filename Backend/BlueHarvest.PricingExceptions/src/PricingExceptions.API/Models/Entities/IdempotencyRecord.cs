namespace PricingExceptions.API.Models.Entities;

// Stores a completed operation's input fingerprint and result so identical retries can be replayed.
public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public required string OperationScope { get; set; }
    public required string Key { get; set; }
    public required string PayloadHash { get; set; }
    public int ResponseStatusCode { get; set; }
    public required string ResponseJson { get; set; }
    public string? Location { get; set; }
    public Guid? RequestId { get; set; }
}

