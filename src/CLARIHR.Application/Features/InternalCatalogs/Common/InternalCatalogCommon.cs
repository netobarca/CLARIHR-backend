using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.InternalCatalogs;

namespace CLARIHR.Application.Features.InternalCatalogs.Common;

public enum InternalCatalogRenderType
{
    Search = 1,
    Select = 2,
    FreeText = 3
}

public enum InternalCatalogCreateOutcome
{
    Created = 1,
    ReusedExact = 2,
    RejectedSimilar = 3
}

internal enum InternalCatalogUsageOutcome
{
    Created = 1,
    ReusedExact = 2,
    ReusedSimilar = 3
}

internal sealed record InternalCatalogDefinition(
    string Context,
    string Identifier,
    string Label,
    InternalCatalogRenderType RenderType,
    string? CatalogKey,
    bool AllowCreate,
    int MinQueryLength);

internal sealed record InternalCatalogSuggestion(
    Guid Id,
    string Value,
    double Score);

internal sealed record InternalCatalogCreateDecision(
    InternalCatalogCreateOutcome Outcome,
    InternalCatalogValue? CreatedValue,
    InternalCatalogValue? ExistingValue,
    IReadOnlyCollection<InternalCatalogSuggestion> Suggestions);

internal sealed record InternalCatalogUsageResolution(
    InternalCatalogUsageOutcome Outcome,
    string ResolvedValue,
    InternalCatalogValue? CreatedValue,
    InternalCatalogValue? ExistingValue);

internal static class InternalCatalogRegistry
{
    public const string JobProfileRequirementsContext = "job-profile.requirements";
    public const string JobProfileRequirementsEducationCatalogKey = "job-profile.requirements.education";
    public const string JobProfileRequirementsKnowledgeCatalogKey = "job-profile.requirements.knowledge";
    public const string JobProfileRequirementsCertificationCatalogKey = "job-profile.requirements.certification";

    private static readonly IReadOnlyCollection<InternalCatalogDefinition> Definitions =
    [
        new(
            JobProfileRequirementsContext,
            JobRequirementType.Education.ToString(),
            "Education",
            InternalCatalogRenderType.Search,
            JobProfileRequirementsEducationCatalogKey,
            AllowCreate: true,
            MinQueryLength: 2),
        new(
            JobProfileRequirementsContext,
            JobRequirementType.Experience.ToString(),
            "Experience",
            InternalCatalogRenderType.FreeText,
            CatalogKey: null,
            AllowCreate: false,
            MinQueryLength: 0),
        new(
            JobProfileRequirementsContext,
            JobRequirementType.Knowledge.ToString(),
            "Knowledge",
            InternalCatalogRenderType.Search,
            JobProfileRequirementsKnowledgeCatalogKey,
            AllowCreate: true,
            MinQueryLength: 2),
        new(
            JobProfileRequirementsContext,
            JobRequirementType.Certification.ToString(),
            "Certification",
            InternalCatalogRenderType.Search,
            JobProfileRequirementsCertificationCatalogKey,
            AllowCreate: true,
            MinQueryLength: 2),
        new(
            JobProfileRequirementsContext,
            JobRequirementType.Other.ToString(),
            "Other",
            InternalCatalogRenderType.FreeText,
            CatalogKey: null,
            AllowCreate: false,
            MinQueryLength: 0)
    ];

    public static IReadOnlyCollection<InternalCatalogDefinition> GetByContext(string context)
    {
        var normalizedContext = NormalizeKey(context);
        return Definitions
            .Where(definition => definition.Context.Equals(normalizedContext, StringComparison.Ordinal))
            .ToArray();
    }

    public static bool TryGetByCatalogKey(string catalogKey, out InternalCatalogDefinition definition)
    {
        var normalizedCatalogKey = NormalizeKey(catalogKey);
        definition = Definitions.SingleOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(candidate.CatalogKey) &&
            candidate.CatalogKey.Equals(normalizedCatalogKey, StringComparison.Ordinal))!;
        return definition is not null;
    }

    public static bool TryGetRequirementDefinition(JobRequirementType requirementType, out InternalCatalogDefinition definition)
    {
        definition = Definitions.SingleOrDefault(candidate =>
            candidate.Context == JobProfileRequirementsContext &&
            candidate.Identifier.Equals(requirementType.ToString(), StringComparison.Ordinal))!;
        return definition is not null;
    }

    private static string NormalizeKey(string value) =>
        InternalCatalogValue.InternalCatalogNormalization.NormalizeCatalogKey(value);
}

public static class InternalCatalogErrors
{
    public static readonly Error ContextNotFound = new(
        "INTERNAL_CATALOG_CONTEXT_NOT_FOUND",
        "The requested internal catalog context is not supported.",
        ErrorType.NotFound);

    public static readonly Error CatalogKeyNotFound = new(
        "INTERNAL_CATALOG_KEY_NOT_FOUND",
        "The requested internal catalog is not supported.",
        ErrorType.NotFound);

    public static readonly Error CreateNotAllowed = new(
        "INTERNAL_CATALOG_CREATE_NOT_ALLOWED",
        "The requested internal catalog does not allow value creation.",
        ErrorType.Validation);

    public static readonly Error InvalidCurrentUser = new(
        "INTERNAL_CATALOG_CURRENT_USER_INVALID",
        "The current user context is invalid.",
        ErrorType.Unauthorized);

    public static readonly Error UserNotFound = new(
        "INTERNAL_CATALOG_CURRENT_USER_NOT_FOUND",
        "The current user could not be resolved.",
        ErrorType.NotFound);

    public static readonly Error SimilarValueConflict = new(
        "INTERNAL_CATALOG_SIMILAR_VALUE_CONFLICT",
        "The value is too similar to an existing catalog value.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch("INTERNAL_CATALOGS", action);
}
