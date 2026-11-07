using Microsoft.EntityFrameworkCore;
using PricingExceptions.API.Data;
using PricingExceptions.API.Exceptions;
using PricingExceptions.API.Models.Enums;
using PricingExceptions.API.Services.Interfaces;
using PricingExceptions.API.Services.Models;

namespace PricingExceptions.API.Services;

public sealed class ApprovedDiscountService(PricingExceptionsDbContext database, IUserService userService, InMemoryWriteLock writeLock) : IApprovedDiscountService
{
    public async Task<ApprovedDiscountResult?> GetAsync(Guid applicationId, Guid actorId, CancellationToken cancellationToken = default)
    {
        await userService.GetUserWithRequiredRoleAsync(actorId, UserRole.MortgageProcess, cancellationToken);
        if (applicationId == Guid.Empty)
            throw new BusinessRuleException("A mortgage application identifier is required.");

        await writeLock.Semaphore.WaitAsync(cancellationToken);
        try
        {
            // Only the current revision can supply an effective discount. Historical approvals
            // no longer apply after revised terms move the request back to Pending.
            return await (
                from pricingExceptionRequest in database.PricingExceptionRequests.AsNoTracking()
                join requestRevision in database.RequestRevisions.AsNoTracking()
                    on new { RequestId = pricingExceptionRequest.Id, RevisionNumber = pricingExceptionRequest.CurrentRevisionNumber }
                    equals new { requestRevision.RequestId, requestRevision.RevisionNumber }
                join reviewDecision in database.ReviewDecisions.AsNoTracking()
                    on new { requestRevision.RequestId, requestRevision.RevisionNumber }
                    equals new { reviewDecision.RequestId, reviewDecision.RevisionNumber }
                where pricingExceptionRequest.MortgageApplicationId == applicationId
                    && pricingExceptionRequest.Status == RequestStatus.Approved && reviewDecision.Outcome == DecisionOutcome.Approved
                select new ApprovedDiscountResult(pricingExceptionRequest.Id, applicationId, requestRevision.DiscountBasisPoints,
                    requestRevision.RevisionNumber, reviewDecision.ReviewedByUserId, reviewDecision.DecidedAtUtc, reviewDecision.ReviewerName)
            ).SingleOrDefaultAsync(cancellationToken);
        }
        finally
        {
            writeLock.Semaphore.Release();
        }
    }
}
