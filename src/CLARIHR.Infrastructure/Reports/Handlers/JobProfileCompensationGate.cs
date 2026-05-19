using System.Text.Json;
using CLARIHR.Application.Features.JobProfiles;

namespace CLARIHR.Infrastructure.Reports.Handlers;

/// <summary>
/// Confidentiality gate for the salary data of an exported job-profile PDF
/// (technical-debt doc 01 §N2). The export worker has no user/JWT context, so
/// the visibility decision is taken at request time (where RBAC is evaluable)
/// and persisted as the server-controlled <c>includeCompensation</c> parameter.
/// This gate enforces it before the payload ever reaches the renderer.
/// </summary>
/// <remarks>
/// Fail-closed by design: salary is included <b>only</b> when the parameter is
/// explicitly <c>true</c>. A missing/false/non-boolean value (e.g. a job queued
/// before this control existed, or a tampered payload) excludes it. The data is
/// removed from the payload — not masked — so the mapper omits the whole
/// "Compensación" section and the salary never reaches the PDF bytes.
/// <para>
/// §N3: the flag is read with <see cref="ReportExportParameters.ReadBoolExact"/>
/// (exact, case-sensitive match), not the case-insensitive / first-match
/// <c>ReadBool</c> helper. A client-supplied key with a different casing can
/// therefore never satisfy the gate, even if one slipped past the request-side
/// stamping or rode along on a job queued before the §N3 fix.
/// </para>
/// </remarks>
internal static class JobProfileCompensationGate
{
    public static JobProfilePrintResponse Apply(JobProfilePrintResponse payload, JsonElement parameters)
    {
        if (ReportExportParameters.ReadBoolExact(parameters, "includeCompensation") == true)
        {
            return payload;
        }

        return payload with
        {
            Profile = payload.Profile with
            {
                Compensation = null,
                MarketSalaryReference = null,
            },
        };
    }
}
