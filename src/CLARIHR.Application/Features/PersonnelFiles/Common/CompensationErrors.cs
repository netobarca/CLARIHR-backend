using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Coded errors for the unified compensation-concepts feature (ingresos/egresos, salario por plaza).
/// Every code must have a matching entry in BackendMessages.resx and BackendMessages.es.resx
/// (parity is enforced by <c>BackendMessageLocalizationTests</c>).
/// </summary>
internal static class CompensationErrors
{
    public static readonly Error ConceptTypeCodeInvalid = new(
        "COMPENSATION_CONCEPT_TYPE_CODE_INVALID",
        "The compensation concept type code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error CalculationBaseRequired = new(
        "COMPENSATION_CONCEPT_CALCULATION_BASE_REQUIRED",
        "A calculation base is required for a percentage concept.",
        ErrorType.UnprocessableEntity);

    public static readonly Error CalculationBaseInvalid = new(
        "COMPENSATION_CONCEPT_CALCULATION_BASE_INVALID",
        "The calculation base code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error PercentageOutOfRange = new(
        "COMPENSATION_CONCEPT_PERCENTAGE_OUT_OF_RANGE",
        "The percentage must be between 0 and 100.",
        ErrorType.UnprocessableEntity);

    public static readonly Error CurrencyInvalid = new(
        "COMPENSATION_CONCEPT_CURRENCY_INVALID",
        "The currency code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error PayPeriodInvalid = new(
        "COMPENSATION_CONCEPT_PAY_PERIOD_INVALID",
        "The pay period code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error DeductionClassRequired = new(
        "COMPENSATION_CONCEPT_DEDUCTION_CLASS_REQUIRED",
        "A deduction class (LEY/INTERNO/EXTERNO) is required for a deduction concept.",
        ErrorType.UnprocessableEntity);

    public static readonly Error AssignedPositionNotFound = new(
        "COMPENSATION_CONCEPT_ASSIGNED_POSITION_NOT_FOUND",
        "The selected assigned position (plaza) could not be found for this employee.",
        ErrorType.UnprocessableEntity);

    public static readonly Error BaseSalaryAlreadyActive = new(
        "COMPENSATION_BASE_SALARY_ALREADY_ACTIVE",
        "The plaza already has an active base salary; close it before adding another.",
        ErrorType.UnprocessableEntity);

    public static readonly Error SalaryOutOfProfileRange = new(
        "COMPENSATION_SALARY_OUT_OF_PROFILE_RANGE",
        "The negotiated salary is outside the salary range configured for the plaza.",
        ErrorType.UnprocessableEntity);
}
