namespace CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;

/// <summary>
/// Single source of truth for the unique-constraint names of the overtime configuration masters. The EF
/// configurations reference these constants and the handlers match them when translating a database unique
/// violation into a 409 (mirrors <c>EmployeeRelationsMasterConstraintNames</c>). Both are filtered unique
/// (WHERE is_active): a code can be reused after inactivation.
/// </summary>
public static class OvertimeMasterConstraintNames
{
    public const string OvertimeTypeCodeUnique = "uq_overtime_types__tenant_code_active";

    public const string OvertimeJustificationTypeCodeUnique = "uq_overtime_justification_types__tenant_code_active";
}
