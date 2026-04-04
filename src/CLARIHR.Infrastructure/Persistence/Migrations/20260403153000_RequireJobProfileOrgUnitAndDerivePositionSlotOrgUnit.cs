using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260403153000_RequireJobProfileOrgUnitAndDerivePositionSlotOrgUnit")]
public partial class RequireJobProfileOrgUnitAndDerivePositionSlotOrgUnit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                override_count integer;
            BEGIN
                SELECT COUNT(*)
                INTO override_count
                FROM position_slots slot
                JOIN job_profiles profile ON profile.id = slot.job_profile_id
                JOIN org_units org_unit ON org_unit.id = slot.org_unit_id
                WHERE slot.cost_center_code IS NOT NULL
                  AND upper(trim(slot.cost_center_code)) <> upper(trim(coalesce(org_unit.cost_center_code, '')));

                IF override_count > 0 THEN
                    RAISE NOTICE 'Detected % position slot cost center overrides that will be removed by this migration.', override_count;
                END IF;
            END
            $$;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM job_profiles WHERE org_unit_id IS NULL) THEN
                    RAISE EXCEPTION 'Cannot apply migration RequireJobProfileOrgUnitAndDerivePositionSlotOrgUnit because one or more job_profiles rows have org_unit_id = NULL.';
                END IF;
            END
            $$;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM position_slots
                    GROUP BY job_profile_id
                    HAVING COUNT(DISTINCT org_unit_id) > 1
                ) THEN
                    RAISE EXCEPTION 'Cannot apply migration RequireJobProfileOrgUnitAndDerivePositionSlotOrgUnit because at least one job profile has position slots in multiple organization units.';
                END IF;
            END
            $$;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM position_slots slot
                    JOIN job_profiles profile ON profile.id = slot.job_profile_id
                    WHERE profile.org_unit_id <> slot.org_unit_id
                ) THEN
                    RAISE EXCEPTION 'Cannot apply migration RequireJobProfileOrgUnitAndDerivePositionSlotOrgUnit because at least one job profile organization unit differs from its historical position slots.';
                END IF;
            END
            $$;
            """);

        migrationBuilder.AlterColumn<long>(
            name: "org_unit_id",
            table: "job_profiles",
            type: "bigint",
            nullable: false,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_org_units__tenant_cost_center_code",
            table: "org_units",
            columns: new[] { "tenant_id", "cost_center_code" });

        migrationBuilder.DropForeignKey(
            name: "fk_position_slots__org_unit",
            table: "position_slots");

        migrationBuilder.DropIndex(
            name: "IX_position_slots_org_unit_id",
            table: "position_slots");

        migrationBuilder.DropIndex(
            name: "ix_position_slots__tenant_org_unit",
            table: "position_slots");

        migrationBuilder.DropColumn(
            name: "cost_center_code",
            table: "position_slots");

        migrationBuilder.DropColumn(
            name: "org_unit_id",
            table: "position_slots");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "cost_center_code",
            table: "position_slots",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "org_unit_id",
            table: "position_slots",
            type: "bigint",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE position_slots slot
            SET org_unit_id = profile.org_unit_id,
                cost_center_code = org_unit.cost_center_code
            FROM job_profiles profile
            JOIN org_units org_unit ON org_unit.id = profile.org_unit_id
            WHERE profile.id = slot.job_profile_id;
            """);

        migrationBuilder.AlterColumn<long>(
            name: "org_unit_id",
            table: "position_slots",
            type: "bigint",
            nullable: false,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_position_slots_org_unit_id",
            table: "position_slots",
            column: "org_unit_id");

        migrationBuilder.CreateIndex(
            name: "ix_position_slots__tenant_org_unit",
            table: "position_slots",
            columns: new[] { "tenant_id", "org_unit_id" });

        migrationBuilder.AddForeignKey(
            name: "fk_position_slots__org_unit",
            table: "position_slots",
            column: "org_unit_id",
            principalTable: "org_units",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropIndex(
            name: "ix_org_units__tenant_cost_center_code",
            table: "org_units");

        migrationBuilder.AlterColumn<long>(
            name: "org_unit_id",
            table: "job_profiles",
            type: "bigint",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "bigint");
    }
}
