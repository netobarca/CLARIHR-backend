using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260514000100_CleanJobProfileCompensations")]
    public partial class CleanJobProfileCompensations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-prod cleanup: drop any rows produced by the legacy backfill in
            // 20260513001000_AddJobProfileCompensationsTable so consumers can recreate
            // compensations from scratch through the API.
            migrationBuilder.Sql(
                """
                DELETE FROM job_profile_compensations;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: data deletion is intentionally irreversible.
        }
    }
}
