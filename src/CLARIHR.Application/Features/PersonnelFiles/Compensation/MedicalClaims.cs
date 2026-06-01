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
    Guid? InsuranceId,
    string? AccountNumber,
    string ClaimTypeCode,
    string? Diagnosis,
    decimal? ClaimAmount,
    string? CurrencyCode,
    decimal? PaidAmount,
    int? ResponseTimeDays,
    string? Notes,
    DateTime ClaimDateUtc,
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
    Guid? InsurancePublicId,
    string? AccountNumber,
    string ClaimTypeCode,
    string? Diagnosis,
    decimal? ClaimAmount,
    string? CurrencyCode,
    decimal? PaidAmount,
    int? ResponseTimeDays,
    string? Notes,
    DateTime ClaimDateUtc,
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
        RuleFor(input => input.ClaimTypeCode).NotEmpty().MaximumLength(80);
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
    public Guid? InsurancePublicId { get; set; }
    public string? AccountNumber { get; set; }
    public string ClaimTypeCode { get; set; } = string.Empty;
    public string? Diagnosis { get; set; }
    public decimal? ClaimAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? PaidAmount { get; set; }
    public int? ResponseTimeDays { get; set; }
    public string? Notes { get; set; }
    public DateTime ClaimDateUtc { get; set; }
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
            ClaimTypeCode = response.ClaimTypeCode,
            Diagnosis = response.Diagnosis,
            ClaimAmount = response.ClaimAmount,
            CurrencyCode = response.CurrencyCode,
            PaidAmount = response.PaidAmount,
            ResponseTimeDays = response.ResponseTimeDays,
            Notes = response.Notes,
            ClaimDateUtc = response.ClaimDateUtc,
            SourceSystem = response.SourceSystem,
            SourceReference = response.SourceReference,
            SourceSyncedUtc = response.SourceSyncedUtc,
            IsActive = response.IsActive
        };

    public MedicalClaimInput ToInput() =>
        new(
            InsurancePublicId,
            AccountNumber,
            ClaimTypeCode,
            Diagnosis,
            ClaimAmount,
            CurrencyCode,
            PaidAmount,
            ResponseTimeDays,
            Notes,
            ClaimDateUtc,
            SourceSystem,
            SourceReference,
            SourceSyncedUtc);
}

