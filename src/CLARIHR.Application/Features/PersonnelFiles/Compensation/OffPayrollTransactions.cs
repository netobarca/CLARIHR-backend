using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record PersonnelFileOffPayrollTransactionResponse(
    Guid OffPayrollTransactionPublicId,
    string OffPayrollTransactionTypeCode,
    string? TransactionTypeName,
    DateTime TransactionDateUtc,
    string CurrencyCode,
    decimal Amount,
    int Year,
    int Month,
    string? Comment,
    Guid? AssetAccessPublicId,
    string? AssetName,
    Guid? CorrectsTransactionPublicId,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => OffPayrollTransactionPublicId;
}

/// <summary>
/// Per-currency subtotal of an employee's off-payroll transactions (D-13). Negative adjustments subtract; there
/// is no FX conversion (D-08), so each currency is reported independently.
/// </summary>
public sealed record OffPayrollTransactionCurrencyTotalResponse(
    string CurrencyCode,
    decimal Total,
    int Count);

public sealed record OffPayrollTransactionInput(
    string TransactionTypeCode,
    DateTime TransactionDateUtc,
    string? CurrencyCode,
    decimal Amount,
    int Year,
    int Month,
    string? Comment,
    Guid? AssetAccessPublicId,
    Guid? CorrectsTransactionPublicId);

public sealed record AddPersonnelFileOffPayrollTransactionCommand(
    Guid PersonnelFileId,
    OffPayrollTransactionInput Item)
    : ICommand<PersonnelFileOffPayrollTransactionResponse>;

public sealed record UpdatePersonnelFileOffPayrollTransactionCommand(
    Guid PersonnelFileId,
    Guid OffPayrollTransactionPublicId,
    OffPayrollTransactionInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileOffPayrollTransactionResponse>;

public sealed record DeletePersonnelFileOffPayrollTransactionCommand(
    Guid PersonnelFileId,
    Guid OffPayrollTransactionPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileOffPayrollTransactionPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileOffPayrollTransactionCommand(
    Guid PersonnelFileId,
    Guid OffPayrollTransactionPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileOffPayrollTransactionPatchOperation> Operations)
    : ICommand<PersonnelFileOffPayrollTransactionResponse>;

public sealed record GetPersonnelFileOffPayrollTransactionsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>>;

public sealed record GetPersonnelFileOffPayrollTransactionByIdQuery(Guid PersonnelFileId, Guid OffPayrollTransactionPublicId)
    : IQuery<PersonnelFileOffPayrollTransactionResponse>;

public sealed record GetPersonnelFileOffPayrollTransactionTotalsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>>;

internal sealed class OffPayrollTransactionInputValidator : AbstractValidator<OffPayrollTransactionInput>
{
    public OffPayrollTransactionInputValidator()
    {
        // Type is mandatory; existence/active is verified against the catalog in the handler (422).
        RuleFor(input => input.TransactionTypeCode).NotEmpty().MaximumLength(80);

        // Amount must be non-zero; negatives are allowed (adjustments). The negative ⇒ correction-reference
        // requirement is enforced both here (400, pure) and in the handler (422, defense in depth) — D-04/D-12.
        RuleFor(input => input.Amount)
            .Must(amount => amount != 0)
            .WithMessage("Amount must be a non-zero value.");

        // Imputation period (D-05): month 1..12, year in a sane range.
        RuleFor(input => input.Month).InclusiveBetween(1, 12);
        RuleFor(input => input.Year).InclusiveBetween(2000, 2100);

        // Currency ISO-4217 (3 chars) — house convention validates length; default resolved in the handler (D-08).
        RuleFor(input => input.CurrencyCode)
            .Length(3)
            .When(input => !string.IsNullOrWhiteSpace(input.CurrencyCode));

        // Transaction date required and not in the future (RN-05).
        RuleFor(input => input.TransactionDateUtc)
            .NotEmpty()
            .Must(date => date <= DateTime.UtcNow.AddDays(1))
            .WithMessage("TransactionDateUtc must not be in the future.");

        RuleFor(input => input.Comment).MaximumLength(2000);

        RuleFor(input => input.CorrectsTransactionPublicId)
            .NotNull()
            .When(input => input.Amount < 0)
            .WithMessage("A negative amount must reference the original transaction it corrects.");
    }
}

internal sealed class AddPersonnelFileOffPayrollTransactionCommandValidator : AbstractValidator<AddPersonnelFileOffPayrollTransactionCommand>
{
    public AddPersonnelFileOffPayrollTransactionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new OffPayrollTransactionInputValidator());
    }
}

internal sealed class UpdatePersonnelFileOffPayrollTransactionCommandValidator : AbstractValidator<UpdatePersonnelFileOffPayrollTransactionCommand>
{
    public UpdatePersonnelFileOffPayrollTransactionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.OffPayrollTransactionPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new OffPayrollTransactionInputValidator());
    }
}

internal sealed class DeletePersonnelFileOffPayrollTransactionCommandValidator : AbstractValidator<DeletePersonnelFileOffPayrollTransactionCommand>
{
    public DeletePersonnelFileOffPayrollTransactionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.OffPayrollTransactionPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileOffPayrollTransactionCommandValidator : AbstractValidator<PatchPersonnelFileOffPayrollTransactionCommand>
{
    public PatchPersonnelFileOffPayrollTransactionCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.OffPayrollTransactionPublicId).NotEmpty();
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

internal sealed class GetPersonnelFileOffPayrollTransactionsQueryValidator : AbstractValidator<GetPersonnelFileOffPayrollTransactionsQuery>
{
    public GetPersonnelFileOffPayrollTransactionsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileOffPayrollTransactionByIdQueryValidator : AbstractValidator<GetPersonnelFileOffPayrollTransactionByIdQuery>
{
    public GetPersonnelFileOffPayrollTransactionByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.OffPayrollTransactionPublicId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileOffPayrollTransactionTotalsQueryValidator : AbstractValidator<GetPersonnelFileOffPayrollTransactionTotalsQuery>
{
    public GetPersonnelFileOffPayrollTransactionTotalsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class PersonnelFileOffPayrollTransactionPatchState
{
    public string TransactionTypeCode { get; set; } = string.Empty;
    public DateTime TransactionDateUtc { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal Amount { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string? Comment { get; set; }
    public Guid? AssetAccessPublicId { get; set; }
    public Guid? CorrectsTransactionPublicId { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileOffPayrollTransactionPatchState From(PersonnelFileOffPayrollTransactionResponse response) =>
        new()
        {
            TransactionTypeCode = response.OffPayrollTransactionTypeCode,
            TransactionDateUtc = response.TransactionDateUtc,
            CurrencyCode = response.CurrencyCode,
            Amount = response.Amount,
            Year = response.Year,
            Month = response.Month,
            Comment = response.Comment,
            AssetAccessPublicId = response.AssetAccessPublicId,
            CorrectsTransactionPublicId = response.CorrectsTransactionPublicId,
            IsActive = response.IsActive
        };

    public OffPayrollTransactionInput ToInput() =>
        new(
            TransactionTypeCode,
            TransactionDateUtc,
            CurrencyCode,
            Amount,
            Year,
            Month,
            Comment,
            AssetAccessPublicId,
            CorrectsTransactionPublicId);
}
