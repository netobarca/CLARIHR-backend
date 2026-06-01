using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;
using FluentValidation.Results;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileBankAccountResponse(
    Guid BankAccountPublicId,
    Guid? BankPublicId,
    string BankCode,
    string? BankName,
    string? BankAlias,
    string? SwiftCode,
    string? RoutingCode,
    string CurrencyCode,
    string AccountNumber,
    string AccountTypeCode,
    bool IsPrimary,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => BankAccountPublicId;
}

public sealed record GetPersonnelFileBankAccountsQuery(Guid PersonnelFileId) : IQuery<IReadOnlyCollection<PersonnelFileBankAccountResponse>>;

public sealed record GetPersonnelFileBankAccountByIdQuery(Guid PersonnelFileId, Guid BankAccountPublicId)
    : IQuery<PersonnelFileBankAccountResponse>;

public sealed record AddPersonnelFileBankAccountCommand(
    Guid PersonnelFileId,
    BankAccountInput BankAccount)
    : ICommand<PersonnelFileBankAccountResponse>;

public sealed record UpdatePersonnelFileBankAccountCommand(
    Guid PersonnelFileId,
    Guid BankAccountPublicId,
    BankAccountInput BankAccount,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileBankAccountResponse>;

public sealed record DeletePersonnelFileBankAccountCommand(
    Guid PersonnelFileId,
    Guid BankAccountPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileBankAccountPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileBankAccountCommand(
    Guid PersonnelFileId,
    Guid BankAccountPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileBankAccountPatchOperation> Operations)
    : ICommand<PersonnelFileBankAccountResponse>;

public sealed record BankAccountInput(
    Guid BankPublicId,
    string CurrencyCode,
    string AccountNumber,
    string AccountTypeCode,
    bool IsPrimary = false);

internal sealed class GetPersonnelFileBankAccountByIdQueryValidator : AbstractValidator<GetPersonnelFileBankAccountByIdQuery>
{
    public GetPersonnelFileBankAccountByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.BankAccountPublicId).NotEmpty();
    }
}

internal sealed class AddPersonnelFileBankAccountCommandValidator : AbstractValidator<AddPersonnelFileBankAccountCommand>
{
    public AddPersonnelFileBankAccountCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.BankAccount).SetValidator(new BankAccountInputValidator());
    }
}

internal sealed class UpdatePersonnelFileBankAccountCommandValidator : AbstractValidator<UpdatePersonnelFileBankAccountCommand>
{
    public UpdatePersonnelFileBankAccountCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.BankAccountPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.BankAccount).SetValidator(new BankAccountInputValidator());
    }
}

internal sealed class DeletePersonnelFileBankAccountCommandValidator : AbstractValidator<DeletePersonnelFileBankAccountCommand>
{
    public DeletePersonnelFileBankAccountCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.BankAccountPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileBankAccountCommandValidator : AbstractValidator<PatchPersonnelFileBankAccountCommand>
{
    public PatchPersonnelFileBankAccountCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.BankAccountPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class BankAccountInputValidator : AbstractValidator<BankAccountInput>
{
    public BankAccountInputValidator()
    {
        RuleFor(input => input.BankPublicId).NotEmpty();
        RuleFor(input => input.CurrencyCode).NotEmpty().MaximumLength(40);
        RuleFor(input => input.AccountNumber).NotEmpty().MaximumLength(80);
        RuleFor(input => input.AccountTypeCode).NotEmpty().MaximumLength(80);
    }
}

internal sealed class PersonnelFileBankAccountPatchState
{
    public Guid BankPublicId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountTypeCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileBankAccountPatchState From(PersonnelFileBankAccountResponse response) =>
        new()
        {
            BankPublicId = response.BankPublicId ?? Guid.Empty,
            CurrencyCode = response.CurrencyCode,
            AccountNumber = response.AccountNumber,
            AccountTypeCode = response.AccountTypeCode,
            IsPrimary = response.IsPrimary
        };

    public BankAccountInput ToInput() =>
        new(
            BankPublicId,
            CurrencyCode,
            AccountNumber,
            AccountTypeCode,
            IsPrimary);
}

