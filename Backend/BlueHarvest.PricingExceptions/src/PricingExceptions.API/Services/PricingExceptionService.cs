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

// Owns workflow transitions, revision history, concurrency checks, and retry handling.
public sealed class PricingExceptionService(
    PricingExceptionsDbContext database, IUserService users,
    InMemoryWriteLock writeLock, TimeProvider clock) : IPricingExceptionService
{
    public async Task<PricingExceptionResult> CreatePricingExceptionRequestAsync(Guid applicationId, int discountBasisPoints,
        string reason, Guid reviewerId, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var requester = await users.RequireRoleAsync(actorId, UserRole.RelationshipManager, cancellationToken);
        return await ExecuteWriteAsync(actorId, "create", idempotencyKey,
            new { applicationId, discountBasisPoints, reason, reviewerId }, async () =>
        {
            ValidateTerms(discountBasisPoints, reason);
            if (applicationId == Guid.Empty)
                throw new BusinessRuleException("A mortgage application identifier is required.");
            var reviewer = await database.Users.AsNoTracking().SingleOrDefaultAsync(
                user => user.Id == reviewerId && user.Role == UserRole.Reviewer && user.Id != actorId, cancellationToken)
                ?? throw new BusinessRuleException("Select an eligible independent reviewer.");
            // Demo policy: one request per application; changed terms use revisions.
            if (await database.PricingExceptionRequests.AnyAsync(x => x.MortgageApplicationId == applicationId, cancellationToken))
                throw new ConflictException("This mortgage application already has an exception request.");
            var now = clock.GetUtcNow();
            var request = new PricingExceptionRequest
            {
                Id = Guid.NewGuid(),
                MortgageApplicationId = applicationId,
                CreatedByUserId = actorId,
                // Store name snapshots so later user profile edits do not rewrite the request record.
                RequesterName = requester.DisplayName,
                AssignedReviewerId = reviewer.Id,
                AssignedReviewerName = reviewer.DisplayName,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            var revision = NewRevision(request, discountBasisPoints, reason, actorId, now);
            request.Revisions.Add(revision);
            AppendHistory(request, "Created", actorId, now);
            database.PricingExceptionRequests.Add(request);
            return Result(request, revision);
        }, cancellationToken);
    }

    public async Task<PricingExceptionResult> SubmitRequestRevisionAsync(Guid requestId, int discountBasisPoints,
        string reason, long expectedVersion, int expectedRevisionNumber, Guid actorId,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        await users.RequireRoleAsync(actorId, UserRole.RelationshipManager, cancellationToken);
        return await ExecuteWriteAsync(actorId, $"revise:{requestId}", idempotencyKey,
            new { discountBasisPoints, reason, expectedVersion, expectedRevisionNumber }, async () =>
        {
            ValidateTerms(discountBasisPoints, reason);
            var request = await FindAsync(requestId, cancellationToken);
            if (request.CreatedByUserId != actorId)
                throw new ForbiddenException("Only the creator may revise this request.");
            CheckVersion(request, expectedVersion, expectedRevisionNumber);
            var now = clock.GetUtcNow();
            // New terms require a fresh decision; the previous revision and approval remain in history.
            request.CurrentRevisionNumber++;
            request.Version++;
            request.Status = RequestStatus.Pending;
            request.UpdatedAtUtc = now;
            var revision = NewRevision(request, discountBasisPoints, reason, actorId, now);
            database.RequestRevisions.Add(revision);
            AppendHistory(request, "Revised", actorId, now);
            return Result(request, revision);
        }, cancellationToken);
    }

    public async Task<PricingExceptionResult> ReviewPricingExceptionRequestAsync(Guid requestId, DecisionOutcome outcome,
        string? reason, long expectedVersion, int reviewedRevisionNumber, Guid actorId,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var reviewer = await users.RequireRoleAsync(actorId, UserRole.Reviewer, cancellationToken);
        return await ExecuteWriteAsync(actorId, $"review:{requestId}", idempotencyKey,
            new { outcome, reason, expectedVersion, reviewedRevisionNumber }, async () =>
        {
            if (!Enum.IsDefined(outcome) || reason?.Length > 2000)
                throw new BusinessRuleException("A valid outcome and review reason of at most 2000 characters are required.");
            var request = await FindAsync(requestId, cancellationToken);
            if (request.CreatedByUserId == actorId)
                throw new ForbiddenException("Self-review is not allowed.");
            if (request.AssignedReviewerId != actorId)
                throw new ForbiddenException("Only the selected reviewer may decide this request.");
            CheckVersion(request, expectedVersion, reviewedRevisionNumber);
            // Each revision can have only one decision, made while that revision is still pending.
            if (request.Status != RequestStatus.Pending ||
                await database.ReviewDecisions.AnyAsync(x => x.RequestId == requestId && x.RevisionNumber == reviewedRevisionNumber, cancellationToken))
                throw new ConflictException("The revision has already been decided.");
            var revision = await database.RequestRevisions.AsNoTracking().SingleAsync(
                x => x.RequestId == requestId && x.RevisionNumber == reviewedRevisionNumber, cancellationToken);
            var now = clock.GetUtcNow();
            var decision = new ReviewDecision
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                RevisionNumber = reviewedRevisionNumber,
                Outcome = outcome,
                ReviewReason = reason?.Trim(),
                ReviewedByUserId = actorId,
                ReviewerName = reviewer.DisplayName,
                DecidedAtUtc = now
            };
            database.ReviewDecisions.Add(decision);
            request.Status = outcome == DecisionOutcome.Approved ? RequestStatus.Approved : RequestStatus.Declined;
            request.Version++;
            request.UpdatedAtUtc = now;
            AppendHistory(request, request.Status.ToString(), actorId, now);
            return Result(request, revision, decision);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PricingExceptionResult>> ListPricingExceptionRequestsAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        await users.GetRequiredAsync(actorId, cancellationToken);
        await writeLock.Semaphore.WaitAsync(cancellationToken);
        try
        {
            var requests = await database.PricingExceptionRequests.AsNoTracking()
                .OrderByDescending(request => request.CreatedAtUtc).ThenBy(request => request.Id)
                .ToListAsync(cancellationToken);
            // Match terms and decisions to each request's current revision, preserving older snapshots.
            var revisions = (await database.RequestRevisions.AsNoTracking().ToListAsync(cancellationToken))
                .ToDictionary(revision => (revision.RequestId, revision.RevisionNumber));
            var decisions = (await database.ReviewDecisions.AsNoTracking().ToListAsync(cancellationToken))
                .ToDictionary(decision => (decision.RequestId, decision.RevisionNumber));
            return requests.Select(request =>
            {
                var key = (request.Id, request.CurrentRevisionNumber);
                decisions.TryGetValue(key, out var decision);
                return Result(request, revisions[key], decision);
            }).ToList();
        }
        finally
        {
            writeLock.Semaphore.Release();
        }
    }

    public async Task<PricingExceptionResult> GetPricingExceptionRequestAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ReadRequestAsync(requestId, actorId, async () =>
        {
            var request = await database.PricingExceptionRequests.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
                ?? throw new NotFoundException("The pricing exception request was not found.");
            var revision = await database.RequestRevisions.AsNoTracking().SingleAsync(
                x => x.RequestId == requestId && x.RevisionNumber == request.CurrentRevisionNumber, cancellationToken);
            var decision = await database.ReviewDecisions.AsNoTracking().SingleOrDefaultAsync(
                x => x.RequestId == requestId && x.RevisionNumber == request.CurrentRevisionNumber, cancellationToken);
            return Result(request, revision, decision);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<HistoryEntry>> GetRequestHistoryAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ReadRequestAsync<IReadOnlyList<HistoryEntry>>(requestId, actorId, async () =>
            await database.HistoryEntries.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.Sequence).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<RequestRevision>> GetRequestRevisionsAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ReadRequestAsync<IReadOnlyList<RequestRevision>>(requestId, actorId, async () =>
            await database.RequestRevisions.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.RevisionNumber).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewDecision>> GetReviewDecisionsAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ReadRequestAsync<IReadOnlyList<ReviewDecision>>(requestId, actorId, async () =>
            await database.ReviewDecisions.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.RevisionNumber).ToListAsync(cancellationToken), cancellationToken);
    }

    // Read all history collections under one lock so a concurrent write cannot split the snapshot.
    public async Task<RequestHistoryResult> GetRequestHistorySnapshotAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
        => await ReadRequestAsync(requestId, actorId, async () => new RequestHistoryResult(
            await database.HistoryEntries.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.Sequence).ToListAsync(cancellationToken),
            await database.RequestRevisions.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.RevisionNumber).ToListAsync(cancellationToken),
            await database.ReviewDecisions.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.RevisionNumber).ToListAsync(cancellationToken)), cancellationToken);

    // Request reads and lookup.
    // Reads share the write lock because the InMemory provider offers no transactional snapshot.
    private async Task<T> ReadRequestAsync<T>(Guid requestId, Guid actorId, Func<Task<T>> read, CancellationToken cancellationToken)
    {
        await users.GetRequiredAsync(actorId, cancellationToken);
        await writeLock.Semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!await database.PricingExceptionRequests.AsNoTracking().AnyAsync(x => x.Id == requestId, cancellationToken))
                throw new NotFoundException("The pricing exception request was not found.");
            return await read();
        }
        finally
        {
            writeLock.Semaphore.Release();
        }
    }

    private async Task<PricingExceptionRequest> FindAsync(Guid id, CancellationToken cancellationToken)
        => await database.PricingExceptionRequests.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("The pricing exception request was not found.");

    // Write execution, idempotency, and concurrency handling.
    private async Task<PricingExceptionResult> ExecuteWriteAsync(Guid actorId, string scope, string key,
        object payload, Func<Task<PricingExceptionResult>> operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200)
            throw new BusinessRuleException("An idempotency key of up to 200 characters is required.");
        // Fingerprint the input to distinguish an identical retry from conflicting reuse of its key.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        var operationName = scope.Split(':', 2)[0];
        using var activity = PricingExceptionsTelemetry.Activities.StartActivity("pricing-exception." + operationName);
        var operationTag = new KeyValuePair<string, object?>("operation", operationName);
        await writeLock.Semaphore.WaitAsync(cancellationToken);
        try
        {
            // Always reload mutable request state after obtaining the process-wide lock.
            // This service owns writes in its scope; callers should not stage unrelated changes.
            database.ChangeTracker.Clear();
            // Keys are scoped to the actor and operation (including the target request for updates).
            var existing = await database.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
                x => x.ActorUserId == actorId && x.OperationScope == scope && x.Key == key, cancellationToken);
            if (existing is not null)
            {
                if (existing.PayloadHash != hash)
                    throw new ConflictException("The idempotency key was already used with different input.");
                PricingExceptionsTelemetry.Replays.Add(1, operationTag);
                // Replay the original result even if the request has since advanced to another version.
                return JsonSerializer.Deserialize<PricingExceptionResult>(existing.ResponseJson)!;
            }
            var result = await operation();
            database.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Id = Guid.NewGuid(),
                ActorUserId = actorId,
                OperationScope = scope,
                Key = key,
                PayloadHash = hash,
                RequestId = result.Id,
                ResponseStatusCode = scope.StartsWith("review:") ? 200 : 201,
                ResponseJson = JsonSerializer.Serialize(result)
            });
            // Save workflow changes, history, and the retry result together in one call.
            // InMemory still provides no transactional rollback if storage fails partway through.
            await database.SaveChangesAsync(cancellationToken);
            PricingExceptionsTelemetry.Mutations.Add(1, operationTag);
            return result;
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

    // Business term and version validation.
    private static void ValidateTerms(int discount, string reason)
    {
        if (discount is < 1 or > 500)
            throw new BusinessRuleException("Discount must be between 1 and 500 basis points.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            throw new BusinessRuleException("A reason of up to 2000 characters is required.");
    }

    // Version changes on every mutation; revision changes only when new terms are submitted.
    // Both must match what the caller viewed before editing or reviewing.
    private static void CheckVersion(PricingExceptionRequest request, long version, int revision)
    {
        if (request.Version != version || request.CurrentRevisionNumber != revision)
            throw new ConflictException("The request version or revision is stale.");
    }

    // Revision and audit history creation.
    private static RequestRevision NewRevision(PricingExceptionRequest request, int discount, string reason, Guid actor, DateTimeOffset now)
        => new()
        {
            RequestId = request.Id,
            RevisionNumber = request.CurrentRevisionNumber,
            DiscountBasisPoints = discount,
            Reason = reason.Trim(),
            SubmittedByUserId = actor,
            SubmittedAtUtc = now
        };

    private void AppendHistory(PricingExceptionRequest request, string action, Guid actor, DateTimeOffset now)
        => database.HistoryEntries.Add(new HistoryEntry
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            RevisionNumber = request.CurrentRevisionNumber,
            // The request version provides a stable event order even when timestamps are identical.
            Sequence = request.Version,
            Action = action,
            ActorUserId = actor,
            OccurredAtUtc = now
        });

    // Current request result mapping.
    private static PricingExceptionResult Result(PricingExceptionRequest request, RequestRevision revision, ReviewDecision? decision = null)
        => new(request.Id, request.MortgageApplicationId, request.CreatedByUserId, request.Status,
            revision.RevisionNumber, request.Version, revision.DiscountBasisPoints, revision.Reason,
            decision?.ReviewedByUserId, decision?.DecidedAtUtc,
            request.RequesterName, request.AssignedReviewerId, request.AssignedReviewerName, decision?.ReviewerName);
}
