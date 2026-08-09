using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// H-01 — STATE TRANSITIONS of a job profile: publish, reopen (with a mandatory reason) and archive.
/// Kept in a dedicated controller because the class-level policy set must map its writes to
/// <see cref="JobProfilePolicies.Publish"/> — a grant that <c>JobProfiles.Admin</c> deliberately does NOT
/// imply, so whoever drafts a descriptor is not automatically the one who approves it (mirrors
/// <c>PayrollRunResolutionController</c>).
/// <para>
/// These endpoints replace the old <c>PATCH /job-profiles/{publicId}</c> with an <c>op</c> on
/// <c>/status</c>, which can no longer change the status: a JSON Patch on the profile answers to the
/// ordinary <c>Manage</c> policy and would have handed the publish right to every profile administrator.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Produces("application/json")]
[Tags("Job Profiles")]
[AuthorizationPolicySet(JobProfilePolicies.Read, JobProfilePolicies.Publish)]
[ResourceActions(JobProfilePermissionCodes.ResourceKey)]
public sealed class JobProfileResolutionController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPatch("job-profiles/{publicId:guid}/publication")]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Publish a job profile",
        Description = """
            `Draft` → `Published`. **The descriptor FREEZES**: neither the profile core nor any of its 9
            collections can be modified afterwards (`422 JOB_PROFILE_STATE_RULE_VIOLATION`) until the
            profile is reopened. Publishing is also what makes the profile usable downstream — a position
            slot cannot be created against a profile that is not published.

            Requires the dedicated `JobProfiles.Publish` grant (`JobProfiles.Admin` does NOT imply it) and
            `If-Match` with the profile's current `concurrencyToken`. The minimum content is enforced:
            objective, responsibilities, at least one requirement and at least one function
            (`422 JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING`). Only a `Draft` profile can be published.

            The competency matrix is deliberately NOT frozen: it is an operational overlay on an approved
            descriptor and stays writable while the profile is published.
            """)]
    public async Task<ActionResult<JobProfileCoreResponse>> Publish(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PublishJobProfileCommand(publicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("job-profiles/{publicId:guid}/reopening")]
    [Consumes("application/json")]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Reopen a published job profile for editing",
        Description = """
            `Published` → `Draft` with a MANDATORY reason — the only way to correct an approved descriptor,
            and the change-control record of why it was unfrozen. The reason is stored in the audit trail
            (`JOB_PROFILE_REOPENED`). `version` is incremented, so every publish/reopen cycle is traceable.

            **Existing position slots and their occupants are untouched**: the state governs new downstream
            writes only. What a reopening blocks is creating or repointing a slot while the profile is not
            published; the plazas already created keep working, and payroll is unaffected.

            Requires the dedicated `JobProfiles.Publish` grant and `If-Match`. Only a `Published` profile
            can be reopened (`422 JOB_PROFILE_STATE_RULE_VIOLATION`).
            """)]
    public async Task<ActionResult<JobProfileCoreResponse>> Reopen(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ReopenJobProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReopenJobProfileCommand(publicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("job-profiles/{publicId:guid}/archival")]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Archive a job profile",
        Description = """
            `Draft` or `Published` → `Archived`, and sets `isActive` to `false`. Terminal: an archived
            profile can no longer be modified, republished or reopened. Idempotent — archiving an already
            archived profile returns it unchanged.

            Existing position slots are NOT removed or deactivated (`job_profile_id` is `RESTRICT`, and
            there is no delete for a job profile). Requires the dedicated `JobProfiles.Publish` grant and
            `If-Match`.
            """)]
    public async Task<ActionResult<JobProfileCoreResponse>> Archive(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ArchiveJobProfileCommand(publicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record ReopenJobProfileRequest(string Reason);
}
