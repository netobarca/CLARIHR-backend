using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// D-08 of the retirement module (RATIFIED, deliberately destructive): the retirements registered through
    /// the legacy PUT are TEST data and are removed so that, from this release on, every non-null
    /// RetirementDate comes from the retirement-request execution (single door, RN-015.3). Their
    /// exit-interview submissions are archived first (coherence with D-09 — a baja that "did not happen"
    /// must not count in rotation analytics). personnel_files.is_active is deliberately NOT touched
    /// (files may be inactive for other reasons).
    /// </summary>
    public partial class CloseLegacyRetirementPathAndCleanupTestData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Archive the exit-interview submissions tied to legacy (test) retirements.
            migrationBuilder.Sql("""
                UPDATE exit_interview_submissions AS s
                SET status = 'Archived'
                FROM personnel_file_employee_profiles AS p
                WHERE s.personnel_file_id = p.personnel_file_id
                  AND s.tenant_id = p.tenant_id
                  AND p.retirement_date IS NOT NULL
                  AND s.status <> 'Archived';
                """);

            // 2) Clear the legacy retirement metadata; a legacy RETIRADO status returns to ACTIVO.
            migrationBuilder.Sql("""
                UPDATE personnel_file_employee_profiles
                SET retirement_category_code = NULL,
                    retirement_reason_code = NULL,
                    retirement_notes = NULL,
                    retirement_date = NULL,
                    employment_status_code = CASE
                        WHEN employment_status_code = 'RETIRADO' THEN 'ACTIVO'
                        ELSE employment_status_code
                    END
                WHERE retirement_date IS NOT NULL
                   OR employment_status_code = 'RETIRADO';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible by design (ratified D-08): the removed rows were test data.
        }
    }
}
