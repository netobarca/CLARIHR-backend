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

public sealed record PersonnelFileAssetAccessResponse(
    Guid AssetAccessPublicId,
    string AssetTypeCode,
    string AssetOrAccessName,
    string? AccessLevelCode,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    DateTime? DeliveryDateUtc,
    string? DeliveryStatusCode,
    bool IsActive,
    string? Notes,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => AssetAccessPublicId;
}

public sealed record AssetAccessInput(
    string AssetTypeCode,
    string AssetOrAccessName,
    string? AccessLevelCode,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    DateTime? DeliveryDateUtc,
    string? DeliveryStatusCode,
    bool IsActive,
    string? Notes);

public sealed record AddPersonnelFileAssetAccessCommand(
    Guid PersonnelFileId,
    AssetAccessInput Item)
    : ICommand<PersonnelFileAssetAccessResponse>;

public sealed record UpdatePersonnelFileAssetAccessCommand(
    Guid PersonnelFileId,
    Guid AssetAccessPublicId,
    AssetAccessInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileAssetAccessResponse>;

public sealed record DeletePersonnelFileAssetAccessCommand(
    Guid PersonnelFileId,
    Guid AssetAccessPublicId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileParentConcurrencyResult>;

public sealed record PersonnelFileAssetAccessPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchPersonnelFileAssetAccessCommand(
    Guid PersonnelFileId,
    Guid AssetAccessPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<PersonnelFileAssetAccessPatchOperation> Operations)
    : ICommand<PersonnelFileAssetAccessResponse>;

public sealed record GetPersonnelFileAssetsAccessesQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>;

public sealed record GetPersonnelFileAssetAccessByIdQuery(Guid PersonnelFileId, Guid AssetAccessPublicId)
    : IQuery<PersonnelFileAssetAccessResponse>;

internal sealed class AssetAccessInputValidator : AbstractValidator<AssetAccessInput>
{
    public AssetAccessInputValidator()
    {
        RuleFor(input => input.AssetTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(input => input.AssetOrAccessName).NotEmpty().MaximumLength(200);
    }
}

internal sealed class AddPersonnelFileAssetAccessCommandValidator : AbstractValidator<AddPersonnelFileAssetAccessCommand>
{
    public AddPersonnelFileAssetAccessCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AssetAccessInputValidator());
    }
}

internal sealed class UpdatePersonnelFileAssetAccessCommandValidator : AbstractValidator<UpdatePersonnelFileAssetAccessCommand>
{
    public UpdatePersonnelFileAssetAccessCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.AssetAccessPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Item).NotNull().SetValidator(new AssetAccessInputValidator());
    }
}

internal sealed class DeletePersonnelFileAssetAccessCommandValidator : AbstractValidator<DeletePersonnelFileAssetAccessCommand>
{
    public DeletePersonnelFileAssetAccessCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.AssetAccessPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchPersonnelFileAssetAccessCommandValidator : AbstractValidator<PatchPersonnelFileAssetAccessCommand>
{
    public PatchPersonnelFileAssetAccessCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.AssetAccessPublicId).NotEmpty();
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

internal sealed class GetPersonnelFileAssetsAccessesQueryValidator : AbstractValidator<GetPersonnelFileAssetsAccessesQuery>
{
    public GetPersonnelFileAssetsAccessesQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetPersonnelFileAssetAccessByIdQueryValidator : AbstractValidator<GetPersonnelFileAssetAccessByIdQuery>
{
    public GetPersonnelFileAssetAccessByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.AssetAccessPublicId).NotEmpty();
    }
}

internal sealed class PersonnelFileAssetAccessPatchState
{
    public string AssetTypeCode { get; set; } = string.Empty;
    public string AssetOrAccessName { get; set; } = string.Empty;
    public string? AccessLevelCode { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public DateTime? DeliveryDateUtc { get; set; }
    public string? DeliveryStatusCode { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public bool IsActiveMutated { get; set; }
    public bool HasMutation { get; set; }

    public static PersonnelFileAssetAccessPatchState From(PersonnelFileAssetAccessResponse response) =>
        new()
        {
            AssetTypeCode = response.AssetTypeCode,
            AssetOrAccessName = response.AssetOrAccessName,
            AccessLevelCode = response.AccessLevelCode,
            StartDateUtc = response.StartDateUtc,
            EndDateUtc = response.EndDateUtc,
            DeliveryDateUtc = response.DeliveryDateUtc,
            DeliveryStatusCode = response.DeliveryStatusCode,
            Notes = response.Notes,
            IsActive = response.IsActive
        };

    public AssetAccessInput ToInput() =>
        new(
            AssetTypeCode,
            AssetOrAccessName,
            AccessLevelCode,
            StartDateUtc,
            EndDateUtc,
            DeliveryDateUtc,
            DeliveryStatusCode,
            IsActive,
            Notes);
}

