using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PricingExceptions.API.DTOs.Responses;
using PricingExceptions.API.Mappings;
using PricingExceptions.API.Services.Interfaces;

namespace PricingExceptions.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("me")]
    public async Task<ActionResult<UserResponseDto>> Me(CancellationToken cancellationToken)
        => Ok(UserMapper.ToResponse(await users.GetRequiredAsync(ActorId, cancellationToken)));

    [HttpGet("reviewers")]
    [Authorize(Roles = "RelationshipManager")]
    public async Task<ActionResult<IReadOnlyList<UserResponseDto>>> Reviewers(CancellationToken cancellationToken)
        => Ok((await users.GetReviewersAsync(ActorId, cancellationToken)).Select(UserMapper.ToResponse).ToList());
}
