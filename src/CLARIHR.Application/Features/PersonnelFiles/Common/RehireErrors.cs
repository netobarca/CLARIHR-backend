using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles.Common;

/// <summary>
/// Error catalog for the employee-rehire flow (RF-001…RF-009, RN-02/RN-06/RN-14, D-04/D-09/D-13).
/// Every code must have a localized message in <c>BackendMessages.resx</c>,
/// <c>BackendMessages.es.resx</c> and <c>BackendMessages.es-SV.resx</c> or the
/// <c>BackendMessageLocalizationTests</c> parity test fails (F11).
/// </summary>
public static class RehireErrors
{
    /// <summary>RN-02 — rehire only targets <c>RecordType=Employee</c> files.</summary>
    public static readonly Error NotAnEmployee = new(
        "REHIRE_NOT_AN_EMPLOYEE",
        "Rehire applies only to employee personnel files.",
        ErrorType.UnprocessableEntity);

    /// <summary>RN-02 — only a retired employee (inactive employment) can be rehired.</summary>
    public static readonly Error NotRetired = new(
        "REHIRE_NOT_RETIRED",
        "Only a retired employee with inactive employment and a prior period can be rehired.",
        ErrorType.UnprocessableEntity);

    /// <summary>RN-14 / D-13 / D-17 — the prior period must be confirmed closed/settled first.</summary>
    public static readonly Error PriorPeriodOpen = new(
        "REHIRE_PRIOR_PERIOD_OPEN",
        "The previous employment period must be confirmed as closed or settled before rehiring.",
        ErrorType.UnprocessableEntity);

    /// <summary>RN-06 / D-04 — file is "not rehireable"; needs an authorized override + justification.</summary>
    public static readonly Error RequiresAuthorization = new(
        "REHIRE_REQUIRES_AUTHORIZATION",
        "This personnel file is marked as not rehireable; an authorized user must approve the rehire with a justification.",
        ErrorType.UnprocessableEntity);

    /// <summary>RN-09 / D-09 / E6 — previous institutional email is taken; a new one is required.</summary>
    public static readonly Error InstitutionalEmailInUse = new(
        "REHIRE_INSTITUTIONAL_EMAIL_IN_USE",
        "The previous institutional email is already in use; provide a new institutional email for the rehired employee.",
        ErrorType.Conflict);
}
