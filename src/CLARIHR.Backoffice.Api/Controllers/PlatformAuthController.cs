using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Application.Features.Platform.Auth;
using CLARIHR.Backoffice.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Backoffice.Api.Controllers;

[ApiController]
[Route("api/platform/auth")]
public sealed class PlatformAuthController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] PlatformLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new PlatformLoginCommand(request.Email, request.Password),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<AuthResponse>.Failure(result.Error))
            : Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] PlatformRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new PlatformRefreshTokenCommand(request.RefreshToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<AuthResponse>.Failure(result.Error))
            : Ok(result.Value);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(new PlatformLogoutCommand(), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<bool>.Failure(result.Error)).Result!;
        }

        return NoContent();
    }
}
