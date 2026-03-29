using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandardizePublicIdsAndCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_rbac_resource_catalog",
                table: "rbac_resource_catalog");

            migrationBuilder.DropIndex(
                name: "uq_legal_representative_representation_type_catalog__code",
                table: "legal_representative_representation_type_catalog");

            migrationBuilder.DropIndex(
                name: "uq_legal_representative_position_title_catalog__code",
                table: "legal_representative_position_title_catalog");

            migrationBuilder.DropIndex(
                name: "uq_legal_representative_document_type_catalog__code",
                table: "legal_representative_document_type_catalog");

            migrationBuilder.DeleteData(
                table: "rbac_resource_catalog",
                keyColumn: "resource_key",
                keyValue: "AUDIT_LOGS");

            migrationBuilder.DeleteData(
                table: "rbac_resource_catalog",
                keyColumn: "resource_key",
                keyValue: "RBAC_PERMISSIONS");

            migrationBuilder.DeleteData(
                table: "rbac_resource_catalog",
                keyColumn: "resource_key",
                keyValue: "RBAC_ROLES");

            migrationBuilder.DeleteData(
                table: "rbac_resource_catalog",
                keyColumn: "resource_key",
                keyValue: "RBAC_USERS");

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "user_companies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "salary_tabulator_change_request_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "role_field_permissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "id",
                table: "rbac_resource_catalog",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "rbac_resource_catalog",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "rbac_permission_audit_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "plan_entitlements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "normalized_code",
                table: "legal_representative_representation_type_catalog",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "legal_representative_representation_type_catalog",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "normalized_code",
                table: "legal_representative_position_title_catalog",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "legal_representative_position_title_catalog",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "normalized_code",
                table: "legal_representative_document_type_catalog",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "legal_representative_document_type_catalog",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "job_profile_working_conditions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "job_profile_trainings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "job_profile_requirements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "job_profile_relations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "job_profile_functions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "job_profile_dependent_positions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "job_profile_competency_expectation_conducts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "job_profile_competencies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "job_profile_compensations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "job_profile_benefits",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "iam_user_role_assignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "linked_user_public_id",
                table: "iam_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "iam_role_permission_assignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "field_permission_audit_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "field_catalog",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "competency_conduct_behaviors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "company_subscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "company_invitation_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "commercial_plan_limits",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "auth_refresh_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "pk_rbac_resource_catalog",
                table: "rbac_resource_catalog",
                column: "id");

            migrationBuilder.UpdateData(
                table: "field_catalog",
                keyColumn: "id",
                keyValue: -2005L,
                column: "public_id",
                value: new Guid("d9fce2a1-ccba-1839-d33e-01708142d245"));

            migrationBuilder.UpdateData(
                table: "field_catalog",
                keyColumn: "id",
                keyValue: -2004L,
                column: "public_id",
                value: new Guid("e1acf680-98cf-d7c5-ac64-3e7cf80ba54f"));

            migrationBuilder.UpdateData(
                table: "field_catalog",
                keyColumn: "id",
                keyValue: -2003L,
                column: "public_id",
                value: new Guid("c5e8f1bc-cb9a-bb5d-a00b-36c184478cd9"));

            migrationBuilder.UpdateData(
                table: "field_catalog",
                keyColumn: "id",
                keyValue: -2002L,
                column: "public_id",
                value: new Guid("7304e673-22e5-0015-01c2-10c87345aeba"));

            migrationBuilder.UpdateData(
                table: "field_catalog",
                keyColumn: "id",
                keyValue: -2001L,
                column: "public_id",
                value: new Guid("15738a62-f6ae-5413-1617-5ee27cbc3e3f"));

            migrationBuilder.UpdateData(
                table: "field_catalog",
                keyColumn: "id",
                keyValue: -2000L,
                column: "public_id",
                value: new Guid("02c89b42-3b79-73a1-c892-7b460e3d8bbb"));

            migrationBuilder.UpdateData(
                table: "legal_representative_document_type_catalog",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "code", "normalized_code", "public_id" },
                values: new object[] { "NATIONALID", "NATIONALID", new Guid("fedf12e0-eaf7-5b63-23f2-3dbd66dfca00") });

            migrationBuilder.UpdateData(
                table: "legal_representative_document_type_catalog",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "code", "normalized_code", "public_id" },
                values: new object[] { "PASSPORT", "PASSPORT", new Guid("c3b0ed20-3b2e-e0a3-4a80-4b2677409133") });

            migrationBuilder.UpdateData(
                table: "legal_representative_document_type_catalog",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "code", "normalized_code", "public_id" },
                values: new object[] { "TAXID", "TAXID", new Guid("b3f3e7aa-8d30-154d-94eb-eab837c8a1db") });

            migrationBuilder.UpdateData(
                table: "legal_representative_document_type_catalog",
                keyColumn: "id",
                keyValue: 4L,
                columns: new[] { "code", "normalized_code", "public_id" },
                values: new object[] { "OTHER", "OTHER", new Guid("8c070836-b6b1-5393-ed1c-648de59d3653") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "OWNER", new Guid("0ef310ac-b7ff-c89a-aec7-9c2c03678981") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "CEO", new Guid("84ba6abe-edc9-f364-7141-a54f67419938") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "EXECUTIVE_MANAGEMENT", new Guid("f83e1ed4-e95a-a367-0204-ab3c6f0f6cce") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 4L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "HUMAN_RESOURCES", new Guid("b0fccf41-39a5-7933-7678-69cdbcfbf154") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 5L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "FINANCE", new Guid("91fb9fb2-07aa-fff8-ed96-326fe9edebec") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 6L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "ACCOUNTING", new Guid("8b2df04d-d57c-1767-7971-841b114df552") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 7L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "OPERATIONS", new Guid("6aa22f66-30bc-76d6-c0e8-bd5d2db06ca4") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 8L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "PROCUREMENT", new Guid("c21b0284-f48b-9c94-9bf2-43ab39f2ef91") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 9L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "SALES", new Guid("beeaa4e2-8044-e91c-a4d8-859c8cf8eaab") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 10L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "MARKETING", new Guid("cd8738ec-d87f-57a3-8780-2f8b1f99329a") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 11L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "CUSTOMER_SERVICE", new Guid("1ad62c2f-b983-6ef1-ebca-414eb1d39d39") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 12L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "INFORMATION_TECHNOLOGY", new Guid("b1fad938-a892-43f7-aff4-425b4fe28594") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 13L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "SOFTWARE_DEVELOPMENT", new Guid("a63b76cf-822f-f56a-ea0b-e84bc3ba050e") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 14L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "INFRASTRUCTURE_DEVOPS", new Guid("d4bbe59d-4864-d1f2-3e5e-99c6202050c7") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 15L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "DATA_ANALYTICS", new Guid("d47b9164-cdb9-43c8-0643-64b8474f176e") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 16L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "LEGAL", new Guid("49cd1563-df08-7f86-5e8b-990b38d6cbb4") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 17L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "ADMINISTRATION", new Guid("7560e5ba-619e-8b09-0fc6-98728793bf9f") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 18L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "LOGISTICS", new Guid("e5648d21-1f5a-db49-7979-a69836b809a2") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 19L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "MAINTENANCE", new Guid("d7281db7-26ca-1868-691b-24a49d576690") });

            migrationBuilder.UpdateData(
                table: "legal_representative_position_title_catalog",
                keyColumn: "id",
                keyValue: 20L,
                columns: new[] { "normalized_code", "public_id" },
                values: new object[] { "SECURITY", new Guid("23e24f56-f387-32f9-4a27-f48ad594f919") });

            migrationBuilder.UpdateData(
                table: "legal_representative_representation_type_catalog",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "code", "normalized_code", "public_id" },
                values: new object[] { "PRIMARYLEGALREPRESENTATIVE", "PRIMARYLEGALREPRESENTATIVE", new Guid("f116b8b0-fbec-26f9-e0d6-bef155566cb7") });

            migrationBuilder.UpdateData(
                table: "legal_representative_representation_type_catalog",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "code", "normalized_code", "public_id" },
                values: new object[] { "ALTERNATELEGALREPRESENTATIVE", "ALTERNATELEGALREPRESENTATIVE", new Guid("6d1b562e-7aa2-3cfd-1d9e-03cf11122280") });

            migrationBuilder.UpdateData(
                table: "legal_representative_representation_type_catalog",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "code", "normalized_code", "public_id" },
                values: new object[] { "ATTORNEYINFACT", "ATTORNEYINFACT", new Guid("a5debeb2-9e02-76b8-4a0c-02c1d3e4881f") });

            migrationBuilder.UpdateData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1002L,
                column: "public_id",
                value: new Guid("0de9b273-f61d-0f2b-084b-0e28cf98f2a3"));

            migrationBuilder.UpdateData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1001L,
                column: "public_id",
                value: new Guid("56c7165e-db4e-49ae-34da-5ce197c6e65d"));

            migrationBuilder.UpdateData(
                table: "plan_entitlements",
                keyColumn: "id",
                keyValue: -1000L,
                column: "public_id",
                value: new Guid("cf3f6862-a265-9d0a-0887-8b4df6b9846f"));

            migrationBuilder.InsertData(
                table: "rbac_resource_catalog",
                columns: new[] { "id", "created_utc", "display_name", "is_active", "modified_utc", "normalized_resource_key", "public_id", "resource_key" },
                values: new object[,]
                {
                    { -4003L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Audit Logs", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "AUDIT_LOGS", new Guid("1e3ad680-bc6e-d527-7260-489be62dc118"), "AUDIT_LOGS" },
                    { -4002L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Permissions", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_PERMISSIONS", new Guid("e605fc76-88fc-769c-73d3-16265ab8f1d9"), "RBAC_PERMISSIONS" },
                    { -4001L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Roles", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_ROLES", new Guid("9c0d9736-80f9-4fbb-cbbb-69fd92622fd8"), "RBAC_ROLES" },
                    { -4000L, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Users", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_USERS", new Guid("5d74f73d-1f26-3217-c60d-c292d161afc9"), "RBAC_USERS" }
                });

            BackfillPublicIds(
                migrationBuilder,
                "auth_refresh_tokens",
                "commercial_plan_limits",
                "company_invitation_tokens",
                "company_subscriptions",
                "competency_conduct_behaviors",
                "field_catalog",
                "field_permission_audit_logs",
                "iam_role_permission_assignments",
                "iam_user_role_assignments",
                "job_profile_benefits",
                "job_profile_compensations",
                "job_profile_competencies",
                "job_profile_competency_expectation_conducts",
                "job_profile_dependent_positions",
                "job_profile_functions",
                "job_profile_relations",
                "job_profile_requirements",
                "job_profile_trainings",
                "job_profile_working_conditions",
                "legal_representative_document_type_catalog",
                "legal_representative_position_title_catalog",
                "legal_representative_representation_type_catalog",
                "plan_entitlements",
                "rbac_permission_audit_logs",
                "rbac_resource_catalog",
                "role_field_permissions",
                "salary_tabulator_change_request_items",
                "user_companies");

            BackfillUppercaseCodes(
                migrationBuilder,
                "legal_representative_document_type_catalog",
                "legal_representative_position_title_catalog",
                "legal_representative_representation_type_catalog");

            migrationBuilder.Sql("""
                UPDATE iam_users
                SET linked_user_public_id = public_id
                WHERE linked_user_public_id IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE iam_users
                SET public_id = gen_random_uuid();
                """);

            migrationBuilder.CreateIndex(
                name: "uq_user_companies__public_id",
                table: "user_companies",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_salary_tabulator_change_request_items__public_id",
                table: "salary_tabulator_change_request_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_role_field_permissions__public_id",
                table: "role_field_permissions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rbac_resource_catalog__public_id",
                table: "rbac_resource_catalog",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rbac_resource_catalog__resource_key",
                table: "rbac_resource_catalog",
                column: "resource_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rbac_permission_audit_logs__public_id",
                table: "rbac_permission_audit_logs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_plan_entitlements__public_id",
                table: "plan_entitlements",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_representation_type_catalog__normalized_code",
                table: "legal_representative_representation_type_catalog",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_representation_type_catalog__public_id",
                table: "legal_representative_representation_type_catalog",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_position_title_catalog__normalized_code",
                table: "legal_representative_position_title_catalog",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_position_title_catalog__public_id",
                table: "legal_representative_position_title_catalog",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_document_type_catalog__normalized_code",
                table: "legal_representative_document_type_catalog",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_document_type_catalog__public_id",
                table: "legal_representative_document_type_catalog",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_working_conditions__public_id",
                table: "job_profile_working_conditions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_trainings__public_id",
                table: "job_profile_trainings",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_requirements__public_id",
                table: "job_profile_requirements",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_relations__public_id",
                table: "job_profile_relations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_functions__public_id",
                table: "job_profile_functions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_dependent_positions__public_id",
                table: "job_profile_dependent_positions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_competency_expectation_conducts__public_id",
                table: "job_profile_competency_expectation_conducts",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_competencies__public_id",
                table: "job_profile_competencies",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_compensations__public_id",
                table: "job_profile_compensations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_profile_benefits__public_id",
                table: "job_profile_benefits",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_iam_users__public_id",
                table: "iam_users",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_iam_users__tenant_linked_user_public_id",
                table: "iam_users",
                columns: new[] { "tenant_id", "linked_user_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_iam_user_role_assignments__public_id",
                table: "iam_user_role_assignments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_iam_roles__public_id",
                table: "iam_roles",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_iam_role_permission_assignments__public_id",
                table: "iam_role_permission_assignments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_iam_permissions__public_id",
                table: "iam_permissions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_field_permission_audit_logs__public_id",
                table: "field_permission_audit_logs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_field_catalog__public_id",
                table: "field_catalog",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_competency_conduct_behaviors__public_id",
                table: "competency_conduct_behaviors",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_subscriptions__public_id",
                table: "company_subscriptions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_invitation_tokens__public_id",
                table: "company_invitation_tokens",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_commercial_plan_limits__public_id",
                table: "commercial_plan_limits",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_auth_refresh_tokens__public_id",
                table: "auth_refresh_tokens",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_user_companies__public_id",
                table: "user_companies");

            migrationBuilder.DropIndex(
                name: "uq_salary_tabulator_change_request_items__public_id",
                table: "salary_tabulator_change_request_items");

            migrationBuilder.DropIndex(
                name: "uq_role_field_permissions__public_id",
                table: "role_field_permissions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_rbac_resource_catalog",
                table: "rbac_resource_catalog");

            migrationBuilder.DropIndex(
                name: "uq_rbac_resource_catalog__public_id",
                table: "rbac_resource_catalog");

            migrationBuilder.DropIndex(
                name: "uq_rbac_resource_catalog__resource_key",
                table: "rbac_resource_catalog");

            migrationBuilder.DropIndex(
                name: "uq_rbac_permission_audit_logs__public_id",
                table: "rbac_permission_audit_logs");

            migrationBuilder.DropIndex(
                name: "uq_plan_entitlements__public_id",
                table: "plan_entitlements");

            migrationBuilder.DropIndex(
                name: "uq_legal_representative_representation_type_catalog__normalized_code",
                table: "legal_representative_representation_type_catalog");

            migrationBuilder.DropIndex(
                name: "uq_legal_representative_representation_type_catalog__public_id",
                table: "legal_representative_representation_type_catalog");

            migrationBuilder.DropIndex(
                name: "uq_legal_representative_position_title_catalog__normalized_code",
                table: "legal_representative_position_title_catalog");

            migrationBuilder.DropIndex(
                name: "uq_legal_representative_position_title_catalog__public_id",
                table: "legal_representative_position_title_catalog");

            migrationBuilder.DropIndex(
                name: "uq_legal_representative_document_type_catalog__normalized_code",
                table: "legal_representative_document_type_catalog");

            migrationBuilder.DropIndex(
                name: "uq_legal_representative_document_type_catalog__public_id",
                table: "legal_representative_document_type_catalog");

            migrationBuilder.DropIndex(
                name: "uq_job_profile_working_conditions__public_id",
                table: "job_profile_working_conditions");

            migrationBuilder.DropIndex(
                name: "uq_job_profile_trainings__public_id",
                table: "job_profile_trainings");

            migrationBuilder.DropIndex(
                name: "uq_job_profile_requirements__public_id",
                table: "job_profile_requirements");

            migrationBuilder.DropIndex(
                name: "uq_job_profile_relations__public_id",
                table: "job_profile_relations");

            migrationBuilder.DropIndex(
                name: "uq_job_profile_functions__public_id",
                table: "job_profile_functions");

            migrationBuilder.DropIndex(
                name: "uq_job_profile_dependent_positions__public_id",
                table: "job_profile_dependent_positions");

            migrationBuilder.DropIndex(
                name: "uq_job_profile_competency_expectation_conducts__public_id",
                table: "job_profile_competency_expectation_conducts");

            migrationBuilder.DropIndex(
                name: "uq_job_profile_competencies__public_id",
                table: "job_profile_competencies");

            migrationBuilder.DropIndex(
                name: "uq_job_profile_compensations__public_id",
                table: "job_profile_compensations");

            migrationBuilder.DropIndex(
                name: "uq_job_profile_benefits__public_id",
                table: "job_profile_benefits");

            migrationBuilder.DropIndex(
                name: "uq_iam_users__public_id",
                table: "iam_users");

            migrationBuilder.DropIndex(
                name: "uq_iam_users__tenant_linked_user_public_id",
                table: "iam_users");

            migrationBuilder.DropIndex(
                name: "uq_iam_user_role_assignments__public_id",
                table: "iam_user_role_assignments");

            migrationBuilder.DropIndex(
                name: "uq_iam_roles__public_id",
                table: "iam_roles");

            migrationBuilder.DropIndex(
                name: "uq_iam_role_permission_assignments__public_id",
                table: "iam_role_permission_assignments");

            migrationBuilder.DropIndex(
                name: "uq_iam_permissions__public_id",
                table: "iam_permissions");

            migrationBuilder.DropIndex(
                name: "uq_field_permission_audit_logs__public_id",
                table: "field_permission_audit_logs");

            migrationBuilder.DropIndex(
                name: "uq_field_catalog__public_id",
                table: "field_catalog");

            migrationBuilder.DropIndex(
                name: "uq_competency_conduct_behaviors__public_id",
                table: "competency_conduct_behaviors");

            migrationBuilder.DropIndex(
                name: "uq_company_subscriptions__public_id",
                table: "company_subscriptions");

            migrationBuilder.DropIndex(
                name: "uq_company_invitation_tokens__public_id",
                table: "company_invitation_tokens");

            migrationBuilder.DropIndex(
                name: "uq_commercial_plan_limits__public_id",
                table: "commercial_plan_limits");

            migrationBuilder.DropIndex(
                name: "uq_auth_refresh_tokens__public_id",
                table: "auth_refresh_tokens");

            migrationBuilder.DeleteData(
                table: "rbac_resource_catalog",
                keyColumn: "id",
                keyColumnType: "bigint",
                keyValue: -4003L);

            migrationBuilder.DeleteData(
                table: "rbac_resource_catalog",
                keyColumn: "id",
                keyColumnType: "bigint",
                keyValue: -4002L);

            migrationBuilder.DeleteData(
                table: "rbac_resource_catalog",
                keyColumn: "id",
                keyColumnType: "bigint",
                keyValue: -4001L);

            migrationBuilder.DeleteData(
                table: "rbac_resource_catalog",
                keyColumn: "id",
                keyColumnType: "bigint",
                keyValue: -4000L);

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "user_companies");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "salary_tabulator_change_request_items");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "role_field_permissions");

            migrationBuilder.DropColumn(
                name: "id",
                table: "rbac_resource_catalog");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "rbac_resource_catalog");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "rbac_permission_audit_logs");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "plan_entitlements");

            migrationBuilder.DropColumn(
                name: "normalized_code",
                table: "legal_representative_representation_type_catalog");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "legal_representative_representation_type_catalog");

            migrationBuilder.DropColumn(
                name: "normalized_code",
                table: "legal_representative_position_title_catalog");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "legal_representative_position_title_catalog");

            migrationBuilder.DropColumn(
                name: "normalized_code",
                table: "legal_representative_document_type_catalog");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "legal_representative_document_type_catalog");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "job_profile_working_conditions");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "job_profile_trainings");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "job_profile_requirements");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "job_profile_relations");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "job_profile_functions");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "job_profile_dependent_positions");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "job_profile_competency_expectation_conducts");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "job_profile_competencies");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "job_profile_compensations");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "job_profile_benefits");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "iam_user_role_assignments");

            migrationBuilder.DropColumn(
                name: "linked_user_public_id",
                table: "iam_users");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "iam_role_permission_assignments");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "field_permission_audit_logs");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "field_catalog");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "competency_conduct_behaviors");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "company_subscriptions");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "company_invitation_tokens");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "commercial_plan_limits");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "auth_refresh_tokens");

            migrationBuilder.AddPrimaryKey(
                name: "pk_rbac_resource_catalog",
                table: "rbac_resource_catalog",
                column: "resource_key");

            migrationBuilder.UpdateData(
                table: "legal_representative_document_type_catalog",
                keyColumn: "id",
                keyValue: 1L,
                column: "code",
                value: "NationalId");

            migrationBuilder.UpdateData(
                table: "legal_representative_document_type_catalog",
                keyColumn: "id",
                keyValue: 2L,
                column: "code",
                value: "Passport");

            migrationBuilder.UpdateData(
                table: "legal_representative_document_type_catalog",
                keyColumn: "id",
                keyValue: 3L,
                column: "code",
                value: "TaxId");

            migrationBuilder.UpdateData(
                table: "legal_representative_document_type_catalog",
                keyColumn: "id",
                keyValue: 4L,
                column: "code",
                value: "Other");

            migrationBuilder.UpdateData(
                table: "legal_representative_representation_type_catalog",
                keyColumn: "id",
                keyValue: 1L,
                column: "code",
                value: "PrimaryLegalRepresentative");

            migrationBuilder.UpdateData(
                table: "legal_representative_representation_type_catalog",
                keyColumn: "id",
                keyValue: 2L,
                column: "code",
                value: "AlternateLegalRepresentative");

            migrationBuilder.UpdateData(
                table: "legal_representative_representation_type_catalog",
                keyColumn: "id",
                keyValue: 3L,
                column: "code",
                value: "AttorneyInFact");

            migrationBuilder.InsertData(
                table: "rbac_resource_catalog",
                columns: new[] { "resource_key", "created_utc", "display_name", "is_active", "modified_utc", "normalized_resource_key" },
                values: new object[,]
                {
                    { "AUDIT_LOGS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Audit Logs", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "AUDIT_LOGS" },
                    { "RBAC_PERMISSIONS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Permissions", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_PERMISSIONS" },
                    { "RBAC_ROLES", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Roles", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_ROLES" },
                    { "RBAC_USERS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Users", true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "RBAC_USERS" }
                });

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_representation_type_catalog__code",
                table: "legal_representative_representation_type_catalog",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_position_title_catalog__code",
                table: "legal_representative_position_title_catalog",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_legal_representative_document_type_catalog__code",
                table: "legal_representative_document_type_catalog",
                column: "code",
                unique: true);
        }

        private static void BackfillPublicIds(MigrationBuilder migrationBuilder, params string[] tables)
        {
            foreach (var table in tables)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"{table}\" " +
                    "SET public_id = gen_random_uuid() " +
                    "WHERE public_id = '00000000-0000-0000-0000-000000000000';");
            }
        }

        private static void BackfillUppercaseCodes(MigrationBuilder migrationBuilder, params string[] tables)
        {
            foreach (var table in tables)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"{table}\" " +
                    "SET code = UPPER(TRIM(code)), normalized_code = UPPER(TRIM(code)) " +
                    "WHERE code IS NOT NULL AND (code <> UPPER(TRIM(code)) OR normalized_code = '');");
            }
        }
    }
}
