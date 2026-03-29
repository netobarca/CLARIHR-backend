using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryCatalogAndCompanyRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "country_catalog_item_id",
                table: "companies",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "country_catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_country_catalog", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "country_catalog",
                columns: new[] { "id", "code", "created_utc", "is_active", "modified_utc", "name", "normalized_code", "public_id", "sort_order" },
                values: new object[,]
                {
                    { -7247L, "ZW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Zimbabwe", "ZW", new Guid("022c9d49-fcb8-4e3a-e77b-63845c7493d3"), 248 },
                    { -7246L, "ZM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Zambia", "ZM", new Guid("b83fd5fe-f638-5bc6-d39d-e85c4d9f5336"), 247 },
                    { -7245L, "YE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Yemen", "YE", new Guid("37c69f70-1413-caa5-6d37-460bbd6f8e03"), 246 },
                    { -7244L, "EH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Western Sahara", "EH", new Guid("6527fe89-8f31-1113-eb30-88832cb100b6"), 245 },
                    { -7243L, "WF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Wallis & Futuna", "WF", new Guid("f2bb61f4-1763-11f8-f2d5-4b7853bd0c13"), 244 },
                    { -7242L, "VN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vietnam", "VN", new Guid("c7e8ac52-1656-0887-6005-ba817afd45c0"), 243 },
                    { -7241L, "VE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Venezuela", "VE", new Guid("f1a11cd9-75d5-582a-ba81-67a3a27d188d"), 242 },
                    { -7240L, "VA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vatican City", "VA", new Guid("b3469661-a32c-ad29-0d51-36c65c360eed"), 241 },
                    { -7239L, "VU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Vanuatu", "VU", new Guid("03a49951-64c7-ad80-7f83-fb8cbf3bb2b6"), 240 },
                    { -7238L, "UZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Uzbekistan", "UZ", new Guid("59a3aab8-cfe8-1512-7693-d3014675d2b1"), 239 },
                    { -7237L, "UY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Uruguay", "UY", new Guid("d39e353b-22fe-53b6-451d-0d08f552396b"), 238 },
                    { -7236L, "US", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "United States", "US", new Guid("af56fbff-ee04-82cf-8bac-c4072f558afc"), 237 },
                    { -7235L, "GB", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "United Kingdom", "GB", new Guid("60ad796b-ee34-9052-b4e3-305bf67ebc97"), 236 },
                    { -7234L, "AE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "United Arab Emirates", "AE", new Guid("58fad332-8a6f-633d-b3bc-81f82aa8d4e1"), 235 },
                    { -7233L, "UA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ukraine", "UA", new Guid("44a23ba6-07cf-6259-258d-77f6cc0cdcf1"), 234 },
                    { -7232L, "UG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Uganda", "UG", new Guid("315a1a30-5760-8bf2-c697-3421292ab9cb"), 233 },
                    { -7231L, "VI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "U.S. Virgin Islands", "VI", new Guid("c819a34f-8b92-500f-d725-70aaa1218d0b"), 232 },
                    { -7230L, "UM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "U.S. Outlying Islands", "UM", new Guid("f91f2e86-0c9d-ebb6-c00f-4f1c41c3ff29"), 231 },
                    { -7229L, "TV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tuvalu", "TV", new Guid("eb04a361-fa1d-d08a-14eb-7856d11da3a7"), 230 },
                    { -7228L, "TC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Turks & Caicos Islands", "TC", new Guid("84e11c89-fe22-559c-29e3-ced7efc93bd9"), 229 },
                    { -7227L, "TM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Turkmenistan", "TM", new Guid("82dfb95f-d3bc-564f-1258-b479f19e0772"), 228 },
                    { -7226L, "TR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Turkiye", "TR", new Guid("44eba2fc-da98-7657-9953-85fbc1a69168"), 227 },
                    { -7225L, "TN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tunisia", "TN", new Guid("8a2ebcfd-b09a-9f5a-2a00-0da7b856b5c2"), 226 },
                    { -7224L, "TT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Trinidad & Tobago", "TT", new Guid("3807515e-0bf1-7b32-d0ca-0beb0127348b"), 225 },
                    { -7223L, "TO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tonga", "TO", new Guid("5f814efe-d5ed-71ab-4d6f-32b6b0b6d645"), 224 },
                    { -7222L, "TK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tokelau", "TK", new Guid("5429464e-7436-bcba-03d6-fcaabe5108ad"), 223 },
                    { -7221L, "TG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Togo", "TG", new Guid("dbf7c52c-bf8e-fd7a-951b-4e906255f54b"), 222 },
                    { -7220L, "TL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Timor-Leste", "TL", new Guid("27a8c162-d2af-66dc-0ea3-c3fbc6a89187"), 221 },
                    { -7219L, "TH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Thailand", "TH", new Guid("b8be86e5-b4d3-dc8e-8c31-cb553adfa499"), 220 },
                    { -7218L, "TZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tanzania", "TZ", new Guid("9511fb4c-ee5d-65a6-13df-2a02d1a6649b"), 219 },
                    { -7217L, "TJ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Tajikistan", "TJ", new Guid("37a7fe4d-fa27-8e2c-3429-b2157279b699"), 218 },
                    { -7216L, "TW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Taiwan", "TW", new Guid("79dfd176-73e3-2b08-2fb8-a2e02b62898b"), 217 },
                    { -7215L, "SY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Syria", "SY", new Guid("05d72557-0d98-8dbf-fde3-e1bd1a41117d"), 216 },
                    { -7214L, "CH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Switzerland", "CH", new Guid("cd6ea7d1-e323-5c1d-7bd1-1e36c2a83c60"), 215 },
                    { -7213L, "SE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sweden", "SE", new Guid("7d62edcf-6448-a72a-e878-23cfb34bee3a"), 214 },
                    { -7212L, "SJ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Svalbard & Jan Mayen", "SJ", new Guid("27f5b818-de4d-5b86-3d05-b9f530054e1c"), 213 },
                    { -7211L, "SR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Suriname", "SR", new Guid("5ce65ae1-1bf7-158e-b20c-8622066d68fe"), 212 },
                    { -7210L, "SD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sudan", "SD", new Guid("a38ce262-768d-c3c5-c029-6131e9163a97"), 211 },
                    { -7209L, "VC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Vincent & Grenadines", "VC", new Guid("76b315a7-cd4b-66cf-08b4-1648c291900a"), 210 },
                    { -7208L, "PM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Pierre & Miquelon", "PM", new Guid("e3403429-4e79-e340-5fbc-590142d9284c"), 209 },
                    { -7207L, "MF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Martin", "MF", new Guid("0b3f1a2c-23a7-e957-429a-d0409529bb13"), 208 },
                    { -7206L, "LC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Lucia", "LC", new Guid("5bd0479d-ff63-fe57-805f-9ef8ea577ddb"), 207 },
                    { -7205L, "KN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Kitts & Nevis", "KN", new Guid("9faf016b-a4aa-b36d-4556-4cb24251c784"), 206 },
                    { -7204L, "SH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Helena", "SH", new Guid("375e5361-4184-a18f-cf3a-970f0888f8b4"), 205 },
                    { -7203L, "BL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "St. Barthelemy", "BL", new Guid("c3aeff1f-4632-9a79-44be-708bc95ce680"), 204 },
                    { -7202L, "LK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sri Lanka", "LK", new Guid("b12e7890-ba5a-292c-dcd3-f6d5ba1da61e"), 203 },
                    { -7201L, "ES", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Spain", "ES", new Guid("ef095456-0955-daf2-ce8a-aa4ad018840f"), 202 },
                    { -7200L, "SS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "South Sudan", "SS", new Guid("b6667f6d-617f-5189-cf79-ae6d03930a90"), 201 },
                    { -7199L, "KR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "South Korea", "KR", new Guid("8a518bd6-13c0-f125-f256-61ef2885ad84"), 200 },
                    { -7198L, "ZA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "South Africa", "ZA", new Guid("6f58e54a-97e2-a364-2cde-dd8b83cb2dfb"), 199 },
                    { -7197L, "SO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Somalia", "SO", new Guid("080c5e94-2fd7-4d58-232e-f094d70ca74f"), 198 },
                    { -7196L, "SB", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Solomon Islands", "SB", new Guid("1584e0cd-9086-7921-2b8f-271a402ec833"), 197 },
                    { -7195L, "SI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Slovenia", "SI", new Guid("7ebf0064-a93b-a100-fc7a-b4cd5d75ff72"), 196 },
                    { -7194L, "SK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Slovakia", "SK", new Guid("b77e4277-bd79-5e35-f2e0-960fc2252e65"), 195 },
                    { -7193L, "SX", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sint Maarten", "SX", new Guid("495b8c39-ddbc-f112-e40c-b5cdd1e0f142"), 194 },
                    { -7192L, "SG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Singapore", "SG", new Guid("5dec3eb7-7e7e-813e-e5ad-4e74cc56fee7"), 193 },
                    { -7191L, "SL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sierra Leone", "SL", new Guid("71f0413b-892c-a93a-f5a2-31ddb66f5405"), 192 },
                    { -7190L, "SC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Seychelles", "SC", new Guid("b9ba3d79-d394-b7f1-4cf2-af65d36eedad"), 191 },
                    { -7189L, "RS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Serbia", "RS", new Guid("62d59937-9d31-715c-f823-e99ffc70634e"), 190 },
                    { -7188L, "SN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Senegal", "SN", new Guid("dd86a979-ac0e-059d-be9f-9b7e7c51ce18"), 189 },
                    { -7187L, "SA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Saudi Arabia", "SA", new Guid("ea2d42a1-3c84-2f3f-bac4-e409394fd5ea"), 188 },
                    { -7186L, "ST", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Sao Tome & Principe", "ST", new Guid("8ff16f3f-1a83-5660-1fcc-9ab82168e5d4"), 187 },
                    { -7185L, "SM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "San Marino", "SM", new Guid("c892de95-5332-b228-1c09-4fbadd235334"), 186 },
                    { -7184L, "WS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Samoa", "WS", new Guid("664d3de2-eb5d-cfa1-68e1-2825bdf0beab"), 185 },
                    { -7183L, "RW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Rwanda", "RW", new Guid("2ef40af2-0082-b7b8-c749-0534f7f7d21f"), 184 },
                    { -7182L, "RU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Russia", "RU", new Guid("db6ee5d5-db93-2c34-87f6-c083ffc821c4"), 183 },
                    { -7181L, "RO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Romania", "RO", new Guid("6a2d39db-3784-2336-43aa-47e93e0edc7d"), 182 },
                    { -7180L, "RE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Reunion", "RE", new Guid("7e3d71f7-0521-58b2-91bf-c5923eaf6358"), 181 },
                    { -7179L, "QA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Qatar", "QA", new Guid("d7b9938d-eb8f-4f70-c367-1d3cabe6cdc7"), 180 },
                    { -7178L, "PR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Puerto Rico", "PR", new Guid("d5a35c59-2a2f-981a-2ee2-aef2c2b9d9d2"), 179 },
                    { -7177L, "PT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Portugal", "PT", new Guid("9560f2d6-b29a-c698-e257-3cfa77d42d7f"), 178 },
                    { -7176L, "PL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Poland", "PL", new Guid("32fd250c-88fb-2bad-9dd8-82e11578c30c"), 177 },
                    { -7175L, "PN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pitcairn Islands", "PN", new Guid("37626e54-6b16-b71e-6b76-6f05e450fd69"), 176 },
                    { -7174L, "PH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Philippines", "PH", new Guid("91bc4173-a392-bc81-e2e7-cd8e7d137ff7"), 175 },
                    { -7173L, "PE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Peru", "PE", new Guid("2f47b218-6dd5-1069-5a91-63e9f8485f55"), 174 },
                    { -7172L, "PY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Paraguay", "PY", new Guid("ef68c7af-643a-bc73-06fc-780aaf840297"), 173 },
                    { -7171L, "PG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Papua New Guinea", "PG", new Guid("f1c3da66-dce0-4540-3f9e-5331926f375a"), 172 },
                    { -7170L, "PA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Panama", "PA", new Guid("0d67b596-1155-c434-c68c-1c614ee5f6ce"), 171 },
                    { -7169L, "PS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Palestinian Territories", "PS", new Guid("abeb336b-8fe8-4241-30d3-be1debf4676e"), 170 },
                    { -7168L, "PW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Palau", "PW", new Guid("6a451257-2888-8cf3-4183-e262679403c5"), 169 },
                    { -7167L, "PK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Pakistan", "PK", new Guid("a9d97769-b011-af8d-49e6-13c41edd7090"), 168 },
                    { -7166L, "OM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Oman", "OM", new Guid("96ee6337-ae78-241d-0eb2-4e25329917f5"), 167 },
                    { -7165L, "NO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Norway", "NO", new Guid("67c0cc58-f3fd-c23a-60cd-413a4706a803"), 166 },
                    { -7164L, "MP", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Northern Mariana Islands", "MP", new Guid("eb7b0de3-b138-2a94-68db-04e7cef3b66a"), 165 },
                    { -7163L, "MK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "North Macedonia", "MK", new Guid("617fff95-9090-1613-e9d8-01c5b8741031"), 164 },
                    { -7162L, "KP", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "North Korea", "KP", new Guid("f3f8aed8-0a5a-6fd1-361d-fc363b5f141a"), 163 },
                    { -7161L, "NF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Norfolk Island", "NF", new Guid("b6304adc-e062-7446-d90d-12f035ba46f8"), 162 },
                    { -7160L, "NU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Niue", "NU", new Guid("10b0c5aa-66ae-62cc-27a1-5527c65c0417"), 161 },
                    { -7159L, "NG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nigeria", "NG", new Guid("1f030f27-2949-5f10-dfab-780caa48f617"), 160 },
                    { -7158L, "NE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Niger", "NE", new Guid("9a6b6c51-3850-a3b9-eb76-09e8ab129706"), 159 },
                    { -7157L, "NI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nicaragua", "NI", new Guid("6f59daa9-637e-5a46-c17c-73736b3506f1"), 158 },
                    { -7156L, "NZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "New Zealand", "NZ", new Guid("ab1a3a65-a5d1-fad5-0643-031c7bcafd9f"), 157 },
                    { -7155L, "NC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "New Caledonia", "NC", new Guid("d5e61089-97fb-4ec5-f098-0bd4597b3ce1"), 156 },
                    { -7154L, "NL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Netherlands", "NL", new Guid("d83b98f6-c017-70f7-4b68-68b8418f43bc"), 155 },
                    { -7153L, "NP", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nepal", "NP", new Guid("c0679401-a6bd-4101-152d-c6ef469133e4"), 154 },
                    { -7152L, "NR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Nauru", "NR", new Guid("99c05900-e5dc-70fa-0ef7-6ceaed568089"), 153 },
                    { -7151L, "NA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Namibia", "NA", new Guid("858df66c-b361-2819-8802-3ab74b37b9cc"), 152 },
                    { -7150L, "MM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Myanmar (Burma)", "MM", new Guid("f7967348-9746-04a9-76f9-ba88f226b2b6"), 151 },
                    { -7149L, "MZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mozambique", "MZ", new Guid("6f3daffd-b673-675b-7a30-f64901f40b24"), 150 },
                    { -7148L, "MA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Morocco", "MA", new Guid("e80f188a-4cea-1bb7-cb55-4f824f98ed10"), 149 },
                    { -7147L, "MS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Montserrat", "MS", new Guid("7740e417-92eb-ae25-e8cf-01225d30535d"), 148 },
                    { -7146L, "ME", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Montenegro", "ME", new Guid("870c7c16-9bd4-85f5-941a-26e0a9708c52"), 147 },
                    { -7145L, "MN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mongolia", "MN", new Guid("be4f8568-44b9-226b-bd4b-d8d647e7f4fe"), 146 },
                    { -7144L, "MC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Monaco", "MC", new Guid("4d2c4419-1e4a-1eed-eaf3-a3e16e7892a4"), 145 },
                    { -7143L, "MD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Moldova", "MD", new Guid("b00996f2-57e4-5bf2-65a4-731fa11c3fe9"), 144 },
                    { -7142L, "FM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Micronesia", "FM", new Guid("78600692-c16e-5dfa-bea9-af1dbd8f8d27"), 143 },
                    { -7141L, "MX", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mexico", "MX", new Guid("5be4674e-3070-8f4a-e419-581492b3f677"), 142 },
                    { -7140L, "YT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mayotte", "YT", new Guid("4b156b57-a367-cd0a-21ef-a19f858c2c67"), 141 },
                    { -7139L, "MU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mauritius", "MU", new Guid("2d6eff06-886f-6070-021d-2b50c03f4242"), 140 },
                    { -7138L, "MR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mauritania", "MR", new Guid("db02be6d-1850-b1da-3072-c766048f55fc"), 139 },
                    { -7137L, "MQ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Martinique", "MQ", new Guid("d865861d-7907-fc33-d0b0-ef84fbf80187"), 138 },
                    { -7136L, "MH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Marshall Islands", "MH", new Guid("de1be22e-699e-4c44-ef74-914cfbd2365b"), 137 },
                    { -7135L, "MT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Malta", "MT", new Guid("d4f78625-a77f-39f5-fa31-74775eeda196"), 136 },
                    { -7134L, "ML", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Mali", "ML", new Guid("314cda6e-2a8e-cb02-d0d1-6a1963c97617"), 135 },
                    { -7133L, "MV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Maldives", "MV", new Guid("b9c63305-7f84-9ca5-0dda-05ef8f5eea29"), 134 },
                    { -7132L, "MY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Malaysia", "MY", new Guid("2e2d4851-fd73-d29c-28c5-e5ff75e5119b"), 133 },
                    { -7131L, "MW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Malawi", "MW", new Guid("37e43d4a-47cb-9628-ebc5-557e730c4580"), 132 },
                    { -7130L, "MG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Madagascar", "MG", new Guid("9c4a6c26-c836-e0d3-141f-23246f49281b"), 131 },
                    { -7129L, "MO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Macao", "MO", new Guid("41cdcd74-127c-041e-b48f-7dc738909f8a"), 130 },
                    { -7128L, "LU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Luxembourg", "LU", new Guid("d0c3f7f5-8f97-7100-1f73-80b8bd2cbb1d"), 129 },
                    { -7127L, "LT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Lithuania", "LT", new Guid("7f7047aa-965e-aee0-ff82-47c1e635d69c"), 128 },
                    { -7126L, "LI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Liechtenstein", "LI", new Guid("f7d157bb-424a-21e3-a58b-8d0941732f60"), 127 },
                    { -7125L, "LY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Libya", "LY", new Guid("98c7ea84-d918-b010-f00c-9569b2d68515"), 126 },
                    { -7124L, "LR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Liberia", "LR", new Guid("510bc644-31fd-39c3-9393-71e2a4263320"), 125 },
                    { -7123L, "LS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Lesotho", "LS", new Guid("d90091c6-3389-33b1-2a14-a6e73f158dd4"), 124 },
                    { -7122L, "LB", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Lebanon", "LB", new Guid("39b70154-5d39-80e3-ea46-b517f24d620e"), 123 },
                    { -7121L, "LV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Latvia", "LV", new Guid("4ae5e72a-18a5-eb7f-4490-354410c6229a"), 122 },
                    { -7120L, "LA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Laos", "LA", new Guid("6d8a5d5a-328c-4e19-23e4-9e18eb9e39eb"), 121 },
                    { -7119L, "KG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kyrgyzstan", "KG", new Guid("8814f8a6-af1d-f779-6396-f3035203e058"), 120 },
                    { -7118L, "KW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kuwait", "KW", new Guid("aa329426-23b8-e80b-5e00-f0ff9efc51cc"), 119 },
                    { -7117L, "XK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kosovo", "XK", new Guid("6a15bb42-194a-7045-b828-0973a9c9a358"), 118 },
                    { -7116L, "KI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kiribati", "KI", new Guid("232dcc72-ed0f-efab-9bb8-c169b9a10540"), 117 },
                    { -7115L, "KE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kenya", "KE", new Guid("064c0873-f8d4-83c5-4be1-3da695efb6d3"), 116 },
                    { -7114L, "KZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Kazakhstan", "KZ", new Guid("83caf43f-ae35-9ccb-b7ad-8c06a7e98bab"), 115 },
                    { -7113L, "JO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Jordan", "JO", new Guid("abd3ffe4-e97a-e7d7-727c-b728cdc12fa3"), 114 },
                    { -7112L, "JE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Jersey", "JE", new Guid("471ca971-ef85-a712-563f-d6562f130ebf"), 113 },
                    { -7111L, "JP", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Japan", "JP", new Guid("a51e6d29-d04b-be74-ee98-018d6a619164"), 112 },
                    { -7110L, "JM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Jamaica", "JM", new Guid("39d2a465-4d54-68f4-171c-2edcc2e84345"), 111 },
                    { -7109L, "IT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Italy", "IT", new Guid("d3dcf854-860f-a98c-7f6b-5a95a300af8e"), 110 },
                    { -7108L, "IL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Israel", "IL", new Guid("a9d7725f-49d6-3d42-b7a3-a9d61c6215cb"), 109 },
                    { -7107L, "IM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Isle of Man", "IM", new Guid("f3d4b455-29a7-bbb6-9a77-0cd7b309d3c5"), 108 },
                    { -7106L, "IE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ireland", "IE", new Guid("900083c4-34da-c680-c91b-dd2b8bb74ae0"), 107 },
                    { -7105L, "IQ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Iraq", "IQ", new Guid("9a9dbaa9-f5e1-7de6-eb1a-2c3d06eb79cf"), 106 },
                    { -7104L, "IR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Iran", "IR", new Guid("d69b89cf-8a4a-d5e1-ab57-6cab8c832ebb"), 105 },
                    { -7103L, "ID", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Indonesia", "ID", new Guid("dc0bb2af-b11e-3022-007f-4f4c53f9608c"), 104 },
                    { -7102L, "IN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "India", "IN", new Guid("ed3b489a-f4ba-6067-66c6-9185c7bf7c4e"), 103 },
                    { -7101L, "IS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Iceland", "IS", new Guid("c018cb0d-b504-f979-c651-1733f820a7ee"), 102 },
                    { -7100L, "HU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Hungary", "HU", new Guid("4d0bf9bc-7cd7-e174-656f-daa19d029d4d"), 101 },
                    { -7099L, "HK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Hong Kong", "HK", new Guid("1dbb46b3-cc6e-e0df-ddc4-1d4b728665e0"), 100 },
                    { -7098L, "HN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Honduras", "HN", new Guid("a2a1eb51-efaf-0566-b881-a073235f7101"), 99 },
                    { -7097L, "HT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Haiti", "HT", new Guid("eb8a0344-81a3-76d2-8060-4f4b0d40adbd"), 98 },
                    { -7096L, "GY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guyana", "GY", new Guid("29495857-ec7e-8bf5-45c4-a48b43fa25f3"), 97 },
                    { -7095L, "GW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guinea-Bissau", "GW", new Guid("ca14c038-8b91-f16c-9249-b46a1d7fe9da"), 96 },
                    { -7094L, "GN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guinea", "GN", new Guid("776fa913-186f-a9d2-7c35-859151496aac"), 95 },
                    { -7093L, "GG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guernsey", "GG", new Guid("1c947fc9-e355-9b7c-3579-d1895d96d7f3"), 94 },
                    { -7092L, "GT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guatemala", "GT", new Guid("4482fc22-baef-a84d-1d72-83c42322b3b1"), 93 },
                    { -7091L, "GU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guam", "GU", new Guid("af3c2cff-a1b0-ae02-2833-663e2f348c5d"), 92 },
                    { -7090L, "GP", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Guadeloupe", "GP", new Guid("c09d83b1-3c03-63af-1247-94f7835c9990"), 91 },
                    { -7089L, "GD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Grenada", "GD", new Guid("b28b1ac7-8e6f-cbae-1199-a2bc71e250fc"), 90 },
                    { -7088L, "GL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Greenland", "GL", new Guid("c79d7f83-9ed5-e0e4-df81-ca6ebbb723c4"), 89 },
                    { -7087L, "GR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Greece", "GR", new Guid("32428479-fc1a-9a33-59f8-d7c7ebcc5f05"), 88 },
                    { -7086L, "GI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Gibraltar", "GI", new Guid("9cab0e17-9897-0491-ccf8-481a165e416c"), 87 },
                    { -7085L, "GH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ghana", "GH", new Guid("ecd3da8b-41a2-3105-938b-886bae0d3dc6"), 86 },
                    { -7084L, "DE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Germany", "DE", new Guid("ea3af316-0ea3-c13d-04a7-507b31eb4d0b"), 85 },
                    { -7083L, "GE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Georgia", "GE", new Guid("3ffdddc6-a2e1-4305-f6f8-e21e3dedbbb4"), 84 },
                    { -7082L, "GM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Gambia", "GM", new Guid("fc03ffc2-8a54-e808-b8f0-3af6717142d3"), 83 },
                    { -7081L, "GA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Gabon", "GA", new Guid("6ec32a47-29bf-1fd9-f7c6-bd803b1a816d"), 82 },
                    { -7080L, "PF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "French Polynesia", "PF", new Guid("4171cae2-7b91-d4eb-3541-a9909278b5b9"), 81 },
                    { -7079L, "GF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "French Guiana", "GF", new Guid("56119d01-3df0-cff8-c26a-fe5d9fd2ef73"), 80 },
                    { -7078L, "FR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "France", "FR", new Guid("91563e58-8854-b169-81a1-d2a260a0dbc3"), 79 },
                    { -7077L, "FI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Finland", "FI", new Guid("26118f54-8d67-3b1e-8dfe-8b5d93c5893a"), 78 },
                    { -7076L, "FJ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Fiji", "FJ", new Guid("99960e4d-f1d1-9cfe-6cbd-5de518832d28"), 77 },
                    { -7075L, "FO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Faroe Islands", "FO", new Guid("af8b3ba6-8ce1-52e1-2636-090779e038c6"), 76 },
                    { -7074L, "FK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Falkland Islands", "FK", new Guid("e4c795f1-e00c-a2fc-185a-cd159cdbedce"), 75 },
                    { -7073L, "ET", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ethiopia", "ET", new Guid("5f070476-4120-7691-1819-fb5048ecbff3"), 74 },
                    { -7072L, "SZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Eswatini", "SZ", new Guid("db65e4da-5f02-2a69-fd70-a685711f7fa4"), 73 },
                    { -7071L, "EE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Estonia", "EE", new Guid("cc5ee3f4-2ec3-f96a-11f3-aad1ed2c41c1"), 72 },
                    { -7070L, "ER", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Eritrea", "ER", new Guid("2376e024-f591-3403-a3e5-23af09b3b119"), 71 },
                    { -7069L, "GQ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Equatorial Guinea", "GQ", new Guid("a33009f6-12f2-9c31-79ed-9a541f31db0f"), 70 },
                    { -7068L, "SV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "El Salvador", "SV", new Guid("4ba8c8cd-d7b0-361f-ef2a-f63e44b04c7b"), 69 },
                    { -7067L, "EG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Egypt", "EG", new Guid("e27a22db-d2f7-f9a6-dd44-2f14ed840ec6"), 68 },
                    { -7066L, "EC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ecuador", "EC", new Guid("ca935f5d-0e9c-fe6c-1030-09af1bc09174"), 67 },
                    { -7065L, "DO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Dominican Republic", "DO", new Guid("3edd9881-c754-74bc-7a08-6c5e47ca96c4"), 66 },
                    { -7064L, "DM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Dominica", "DM", new Guid("65fe830a-bb79-cf28-21b8-0936b4cd7056"), 65 },
                    { -7063L, "DJ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Djibouti", "DJ", new Guid("2652a191-e94f-e20e-6bd5-cb81d3e1457f"), 64 },
                    { -7062L, "DG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Diego Garcia", "DG", new Guid("9e08cef7-41a2-411d-70a1-53dbf5b9b4d3"), 63 },
                    { -7061L, "DK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Denmark", "DK", new Guid("f3c744aa-049a-8935-8251-4a14d2a47ed0"), 62 },
                    { -7060L, "CZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Czechia", "CZ", new Guid("a28e8926-b5ef-0da3-319b-83a0f6b9e63b"), 61 },
                    { -7059L, "CY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cyprus", "CY", new Guid("4b7207f5-3f84-773c-d9f5-8dbd0be1805b"), 60 },
                    { -7058L, "CW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Curacao", "CW", new Guid("2a9b1919-4608-5b6b-6b06-b808c3108d33"), 59 },
                    { -7057L, "CU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cuba", "CU", new Guid("82767b58-6830-93fe-4aba-9b54796eae55"), 58 },
                    { -7056L, "HR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Croatia", "HR", new Guid("f284c98e-ab4a-ce97-60db-e651034cdf00"), 57 },
                    { -7055L, "CI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cote d'Ivoire", "CI", new Guid("b6b3b6b8-e327-cbb5-10f7-25854e86d052"), 56 },
                    { -7054L, "CR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Costa Rica", "CR", new Guid("708cbe5a-67a4-2b1d-b321-7789fc21e493"), 55 },
                    { -7053L, "CK", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cook Islands", "CK", new Guid("f960e81e-2888-aef3-ac43-8ee1dd79abd7"), 54 },
                    { -7052L, "CD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Congo - Kinshasa", "CD", new Guid("8f95006a-4b4c-e0b8-95de-151c421d5018"), 53 },
                    { -7051L, "CG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Congo - Brazzaville", "CG", new Guid("67159e77-f91f-eaf9-f588-84011dc45d10"), 52 },
                    { -7050L, "KM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Comoros", "KM", new Guid("450ec4f9-9473-02f3-ea84-33f326c401c7"), 51 },
                    { -7049L, "CO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Colombia", "CO", new Guid("faed71bb-e976-55ae-67ae-a30e2a0341b7"), 50 },
                    { -7048L, "CC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cocos (Keeling) Islands", "CC", new Guid("58d3c78b-4c04-9032-94f0-1acc161291a0"), 49 },
                    { -7047L, "CX", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Christmas Island", "CX", new Guid("d8160a33-93f3-6337-8d40-43c8b166ddd4"), 48 },
                    { -7046L, "CN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "China mainland", "CN", new Guid("b0a78491-9f7d-9f17-9013-7ba2f818cc08"), 47 },
                    { -7045L, "CL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chile", "CL", new Guid("a9c17e33-89cf-8aa9-d450-d34688cdb0ff"), 46 },
                    { -7044L, "IO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chagos Archipelago", "IO", new Guid("16c8e523-4be0-c8f1-a247-1dcd200944ec"), 45 },
                    { -7043L, "TD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Chad", "TD", new Guid("12e2d434-ff12-9251-28df-04cd07d98821"), 44 },
                    { -7042L, "EA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ceuta & Melilla", "EA", new Guid("e672f9d7-c555-001d-7ef2-fbe1711523ba"), 43 },
                    { -7041L, "CF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Central African Republic", "CF", new Guid("8c66b86c-4cad-ca0b-c1c7-86d1cbffaa87"), 42 },
                    { -7040L, "KY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cayman Islands", "KY", new Guid("9330af41-eb0f-73bb-97b6-a7b8474d2557"), 41 },
                    { -7039L, "BQ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Caribbean Netherlands", "BQ", new Guid("893fa715-3ae9-9d5c-9582-7d5e6394ebc0"), 40 },
                    { -7038L, "CV", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cape Verde", "CV", new Guid("772ddeff-bb5e-3228-7e90-dfdded3df25d"), 39 },
                    { -7037L, "IC", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Canary Islands", "IC", new Guid("10461941-f9c5-f547-4f84-62c79b31b59f"), 38 },
                    { -7036L, "CA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Canada", "CA", new Guid("3d3f0f1a-571b-8ebb-f2e1-82a6f8cdfb67"), 37 },
                    { -7035L, "CM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cameroon", "CM", new Guid("a0842837-2e57-cee4-80cf-3736a2043f21"), 36 },
                    { -7034L, "KH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Cambodia", "KH", new Guid("abf92bf5-df28-6a14-a547-5fecdaaf45bd"), 35 },
                    { -7033L, "BI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Burundi", "BI", new Guid("f821e3b8-fbc5-5491-a1e4-28168bfab8b8"), 34 },
                    { -7032L, "BF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Burkina Faso", "BF", new Guid("1bb71f5e-8652-878f-ef0f-e124fbf5d659"), 33 },
                    { -7031L, "BG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bulgaria", "BG", new Guid("94fbfc68-3efd-47fc-4cb3-f9dcdac24d31"), 32 },
                    { -7030L, "BN", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Brunei", "BN", new Guid("4ff347de-75b9-e25b-53ef-67a4a58af95c"), 31 },
                    { -7029L, "VG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "British Virgin Islands", "VG", new Guid("6b808b22-3ae6-bf1e-edc3-590693b37f75"), 30 },
                    { -7028L, "BR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Brazil", "BR", new Guid("3639c8c5-5018-eb39-2dd3-b9ad5d287790"), 29 },
                    { -7027L, "BW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Botswana", "BW", new Guid("8b691019-abd0-a23d-8679-65dcb0a98b65"), 28 },
                    { -7026L, "BA", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bosnia & Herzegovina", "BA", new Guid("e111f756-9145-291e-360e-d06e4d7fb429"), 27 },
                    { -7025L, "BO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bolivia", "BO", new Guid("8943ebd2-bc23-1b64-c031-3f28398efb6d"), 26 },
                    { -7024L, "BT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bhutan", "BT", new Guid("d2219e2c-3d83-d6e2-3189-11b56b7abb04"), 25 },
                    { -7023L, "BM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bermuda", "BM", new Guid("15a98680-9859-c663-0f1b-d659e9d54dd0"), 24 },
                    { -7022L, "BJ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Benin", "BJ", new Guid("85664618-e24e-bfdf-5ca3-665da6822633"), 23 },
                    { -7021L, "BZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Belize", "BZ", new Guid("57a7f3d9-bf2f-28f4-103f-6909846ba75d"), 22 },
                    { -7020L, "BE", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Belgium", "BE", new Guid("ff505ce8-e127-82c5-0f90-721f627c70f2"), 21 },
                    { -7019L, "BY", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Belarus", "BY", new Guid("1249c7c9-ae9a-4a65-5cfb-821e3fb45bba"), 20 },
                    { -7018L, "BB", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Barbados", "BB", new Guid("f6b312b1-a7d1-2cf4-ff53-8726cff0cf64"), 19 },
                    { -7017L, "BD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bangladesh", "BD", new Guid("8dbad947-a9b8-c3bf-b755-5c52dd6cadfa"), 18 },
                    { -7016L, "BH", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bahrain", "BH", new Guid("917e476f-7803-5631-ec88-7e0715073b29"), 17 },
                    { -7015L, "BS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Bahamas", "BS", new Guid("ebd638b9-d7ed-fcb8-2c32-170e09da380d"), 16 },
                    { -7014L, "AZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Azerbaijan", "AZ", new Guid("b480c305-4f92-cbef-41db-96d902b4cb97"), 15 },
                    { -7013L, "AT", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Austria", "AT", new Guid("1a0e768f-ac25-728e-dfce-e5fdb1a4f311"), 14 },
                    { -7012L, "AU", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Australia", "AU", new Guid("93f3885e-c4f5-f8ab-ede0-12b60a9e9cfc"), 13 },
                    { -7011L, "AW", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aruba", "AW", new Guid("3affeadc-f404-2a8b-c578-3f21f8609415"), 12 },
                    { -7010L, "AM", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Armenia", "AM", new Guid("eb440fc9-a8b6-f042-758d-ab67c84ac3ec"), 11 },
                    { -7009L, "AR", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Argentina", "AR", new Guid("3b97a9ae-6d22-54f2-fb78-646e120af0b8"), 10 },
                    { -7008L, "AG", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Antigua & Barbuda", "AG", new Guid("c4fb9808-a455-9e96-a219-ae6535783137"), 9 },
                    { -7007L, "AI", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Anguilla", "AI", new Guid("5879810a-56c5-cbd3-c649-0cc715c4709b"), 8 },
                    { -7006L, "AO", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Angola", "AO", new Guid("fab95c4c-a5db-e7ff-e4c9-5c7e21af0b06"), 7 },
                    { -7005L, "AD", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Andorra", "AD", new Guid("82ee27ea-1466-0637-1784-67408c139f54"), 6 },
                    { -7004L, "AS", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "American Samoa", "AS", new Guid("6c86b297-d55d-62fc-d4d6-3f767625cef8"), 5 },
                    { -7003L, "DZ", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Algeria", "DZ", new Guid("c9513617-54cf-0086-222b-e5110d253ff5"), 4 },
                    { -7002L, "AL", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Albania", "AL", new Guid("7131e6e0-1257-eba1-832d-7d6b9b715ed5"), 3 },
                    { -7001L, "AX", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Aland Islands", "AX", new Guid("d0a9cf43-51b0-4caa-c7ae-cc80188aebfe"), 2 },
                    { -7000L, "AF", new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, new DateTime(2026, 3, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Afghanistan", "AF", new Guid("2930bd1e-9301-5047-e2b2-306f2de35755"), 1 }
                });

            migrationBuilder.Sql(
                """
                UPDATE companies AS company
                SET country_catalog_item_id = country_catalog.id
                FROM country_catalog
                WHERE country_catalog.normalized_code = UPPER(company.country_code);
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM companies
                        WHERE country_catalog_item_id IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Some companies could not be mapped to the country catalog. Review companies.country_code before applying this migration.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "country_catalog_item_id",
                table: "companies",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_companies__country_catalog_item",
                table: "companies",
                column: "country_catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_country_catalog__name",
                table: "country_catalog",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "uq_country_catalog__normalized_code",
                table: "country_catalog",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_country_catalog__public_id",
                table: "country_catalog",
                column: "public_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_companies__country_catalog_item",
                table: "companies",
                column: "country_catalog_item_id",
                principalTable: "country_catalog",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_companies__country_catalog_item",
                table: "companies");

            migrationBuilder.DropTable(
                name: "country_catalog");

            migrationBuilder.DropIndex(
                name: "ix_companies__country_catalog_item",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "country_catalog_item_id",
                table: "companies");
        }
    }
}
