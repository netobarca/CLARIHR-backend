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

    public const string PayrollPeriodUnique = "uq_payroll_period_definitions__tenant_type_year_number";
}
