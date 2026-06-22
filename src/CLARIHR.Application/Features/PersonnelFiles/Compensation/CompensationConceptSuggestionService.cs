using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Auto-suggests the statutory (de-ley) deductions — ISSS/AFP — when a plaza (employment assignment) is
/// created (D-20). For each active statutory egreso type that carries a default employee rate, it adds an
/// EGRESO concept flagged <c>IsSystemSuggested=true</c>, scoped to the new plaza, pre-filled from the
/// catalog defaults (deduction class, calculation type/base, employee/employer rate, cap). Editable and
/// removable by the user. Idempotent: skips a type that already has an active concept on the plaza. The
/// concepts are registered on the unit of work; the caller's transaction persists them in the same commit.
/// </summary>
internal static class CompensationConceptSuggestionService
{
    private const string DefaultPayPeriodCode = "MENSUAL";
    private const string DefaultCurrencyCode = "USD";

    public static async Task SuggestStatutoryForAssignmentAsync(
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        PersonnelFile personnelFile,
        Guid assignedPositionPublicId,
        DateTime startDate,
        CancellationToken cancellationToken)
    {
        var countryCode = await personnelFileRepository.GetCompanyCountryCodeAsync(personnelFile.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return;
        }

        var statutoryTypes = (await personnelFileRepository.GetCompensationConceptTypesAsync(
                countryCode, CompensationNature.Egreso, cancellationToken))
            .Where(type => type is { IsActive: true, IsStatutory: true, DefaultEmployeeRate: not null })
            .ToArray();
        if (statutoryTypes.Length == 0)
        {
            return;
        }

        var existing = await employeeRepository.GetCompensationConceptsAsync(personnelFile.PublicId, cancellationToken);

        foreach (var type in statutoryTypes)
        {
            var alreadyPresent = existing.Any(concept =>
                concept.IsActive
                && concept.AssignedPositionPublicId == assignedPositionPublicId
                && string.Equals(concept.ConceptTypeCode, type.Code, StringComparison.OrdinalIgnoreCase));
            if (alreadyPresent)
            {
                continue;
            }

            var concept = PersonnelFileCompensationConcept.Create(
                assignedPositionPublicId,
                CompensationNature.Egreso,
                type.Code,
                type.DefaultDeductionClass ?? DeductionClass.Ley,
                type.DefaultCalculationType,
                type.DefaultEmployeeRate ?? 0m,
                type.DefaultCalculationBaseCode,
                type.DefaultEmployerRate,
                type.ContributionCap,
                DefaultCurrencyCode,
                DefaultPayPeriodCode,
                counterpartyName: null,
                externalReference: null,
                startDate,
                endDate: null,
                isActive: true,
                isSystemSuggested: true,
                notes: null);
            concept.BindToPersonnelFile(personnelFile.Id);
            concept.SetTenantId(personnelFile.TenantId);
            _ = await employeeRepository.AddCompensationConceptAsync(personnelFile.Id, personnelFile.TenantId, concept, cancellationToken);
        }
    }
}
