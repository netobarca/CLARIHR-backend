using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260513000400_AddJobProfileCompetencyConcurrencyToken")]
    public partial class AddJobProfileCompetencyConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE job_profile_competencies
                ADD COLUMN IF NOT EXISTS concurrency_token uuid;

                UPDATE job_profile_competencies
                SET concurrency_token = (md5(random()::text || clock_timestamp()::text))::uuid
                WHERE concurrency_token IS NULL;

                ALTER TABLE job_profile_competencies
                ALTER COLUMN concurrency_token SET NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE job_profile_competencies
                DROP COLUMN IF EXISTS concurrency_token;
                """);
        }
    }
}
