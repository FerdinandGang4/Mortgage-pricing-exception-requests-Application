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
    private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    [Authorize(Roles = "RelationshipManager")]
    [ProducesResponseType<PricingExceptionResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PricingExceptionResponseDto>> Create(
        CreateExceptionRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request.MortgageApplicationId, request.DiscountBps,
            request.Reason, ActorId, idempotencyKey, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, PricingExceptionMapper.ToResponse(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PricingExceptionResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PricingExceptionResponseDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, ActorId, cancellationToken);
        return Ok(PricingExceptionMapper.ToResponse(result));
    }

    [HttpPost("{id:guid}/revisions")]
    [Authorize(Roles = "RelationshipManager")]
    [ProducesResponseType<PricingExceptionResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PricingExceptionResponseDto>> SubmitRevision(
        Guid id, SubmitRevisionDto request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await service.SubmitRevisionAsync(id, request.DiscountBps, request.Reason,
            request.ExpectedVersion, request.ExpectedRevisionNumber, ActorId, idempotencyKey, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, PricingExceptionMapper.ToResponse(result));
    }

    [HttpPost("{id:guid}/decisions")]
    [Authorize(Roles = "Reviewer")]
    [ProducesResponseType<PricingExceptionResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PricingExceptionResponseDto>> Review(
        Guid id, ReviewDecisionDto request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await service.ReviewAsync(id, request.Outcome!.Value,
            request.Reason, request.ExpectedVersion, request.ReviewedRevisionNumber,
            ActorId, idempotencyKey, cancellationToken);
        return Ok(PricingExceptionMapper.ToResponse(result));
    }

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<HistoryResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HistoryResponseDto>> History(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await service.GetHistorySnapshotAsync(id, ActorId, cancellationToken);
        return Ok(HistoryMapper.ToResponse(snapshot));
    }
}
