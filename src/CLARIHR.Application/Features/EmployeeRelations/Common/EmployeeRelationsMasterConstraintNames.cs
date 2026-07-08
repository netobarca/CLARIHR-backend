namespace CLARIHR.Application.Features.EmployeeRelations.Common;

/// <summary>
/// Single source of truth for the unique-constraint names of the employee-relations configuration
/// masters. The EF configurations reference these constants and the handlers match them when
/// translating a database unique violation into a 409 (mirrors <c>LeaveMasterConstraintNames</c>).
/// All are filtered unique (WHERE is_active): a code can be reused after inactivation.
/// </summary>
public static class EmployeeRelationsMasterConstraintNames
{
    public const string RecognitionTypeCodeUnique = "uq_recognition_types__tenant_code_active";

    public const string DisciplinaryActionTypeCodeUnique = "uq_disciplinary_action_types__tenant_code_active";

    public const string DisciplinaryActionCauseCodeUnique = "uq_disciplinary_action_causes__tenant_code_active";
}
