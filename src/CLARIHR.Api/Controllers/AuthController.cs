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
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Anonymous authentication command surface (register / external / login / refresh / logout /
// password-reset / invitation-accept). NOT a CRUD resource controller: the underlying aggregates
// (User, RefreshToken, PasswordResetToken) are never exposed as REST resources — the controller
// executes authentication protocols, so canonical CRUD criteria (concurrency token, PATCH, GET-all,
// soft-delete) do not apply by design (see canonical-alignment doc 30).
//
// Authorization: every endpoint is [AllowAnonymous] except Logout ([Authorize]); there is no RBAC
// resource to govern, so [AuthorizationPolicySet] does NOT apply by design (the Auth family stays out
// of GovernedFamilyRegex). It IS enrolled in the OpenAPI guardrail ("Authentication") so a dropped
// [Tags]/[SwaggerOperation] fails CI. The route is canonically versioned under `api/v1/auth`.
//
// Error declarations are kept inline as [ProducesResponseType<ProblemDetails>] rather than
// [ProducesStandardErrors]: the anonymous-auth error profile (202/429/422/500, never 403/404) does not
// match the standard tenant/RBAC error sets, so inline is the more accurate contract.
//
// Tags: the self-contained password-reset sub-flow (request → validate → redeem) carries a per-action
// [Tags("Password Reset")] for Swagger grouping; the single invitation-accept endpoint stays under
// "Authentication" by design (it is anonymous and mints a session — it is core auth, not CompanyUsers).
[ApiController]
[Route("api/v1/auth")]
[Tags("Authentication")]
public sealed class AuthController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [AllowAnonymous]
    [Tags("Password Reset")]
    [EnableRateLimiting("auth-password-reset-request")]
    [HttpPost("password-reset/request")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [SwaggerOperation(
        Summary = "Request a password reset email",
        Description = """
            Starts the password-reset flow for a local account. Always responds `202 Accepted`
            regardless of whether the email exists or is eligible — this uniform response prevents
            account enumeration. When eligible, any active reset tokens are revoked, a new single-use
            token is issued and a signed reset link is emailed. A per-account cooldown plus the
            `auth-password-reset-request` rate limit (`429`) throttle abuse.
            """)]
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
    [Tags("Password Reset")]
    [HttpPost("password-reset/validate")]
    [ProducesResponseType<PasswordResetTokenValidationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [SwaggerOperation(
        Summary = "Validate a password reset token",
        Description = """
            Pre-flight check used by the reset form before it shows the new-password fields. For an
            active (unused, unexpired, unrevoked) token it returns the token expiry and a masked email;
            an invalid, expired or already-used token yields `401`.
            """)]
    public async Task<ActionResult<PasswordResetTokenValidationResponse>> ValidatePasswordReset(
        [FromBody] ValidatePasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(new ValidatePasswordResetTokenQuery(request.Token), cancellationToken);
        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [Tags("Password Reset")]
    [HttpPost("password-reset/redeem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [SwaggerOperation(
        Summary = "Redeem a password reset token",
        Description = """
            Consumes the single-use reset token and sets the new password (enforcing the password
            policy → `400` on violation). On success it marks the token used, revokes every other active
            reset token and revokes all refresh sessions (Core + Platform), then returns `204`. An
            invalid/expired/used token yields `401`. The user must log in again with the new password.
            """)]
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
    [SwaggerOperation(
        Summary = "Register a new local user",
        Description = """
            Creates an active local user (enforcing the password policy) and seeds default user
            preferences in a single transaction, then returns `201 Created` with the issued access and
            refresh tokens and the authenticated user. A duplicate email yields `409`; validation or
            password-policy failures yield `400`.
            """)]
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
    [SwaggerOperation(
        Summary = "Authenticate or register via an external identity provider",
        Description = """
            Validates the external provider id-token (e.g. Google) and either logs in the matching user
            or provisions a new one linked to the provider. Returns `201 Created` when a new user is
            provisioned and `200 OK` when an existing user logs in, both with the issued session. An
            invalid token yields `401`, a provider-link conflict yields `409`, and a provider that
            returns no email yields `422`.
            """)]
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
    [SwaggerOperation(
        Summary = "Authenticate with email and password",
        Description = """
            Exchanges email + password for a session. Returns `200 OK` with the access and refresh
            tokens and the authenticated user for an active local account. Invalid credentials, an
            inactive account or a non-local account all yield a uniform `401` to avoid leaking which
            condition failed (anti-enumeration). The `auth-login` rate limit throttles brute force.
            """)]
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
    [SwaggerOperation(
        Summary = "Accept a company user invitation",
        Description = """
            Anonymous endpoint that consumes the single-use invitation token, sets the invited user's
            password and activates the user, their company membership and their IAM user, then returns
            `200 OK` with a tenant-scoped session. An invalid or expired token yields `401`; if the
            invitation's company has been archived the session is refused with `409`.
            """)]
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
    [SwaggerOperation(
        Summary = "Exchange a refresh token for a new session",
        Description = """
            Rotates the supplied refresh token: the presented token is single-use and is revoked as it
            is exchanged for a fresh access/refresh pair (`200 OK`). Replaying an already-rotated token
            is detected as reuse and revokes the whole token family. An invalid, expired or revoked
            token yields `401`.
            """)]
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
    [SwaggerOperation(
        Summary = "Log out the current session",
        Description = """
            Requires an authenticated caller. Revokes the caller's active Core refresh tokens so the
            session can no longer be refreshed, then returns `204 No Content`. Idempotent — logging out
            an already-revoked session still succeeds.
            """)]
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
