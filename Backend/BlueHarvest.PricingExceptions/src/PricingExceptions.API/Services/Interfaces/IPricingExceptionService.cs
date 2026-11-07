using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Services.Models;

namespace PricingExceptions.API.Services.Interfaces;

// actorId must eventually come from authenticated identity, not an HTTP request body.
public interface IPricingExceptionService
{
    Task<PricingExceptionResult> CreatePricingExceptionRequestAsync(Guid applicationId, int discountBasisPoints, string reason, Guid reviewerId, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<PricingExceptionResult> SubmitRequestRevisionAsync(Guid requestId, int discountBasisPoints, string reason, long expectedVersion, int expectedRevisionNumber, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<PricingExceptionResult> ReviewPricingExceptionRequestAsync(Guid requestId, DecisionOutcome outcome, string? reason, long expectedVersion, int reviewedRevisionNumber, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PricingExceptionResult>> ListPricingExceptionRequestsAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<PricingExceptionResult> GetPricingExceptionRequestAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HistoryEntry>> GetRequestHistoryAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestRevision>> GetRequestRevisionsAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReviewDecision>> GetReviewDecisionsAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<RequestHistoryResult> GetRequestHistorySnapshotAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
}
