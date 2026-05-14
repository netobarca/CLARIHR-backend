using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260513000600_BackfillJobProfileBenefitConcurrencyTokens")]
    public partial class BackfillJobProfileBenefitConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'job_profile_benefits'
                          AND column_name = 'ConcurrencyToken'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'job_profile_benefits'
                          AND column_name = 'concurrency_token'
                    ) THEN
                        ALTER TABLE job_profile_benefits
                        RENAME COLUMN "ConcurrencyToken" TO concurrency_token;
                    END IF;
                END $$;

                ALTER TABLE job_profile_benefits
                ADD COLUMN IF NOT EXISTS concurrency_token uuid;

                UPDATE job_profile_benefits
                SET concurrency_token = (md5(random()::text || clock_timestamp()::text))::uuid
                WHERE concurrency_token IS NULL
                   OR concurrency_token = '00000000-0000-0000-0000-000000000000'::uuid;

                ALTER TABLE job_profile_benefits
                ALTER COLUMN concurrency_token SET NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'job_profile_benefits'
                          AND column_name = 'concurrency_token'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'job_profile_benefits'
                          AND column_name = 'ConcurrencyToken'
                    ) THEN
                        ALTER TABLE job_profile_benefits
                        RENAME COLUMN concurrency_token TO "ConcurrencyToken";
                    END IF;
                END $$;
                """);
        }
    }
}
