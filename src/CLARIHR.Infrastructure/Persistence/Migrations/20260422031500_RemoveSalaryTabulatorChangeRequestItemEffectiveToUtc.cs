using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260422031500_RemoveSalaryTabulatorChangeRequestItemEffectiveToUtc")]
    public partial class RemoveSalaryTabulatorChangeRequestItemEffectiveToUtc : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE salary_tabulator_change_requests
                ADD COLUMN IF NOT EXISTS effective_to_utc timestamp with time zone NULL;

                ALTER TABLE salary_tabulator_change_request_items
                DROP COLUMN IF EXISTS effective_to_utc;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE salary_tabulator_change_request_items
                ADD COLUMN IF NOT EXISTS effective_to_utc timestamp with time zone NULL;
                """);
        }
    }
}
