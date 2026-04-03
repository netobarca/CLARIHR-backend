using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelaxCurrentPlanChangeReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_company_subscription_plan_changes__current_commercial_plans",
                table: "company_subscription_plan_changes");

            migrationBuilder.DropForeignKey(
                name: "fk_company_subscription_plan_changes__current_plan_versions",
                table: "company_subscription_plan_changes");

            migrationBuilder.DropIndex(
                name: "IX_company_subscription_plan_changes_current_commercial_plan_id",
                table: "company_subscription_plan_changes");

            migrationBuilder.DropIndex(
                name: "IX_company_subscription_plan_changes_current_commercial_plan_v~",
                table: "company_subscription_plan_changes");

            migrationBuilder.AlterColumn<long>(
                name: "current_commercial_plan_version_id",
                table: "company_subscription_plan_changes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "current_commercial_plan_id",
                table: "company_subscription_plan_changes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "current_commercial_plan_version_id",
                table: "company_subscription_plan_changes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "current_commercial_plan_id",
                table: "company_subscription_plan_changes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_subscription_plan_changes_current_commercial_plan_id",
                table: "company_subscription_plan_changes",
                column: "current_commercial_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_subscription_plan_changes_current_commercial_plan_v~",
                table: "company_subscription_plan_changes",
                column: "current_commercial_plan_version_id");

            migrationBuilder.AddForeignKey(
                name: "fk_company_subscription_plan_changes__current_commercial_plans",
                table: "company_subscription_plan_changes",
                column: "current_commercial_plan_id",
                principalTable: "commercial_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_company_subscription_plan_changes__current_plan_versions",
                table: "company_subscription_plan_changes",
                column: "current_commercial_plan_version_id",
                principalTable: "commercial_plan_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
