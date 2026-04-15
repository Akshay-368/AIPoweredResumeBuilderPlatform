using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ResumeAI.ATSScore.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateRenderingMetadataAndAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "asset_group_key",
                schema: "projects",
                table: "resume_builder_templates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "projects",
                table: "resume_builder_templates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "preview_thumbnail_base64",
                schema: "projects",
                table: "resume_builder_templates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "render_contract_json",
                schema: "projects",
                table: "resume_builder_templates",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "resume_template_assets",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<string>(type: "text", nullable: false),
                    asset_key = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "text", nullable: false),
                    base64_data = table.Column<string>(type: "text", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_template_assets", x => x.id);
                    table.ForeignKey(
                        name: "FK_resume_template_assets_resume_builder_templates_template_id",
                        column: x => x.template_id,
                        principalSchema: "projects",
                        principalTable: "resume_builder_templates",
                        principalColumn: "template_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                schema: "projects",
                table: "resume_builder_templates",
                keyColumn: "template_id",
                keyValue: "deedy-one-page-two-column",
                columns: new[] { "asset_group_key", "category", "description", "preview_thumbnail_base64", "render_contract_json" },
                values: new object[] { "deedy-default", "professional", "Balanced one-page layout with a strong two-column hierarchy for concise, high-signal resumes.", null, "{\"layoutType\":\"two_column\",\"page\":{\"size\":\"A4\",\"margin\":24},\"columns\":{\"leftRatio\":0.34,\"rightRatio\":0.66,\"gap\":16},\"typography\":{\"fontFamily\":\"Arial\",\"nameSize\":20,\"roleSize\":10,\"sectionTitleSize\":11,\"bodySize\":9,\"smallTextSize\":8},\"colors\":{\"primary\":\"#0f766e\",\"secondary\":\"#111827\",\"muted\":\"#4b5563\"},\"sectionOrder\":{\"left\":[\"summary\",\"skills\",\"education\"],\"right\":[\"experience\",\"projects\"]},\"limits\":{\"maxPages\":1,\"truncateOverflow\":true,\"maxBulletsPerJob\":4,\"maxExperienceItems\":3,\"maxProjectItems\":3}}" });

            migrationBuilder.InsertData(
                schema: "projects",
                table: "resume_builder_templates",
                columns: new[] { "template_id", "asset_group_key", "category", "created_at", "description", "is_active", "is_default", "preview_thumbnail_base64", "render_contract_json", "style_guide_json", "title", "updated_at" },
                values: new object[,]
                {
                    { "jakes-template", "jakes-default", "professional", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Single-column modern professional template with high readability and strong section rhythm.", true, false, null, "{\"layoutType\":\"single_column\",\"page\":{\"size\":\"A4\",\"margin\":28},\"typography\":{\"fontFamily\":\"Arial\",\"nameSize\":22,\"roleSize\":10,\"sectionTitleSize\":12,\"bodySize\":9,\"smallTextSize\":8},\"colors\":{\"primary\":\"#0f172a\",\"secondary\":\"#1f2937\",\"muted\":\"#6b7280\"},\"sectionOrder\":{\"main\":[\"summary\",\"experience\",\"projects\",\"education\",\"skills\"]},\"limits\":{\"maxPages\":1,\"truncateOverflow\":true,\"maxBulletsPerJob\":4,\"maxExperienceItems\":4,\"maxProjectItems\":3}}", "{\"layout\":\"single_column\",\"pageLength\":\"one_page\",\"tone\":\"professional\",\"accentColor\":\"#0f172a\"}", "Jake's Resume Template", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "simple-hipster", "simple-hipster-icons", "creative", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Clean casual layout with icon-assisted contact header and lightweight visual accents.", true, false, null, "{\"layoutType\":\"hipster\",\"page\":{\"size\":\"A4\",\"margin\":26},\"typography\":{\"fontFamily\":\"Arial\",\"nameSize\":21,\"roleSize\":10,\"sectionTitleSize\":11,\"bodySize\":9,\"smallTextSize\":8},\"colors\":{\"primary\":\"#155e75\",\"secondary\":\"#0f172a\",\"muted\":\"#475569\"},\"sectionOrder\":{\"main\":[\"summary\",\"experience\",\"projects\",\"education\",\"skills\"]},\"assets\":{\"iconKeys\":[\"phone\",\"email\",\"linkedin\",\"github\"]},\"limits\":{\"maxPages\":1,\"truncateOverflow\":true,\"maxBulletsPerJob\":3,\"maxExperienceItems\":3,\"maxProjectItems\":3}}", "{\"layout\":\"creative_clean\",\"pageLength\":\"one_page\",\"tone\":\"modern\",\"accentColor\":\"#155e75\"}", "Simple Hipster", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                schema: "projects",
                table: "resume_template_assets",
                columns: new[] { "id", "asset_key", "base64_data", "created_at", "height", "is_active", "mime_type", "template_id", "updated_at", "width" },
                values: new object[,]
                {
                    { new Guid("f702a82e-6a2b-4ddf-a0ad-f7f0535db001"), "phone", "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxNiAxNiI+PGNpcmNsZSBjeD0iOCIgY3k9IjgiIHI9IjciIGZpbGw9IiMwZjc2NmUiLz48cGF0aCBkPSJNNSA0aDJsMSAyLTEgMWMuNSAxIDEuNSAyIDIuNSAyLjVsMS0xIDIgMXYyYy0zLjUuNS03LTMtNy41LTcuNXoiIGZpbGw9IiNmZmYiLz48L3N2Zz4=", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), 16, true, "image/svg+xml", "simple-hipster", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), 16 },
                    { new Guid("f702a82e-6a2b-4ddf-a0ad-f7f0535db002"), "email", "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxNiAxNiI+PHJlY3QgeD0iMSIgeT0iMyIgd2lkdGg9IjE0IiBoZWlnaHQ9IjEwIiByeD0iMiIgZmlsbD0iIzBmNzY2ZSIvPjxwYXRoIGQ9Ik0yIDVsNiA0IDYtNCIgc3Ryb2tlPSIjZmZmIiBzdHJva2Utd2lkdGg9IjEuMiIgZmlsbD0ibm9uZSIvPjwvc3ZnPg==", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), 16, true, "image/svg+xml", "simple-hipster", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), 16 },
                    { new Guid("f702a82e-6a2b-4ddf-a0ad-f7f0535db003"), "linkedin", "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxNiAxNiI+PHJlY3QgeD0iMSIgeT0iMSIgd2lkdGg9IjE0IiBoZWlnaHQ9IjE0IiByeD0iMiIgZmlsbD0iIzBmNzY2ZSIvPjxjaXJjbGUgY3g9IjUiIGN5PSI1IiByPSIxIiBmaWxsPSIjZmZmIi8+PHJlY3QgeD0iNC4yIiB5PSI2LjUiIHdpZHRoPSIxLjYiIGhlaWdodD0iNSIgZmlsbD0iI2ZmZiIvPjxwYXRoIGQ9Ik04IDYuNWgxLjV2LjhjLjMtLjUuOS0uOSAxLjctLjkgMS4zIDAgMiAuOCAyIDIuNHYyLjdoLTEuNlY5LjFjMC0uOC0uMy0xLjItMS0xLjItLjcgMC0xLjEuNS0xLjEgMS4zdjIuM0g4eiIgZmlsbD0iI2ZmZiIvPjwvc3ZnPg==", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), 16, true, "image/svg+xml", "simple-hipster", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), 16 },
                    { new Guid("f702a82e-6a2b-4ddf-a0ad-f7f0535db004"), "github", "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxNiAxNiI+PGNpcmNsZSBjeD0iOCIgY3k9IjgiIHI9IjciIGZpbGw9IiMxMTE4MjciLz48cGF0aCBkPSJNOCAzLjVhNC41IDQuNSAwIDAgMC0xLjQgOC44di0xLjdjLTEuNi4zLTItLjctMi0uNy0uMy0uNy0uOC0uOS0uOC0uOS0uNy0uNSAwLS41IDAtLjUuOC4xIDEuMi44IDEuMi44LjcgMS4xIDEuOC44IDIuMi42LjEtLjUuMy0uOC41LTEtMS4zLS4xLTIuNy0uNy0yLjctMyAwLS43LjItMS4zLjctMS43LS4xLS4yLS4zLS45LjEtMS45IDAgMCAuNi0uMiAxLjkuN2E2LjUgNi41IDAgMCAxIDMuNCAwYzEuMy0uOSAxLjktLjcgMS45LS43LjQgMSAuMiAxLjcuMSAxLjkuNC41LjcgMSAuNyAxLjcgMCAyLjMtMS40IDIuOS0yLjcgMyAuMy4yLjUuNy41IDEuM3YxLjlBNC41IDQuNSAwIDAgMCA4IDMuNXoiIGZpbGw9IiNmZmYiLz48L3N2Zz4=", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), 16, true, "image/svg+xml", "simple-hipster", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), 16 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_resume_template_assets_template_id_asset_key",
                schema: "projects",
                table: "resume_template_assets",
                columns: new[] { "template_id", "asset_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resume_template_assets",
                schema: "projects");

            migrationBuilder.DeleteData(
                schema: "projects",
                table: "resume_builder_templates",
                keyColumn: "template_id",
                keyValue: "jakes-template");

            migrationBuilder.DeleteData(
                schema: "projects",
                table: "resume_builder_templates",
                keyColumn: "template_id",
                keyValue: "simple-hipster");

            migrationBuilder.DropColumn(
                name: "asset_group_key",
                schema: "projects",
                table: "resume_builder_templates");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "projects",
                table: "resume_builder_templates");

            migrationBuilder.DropColumn(
                name: "preview_thumbnail_base64",
                schema: "projects",
                table: "resume_builder_templates");

            migrationBuilder.DropColumn(
                name: "render_contract_json",
                schema: "projects",
                table: "resume_builder_templates");

            migrationBuilder.UpdateData(
                schema: "projects",
                table: "resume_builder_templates",
                keyColumn: "template_id",
                keyValue: "deedy-one-page-two-column",
                column: "description",
                value: "Balanced one-page layout with a clean two-column structure for concise, high-signal resumes.");
        }
    }
}
