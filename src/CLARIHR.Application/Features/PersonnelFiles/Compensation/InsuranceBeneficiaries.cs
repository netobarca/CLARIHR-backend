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

public sealed record PersonnelFileInsuranceBeneficiaryResponse(
    Guid BeneficiaryPublicId,
    string FullName,
    string? DocumentNumber,
    DateTime? BirthDate,
    string KinshipCode,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => BeneficiaryPublicId;
}

public sealed record InsuranceBeneficiaryInput(
    string FullName,
    string? DocumentNumber,
    DateTime? BirthDate,
    string KinshipCode);

public sealed record AddPersonnelFileInsuranceBeneficiaryCommand(
    Guid PersonnelFileId,
    Guid InsurancePublicId,
    InsuranceBeneficiaryInput Item)
    : ICommand<PersonnelFileInsuranceBeneficiaryResponse>;

public sealed record UpdatePersonnelFileInsuranceBeneficiaryCommand(
    Guid PersonnelFileId,
    Guid InsurancePublicId,
    Guid BeneficiaryPublicId,
    InsuranceBeneficiaryInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileInsuranceBeneficiaryResponse>;

public sealed record DeletePersonnelFileInsuranceBeneficiaryCommand(
    Guid PersonnelFileId,
    Guid InsurancePublicId,
    Guid BeneficiaryPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileInsuranceBeneficiaryPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileInsuranceBeneficiaryCommand(
    Guid PersonnelFileId,
    Guid InsurancePublicId,
    Guid BeneficiaryPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryPatchOperation> Operations)
    : ICommand<PersonnelFileInsuranceBeneficiaryResponse>;

public sealed record GetPersonnelFileInsuranceBeneficiariesQuery(Guid PersonnelFileId, Guid InsurancePublicId)
    : IQuery<IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse>>;

public sealed record GetPersonnelFileInsuranceBeneficiaryByIdQuery(
    Guid PersonnelFileId,
    Guid InsurancePublicId,
    Guid BeneficiaryPublicId)
    : IQuery<PersonnelFileInsuranceBeneficiaryResponse>;

internal sealed class InsuranceBeneficiaryInputValidator : AbstractValidator<InsuranceBeneficiaryInput>
{
    public InsuranceBeneficiaryInputValidator()
    {
        RuleFor(input => input.FullName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.KinshipCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class AddPersonnelFileInsuranceBeneficiaryCommandValidator : AbstractValidator<AddPersonnelFileInsuranceBeneficiaryCommand>
{
    public AddPersonnelFileInsuranceBeneficiaryCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.InsurancePublicId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new InsuranceBeneficiaryInputValidator());
    }
}

internal sealed class UpdatePersonnelFileInsuranceBeneficiaryCommandValidator : AbstractValidator<UpdatePersonnelFileInsuranceBeneficiaryCommand>
{
    public UpdatePersonnelFileInsuranceBeneficiaryCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.InsurancePublicId).NotEmpty();
        RuleFor(c => c.BeneficiaryPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new InsuranceBeneficiaryInputValidator());
    }
}

internal sealed class DeletePersonnelFileInsuranceBeneficiaryCommandValidator : AbstractValidator<DeletePersonnelFileInsuranceBeneficiaryCommand>
{
    public DeletePersonnelFileInsuranceBeneficiaryCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.InsurancePublicId).NotEmpty();
        RuleFor(c => c.BeneficiaryPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileInsuranceBeneficiaryCommandValidator : AbstractValidator<PatchPersonnelFileInsuranceBeneficiaryCommand>
{
    public PatchPersonnelFileInsuranceBeneficiaryCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.InsurancePublicId).NotEmpty();
        RuleFor(c => c.BeneficiaryPublicId).NotEmpty();
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

internal sealed class GetPersonnelFileInsuranceBeneficiariesQueryValidator : AbstractValidator<GetPersonnelFileInsuranceBeneficiariesQuery>
{
    public GetPersonnelFileInsuranceBeneficiariesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.InsurancePublicId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileInsuranceBeneficiaryByIdQueryValidator : AbstractValidator<GetPersonnelFileInsuranceBeneficiaryByIdQuery>
{
    public GetPersonnelFileInsuranceBeneficiaryByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.InsurancePublicId).NotEmpty();
        RuleFor(query => query.BeneficiaryPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileInsuranceBeneficiaryPatchState
{
    public string FullName { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public DateTime? BirthDate { get; set; }
    public string KinshipCode { get; set; } = string.Empty;
    public bool KinshipCodeMutated { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileInsuranceBeneficiaryPatchState From(PersonnelFileInsuranceBeneficiaryResponse response) =>
        new()
        {
            FullName = response.FullName,
            DocumentNumber = response.DocumentNumber,
            BirthDate = response.BirthDate,
            KinshipCode = response.KinshipCode,
            IsActive = response.IsActive
        };

    public InsuranceBeneficiaryInput ToInput() =>
        new(
            FullName,
            DocumentNumber,
            BirthDate,
            KinshipCode);
}

