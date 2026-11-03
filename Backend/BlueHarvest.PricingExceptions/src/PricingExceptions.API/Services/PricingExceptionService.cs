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
    PricingExceptionsDbContext database, IUserService users,
    InMemoryWriteLock writeLock, TimeProvider clock) : IPricingExceptionService
{
    public async Task<PricingExceptionResult> CreateAsync(Guid applicationId, int discountBasisPoints,
        string reason, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        await users.RequireRoleAsync(actorId, UserRole.RelationshipManager, cancellationToken);
        return await ExecuteWriteAsync(actorId, "create", idempotencyKey,
            new { applicationId, discountBasisPoints, reason }, async () =>
        {
            ValidateTerms(discountBasisPoints, reason);
            if (applicationId == Guid.Empty)
                throw new BusinessRuleException("A mortgage application identifier is required.");
            // Demo policy: one request per application; changed terms use revisions.
            if (await database.PricingExceptionRequests.AnyAsync(x => x.MortgageApplicationId == applicationId, cancellationToken))
                throw new ConflictException("This mortgage application already has an exception request.");
            var now = clock.GetUtcNow();
            var request = new PricingExceptionRequest
            {
                Id = Guid.NewGuid(),
                MortgageApplicationId = applicationId,
                CreatedByUserId = actorId,
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

    public async Task<PricingExceptionResult> GetAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
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

    public async Task<PricingExceptionResult> SubmitRevisionAsync(Guid requestId, int discountBasisPoints,
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

    public async Task<PricingExceptionResult> ReviewAsync(Guid requestId, DecisionOutcome outcome,
        string? reason, long expectedVersion, int reviewedRevisionNumber, Guid actorId,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        await users.RequireRoleAsync(actorId, UserRole.Reviewer, cancellationToken);
        return await ExecuteWriteAsync(actorId, $"review:{requestId}", idempotencyKey,
            new { outcome, reason, expectedVersion, reviewedRevisionNumber }, async () =>
        {
            if (!Enum.IsDefined(outcome) || reason?.Length > 2000)
                throw new BusinessRuleException("A valid outcome and review reason of at most 2000 characters are required.");
            var request = await FindAsync(requestId, cancellationToken);
            if (request.CreatedByUserId == actorId)
                throw new ForbiddenException("Self-review is not allowed.");
            CheckVersion(request, expectedVersion, reviewedRevisionNumber);
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

    public async Task<IReadOnlyList<HistoryEntry>> GetHistoryAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ReadRequestAsync<IReadOnlyList<HistoryEntry>>(requestId, actorId, async () =>
            await database.HistoryEntries.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.Sequence).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<RequestRevision>> GetRevisionsAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ReadRequestAsync<IReadOnlyList<RequestRevision>>(requestId, actorId, async () =>
            await database.RequestRevisions.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.RevisionNumber).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewDecision>> GetDecisionsAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        return await ReadRequestAsync<IReadOnlyList<ReviewDecision>>(requestId, actorId, async () =>
            await database.ReviewDecisions.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.RevisionNumber).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<RequestHistoryResult> GetHistorySnapshotAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default)
        => await ReadRequestAsync(requestId, actorId, async () => new RequestHistoryResult(
            await database.HistoryEntries.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.Sequence).ToListAsync(cancellationToken),
            await database.RequestRevisions.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.RevisionNumber).ToListAsync(cancellationToken),
            await database.ReviewDecisions.AsNoTracking().Where(x => x.RequestId == requestId)
                .OrderBy(x => x.RevisionNumber).ToListAsync(cancellationToken)), cancellationToken);

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

    private async Task<PricingExceptionResult> ExecuteWriteAsync(Guid actorId, string scope, string key,
        object payload, Func<Task<PricingExceptionResult>> operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200)
            throw new BusinessRuleException("An idempotency key of up to 200 characters is required.");
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
            var existing = await database.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
                x => x.ActorUserId == actorId && x.OperationScope == scope && x.Key == key, cancellationToken);
            if (existing is not null)
            {
                if (existing.PayloadHash != hash)
                    throw new ConflictException("The idempotency key was already used with different input.");
                PricingExceptionsTelemetry.Replays.Add(1, operationTag);
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

    private async Task<PricingExceptionRequest> FindAsync(Guid id, CancellationToken cancellationToken)
        => await database.PricingExceptionRequests.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("The pricing exception request was not found.");

    private static void ValidateTerms(int discount, string reason)
    {
        if (discount is < 1 or > 500)
            throw new BusinessRuleException("Discount must be between 1 and 500 basis points.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            throw new BusinessRuleException("A reason of up to 2000 characters is required.");
    }

    private static void CheckVersion(PricingExceptionRequest request, long version, int revision)
    {
        if (request.Version != version || request.CurrentRevisionNumber != revision)
            throw new ConflictException("The request version or revision is stale.");
    }

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
            Sequence = request.Version,
            Action = action,
            ActorUserId = actor,
            OccurredAtUtc = now
        });

    private static PricingExceptionResult Result(PricingExceptionRequest request, RequestRevision revision, ReviewDecision? decision = null)
        => new(request.Id, request.MortgageApplicationId, request.CreatedByUserId, request.Status,
            revision.RevisionNumber, request.Version, revision.DiscountBasisPoints, revision.Reason,
            decision?.ReviewedByUserId, decision?.DecidedAtUtc);
}
