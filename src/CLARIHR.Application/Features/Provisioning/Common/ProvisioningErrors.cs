using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Provisioning.Common;

public static class ProvisioningErrors
{
    public static readonly Error ProvisioningFailed = new(
        "provisioning.failed",
        "Initial company provisioning failed.",
        ErrorType.Unexpected);

    public static readonly Error UserNotFound = new(
        "provisioning.user_not_found",
        "The user to provision was not found.",
        ErrorType.NotFound);

    public static readonly Error InitialLegalRepresentativeRequired = new(
        "provisioning.initial_legal_representative_required",
        "An initial active legal representative is required to create the company.",
        ErrorType.Validation);
}
