using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHobbyAssociationAndBenefitTypeCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RT-06 (drop & recreate ratificado): hobbies and associations gain a REQUIRED catalog-backed
            // code column. Existing free-text rows carry no code and are removed — no backfill/mapping by
            // design ("no importa que haya datos, se deben eliminar"). Additional benefits keep their rows:
            // benefit_type_code already exists; only non-conforming codes are cleaned below (after the
            // catalog InsertData so the comparison sees the seeded codes).
            migrationBuilder.Sql("DELETE FROM personnel_file_hobbies;");
            migrationBuilder.Sql("DELETE FROM personnel_file_associations;");

            migrationBuilder.AlterColumn<string>(
                name: "hobby_name",
                table: "personnel_file_hobbies",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<string>(
                name: "hobby_code",
                table: "personnel_file_hobbies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "association_code",
                table: "personnel_file_associations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "additional_benefit_type_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_additional_benefit_type_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_additional_benefit_type_catalog_items_country_catalog_count~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "association_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_association_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_association_catalog_items_country_catalog_country_catalog_i~",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hobby_catalog_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    country_catalog_item_id = table.Column<long>(type: "bigint", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hobby_catalog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_hobby_catalog_items_country_catalog_country_catalog_item_id",
                        column: x => x.country_catalog_item_id,
                        principalTable: "country_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "additional_benefit_type_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9679L, "OTRO", new Guid("ca3b80f6-62fa-d5ce-b15a-33556c4742f3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("42613ecb-1d60-b177-c5a2-2b60e116ed2b"), 100 },
                    { -9678L, "VEHICULO", new Guid("0383d855-80db-8327-e415-48f990e87f3d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vehículo / combustible", "VEHICULO", "VEHÍCULO / COMBUSTIBLE", new Guid("fb55ff6c-d4cd-5634-1f85-5e10a4510cb9"), 90 },
                    { -9677L, "PLAN_TELEFONO", new Guid("d7abb578-eafb-46b2-6e24-35457a130d0b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Plan de teléfono", "PLAN_TELEFONO", "PLAN DE TELÉFONO", new Guid("9cfe7827-5475-2b52-334c-aefbcf5ace94"), 80 },
                    { -9676L, "BECA_CAPACITACION", new Guid("b9e80de9-9570-269a-5db5-6ceb505f0779"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Beca / capacitación", "BECA_CAPACITACION", "BECA / CAPACITACIÓN", new Guid("4a9fbc05-b3b4-7a90-7317-627def869e03"), 70 },
                    { -9675L, "GIMNASIO", new Guid("cbd3671f-f028-3982-d699-07c271074770"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Gimnasio", "GIMNASIO", "GIMNASIO", new Guid("99f7588c-bb3a-e7ca-8afe-c5f0e2fd16da"), 60 },
                    { -9674L, "AYUDA_TRANSPORTE", new Guid("4624bb09-2977-eee9-0fc6-0911debdd866"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ayuda de transporte", "AYUDA_TRANSPORTE", "AYUDA DE TRANSPORTE", new Guid("96baa928-4910-2be1-dffd-15112163c6f0"), 50 },
                    { -9673L, "VALE_DESPENSA", new Guid("33973c2e-a0a0-8e9f-24f9-5c605a06a121"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vale de despensa", "VALE_DESPENSA", "VALE DE DESPENSA", new Guid("ef4a81c9-7383-86e8-93fd-c8ac90bcec4b"), 40 },
                    { -9672L, "BONO_ALIMENTACION", new Guid("6f33d93e-e139-e9ca-0245-bc87d153273d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bono de alimentación", "BONO_ALIMENTACION", "BONO DE ALIMENTACIÓN", new Guid("11121838-b81d-849f-2cbe-fa587f1953db"), 30 },
                    { -9671L, "SEGURO_MEDICO", new Guid("8028f30b-a893-0f26-a79b-f12e578a84a9"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Seguro médico privado", "SEGURO_MEDICO", "SEGURO MÉDICO PRIVADO", new Guid("8ba5a025-6481-a306-0a9d-ffc7509664d7"), 20 },
                    { -9670L, "SEGURO_VIDA", new Guid("5e2128ff-71b4-411a-2db0-6c970e06c595"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Seguro de vida", "SEGURO_VIDA", "SEGURO DE VIDA", new Guid("60b33bf7-0ea6-0122-1ab4-017d2bd5e15c"), 10 }
                });

            migrationBuilder.InsertData(
                table: "association_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9657L, "OTRA", new Guid("a8359e59-c435-d7fb-b89c-89022f41541c"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otra", "OTRA", "OTRA", new Guid("9e5e29f2-20ee-ec64-cf43-aa40964e2f8f"), 80 },
                    { -9656L, "COOPERATIVA", new Guid("19f5eebc-d8a9-1cf3-2ff3-bb693a05fffa"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cooperativa", "COOPERATIVA", "COOPERATIVA", new Guid("e8a5d11b-b065-acbf-73a8-44142d559922"), 70 },
                    { -9655L, "RELIGIOSA", new Guid("dce58469-6590-8629-7a08-4d4f39c729c4"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Asociación religiosa", "RELIGIOSA", "ASOCIACIÓN RELIGIOSA", new Guid("18b88133-4d68-93f1-fb94-74bd58834a3e"), 60 },
                    { -9654L, "CLUB", new Guid("b20d5101-d87c-3605-c29d-fbc6079ade75"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Club social o deportivo", "CLUB", "CLUB SOCIAL O DEPORTIVO", new Guid("5bc40f28-fccb-9987-1aed-5780c35aa2fa"), 50 },
                    { -9653L, "ONG", new Guid("7524969a-8e6b-76e3-5404-300703211ef3"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "ONG / Fundación", "ONG", "ONG / FUNDACIÓN", new Guid("09032e17-7cce-6ebf-19a9-44dec449f4af"), 40 },
                    { -9652L, "CAMARA", new Guid("c5892eef-e944-a699-ecf4-c7ca16db4afd"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cámara empresarial/gremial", "CAMARA", "CÁMARA EMPRESARIAL/GREMIAL", new Guid("05d33a2e-f488-dbca-5b33-d73bd68baaba"), 30 },
                    { -9651L, "COLEGIO_PROF", new Guid("d3ada356-5f90-e672-3878-83ddb961bcd7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Colegio profesional", "COLEGIO_PROF", "COLEGIO PROFESIONAL", new Guid("1ef21328-132a-9c39-f4c9-becf96915b9c"), 20 },
                    { -9650L, "SINDICATO", new Guid("c7e5f33d-21df-d843-ea29-7cd3f12247a1"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sindicato", "SINDICATO", "SINDICATO", new Guid("122e4a77-74fd-c0e1-e0ab-0d8cd637e9da"), 10 }
                });

            migrationBuilder.InsertData(
                table: "hobby_catalog_items",
                columns: new[] { "id", "code", "concurrency_token", "country_catalog_item_id", "country_code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "normalized_name", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -9641L, "OTRO", new Guid("484767a5-808b-f053-40f6-32495b2edc43"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Otro", "OTRO", "OTRO", new Guid("e367df88-b37e-a4d6-d4b5-fc64f0b909e3"), 120 },
                    { -9640L, "VOLUNTARIADO", new Guid("64650b6b-9dcb-5f9f-e421-f920de1781d2"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Voluntariado", "VOLUNTARIADO", "VOLUNTARIADO", new Guid("0637051d-e64e-3e89-4668-2c0942019127"), 110 },
                    { -9639L, "JARDINERIA", new Guid("53fa2e21-cab4-59af-bad5-cfbdac053f3a"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Jardinería", "JARDINERIA", "JARDINERÍA", new Guid("5dc275a6-aaac-cbae-2c66-f651197208e4"), 100 },
                    { -9638L, "FOTOGRAFIA", new Guid("70e8649e-c48e-2c8b-945c-68d3069dff18"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Fotografía", "FOTOGRAFIA", "FOTOGRAFÍA", new Guid("3a011a71-8763-9c50-042f-140fb87f0935"), 90 },
                    { -9637L, "TECNOLOGIA", new Guid("995b6cff-f474-381f-42ef-854e8e0b3a9f"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tecnología", "TECNOLOGIA", "TECNOLOGÍA", new Guid("eede32ae-dd4e-5284-6340-7498eae829f1"), 80 },
                    { -9636L, "ARTE", new Guid("1936eb6a-48a3-23e8-7eea-f7f04e99c29d"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Arte y pintura", "ARTE", "ARTE Y PINTURA", new Guid("1b941897-a535-b598-1f3a-4dea2cf3fbe4"), 70 },
                    { -9635L, "COCINA", new Guid("3e5573f0-7ce0-4e0c-34d7-cf3e23558f15"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cocina", "COCINA", "COCINA", new Guid("cc04700e-c51e-3f26-cc14-b0eae83c898d"), 60 },
                    { -9634L, "VIAJES", new Guid("05ea0cfd-ff5b-6950-0688-0b7d95714bb1"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Viajes", "VIAJES", "VIAJES", new Guid("2afc70c8-59ac-c0fa-cf5a-1fd13cb3fc1a"), 50 },
                    { -9633L, "CINE", new Guid("1e42bf1c-936c-7e86-0e0d-2e5df0233a8a"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cine y series", "CINE", "CINE Y SERIES", new Guid("da5df596-e87d-d45b-17c9-d6592d6fca6f"), 40 },
                    { -9632L, "MUSICA", new Guid("6bf2ca89-f902-a531-7159-7ab259f4694b"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Música", "MUSICA", "MÚSICA", new Guid("38517cfd-3f71-fa61-21fc-4faf841c7974"), 30 },
                    { -9631L, "LECTURA", new Guid("c6c3bc21-40f9-246b-e25e-29dd63cbf4ee"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Lectura", "LECTURA", "LECTURA", new Guid("1a250e77-d51c-e84e-f020-a18c20766dcd"), 20 },
                    { -9630L, "DEPORTE", new Guid("b0a2a5eb-a311-3f1a-32c4-62f351c476e7"), -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Deportes", "DEPORTE", "DEPORTES", new Guid("a5ee0a76-2d29-bbf1-6cd5-b1459bf047ca"), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_additional_benefit_type_catalog_items__active_sort",
                table: "additional_benefit_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_additional_benefit_type_catalog_items__country_code",
                table: "additional_benefit_type_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_additional_benefit_type_catalog_items__public_id",
                table: "additional_benefit_type_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_association_catalog_items__country_active_sort",
                table: "association_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_association_catalog_items__country_code",
                table: "association_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_association_catalog_items__public_id",
                table: "association_catalog_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hobby_catalog_items__country_active_sort",
                table: "hobby_catalog_items",
                columns: new[] { "country_catalog_item_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "uq_hobby_catalog_items__country_code",
                table: "hobby_catalog_items",
                columns: new[] { "country_catalog_item_id", "normalized_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_hobby_catalog_items__public_id",
                table: "hobby_catalog_items",
                column: "public_id",
                unique: true);

            // RT-06: existing additional benefits whose free-text benefit_type_code does not match an
            // active seeded catalog code are removed (no backfill). Runs AFTER the catalog InsertData.
            migrationBuilder.Sql(
                """
                DELETE FROM personnel_file_additional_benefits benefit
                WHERE UPPER(TRIM(benefit.benefit_type_code)) NOT IN (
                    SELECT catalog.normalized_code
                    FROM additional_benefit_type_catalog_items catalog
                    WHERE catalog.is_active
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "additional_benefit_type_catalog_items");

            migrationBuilder.DropTable(
                name: "association_catalog_items");

            migrationBuilder.DropTable(
                name: "hobby_catalog_items");

            migrationBuilder.DropColumn(
                name: "hobby_code",
                table: "personnel_file_hobbies");

            migrationBuilder.DropColumn(
                name: "association_code",
                table: "personnel_file_associations");

            migrationBuilder.AlterColumn<string>(
                name: "hobby_name",
                table: "personnel_file_hobbies",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);
        }
    }
}
