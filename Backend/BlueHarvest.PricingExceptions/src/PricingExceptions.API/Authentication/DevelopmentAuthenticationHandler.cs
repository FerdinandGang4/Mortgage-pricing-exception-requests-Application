using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PricingExceptions.API.Data;

namespace PricingExceptions.API.Authentication;

// Local demo only. Never accepts this header outside Development.
public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IHostEnvironment environment, PricingExceptionsDbContext database)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevelopmentUser";
    public const string HeaderName = "X-User-Id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment())
            return AuthenticateResult.NoResult();
        if (!Request.Headers.TryGetValue(HeaderName, out var header))
            return AuthenticateResult.NoResult();
        if (!Guid.TryParse(header, out var id))
            return AuthenticateResult.Fail("Invalid demo user ID.");
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, Context.RequestAborted);
        if (user is null)
            return AuthenticateResult.Fail("Unknown demo user.");
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        }, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
