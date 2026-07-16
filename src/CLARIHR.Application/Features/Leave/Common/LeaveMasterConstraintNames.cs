namespace CLARIHR.Application.Features.Leave.Common;

/// <summary>
/// Single source of truth for the unique-constraint names of the leave/incapacity masters. The EF
/// configurations reference these constants and the handlers match them when translating a database
/// unique violation into a 409 (mirrors <c>CostCenterValidationRules.CodeUniqueConstraintName</c>).
/// </summary>
public static class LeaveMasterConstraintNames
{
    public const string MedicalClinicDescriptionUnique = "uq_medical_clinics__tenant_description";

    public const string IncapacityRiskCodeUnique = "uq_incapacity_risks__tenant_code";

    public const string IncapacityTypeCodeUnique = "uq_incapacity_types__tenant_code";

    public const string CompanyHolidayDateUnique = "uq_company_holidays__tenant_date";

    // Since REQ-012 M2 this index is PARTIAL (WHERE payroll_definition_id IS NULL): it keeps amparando the
    // legacy rows without a Nómina, while periods that hang from one are guarded by the new
    // per-definition unique below — two Nóminas of the same frequency may each own the same (year, number).
    public const string PayrollPeriodUnique = "uq_payroll_period_definitions__tenant_type_year_number";

    // Filtered unique (WHERE payroll_definition_id IS NOT NULL): one (year, number) per Nómina (REQ-012 §1.2).
    public const string PayrollPeriodDefinitionScopedUnique = "uq_payroll_period_definitions__tenant_definition_year_number";

    // Filtered unique (WHERE is_active): a compensatory-time-type code can be reused after inactivation.
    public const string CompensatoryTimeTypeCodeUnique = "uq_compensatory_time_types__tenant_code_active";
}
