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

public sealed record PersonnelFilePaymentMethodResponse(
    Guid PaymentMethodPublicId,
    string PaymentMethodCode,
    Guid? BankAccountId,
    bool IsPrimary,
    bool IsActive,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => PaymentMethodPublicId;
}

public sealed record PaymentMethodInput(
    string PaymentMethodCode,
    Guid? BankAccountPublicId,
    bool IsPrimary,
    bool IsActive,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes);

public sealed record AddPersonnelFilePaymentMethodCommand(
    Guid PersonnelFileId,
    PaymentMethodInput Item)
    : ICommand<PersonnelFilePaymentMethodResponse>;

public sealed record UpdatePersonnelFilePaymentMethodCommand(
    Guid PersonnelFileId,
    Guid PaymentMethodPublicId,
    PaymentMethodInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFilePaymentMethodResponse>;

public sealed record DeletePersonnelFilePaymentMethodCommand(
    Guid PersonnelFileId,
    Guid PaymentMethodPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFilePaymentMethodPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFilePaymentMethodCommand(
    Guid PersonnelFileId,
    Guid PaymentMethodPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFilePaymentMethodPatchOperation> Operations)
    : ICommand<PersonnelFilePaymentMethodResponse>;

public sealed record GetPersonnelFilePaymentMethodsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>;

public sealed record GetPersonnelFilePaymentMethodByIdQuery(Guid PersonnelFileId, Guid PaymentMethodPublicId)
    : IQuery<PersonnelFilePaymentMethodResponse>;

internal sealed class PaymentMethodInputValidator : AbstractValidator<PaymentMethodInput>
{
    public PaymentMethodInputValidator()
    {
        RuleFor(input => input.PaymentMethodCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.EffectiveFromUtc).LessThanOrEqualTo(input => input.EffectiveToUtc!.Value).When(input => input.EffectiveToUtc.HasValue);
    }
}

internal sealed class AddPersonnelFilePaymentMethodCommandValidator : AbstractValidator<AddPersonnelFilePaymentMethodCommand>
{
    public AddPersonnelFilePaymentMethodCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PaymentMethodInputValidator());
    }
}

internal sealed class UpdatePersonnelFilePaymentMethodCommandValidator : AbstractValidator<UpdatePersonnelFilePaymentMethodCommand>
{
    public UpdatePersonnelFilePaymentMethodCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.PaymentMethodPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new PaymentMethodInputValidator());
    }
}

internal sealed class DeletePersonnelFilePaymentMethodCommandValidator : AbstractValidator<DeletePersonnelFilePaymentMethodCommand>
{
    public DeletePersonnelFilePaymentMethodCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.PaymentMethodPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFilePaymentMethodCommandValidator : AbstractValidator<PatchPersonnelFilePaymentMethodCommand>
{
    public PatchPersonnelFilePaymentMethodCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.PaymentMethodPublicId).NotEmpty();
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

internal sealed class GetPersonnelFilePaymentMethodsQueryValidator : AbstractValidator<GetPersonnelFilePaymentMethodsQuery>
{
    public GetPersonnelFilePaymentMethodsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFilePaymentMethodByIdQueryValidator : AbstractValidator<GetPersonnelFilePaymentMethodByIdQuery>
{
    public GetPersonnelFilePaymentMethodByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.PaymentMethodPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFilePaymentMethodPatchState
{
    public string PaymentMethodCode { get; set; } = string.Empty;
    public Guid? BankAccountPublicId { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFilePaymentMethodPatchState From(PersonnelFilePaymentMethodResponse response) =>
        new()
        {
            PaymentMethodCode = response.PaymentMethodCode,
            BankAccountPublicId = response.BankAccountId,
            IsPrimary = response.IsPrimary,
            EffectiveFromUtc = response.EffectiveFromUtc,
            EffectiveToUtc = response.EffectiveToUtc,
            Notes = response.Notes,
            IsActive = response.IsActive
        };

    public PaymentMethodInput ToInput() =>
        new(
            PaymentMethodCode,
            BankAccountPublicId,
            IsPrimary,
            IsActive,
            EffectiveFromUtc,
            EffectiveToUtc,
            Notes);
}

