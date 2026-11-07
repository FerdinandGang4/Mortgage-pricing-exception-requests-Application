using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PricingExceptions.API.Data;
using PricingExceptions.API.Exceptions;
using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Observability;
using PricingExceptions.API.Services.Interfaces;
using PricingExceptions.API.Services.Models;

namespace PricingExceptions.API.Services;

public sealed class PricingExceptionService(
    PricingExceptionsDbContext database, IUserService userService,
    InMemoryWriteLock writeLock, TimeProvider clock) : IPricingExceptionService
{
    public async Task<PricingExceptionResult> CreatePricingExceptionRequestAsync(Guid applicationId, int discountBasisPoints,
        string reason, Guid reviewerId, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var relationshipManager = await userService.GetUserWithRequiredRoleAsync(actorId, UserRole.RelationshipManager, cancellationToken);
        return await ExecuteIdempotentWriteAsync(actorId, "create", idempotencyKey,
            new { applicationId, discountBasisPoints, reason, reviewerId }, async () =>
        {
            ValidateDiscountTerms(discountBasisPoints, reason);
            if (applicationId == Guid.Empty)
                throw new BusinessRuleException("A mortgage application identifier is required.");
            var reviewer = await database.Users.AsNoTracking().SingleOrDefaultAsync(
                user => user.Id == reviewerId && user.Role == UserRole.Reviewer && user.Id != actorId, cancellationToken)
                ?? throw new BusinessRuleException("Select an eligible independent reviewer.");
            if (await database.PricingExceptionRequests.AnyAsync(storedPricingExceptionRequest => storedPricingExceptionRequest.MortgageApplicationId == applicationId, cancellationToken))
                throw new ConflictException("This mortgage application already has an exception request.");
            var operationTimeUtc = clock.GetUtcNow();
            var pricingExceptionRequest = new PricingExceptionRequest
            {
                Id = Guid.NewGuid(),
                MortgageApplicationId = applicationId,
                CreatedByUserId = actorId,
                RequesterName = relationshipManager.DisplayName,
                AssignedReviewerId = reviewer.Id,
                AssignedReviewerName = reviewer.DisplayName,
                CreatedAtUtc = operationTimeUtc,
                UpdatedAtUtc = operationTimeUtc
            };
            var requestRevision = CreateRequestRevision(pricingExceptionRequest, discountBasisPoints, reason, actorId, operationTimeUtc);
            pricingExceptionRequest.Revisions.Add(requestRevision);
            AppendRequestHistoryEntry(pricingExceptionRequest, "Created", actorId, operationTimeUtc);
            database.PricingExceptionRequests.Add(pricingExceptionRequest);
            return MapToPricingExceptionResult(pricingExceptionRequest, requestRevision);
        }, cancellationToken);
    }

    public async Task<PricingExceptionResult> SubmitRequestRevisionAsync(Guid requestId, int discountBasisPoints,
        string reason, long expectedVersion, int expectedRevisionNumber, Guid actorId,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        await userService.GetUserWithRequiredRoleAsync(actorId, UserRole.RelationshipManager, cancellationToken);
        return await ExecuteIdempotentWriteAsync(actorId, $"revise:{requestId}", idempotencyKey,
            new { discountBasisPoints, reason, expectedVersion, expectedRevisionNumber }, async () =>
        {
            ValidateDiscountTerms(discountBasisPoints, reason);
            var pricingExceptionRequest = await FindRequestOrThrowAsync(requestId, cancellationToken);
            if (pricingExceptionRequest.CreatedByUserId != actorId)
                throw new ForbiddenException("Only the creator may revise this request.");
            ValidateRequestVersionAndRevision(pricingExceptionRequest, expectedVersion, expectedRevisionNumber);
            var operationTimeUtc = clock.GetUtcNow();
            pricingExceptionRequest.CurrentRevisionNumber++;
            pricingExceptionRequest.Version++;
            pricingExceptionRequest.Status = RequestStatus.Pending;
            pricingExceptionRequest.UpdatedAtUtc = operationTimeUtc;
            var requestRevision = CreateRequestRevision(pricingExceptionRequest, discountBasisPoints, reason, actorId, operationTimeUtc);
            database.RequestRevisions.Add(requestRevision);
            AppendRequestHistoryEntry(pricingExceptionRequest, "Revised", actorId, operationTimeUtc);
            return MapToPricingExceptionResult(pricingExceptionRequest, requestRevision);
        }, cancellationToken);
    }

    public async Task<PricingExceptionResult> ReviewPricingExceptionRequestAsync(Guid requestId, DecisionOutcome outcome,
        string? reason, long expectedVersion, int reviewedRevisionNumber, Guid actorId,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var reviewer = await userService.GetUserWithRequiredRoleAsync(actorId, UserRole.Reviewer, cancellationToken);
        return await ExecuteIdempotentWriteAsync(actorId, $"review:{requestId}", idempotencyKey,
            new { outcome, reason, expectedVersion, reviewedRevisionNumber }, async () =>
        {
            if (!Enum.IsDefined(outcome) || reason?.Length > 2000)
                throw new BusinessRuleException("A valid outcome and review reason of at most 2000 characters are required.");
            var pricingExceptionRequest = await FindRequestOrThrowAsync(requestId, cancellationToken);
            if (pricingExceptionRequest.CreatedByUserId == actorId)
                throw new ForbiddenException("Self-review is not allowed.");
            if (pricingExceptionRequest.AssignedReviewerId != actorId)
                throw new ForbiddenException("Only the selected reviewer may decide this request.");
            ValidateRequestVersionAndRevision(pricingExceptionRequest, expectedVersion, reviewedRevisionNumber);
            if (pricingExceptionRequest.Status != RequestStatus.Pending ||
                await database.ReviewDecisions.AnyAsync(storedReviewDecision => storedReviewDecision.RequestId == requestId && storedReviewDecision.RevisionNumber == reviewedRevisionNumber, cancellationToken))
                throw new ConflictException("The revision has already been decided.");
            var requestRevision = await database.RequestRevisions.AsNoTracking().SingleAsync(
                storedRequestRevision => storedRequestRevision.RequestId == requestId && storedRequestRevision.RevisionNumber == reviewedRevisionNumber, cancellationToken);
            var operationTimeUtc = clock.GetUtcNow();
            var reviewDecision = new ReviewDecision
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                RevisionNumber = reviewedRevisionNumber,
                Outcome = outcome,
                ReviewReason = reason?.Trim(),
                ReviewedByUserId = actorId,
                ReviewerName = reviewer.DisplayName,
                DecidedAtUtc = operationTimeUtc
            };
            database.ReviewDecisions.Add(reviewDecision);
            pricingExceptionRequest.Status = outcome == DecisionOutcome.Approved ? RequestStatus.Approved : RequestStatus.Declined;
            pricingExceptionRequest.Version++;
            pricingExceptionRequest.UpdatedAtUtc = operationTimeUtc;
            AppendRequestHistoryEntry(pricingExceptionRequest, pricingExceptionRequest.Status.ToString(), actorId, operationTimeUtc);
            return MapToPricingExceptionResult(pricingExceptionRequest, requestRevision, reviewDecision);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PricingExceptionResult>> ListPricingExceptionRequestsAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        await userService.GetUserOrThrowAsync(actorId, cancellationToken);
        await writeLock.Semaphore.WaitAsync(cancellationToken);
        try
        {
            var pricingExceptionRequests = await database.PricingExceptionRequests.AsNoTracking()
                .OrderByDescending(pricingExceptionRequest => pricingExceptionRequest.CreatedAtUtc).ThenBy(pricingExceptionRequest => pricingExceptionRequest.Id)
                .ToListAsync(cancellationToken);
            var requestRevisions = (await database.RequestRevisions.AsNoTracking().ToListAsync(cancellationToken))
                .ToDictionary(requestRevision => (requestRevision.RequestId, requestRevision.RevisionNumber));
            var reviewDecisions = (await database.ReviewDecisions.AsNoTracking().ToListAsync(cancellationToken))
                .ToDictionary(reviewDecision => (reviewDecision.RequestId, reviewDecision.RevisionNumber));
            return pricingExceptionRequests.Select(pricingExceptionRequest =>
            {
                var requestRevisionKey = (pricingExceptionRequest.Id, pricingExceptionRequest.CurrentRevisionNumber);
                reviewDecisions.TryGetValue(requestRevisionKey, out var reviewDecision);
                return MapToPricingExceptionResult(pricingExceptionRequest, requestRevisions[requestRevisionKey], reviewDecision);
            }).ToList();
        }
        finally
        {
            writeLock.Semaphore.Release();
        }
    }

    public async Task<PricingExceptionResult> GetPricingExceptionRequestAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAuthorizedRequestReadAsync(requestId, actorId, async () =>
        {
            var pricingExceptionRequest = await database.PricingExceptionRequests.AsNoTracking()
                .SingleOrDefaultAsync(storedPricingExceptionRequest => storedPricingExceptionRequest.Id == requestId, cancellationToken)
                ?? throw new NotFoundException("The pricing exception request was not found.");
            var requestRevision = await database.RequestRevisions.AsNoTracking().SingleAsync(
                storedRequestRevision => storedRequestRevision.RequestId == requestId && storedRequestRevision.RevisionNumber == pricingExceptionRequest.CurrentRevisionNumber, cancellationToken);
            var reviewDecision = await database.ReviewDecisions.AsNoTracking().SingleOrDefaultAsync(
                storedReviewDecision => storedReviewDecision.RequestId == requestId && storedReviewDecision.RevisionNumber == pricingExceptionRequest.CurrentRevisionNumber, cancellationToken);
            return MapToPricingExceptionResult(pricingExceptionRequest, requestRevision, reviewDecision);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<HistoryEntry>> GetRequestHistoryAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAuthorizedRequestReadAsync<IReadOnlyList<HistoryEntry>>(requestId, actorId, async () =>
            await database.HistoryEntries.AsNoTracking().Where(storedHistoryEntry => storedHistoryEntry.RequestId == requestId)
                .OrderBy(storedHistoryEntry => storedHistoryEntry.Sequence).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<RequestRevision>> GetRequestRevisionsAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAuthorizedRequestReadAsync<IReadOnlyList<RequestRevision>>(requestId, actorId, async () =>
            await database.RequestRevisions.AsNoTracking().Where(storedRequestRevision => storedRequestRevision.RequestId == requestId)
                .OrderBy(storedRequestRevision => storedRequestRevision.RevisionNumber).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewDecision>> GetReviewDecisionsAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAuthorizedRequestReadAsync<IReadOnlyList<ReviewDecision>>(requestId, actorId, async () =>
            await database.ReviewDecisions.AsNoTracking().Where(storedReviewDecision => storedReviewDecision.RequestId == requestId)
                .OrderBy(storedReviewDecision => storedReviewDecision.RevisionNumber).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<RequestHistoryResult> GetRequestHistorySnapshotAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
        => await ExecuteAuthorizedRequestReadAsync(requestId, actorId, async () => new RequestHistoryResult(
            await database.HistoryEntries.AsNoTracking().Where(storedHistoryEntry => storedHistoryEntry.RequestId == requestId)
                .OrderBy(storedHistoryEntry => storedHistoryEntry.Sequence).ToListAsync(cancellationToken),
            await database.RequestRevisions.AsNoTracking().Where(storedRequestRevision => storedRequestRevision.RequestId == requestId)
                .OrderBy(storedRequestRevision => storedRequestRevision.RevisionNumber).ToListAsync(cancellationToken),
            await database.ReviewDecisions.AsNoTracking().Where(storedReviewDecision => storedReviewDecision.RequestId == requestId)
                .OrderBy(storedReviewDecision => storedReviewDecision.RevisionNumber).ToListAsync(cancellationToken)), cancellationToken);

    private async Task<T> ExecuteAuthorizedRequestReadAsync<T>(Guid requestId, Guid actorId, Func<Task<T>> read, CancellationToken cancellationToken)
    {
        await userService.GetUserOrThrowAsync(actorId, cancellationToken);
        await writeLock.Semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!await database.PricingExceptionRequests.AsNoTracking().AnyAsync(storedPricingExceptionRequest => storedPricingExceptionRequest.Id == requestId, cancellationToken))
                throw new NotFoundException("The pricing exception request was not found.");
            return await read();
        }
        finally
        {
            writeLock.Semaphore.Release();
        }
    }

    private async Task<PricingExceptionRequest> FindRequestOrThrowAsync(Guid id, CancellationToken cancellationToken)
        => await database.PricingExceptionRequests.SingleOrDefaultAsync(storedPricingExceptionRequest => storedPricingExceptionRequest.Id == id, cancellationToken)
            ?? throw new NotFoundException("The pricing exception request was not found.");

    private async Task<PricingExceptionResult> ExecuteIdempotentWriteAsync(Guid actorId, string operationScope, string idempotencyKey,
        object operationPayload, Func<Task<PricingExceptionResult>> executeWorkflowChange, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            throw new BusinessRuleException("An idempotency key of up to 200 characters is required.");
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(operationPayload))));
        var operationName = operationScope.Split(':', 2)[0];
        using var activity = PricingExceptionsTelemetry.Activities.StartActivity("pricing-exception." + operationName);
        var operationTag = new KeyValuePair<string, object?>("operation", operationName);
        await writeLock.Semaphore.WaitAsync(cancellationToken);
        try
        {
            database.ChangeTracker.Clear();
            var existingIdempotencyRecord = await database.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
                storedIdempotencyRecord => storedIdempotencyRecord.ActorUserId == actorId && storedIdempotencyRecord.OperationScope == operationScope && storedIdempotencyRecord.Key == idempotencyKey, cancellationToken);
            if (existingIdempotencyRecord is not null)
            {
                if (existingIdempotencyRecord.PayloadHash != payloadHash)
                    throw new ConflictException("The idempotency key was already used with different input.");
                PricingExceptionsTelemetry.Replays.Add(1, operationTag);
                return JsonSerializer.Deserialize<PricingExceptionResult>(existingIdempotencyRecord.ResponseJson)!;
            }
            var pricingExceptionResult = await executeWorkflowChange();
            database.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Id = Guid.NewGuid(),
                ActorUserId = actorId,
                OperationScope = operationScope,
                Key = idempotencyKey,
                PayloadHash = payloadHash,
                RequestId = pricingExceptionResult.Id,
                ResponseStatusCode = operationScope.StartsWith("review:") ? 200 : 201,
                ResponseJson = JsonSerializer.Serialize(pricingExceptionResult)
            });
            await database.SaveChangesAsync(cancellationToken);
            PricingExceptionsTelemetry.Mutations.Add(1, operationTag);
            return pricingExceptionResult;
        }
        catch (DbUpdateConcurrencyException)
        {
            PricingExceptionsTelemetry.Conflicts.Add(1, operationTag);
            database.ChangeTracker.Clear();
            throw new ConflictException("The request changed. Reload it before trying again.");
        }
        catch (ConflictException)
        {
            PricingExceptionsTelemetry.Conflicts.Add(1, operationTag);
            database.ChangeTracker.Clear();
            throw;
        }
        catch
        {
            database.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            writeLock.Semaphore.Release();
        }
    }

    private static void ValidateDiscountTerms(int discountBasisPoints, string reason)
    {
        if (discountBasisPoints is < 1 or > 500)
            throw new BusinessRuleException("Discount must be between 1 and 500 basis points.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            throw new BusinessRuleException("A reason of up to 2000 characters is required.");
    }

    private static void ValidateRequestVersionAndRevision(PricingExceptionRequest pricingExceptionRequest, long expectedRequestVersion, int expectedRevisionNumber)
    {
        if (pricingExceptionRequest.Version != expectedRequestVersion || pricingExceptionRequest.CurrentRevisionNumber != expectedRevisionNumber)
            throw new ConflictException("The request version or revision is stale.");
    }

    private static RequestRevision CreateRequestRevision(PricingExceptionRequest pricingExceptionRequest, int discountBasisPoints, string reason, Guid actorUserId, DateTimeOffset operationTimeUtc)
        => new()
        {
            RequestId = pricingExceptionRequest.Id,
            RevisionNumber = pricingExceptionRequest.CurrentRevisionNumber,
            DiscountBasisPoints = discountBasisPoints,
            Reason = reason.Trim(),
            SubmittedByUserId = actorUserId,
            SubmittedAtUtc = operationTimeUtc
        };

    private void AppendRequestHistoryEntry(PricingExceptionRequest pricingExceptionRequest, string action, Guid actorUserId, DateTimeOffset operationTimeUtc)
        => database.HistoryEntries.Add(new HistoryEntry
        {
            Id = Guid.NewGuid(),
            RequestId = pricingExceptionRequest.Id,
            RevisionNumber = pricingExceptionRequest.CurrentRevisionNumber,
            Sequence = pricingExceptionRequest.Version,
            Action = action,
            ActorUserId = actorUserId,
            OccurredAtUtc = operationTimeUtc
        });

    private static PricingExceptionResult MapToPricingExceptionResult(PricingExceptionRequest pricingExceptionRequest, RequestRevision requestRevision, ReviewDecision? reviewDecision = null)
        => new(pricingExceptionRequest.Id, pricingExceptionRequest.MortgageApplicationId, pricingExceptionRequest.CreatedByUserId, pricingExceptionRequest.Status,
            requestRevision.RevisionNumber, pricingExceptionRequest.Version, requestRevision.DiscountBasisPoints, requestRevision.Reason,
            reviewDecision?.ReviewedByUserId, reviewDecision?.DecidedAtUtc,
            pricingExceptionRequest.RequesterName, pricingExceptionRequest.AssignedReviewerId, pricingExceptionRequest.AssignedReviewerName, reviewDecision?.ReviewerName);
}
