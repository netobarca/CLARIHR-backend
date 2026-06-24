using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileMedicalClaimResponse(
    Guid MedicalClaimPublicId,
    Guid InsuranceId,
    string? InsuranceName,
    string? AccountNumber,
    string ClaimantType,
    Guid? BeneficiaryPublicId,
    string? PatientName,
    string? KinshipCode,
    string ClaimTypeCode,
    string? Diagnosis,
    decimal? ClaimAmount,
    string? CurrencyCode,
    decimal? PaidAmount,
    int? ResponseTimeDays,
    string? Notes,
    DateTime ClaimDateUtc,
    DateTime? ResolutionDateUtc,
    string? ClaimStatusCode,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => MedicalClaimPublicId;
}

public sealed record MedicalClaimInput(
    Guid InsurancePublicId,
    string? AccountNumber,
    string ClaimantType,
    Guid? BeneficiaryPublicId,
    string ClaimTypeCode,
    string? Diagnosis,
    decimal? ClaimAmount,
    string? CurrencyCode,
    decimal? PaidAmount,
    string? Notes,
    DateTime ClaimDateUtc,
    DateTime? ResolutionDateUtc,
    string? ClaimStatusCode,
    string? SourceSystem,
    string? SourceReference,
    DateTime? SourceSyncedUtc);

public sealed record AddPersonnelFileMedicalClaimCommand(
    Guid PersonnelFileId,
    MedicalClaimInput Item)
    : ICommand<PersonnelFileMedicalClaimResponse>;

public sealed record UpdatePersonnelFileMedicalClaimCommand(
    Guid PersonnelFileId,
    Guid MedicalClaimPublicId,
    MedicalClaimInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileMedicalClaimResponse>;

public sealed record DeletePersonnelFileMedicalClaimCommand(
    Guid PersonnelFileId,
    Guid MedicalClaimPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileMedicalClaimPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileMedicalClaimCommand(
    Guid PersonnelFileId,
    Guid MedicalClaimPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileMedicalClaimPatchOperation> Operations)
    : ICommand<PersonnelFileMedicalClaimResponse>;

public sealed record GetPersonnelFileMedicalClaimsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>;

public sealed record GetPersonnelFileMedicalClaimByIdQuery(Guid PersonnelFileId, Guid MedicalClaimPublicId)
    : IQuery<PersonnelFileMedicalClaimResponse>;

internal sealed class MedicalClaimInputValidator : AbstractValidator<MedicalClaimInput>
{
    public MedicalClaimInputValidator()
    {
        // Insurance is mandatory (D-03). Existence/ownership is verified in the handler (422).
        RuleFor(input => input.InsurancePublicId).NotEmpty();

        // Patient dimension (D-02). Beneficiary membership is verified in the handler (422).
        RuleFor(input => input.ClaimantType)
            .NotEmpty()
            .Must(value => MedicalClaimClaimantTypes.IsValid(value))
            .WithMessage("ClaimantType must be TITULAR or BENEFICIARIO.");
        RuleFor(input => input.BeneficiaryPublicId)
            .NotEmpty()
            .When(input => string.Equals(
                input.ClaimantType?.Trim(),
                MedicalClaimClaimantTypes.Beneficiario,
                StringComparison.OrdinalIgnoreCase));

        RuleFor(input => input.ClaimTypeCode).NotEmpty().MaximumLength(80);

        // Monetary (D-06): non-negative is a hard rule; paid > claimed is NOT blocked (reimbursement).
        RuleFor(input => input.ClaimAmount).GreaterThanOrEqualTo(0).When(input => input.ClaimAmount.HasValue);
        RuleFor(input => input.PaidAmount).GreaterThanOrEqualTo(0).When(input => input.PaidAmount.HasValue);

        // Currency ISO-4217 (3 chars) — house convention validates length (D-05).
        RuleFor(input => input.CurrencyCode)
            .Length(3)
            .When(input => !string.IsNullOrWhiteSpace(input.CurrencyCode));

        // Claim date required and not in the future (RF-009); resolution date not before claim date (RF-006).
        RuleFor(input => input.ClaimDateUtc)
            .NotEmpty()
            .Must(date => date <= DateTime.UtcNow.AddDays(1))
            .WithMessage("ClaimDateUtc must not be in the future.");
        RuleFor(input => input.ResolutionDateUtc)
            .GreaterThanOrEqualTo(input => input.ClaimDateUtc)
            .When(input => input.ResolutionDateUtc.HasValue);

        RuleFor(input => input.ClaimStatusCode).MaximumLength(80);
        RuleFor(input => input.AccountNumber).MaximumLength(120);
        RuleFor(input => input.Diagnosis).MaximumLength(1000);
        RuleFor(input => input.Notes).MaximumLength(2000);
    }
}

internal sealed class AddPersonnelFileMedicalClaimCommandValidator : AbstractValidator<AddPersonnelFileMedicalClaimCommand>
{
    public AddPersonnelFileMedicalClaimCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new MedicalClaimInputValidator());
    }
}

internal sealed class UpdatePersonnelFileMedicalClaimCommandValidator : AbstractValidator<UpdatePersonnelFileMedicalClaimCommand>
{
    public UpdatePersonnelFileMedicalClaimCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.MedicalClaimPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new MedicalClaimInputValidator());
    }
}

internal sealed class DeletePersonnelFileMedicalClaimCommandValidator : AbstractValidator<DeletePersonnelFileMedicalClaimCommand>
{
    public DeletePersonnelFileMedicalClaimCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.MedicalClaimPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileMedicalClaimCommandValidator : AbstractValidator<PatchPersonnelFileMedicalClaimCommand>
{
    public PatchPersonnelFileMedicalClaimCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.MedicalClaimPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Operations).NotEmpty();
        RuleFor(c => c.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(c => c.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class GetPersonnelFileMedicalClaimsQueryValidator : AbstractValidator<GetPersonnelFileMedicalClaimsQuery>
{
    public GetPersonnelFileMedicalClaimsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileMedicalClaimByIdQueryValidator : AbstractValidator<GetPersonnelFileMedicalClaimByIdQuery>
{
    public GetPersonnelFileMedicalClaimByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.MedicalClaimPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileMedicalClaimPatchState
{
    public Guid InsurancePublicId { get; set; }
    public string? AccountNumber { get; set; }
    public string ClaimantType { get; set; } = MedicalClaimClaimantTypes.Titular;
    public Guid? BeneficiaryPublicId { get; set; }
    public string ClaimTypeCode { get; set; } = string.Empty;
    public string? Diagnosis { get; set; }
    public decimal? ClaimAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? PaidAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime ClaimDateUtc { get; set; }
    public DateTime? ResolutionDateUtc { get; set; }
    public string? ClaimStatusCode { get; set; }
    public string? SourceSystem { get; set; }
    public string? SourceReference { get; set; }
    public DateTime? SourceSyncedUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileMedicalClaimPatchState From(PersonnelFileMedicalClaimResponse response) =>
        new()
        {
            InsurancePublicId = response.InsuranceId,
            AccountNumber = response.AccountNumber,
            ClaimantType = response.ClaimantType,
            BeneficiaryPublicId = response.BeneficiaryPublicId,
            ClaimTypeCode = response.ClaimTypeCode,
            Diagnosis = response.Diagnosis,
            ClaimAmount = response.ClaimAmount,
            CurrencyCode = response.CurrencyCode,
            PaidAmount = response.PaidAmount,
            Notes = response.Notes,
            ClaimDateUtc = response.ClaimDateUtc,
            ResolutionDateUtc = response.ResolutionDateUtc,
            ClaimStatusCode = response.ClaimStatusCode,
            SourceSystem = response.SourceSystem,
            SourceReference = response.SourceReference,
            SourceSyncedUtc = response.SourceSyncedUtc,
            IsActive = response.IsActive
        };

    public MedicalClaimInput ToInput() =>
        new(
            InsurancePublicId,
            AccountNumber,
            ClaimantType,
            BeneficiaryPublicId,
            ClaimTypeCode,
            Diagnosis,
            ClaimAmount,
            CurrencyCode,
            PaidAmount,
            Notes,
            ClaimDateUtc,
            ResolutionDateUtc,
            ClaimStatusCode,
            SourceSystem,
            SourceReference,
            SourceSyncedUtc);
}

