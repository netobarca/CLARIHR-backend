using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserPreferenceLanguageAndBackfillBaseLanguageCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "locale",
                table: "user_preferences",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7247L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7246L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7245L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7244L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7243L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7242L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7241L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7240L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7239L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7238L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7237L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7236L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7235L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7234L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7233L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7232L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7231L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7230L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7229L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7228L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7227L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7226L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7225L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7224L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7223L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7222L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7221L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7220L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7219L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7218L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7217L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7216L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7215L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7214L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7213L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7212L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7211L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7210L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7209L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7208L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7207L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7206L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7205L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7204L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7203L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7202L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7201L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7200L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7199L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7198L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7197L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7196L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7195L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7194L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7193L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7192L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7191L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7190L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7189L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7188L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7187L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7186L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7185L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7184L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7183L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7182L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7181L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7180L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7179L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7178L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7177L,
                column: "default_locale",
                value: "pt");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7176L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7175L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7174L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7173L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7172L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7171L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7170L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7169L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7168L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7167L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7166L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7165L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7164L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7163L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7162L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7161L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7160L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7159L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7158L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7157L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7156L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7155L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7154L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7153L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7152L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7151L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7150L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7149L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7148L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7147L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7146L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7145L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7144L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7143L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7142L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7141L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7140L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7139L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7138L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7137L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7136L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7135L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7134L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7133L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7132L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7131L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7130L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7129L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7128L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7127L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7126L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7125L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7124L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7123L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7122L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7121L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7120L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7119L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7118L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7117L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7116L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7115L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7114L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7113L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7112L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7111L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7110L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7109L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7108L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7107L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7106L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7105L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7104L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7103L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7102L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7101L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7100L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7099L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7098L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7097L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7096L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7095L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7094L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7093L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7092L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7091L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7090L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7089L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7088L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7087L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7086L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7085L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7084L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7083L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7082L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7081L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7080L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7079L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7078L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7077L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7076L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7075L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7074L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7073L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7072L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7071L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7070L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7069L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7068L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7067L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7066L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7065L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7064L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7063L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7062L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7061L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7060L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7059L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7058L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7057L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7056L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7055L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7054L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7053L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7052L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7051L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7050L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7049L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7048L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7047L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7046L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7045L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7044L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7043L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7042L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7041L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7040L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7039L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7038L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7037L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7036L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7035L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7034L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7033L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7032L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7031L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7030L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7029L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7028L,
                column: "default_locale",
                value: "pt");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7027L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7026L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7025L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7024L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7023L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7022L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7021L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7020L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7019L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7018L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7017L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7016L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7015L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7014L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7013L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7012L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7011L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7010L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7009L,
                column: "default_locale",
                value: "es");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7008L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7007L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7006L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7005L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7004L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7003L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7002L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7001L,
                column: "default_locale",
                value: "en");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7000L,
                column: "default_locale",
                value: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "locale",
                table: "user_preferences",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7247L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7246L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7245L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7244L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7243L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7242L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7241L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7240L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7239L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7238L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7237L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7236L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7235L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7234L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7233L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7232L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7231L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7230L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7229L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7228L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7227L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7226L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7225L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7224L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7223L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7222L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7221L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7220L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7219L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7218L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7217L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7216L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7215L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7214L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7213L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7212L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7211L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7210L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7209L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7208L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7207L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7206L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7205L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7204L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7203L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7202L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7201L,
                column: "default_locale",
                value: "es-ES");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7200L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7199L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7198L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7197L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7196L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7195L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7194L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7193L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7192L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7191L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7190L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7189L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7188L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7187L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7186L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7185L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7184L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7183L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7182L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7181L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7180L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7179L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7178L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7177L,
                column: "default_locale",
                value: "pt-PT");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7176L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7175L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7174L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7173L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7172L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7171L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7170L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7169L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7168L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7167L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7166L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7165L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7164L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7163L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7162L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7161L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7160L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7159L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7158L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7157L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7156L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7155L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7154L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7153L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7152L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7151L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7150L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7149L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7148L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7147L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7146L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7145L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7144L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7143L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7142L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7141L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7140L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7139L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7138L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7137L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7136L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7135L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7134L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7133L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7132L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7131L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7130L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7129L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7128L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7127L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7126L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7125L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7124L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7123L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7122L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7121L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7120L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7119L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7118L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7117L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7116L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7115L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7114L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7113L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7112L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7111L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7110L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7109L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7108L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7107L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7106L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7105L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7104L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7103L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7102L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7101L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7100L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7099L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7098L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7097L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7096L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7095L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7094L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7093L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7092L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7091L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7090L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7089L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7088L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7087L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7086L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7085L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7084L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7083L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7082L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7081L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7080L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7079L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7078L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7077L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7076L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7075L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7074L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7073L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7072L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7071L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7070L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7069L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7068L,
                column: "default_locale",
                value: "es-SV");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7067L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7066L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7065L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7064L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7063L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7062L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7061L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7060L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7059L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7058L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7057L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7056L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7055L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7054L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7053L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7052L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7051L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7050L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7049L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7048L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7047L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7046L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7045L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7044L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7043L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7042L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7041L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7040L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7039L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7038L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7037L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7036L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7035L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7034L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7033L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7032L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7031L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7030L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7029L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7028L,
                column: "default_locale",
                value: "pt-BR");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7027L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7026L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7025L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7024L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7023L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7022L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7021L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7020L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7019L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7018L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7017L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7016L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7015L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7014L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7013L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7012L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7011L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7010L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7009L,
                column: "default_locale",
                value: "es-419");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7008L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7007L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7006L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7005L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7004L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7003L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7002L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7001L,
                column: "default_locale",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "country_catalog",
                keyColumn: "id",
                keyValue: -7000L,
                column: "default_locale",
                value: "en-US");
        }
    }
}
