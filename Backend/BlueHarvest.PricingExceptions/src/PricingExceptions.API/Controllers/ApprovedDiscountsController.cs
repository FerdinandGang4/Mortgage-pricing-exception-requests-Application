using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PricingExceptions.API.DTOs.Responses;
using PricingExceptions.API.Exceptions;
using PricingExceptions.API.Mappings;
using PricingExceptions.API.Services.Interfaces;

namespace PricingExceptions.API.Controllers;

[ApiController]
[Authorize(Roles = "MortgageProcess")]
[Route("api/v1/mortgage-applications")]
public sealed class ApprovedDiscountsController(IApprovedDiscountService service) : ControllerBase
{
    [HttpGet("{id:guid}/approved-discount")]
    [ProducesResponseType<ApprovedDiscountResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApprovedDiscountResponseDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var actorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        // A missing effective approval is a 404, including when revised terms await a new decision.
        var result = await service.GetAsync(id, actorId, cancellationToken)
            ?? throw new NotFoundException("No current approved discount exists for this mortgage application.");
        return Ok(ApprovedDiscountMapper.ToResponse(result));
    }
}
