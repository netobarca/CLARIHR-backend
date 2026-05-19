using CLARIHR.Api.Controllers;
using CLARIHR.Application.Features.JobProfileCatalogTypes;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using Xunit;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Anti-drift guardrail for <see cref="JobProfileCatalogBindingMap"/> — the single
/// source of truth for the Job Profile catalog manifest and the registry seed.
/// Mirrors <see cref="PositionDescriptionCatalogRouteMapTests"/>: any change to the
/// canonical set must be a deliberate, reviewed edit, and the PositionDescription
/// slugs can never drift from <see cref="PositionDescriptionCatalogRouteMap"/>.
/// </summary>
public class JobProfileCatalogBindingMapGuardrailsTests
{
    [Fact]
    public void CanonicalTypes_ShouldBeTheExactExpectedSet()
    {
        (string Family, string RegistryCode, string Slug)[] expected =
        [
            (CatalogFamilies.PositionDescription, "PositionFunctionType", "position-function-types"),
            (CatalogFamilies.PositionDescription, "PositionContractType", "position-contract-types"),
            (CatalogFamilies.PositionDescription, "StrategicObjective", "strategic-objectives"),
            (CatalogFamilies.PositionDescription, "Frequency", "frequencies"),
            (CatalogFamilies.PositionDescription, "RequirementType", "requirement-types"),
            (CatalogFamilies.PositionDescription, "Requirement", "requirements"),
            (CatalogFamilies.PositionDescription, "GeneralFunction", "general-functions"),
            (CatalogFamilies.PositionDescription, "SalaryClass", "salary-classes"),
            (CatalogFamilies.PositionDescription, "WorkEquipment", "work-equipments"),
            (CatalogFamilies.PositionDescription, "Responsibility", "responsibilities-catalog"),
            (CatalogFamilies.PositionDescription, "Benefit", "benefits-catalog"),
            (CatalogFamilies.PositionDescription, "WorkConditionType", "work-condition-types"),
            (CatalogFamilies.PositionDescription, "WorkCondition", "work-conditions"),
            (CatalogFamilies.JobCatalog, "EducationLevel", "EducationLevel"),
            (CatalogFamilies.JobCatalog, "KnowledgeArea", "KnowledgeArea"),
            (CatalogFamilies.JobCatalog, "Competency", "Competency"),
            (CatalogFamilies.JobCatalog, "Training", "Training"),
            (CatalogFamilies.JobCatalog, "BenefitType", "BenefitType"),
            (CatalogFamilies.JobCatalog, "WorkingCondition", "WorkingCondition"),
            (CatalogFamilies.JobCatalog, "RelationType", "RelationType"),
            (CatalogFamilies.JobCatalog, "DecisionLevel", "DecisionLevel"),
            (CatalogFamilies.JobCatalog, "CompetencyType", "CompetencyType"),
            (CatalogFamilies.JobCatalog, "BehaviorLevel", "BehaviorLevel"),
            (CatalogFamilies.JobCatalog, "Behavior", "Behavior"),
            (CatalogFamilies.Internal, "RequirementsEducation", "job-profile.requirements.education"),
            (CatalogFamilies.Internal, "RequirementsKnowledge", "job-profile.requirements.knowledge"),
            (CatalogFamilies.Internal, "RequirementsCertification", "job-profile.requirements.certification"),
        ];

        var actual = JobProfileCatalogBindingMap.CanonicalTypes
            .Select(definition => (definition.Family, definition.RegistryCode, definition.Slug))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanonicalTypes_ShouldCoverEveryPositionDescriptionCatalogType_WithRouteMapSlug()
    {
        foreach (var type in Enum.GetValues<PositionDescriptionCatalogType>())
        {
            var match = JobProfileCatalogBindingMap.CanonicalTypes.SingleOrDefault(definition =>
                definition.Family == CatalogFamilies.PositionDescription &&
                definition.RegistryCode == type.ToString());

            Assert.True(match is not null, $"Missing PositionDescription canonical type for '{type}'.");

            Assert.True(
                PositionDescriptionCatalogRouteMap.TryResolve(match!.Slug, out var resolved),
                $"Slug '{match.Slug}' is not resolvable by the model binder.");
            Assert.Equal(type, resolved);
            Assert.Equal(PositionDescriptionCatalogRouteMap.ToSlug(type), match.Slug);
        }
    }

    [Fact]
    public void CanonicalTypes_ShouldCoverEveryJobCatalogCategory_WithEnumNameSlug()
    {
        foreach (var category in Enum.GetValues<JobCatalogCategory>())
        {
            var match = JobProfileCatalogBindingMap.CanonicalTypes.SingleOrDefault(definition =>
                definition.Family == CatalogFamilies.JobCatalog &&
                definition.RegistryCode == category.ToString());

            Assert.True(match is not null, $"Missing JobCatalog canonical type for '{category}'.");

            // The /job-catalogs/{category} route uses the default enum binder, so the
            // wire slug must be the exact enum member name.
            Assert.Equal(category.ToString(), match!.Slug);
        }
    }

    [Fact]
    public void RegistryCodes_ShouldBeUniqueAndNonEmpty()
    {
        Assert.All(JobProfileCatalogBindingMap.CanonicalTypes, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.RegistryCode));
            Assert.False(string.IsNullOrWhiteSpace(definition.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(definition.Slug));
        });

        var codes = JobProfileCatalogBindingMap.CanonicalTypes
            .Select(definition => definition.RegistryCode)
            .ToArray();
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void FieldBindings_ShouldReferenceKnownCanonicalCodesAndSubResources()
    {
        var canonicalCodes = JobProfileCatalogBindingMap.CanonicalTypes
            .Select(definition => definition.RegistryCode)
            .ToHashSet(StringComparer.Ordinal);
        var subResources = JobProfileCatalogBindingMap.SubResources.ToHashSet(StringComparer.Ordinal);

        Assert.All(JobProfileCatalogBindingMap.FieldBindings, binding =>
        {
            Assert.Contains(binding.RegistryCode, canonicalCodes);
            Assert.Contains(binding.SubResource, subResources);
            Assert.False(string.IsNullOrWhiteSpace(binding.FieldName));
        });
    }
}
