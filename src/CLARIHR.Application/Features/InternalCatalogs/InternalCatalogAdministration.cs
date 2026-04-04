using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.InternalCatalogs;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.InternalCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.InternalCatalogs;
using FluentValidation;

namespace CLARIHR.Application.Features.InternalCatalogs;

public sealed record InternalCatalogDefinitionResponse(
    string Context,
    string Identifier,
    string Label,
    InternalCatalogRenderType RenderType,
    string? CatalogKey,
    bool AllowCreate,
    int MinQueryLength);

public sealed record InternalCatalogValueSuggestionResponse(
    Guid Id,
    string Value,
    double Score);

public sealed record CreateInternalCatalogValueResponse(
    InternalCatalogCreateOutcome Outcome,
    InternalCatalogValueSuggestionResponse? CatalogValue,
    IReadOnlyCollection<InternalCatalogValueSuggestionResponse> Suggestions);

public sealed record GetInternalCatalogDefinitionsQuery(string Context)
    : IQuery<IReadOnlyCollection<InternalCatalogDefinitionResponse>>;

public sealed record SearchInternalCatalogValuesQuery(
    string CatalogKey,
    string? Search,
    int Limit = 10)
    : IQuery<IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>;

public sealed record CreateInternalCatalogValueCommand(
    string CatalogKey,
    string Value)
    : ICommand<CreateInternalCatalogValueResponse>;

internal sealed class GetInternalCatalogDefinitionsQueryValidator : AbstractValidator<GetInternalCatalogDefinitionsQuery>
{
    public GetInternalCatalogDefinitionsQueryValidator()
    {
        RuleFor(query => query.Context)
            .NotEmpty()
            .MaximumLength(120);
    }
}

internal sealed class SearchInternalCatalogValuesQueryValidator : AbstractValidator<SearchInternalCatalogValuesQuery>
{
    public SearchInternalCatalogValuesQueryValidator()
    {
        RuleFor(query => query.CatalogKey)
            .NotEmpty()
            .MaximumLength(120);
        RuleFor(query => query.Search)
            .MaximumLength(200);
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, 20);
    }
}

internal sealed class CreateInternalCatalogValueCommandValidator : AbstractValidator<CreateInternalCatalogValueCommand>
{
    public CreateInternalCatalogValueCommandValidator()
    {
        RuleFor(command => command.CatalogKey)
            .NotEmpty()
            .MaximumLength(120);
        RuleFor(command => command.Value)
            .NotEmpty()
            .MaximumLength(200);
    }
}

internal sealed class GetInternalCatalogDefinitionsQueryHandler(
    ICurrentUserService currentUserService)
    : IQueryHandler<GetInternalCatalogDefinitionsQuery, IReadOnlyCollection<InternalCatalogDefinitionResponse>>
{
    public Task<Result<IReadOnlyCollection<InternalCatalogDefinitionResponse>>> Handle(
        GetInternalCatalogDefinitionsQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return Task.FromResult(Result<IReadOnlyCollection<InternalCatalogDefinitionResponse>>.Failure(AuthorizationErrors.Unauthenticated));
        }

        var definitions = InternalCatalogRegistry.GetByContext(query.Context);
        if (definitions.Count == 0)
        {
            return Task.FromResult(Result<IReadOnlyCollection<InternalCatalogDefinitionResponse>>.Failure(InternalCatalogErrors.ContextNotFound));
        }

        return Task.FromResult(Result<IReadOnlyCollection<InternalCatalogDefinitionResponse>>.Success(
            definitions
                .Select(static definition => new InternalCatalogDefinitionResponse(
                    definition.Context,
                    definition.Identifier,
                    definition.Label,
                    definition.RenderType,
                    definition.CatalogKey,
                    definition.AllowCreate,
                    definition.MinQueryLength))
                .ToArray()));
    }
}

internal sealed class SearchInternalCatalogValuesQueryHandler(
    ICurrentUserService currentUserService,
    IInternalCatalogRepository repository)
    : IQueryHandler<SearchInternalCatalogValuesQuery, IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>
{
    private const double SearchSimilarityThreshold = 0.70d;

    public async Task<Result<IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>> Handle(
        SearchInternalCatalogValuesQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return Result<IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (!InternalCatalogRegistry.TryGetByCatalogKey(query.CatalogKey, out var definition))
        {
            return Result<IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>.Failure(InternalCatalogErrors.CatalogKeyNotFound);
        }

        if (definition.RenderType is not InternalCatalogRenderType.Search and not InternalCatalogRenderType.Select)
        {
            return Result<IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>.Failure(InternalCatalogErrors.CatalogKeyNotFound);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(query.Search)
            ? string.Empty
            : InternalCatalogValue.InternalCatalogNormalization.NormalizeValue(query.Search);
        if (normalizedSearch.Length < definition.MinQueryLength)
        {
            return Result<IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>.Success([]);
        }

        var matches = await repository.SearchAsync(
            definition.CatalogKey!,
            normalizedSearch,
            query.Limit,
            SearchSimilarityThreshold,
            cancellationToken);

        return Result<IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>.Success(
            matches.Select(static match => new InternalCatalogValueSuggestionResponse(match.Id, match.Value, match.Score)).ToArray());
    }
}

internal sealed class CreateInternalCatalogValueCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IInternalCatalogRepository repository,
    IPlatformAuditService platformAuditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateInternalCatalogValueCommand, CreateInternalCatalogValueResponse>
{
    public async Task<Result<CreateInternalCatalogValueResponse>> Handle(
        CreateInternalCatalogValueCommand command,
        CancellationToken cancellationToken)
    {
        if (!InternalCatalogRegistry.TryGetByCatalogKey(command.CatalogKey, out var definition))
        {
            return Result<CreateInternalCatalogValueResponse>.Failure(InternalCatalogErrors.CatalogKeyNotFound);
        }

        if (!definition.AllowCreate || definition.RenderType != InternalCatalogRenderType.Search)
        {
            return Result<CreateInternalCatalogValueResponse>.Failure(InternalCatalogErrors.CreateNotAllowed);
        }

        var actorResult = await InternalCatalogActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);
        if (actorResult.IsFailure)
        {
            return Result<CreateInternalCatalogValueResponse>.Failure(actorResult.Error);
        }

        var decision = await InternalCatalogValueResolver.ResolveForCreateAsync(
            definition.CatalogKey!,
            command.Value,
            actorResult.Value.PublicId,
            repository,
            cancellationToken);
        if (decision.IsFailure)
        {
            return Result<CreateInternalCatalogValueResponse>.Failure(decision.Error);
        }

        if (decision.Value.Outcome == InternalCatalogCreateOutcome.ReusedExact)
        {
            return Result<CreateInternalCatalogValueResponse>.Success(
                new CreateInternalCatalogValueResponse(
                    decision.Value.Outcome,
                    InternalCatalogResponseMapper.MapSuggestion(decision.Value.ExistingValue!, scoreOverride: 1d),
                    []));
        }

        if (decision.Value.Outcome == InternalCatalogCreateOutcome.RejectedSimilar)
        {
            return Result<CreateInternalCatalogValueResponse>.Success(
                new CreateInternalCatalogValueResponse(
                    decision.Value.Outcome,
                    CatalogValue: null,
                    decision.Value.Suggestions.Select(InternalCatalogResponseMapper.MapSuggestion).ToArray()));
        }

        var createdValue = decision.Value.CreatedValue
            ?? throw new InvalidOperationException("Internal catalog value creation did not produce an entity.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(createdValue);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = InternalCatalogResponseMapper.MapSuggestion(createdValue, scoreOverride: 1d);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.InternalCatalogValueCreated,
                    AuditEntityTypes.InternalCatalogValue,
                    createdValue.PublicId,
                    createdValue.CatalogKey,
                    AuditActions.Create,
                    $"Created internal catalog value '{createdValue.Value}' in '{createdValue.CatalogKey}'.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result<CreateInternalCatalogValueResponse>.Success(
                new CreateInternalCatalogValueResponse(
                    InternalCatalogCreateOutcome.Created,
                    response,
                    []));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class InternalCatalogValueResolver
{
    private const double DuplicateSimilarityThreshold = 0.90d;
    private const int DuplicateSuggestionLimit = 5;

    public static async Task<Result<InternalCatalogCreateDecision>> ResolveForCreateAsync(
        string catalogKey,
        string value,
        Guid actorUserPublicId,
        IInternalCatalogRepository repository,
        CancellationToken cancellationToken)
    {
        var normalizedValue = InternalCatalogValue.InternalCatalogNormalization.NormalizeValue(value);

        var exact = await repository.FindActiveByExactValueAsync(catalogKey, normalizedValue, cancellationToken);
        if (exact is not null)
        {
            return Result<InternalCatalogCreateDecision>.Success(
                new InternalCatalogCreateDecision(
                    InternalCatalogCreateOutcome.ReusedExact,
                    CreatedValue: null,
                    ExistingValue: exact,
                    Suggestions: []));
        }

        var similarMatches = await repository.FindSimilarAsync(
            catalogKey,
            normalizedValue,
            DuplicateSuggestionLimit,
            DuplicateSimilarityThreshold,
            cancellationToken);
        if (similarMatches.Count > 0)
        {
            return Result<InternalCatalogCreateDecision>.Success(
                new InternalCatalogCreateDecision(
                    InternalCatalogCreateOutcome.RejectedSimilar,
                    CreatedValue: null,
                    ExistingValue: null,
                    similarMatches.Select(static match => new InternalCatalogSuggestion(match.Id, match.Value, match.Score)).ToArray()));
        }

        return Result<InternalCatalogCreateDecision>.Success(
            new InternalCatalogCreateDecision(
                InternalCatalogCreateOutcome.Created,
                InternalCatalogValue.Create(catalogKey, value, actorUserPublicId),
                ExistingValue: null,
                Suggestions: []));
    }

    public static async Task<Result<InternalCatalogUsageResolution>> ResolveForUsageAsync(
        string catalogKey,
        string value,
        Guid actorUserPublicId,
        IInternalCatalogRepository repository,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
    {
        var normalizedValue = InternalCatalogValue.InternalCatalogNormalization.NormalizeValue(value);

        var exact = await repository.FindActiveByExactValueAsync(catalogKey, normalizedValue, cancellationToken);
        if (exact is not null)
        {
            exact.RegisterUsage(dateTimeProvider.UtcNow);
            return Result<InternalCatalogUsageResolution>.Success(
                new InternalCatalogUsageResolution(
                    InternalCatalogUsageOutcome.ReusedExact,
                    exact.Value,
                    CreatedValue: null,
                    ExistingValue: exact));
        }

        var similarMatches = await repository.FindSimilarAsync(
            catalogKey,
            normalizedValue,
            limit: 1,
            DuplicateSimilarityThreshold,
            cancellationToken);
        if (similarMatches.Count > 0)
        {
            var similar = await repository.GetByIdAsync(similarMatches.First().Id, cancellationToken)
                ?? throw new InvalidOperationException("Internal catalog match could not be resolved for usage.");

            similar.RegisterUsage(dateTimeProvider.UtcNow);
            return Result<InternalCatalogUsageResolution>.Success(
                new InternalCatalogUsageResolution(
                    InternalCatalogUsageOutcome.ReusedSimilar,
                    similar.Value,
                    CreatedValue: null,
                    ExistingValue: similar));
        }

        var created = InternalCatalogValue.Create(catalogKey, value, actorUserPublicId);
        created.RegisterUsage(dateTimeProvider.UtcNow);

        return Result<InternalCatalogUsageResolution>.Success(
            new InternalCatalogUsageResolution(
                InternalCatalogUsageOutcome.Created,
                created.Value,
                created,
                ExistingValue: null));
    }
}

internal static class InternalCatalogResponseMapper
{
    public static InternalCatalogValueSuggestionResponse MapSuggestion(
        InternalCatalogValue value,
        double scoreOverride) =>
        new(value.PublicId, value.Value, scoreOverride);

    public static InternalCatalogValueSuggestionResponse MapSuggestion(InternalCatalogSuggestion value) =>
        new(value.Id, value.Value, value.Score);
}

internal static class InternalCatalogActorResolver
{
    public static async Task<Result<User>> ResolveCurrentUserAsync(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUserService.UserId, out var currentUserPublicId))
        {
            return Result<User>.Failure(InternalCatalogErrors.InvalidCurrentUser);
        }

        var user = await userRepository.GetByPublicIdAsync(currentUserPublicId, cancellationToken);
        return user is null
            ? Result<User>.Failure(InternalCatalogErrors.UserNotFound)
            : Result<User>.Success(user);
    }
}
