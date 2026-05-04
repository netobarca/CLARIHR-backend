using CLARIHR.Api.Common;
using CLARIHR.Application.Features.Auth.AcceptInvitation;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.External;
using CLARIHR.Application.Features.Auth.Login;
using CLARIHR.Application.Features.Auth.Logout;
using CLARIHR.Application.Features.Auth.PasswordReset;
using CLARIHR.Application.Features.Auth.RefreshToken;
using CLARIHR.Application.Features.Auth.RegisterUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("auth-password-reset-request")]
    [HttpPost("password-reset/request")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestPasswordReset(
        [FromBody] RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(new RequestPasswordResetCommand(request.Email), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        return Accepted();
    }

    [AllowAnonymous]
    [HttpPost("password-reset/validate")]
    [ProducesResponseType<PasswordResetTokenValidationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PasswordResetTokenValidationResponse>> ValidatePasswordReset(
        [FromBody] ValidatePasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new ValidatePasswordResetTokenQuery(request.Token), cancellationToken);
        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("password-reset/redeem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RedeemPasswordReset(
        [FromBody] RedeemPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new RedeemPasswordResetCommand(request.Token, request.NewPassword),
            cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-register")]
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            request.Country,
            request.Source);

        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<AuthResponse>.Failure(result.Error));
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-register")]
    [HttpPost("external")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> External(
        [FromBody] RegisterExternalRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new RegisterExternalUserCommand(
                request.Provider,
                request.IdToken,
                request.Country,
                request.Source),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<AuthResponse>.Failure(result.Error));
        }

        return result.Value.WasCreated
            ? StatusCode(StatusCodes.Status201Created, result.Value.Response)
            : Ok(result.Value.Response);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<AuthResponse>.Failure(result.Error))
            : Ok(result.Value);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-invite-accept")]
    [HttpPost("company-user-invitations/accept")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> AcceptCompanyUserInvitation(
        [FromBody] AcceptCompanyUserInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new AcceptCompanyUserInvitationCommand(request.Token, request.Password),
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new RefreshTokenCommand(request.RefreshToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<AuthResponse>.Failure(result.Error))
            : Ok(result.Value);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(new LogoutCommand(), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<bool>.Failure(result.Error)).Result!;
        }

        return NoContent();
    }

    public sealed record RequestPasswordResetRequest(string Email);

    public sealed record ValidatePasswordResetRequest(string Token);

    public sealed record RedeemPasswordResetRequest(string Token, string NewPassword);
}
