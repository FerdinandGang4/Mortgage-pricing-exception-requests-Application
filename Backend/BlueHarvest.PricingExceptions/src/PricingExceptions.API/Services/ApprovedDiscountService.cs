using Microsoft.EntityFrameworkCore;
using PricingExceptions.API.Data;
using PricingExceptions.API.Exceptions;
using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Services.Interfaces;
using PricingExceptions.API.Services.Models;

namespace PricingExceptions.API.Services;

public sealed class ApprovedDiscountService(PricingExceptionsDbContext database, IUserService users, InMemoryWriteLock writeLock) : IApprovedDiscountService
{
    public async Task<ApprovedDiscountResult?> GetAsync(Guid applicationId, Guid actorId, CancellationToken cancellationToken = default)
    {
        await users.RequireRoleAsync(actorId, UserRole.MortgageProcess, cancellationToken);
        if (applicationId == Guid.Empty)
            throw new BusinessRuleException("A mortgage application identifier is required.");

        await writeLock.Semaphore.WaitAsync(cancellationToken);
        try
        {
            // Only the current revision can supply an effective discount. Historical approvals
            // no longer apply after revised terms move the request back to Pending.
            return await (
                from request in database.PricingExceptionRequests.AsNoTracking()
                join revision in database.RequestRevisions.AsNoTracking()
                    on new { RequestId = request.Id, RevisionNumber = request.CurrentRevisionNumber }
                    equals new { revision.RequestId, revision.RevisionNumber }
                join decision in database.ReviewDecisions.AsNoTracking()
                    on new { revision.RequestId, revision.RevisionNumber }
                    equals new { decision.RequestId, decision.RevisionNumber }
                where request.MortgageApplicationId == applicationId
                    && request.Status == RequestStatus.Approved && decision.Outcome == DecisionOutcome.Approved
                select new ApprovedDiscountResult(request.Id, applicationId, revision.DiscountBasisPoints,
                    revision.RevisionNumber, decision.ReviewedByUserId, decision.DecidedAtUtc, decision.ReviewerName)
            ).SingleOrDefaultAsync(cancellationToken);
        }
        finally
        {
            writeLock.Semaphore.Release();
        }
    }
}
