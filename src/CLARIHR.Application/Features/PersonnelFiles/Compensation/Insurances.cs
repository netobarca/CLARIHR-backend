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

public sealed record PersonnelFileInsuranceResponse(
    Guid InsurancePublicId,
    string InsuranceCode,
    decimal? EmployeeContribution,
    decimal? EmployerContribution,
    string? RangeCode,
    string? PolicyNumber,
    decimal? InsuredAmount,
    string? CurrencyCode,
    bool IsActive,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse> Beneficiaries,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => InsurancePublicId;
}

public sealed record InsuranceInput(
    string InsuranceCode,
    decimal? EmployeeContribution,
    decimal? EmployerContribution,
    string? RangeCode,
    string? PolicyNumber,
    decimal? InsuredAmount,
    string? CurrencyCode,
    bool IsActive,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc);

public sealed record AddPersonnelFileInsuranceCommand(
    Guid PersonnelFileId,
    InsuranceInput Item)
    : ICommand<PersonnelFileInsuranceResponse>;

public sealed record UpdatePersonnelFileInsuranceCommand(
    Guid PersonnelFileId,
    Guid InsurancePublicId,
    InsuranceInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileInsuranceResponse>;

public sealed record DeletePersonnelFileInsuranceCommand(
    Guid PersonnelFileId,
    Guid InsurancePublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileInsurancePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileInsuranceCommand(
    Guid PersonnelFileId,
    Guid InsurancePublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileInsurancePatchOperation> Operations)
    : ICommand<PersonnelFileInsuranceResponse>;

public sealed record GetPersonnelFileInsurancesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileInsuranceResponse>>;

public sealed record GetPersonnelFileInsuranceByIdQuery(Guid PersonnelFileId, Guid InsurancePublicId)
    : IQuery<PersonnelFileInsuranceResponse>;

// ─── Insurance Beneficiaries ──────────────────────────────────────────────────

internal sealed class InsuranceInputValidator : AbstractValidator<InsuranceInput>
{
    public InsuranceInputValidator()
    {
        RuleFor(input => input.InsuranceCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.RangeCode).MaximumLength(80);
        RuleFor(input => input.PolicyNumber).MaximumLength(120);
        RuleFor(input => input.CurrencyCode).MaximumLength(40);
        RuleFor(input => input.EmployeeContribution).GreaterThanOrEqualTo(0m).When(input => input.EmployeeContribution.HasValue);
        RuleFor(input => input.EmployerContribution).GreaterThanOrEqualTo(0m).When(input => input.EmployerContribution.HasValue);
        RuleFor(input => input.InsuredAmount).GreaterThanOrEqualTo(0m).When(input => input.InsuredAmount.HasValue);
        RuleFor(input => input.StartDateUtc)
            .LessThanOrEqualTo(input => input.EndDateUtc!.Value)
            .When(input => input.StartDateUtc.HasValue && input.EndDateUtc.HasValue);
    }
}

internal sealed class AddPersonnelFileInsuranceCommandValidator : AbstractValidator<AddPersonnelFileInsuranceCommand>
{
    public AddPersonnelFileInsuranceCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new InsuranceInputValidator());
    }
}

internal sealed class UpdatePersonnelFileInsuranceCommandValidator : AbstractValidator<UpdatePersonnelFileInsuranceCommand>
{
    public UpdatePersonnelFileInsuranceCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.InsurancePublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new InsuranceInputValidator());
    }
}

internal sealed class DeletePersonnelFileInsuranceCommandValidator : AbstractValidator<DeletePersonnelFileInsuranceCommand>
{
    public DeletePersonnelFileInsuranceCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.InsurancePublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileInsuranceCommandValidator : AbstractValidator<PatchPersonnelFileInsuranceCommand>
{
    public PatchPersonnelFileInsuranceCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.InsurancePublicId).NotEmpty();
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

internal sealed class GetPersonnelFileInsurancesQueryValidator : AbstractValidator<GetPersonnelFileInsurancesQuery>
{
    public GetPersonnelFileInsurancesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileInsuranceByIdQueryValidator : AbstractValidator<GetPersonnelFileInsuranceByIdQuery>
{
    public GetPersonnelFileInsuranceByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.InsurancePublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileInsurancePatchState
{
    public string InsuranceCode { get; set; } = string.Empty;
    public decimal? EmployeeContribution { get; set; }
    public decimal? EmployerContribution { get; set; }
    public string? RangeCode { get; set; }
    public string? PolicyNumber { get; set; }
    public decimal? InsuredAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileInsurancePatchState From(PersonnelFileInsuranceResponse response) =>
        new()
        {
            InsuranceCode = response.InsuranceCode,
            EmployeeContribution = response.EmployeeContribution,
            EmployerContribution = response.EmployerContribution,
            RangeCode = response.RangeCode,
            PolicyNumber = response.PolicyNumber,
            InsuredAmount = response.InsuredAmount,
            CurrencyCode = response.CurrencyCode,
            StartDateUtc = response.StartDateUtc,
            EndDateUtc = response.EndDateUtc,
            IsActive = response.IsActive
        };

    public InsuranceInput ToInput() =>
        new(
            InsuranceCode,
            EmployeeContribution,
            EmployerContribution,
            RangeCode,
            PolicyNumber,
            InsuredAmount,
            CurrencyCode,
            IsActive,
            StartDateUtc,
            EndDateUtc);
}

