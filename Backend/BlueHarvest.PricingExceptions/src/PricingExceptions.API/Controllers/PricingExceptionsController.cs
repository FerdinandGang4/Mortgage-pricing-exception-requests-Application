using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PricingExceptions.API.DTOs.Requests;
using PricingExceptions.API.DTOs.Responses;
using PricingExceptions.API.Mappings;
using PricingExceptions.API.Services.Interfaces;

namespace PricingExceptions.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/pricing-exceptions")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class PricingExceptionsController(IPricingExceptionService service) : ControllerBase
{
    // Take the actor from authenticated claims rather than accepting a caller ID in the body.
    private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    [Authorize(Roles = "RelationshipManager")]
    [ProducesResponseType<PricingExceptionResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PricingExceptionResponseDto>> CreatePricingExceptionRequestAsync(
        CreateExceptionRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var pricingExceptionResult = await service.CreatePricingExceptionRequestAsync(request.MortgageApplicationId, request.DiscountBps,
            request.Reason, request.ReviewerId!.Value, ActorId, idempotencyKey, cancellationToken);
        // Include a Location header pointing to the newly created request's GET endpoint.
        return CreatedAtAction(nameof(GetPricingExceptionRequestByIdAsync), new { id = pricingExceptionResult.Id }, PricingExceptionMapper.ToResponse(pricingExceptionResult));
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PricingExceptionResponseDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PricingExceptionResponseDto>>> GetPricingExceptionRequestsAsync(CancellationToken cancellationToken)
        => Ok((await service.ListPricingExceptionRequestsAsync(ActorId, cancellationToken)).Select(PricingExceptionMapper.ToResponse).ToList());

    [HttpGet("{id:guid}")]
    [ActionName(nameof(GetPricingExceptionRequestByIdAsync))]
    [ProducesResponseType<PricingExceptionResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PricingExceptionResponseDto>> GetPricingExceptionRequestByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var pricingExceptionResult = await service.GetPricingExceptionRequestAsync(id, ActorId, cancellationToken);
        return Ok(PricingExceptionMapper.ToResponse(pricingExceptionResult));
    }

    [HttpPost("{id:guid}/decisions")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType<PricingExceptionResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PricingExceptionResponseDto>> RecordPricingExceptionDecisionAsync(
        Guid id, ReviewDecisionDto request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var pricingExceptionResult = await service.ReviewPricingExceptionRequestAsync(id, request.Outcome!.Value,
            request.Reason, request.ExpectedVersion, request.ReviewedRevisionNumber,
            ActorId, idempotencyKey, cancellationToken);
        return Ok(PricingExceptionMapper.ToResponse(pricingExceptionResult));
    }

    [HttpPost("{id:guid}/revisions")]
    [Authorize(Roles = "RelationshipManager")]
    [ProducesResponseType<PricingExceptionResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PricingExceptionResponseDto>> SubmitPricingExceptionRevisionAsync(
        Guid id, SubmitRevisionDto request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var pricingExceptionResult = await service.SubmitRequestRevisionAsync(id, request.DiscountBps, request.Reason,
            request.ExpectedVersion, request.ExpectedRevisionNumber, ActorId, idempotencyKey, cancellationToken);
        return CreatedAtAction(nameof(GetPricingExceptionRequestByIdAsync), new { id }, PricingExceptionMapper.ToResponse(pricingExceptionResult));
    }

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<HistoryResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HistoryResponseDto>> GetPricingExceptionHistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var requestHistorySnapshot = await service.GetRequestHistorySnapshotAsync(id, ActorId, cancellationToken);
        return Ok(HistoryMapper.ToResponse(requestHistorySnapshot));
    }
}
