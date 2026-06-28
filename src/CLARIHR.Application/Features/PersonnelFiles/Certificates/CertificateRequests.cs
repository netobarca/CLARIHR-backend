using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileCertificateRequestResponse(
    Guid CertificateRequestPublicId,
    string CertificateTypeCode,
    string? TypeName,
    string RequestStatusCode,
    string PurposeCode,
    string? AddressedTo,
    string DeliveryMethodCode,
    string LanguageCode,
    int Copies,
    DateTime RequestDateUtc,
    DateTime? NeededByDateUtc,
    Guid RequestedByUserId,
    Guid? IssuedByUserId,
    DateTime? IssuedDateUtc,
    DateTime? DeliveredDateUtc,
    string? ResolutionNotes,
    int? ResponseTimeDays,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => CertificateRequestPublicId;
}

/// <summary>
/// Business fields an employee (self-service) or HR provides when requesting a certificate. The status,
/// issuance and delivery are NOT set here — they are driven by the dedicated HR actions (PR-6). No money is
/// involved (D-03).
/// </summary>
public sealed record CertificateRequestInput(
    string TypeCode,
    string PurposeCode,
    string? AddressedTo,
    string DeliveryMethodCode,
    string? LanguageCode,
    int? Copies,
    DateTime RequestDateUtc,
    DateTime? NeededByDateUtc);

public sealed record AddPersonnelFileCertificateRequestCommand(
    Guid PersonnelFileId,
    CertificateRequestInput Item)
    : ICommand<PersonnelFileCertificateRequestResponse>;

public sealed record UpdatePersonnelFileCertificateRequestCommand(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    CertificateRequestInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCertificateRequestResponse>;

public sealed record DeletePersonnelFileCertificateRequestCommand(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record GetPersonnelFileCertificateRequestsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileCertificateRequestResponse>>;

public sealed record GetPersonnelFileCertificateRequestByIdQuery(Guid PersonnelFileId, Guid CertificateRequestPublicId)
    : IQuery<PersonnelFileCertificateRequestResponse>;

// --- HR lifecycle actions (D-04, linear). Handlers in PR-6; the issue handler also generates the PDF (PR-5). ---

public sealed record ProcessCertificateRequestCommand(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCertificateRequestResponse>;

public sealed record IssueCertificateRequestCommand(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCertificateRequestResponse>;

public sealed record DeliverCertificateRequestCommand(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    DateTime DeliveredDateUtc,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCertificateRequestResponse>;

public sealed record RejectCertificateRequestCommand(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCertificateRequestResponse>;

public sealed record CancelCertificateRequestCommand(
    Guid PersonnelFileId,
    Guid CertificateRequestPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCertificateRequestResponse>;

internal sealed class CertificateRequestInputValidator : AbstractValidator<CertificateRequestInput>
{
    public CertificateRequestInputValidator()
    {
        // Type/purpose/delivery are mandatory; existence/active is verified against the catalogs in the handler (422).
        RuleFor(input => input.TypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.PurposeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.DeliveryMethodCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.AddressedTo).MaximumLength(500);

        // Language is es/en (D-07); default resolved in the domain when omitted.
        RuleFor(input => input.LanguageCode)
            .Must(code => string.Equals(code, "es", StringComparison.OrdinalIgnoreCase) || string.Equals(code, "en", StringComparison.OrdinalIgnoreCase))
            .When(input => !string.IsNullOrWhiteSpace(input.LanguageCode));

        RuleFor(input => input.Copies)
            .GreaterThan(0)
            .When(input => input.Copies.HasValue);

        // Request date required and not in the future.
        RuleFor(input => input.RequestDateUtc)
            .NotEmpty()
            .Must(date => date <= DateTime.UtcNow.AddDays(1));

        // Needed-by date (if any) must not precede the request date.
        RuleFor(input => input.NeededByDateUtc)
            .Must((input, neededBy) => neededBy is null || neededBy.Value.Date >= input.RequestDateUtc.Date);
    }
}

internal sealed class AddPersonnelFileCertificateRequestCommandValidator : AbstractValidator<AddPersonnelFileCertificateRequestCommand>
{
    public AddPersonnelFileCertificateRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new CertificateRequestInputValidator());
    }
}

internal sealed class UpdatePersonnelFileCertificateRequestCommandValidator : AbstractValidator<UpdatePersonnelFileCertificateRequestCommand>
{
    public UpdatePersonnelFileCertificateRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CertificateRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new CertificateRequestInputValidator());
    }
}

internal sealed class DeletePersonnelFileCertificateRequestCommandValidator : AbstractValidator<DeletePersonnelFileCertificateRequestCommand>
{
    public DeletePersonnelFileCertificateRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CertificateRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetPersonnelFileCertificateRequestsQueryValidator : AbstractValidator<GetPersonnelFileCertificateRequestsQuery>
{
    public GetPersonnelFileCertificateRequestsQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class GetPersonnelFileCertificateRequestByIdQueryValidator : AbstractValidator<GetPersonnelFileCertificateRequestByIdQuery>
{
    public GetPersonnelFileCertificateRequestByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CertificateRequestPublicId).NotEmpty();
    }
}

internal sealed class ProcessCertificateRequestCommandValidator : AbstractValidator<ProcessCertificateRequestCommand>
{
    public ProcessCertificateRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CertificateRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class IssueCertificateRequestCommandValidator : AbstractValidator<IssueCertificateRequestCommand>
{
    public IssueCertificateRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CertificateRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

internal sealed class DeliverCertificateRequestCommandValidator : AbstractValidator<DeliverCertificateRequestCommand>
{
    public DeliverCertificateRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CertificateRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.DeliveredDateUtc).NotEmpty();
    }
}

internal sealed class RejectCertificateRequestCommandValidator : AbstractValidator<RejectCertificateRequestCommand>
{
    public RejectCertificateRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CertificateRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

internal sealed class CancelCertificateRequestCommandValidator : AbstractValidator<CancelCertificateRequestCommand>
{
    public CancelCertificateRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.CertificateRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}
