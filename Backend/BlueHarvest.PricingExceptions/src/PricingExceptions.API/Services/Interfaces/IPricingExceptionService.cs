using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Services.Models;

namespace PricingExceptions.API.Services.Interfaces;

// actorId must eventually come from authenticated identity, not an HTTP request body.
public interface IPricingExceptionService
{
    Task<PricingExceptionResult> CreateAsync(Guid applicationId, int discountBasisPoints, string reason, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<PricingExceptionResult> GetAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<PricingExceptionResult> SubmitRevisionAsync(Guid requestId, int discountBasisPoints, string reason, long expectedVersion, int expectedRevisionNumber, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<PricingExceptionResult> ReviewAsync(Guid requestId, DecisionOutcome outcome, string? reason, long expectedVersion, int reviewedRevisionNumber, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HistoryEntry>> GetHistoryAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestRevision>> GetRevisionsAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReviewDecision>> GetDecisionsAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<RequestHistoryResult> GetHistorySnapshotAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
}
