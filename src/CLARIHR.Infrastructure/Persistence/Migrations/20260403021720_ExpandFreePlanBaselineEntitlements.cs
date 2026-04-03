using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandFreePlanBaselineEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "plan_entitlements",
                columns: new[] { "id", "commercial_plan_id", "created_utc", "is_enabled", "modified_utc", "module_key", "plan_code", "public_id" },
                values: new object[,]
                {
                    { -1012L, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "PERSONNEL_FILES", "FREE", new Guid("b958aa1c-cff1-2d49-bf47-b3d1972215de") },
                    { -1011L, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "LOCATIONS", "FREE", new Guid("535ee7f7-bb7a-52f9-9153-9013b2268e6c") },
                    { -1010L, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ORG_UNITS", "FREE", new Guid("633547b7-6c6d-cb77-067e-088d0129717f") },
                    { -1009L, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "COMPETENCY_FRAMEWORK", "FREE", new Guid("148950dc-39f7-e8f9-c9cc-ea6ad7c7675d") },
                    { -1008L, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "LEGAL_REPRESENTATIVES", "FREE", new Guid("ff84f2b2-dba8-bb0a-facc-9cb3200ccfeb") },
                    { -1007L, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "COST_CENTERS", "FREE", new Guid("55c0c7cf-693d-d96a-a261-00eca37b38e5") },
                    { -1006L, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "SALARY_TABULATOR", "FREE", new Guid("3225785e-2969-d164-597e-eab4842f8301") },
                    { -1005L, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "POSITION_SLOTS", "FREE", new Guid("6350efac-fe0b-5515-2fba-fcb461cc45ec") },
                    { -1004L, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "JOB_PROFILES", "FREE", new Guid("4d87b97f-409d-d231-3356-f2c1069c2337") },
                    { -1003L, -3000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "POSITION_DESCRIPTION_CATALOGS", "FREE", new Guid("e8f03b3f-124a-43e0-3086-d3fd87531449") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1012L);

            migrationBuilder.DeleteData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1011L);

            migrationBuilder.DeleteData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1010L);

            migrationBuilder.DeleteData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1009L);

            migrationBuilder.DeleteData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1008L);

            migrationBuilder.DeleteData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1007L);

            migrationBuilder.DeleteData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1006L);

            migrationBuilder.DeleteData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1005L);

            migrationBuilder.DeleteData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1004L);

            migrationBuilder.DeleteData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1003L);
        }
    }
}
