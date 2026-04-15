using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResumeAI.ATSScore.API.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeBuilderWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resume_builder_templates",
                schema: "projects",
                columns: table => new
                {
                    template_id = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    style_guide_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_builder_templates", x => x.template_id);
                });

            migrationBuilder.CreateTable(
                name: "resume_builder_artifacts",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<string>(type: "text", nullable: false),
                    builder_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    generated_resume_json = table.Column<string>(type: "jsonb", nullable: false),
                    generation_model = table.Column<string>(type: "text", nullable: false),
                    last_change_request = table.Column<string>(type: "text", nullable: true),
                    revision_count = table.Column<int>(type: "integer", nullable: false),
                    is_finalized = table.Column<bool>(type: "boolean", nullable: false),
                    finalized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_builder_artifacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_resume_builder_artifacts_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resume_builder_artifacts_resume_builder_templates_template_~",
                        column: x => x.template_id,
                        principalSchema: "projects",
                        principalTable: "resume_builder_templates",
                        principalColumn: "template_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resume_pdf_exports",
                schema: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    template_id = table.Column<string>(type: "text", nullable: false),
                    render_options_json = table.Column<string>(type: "jsonb", nullable: false),
                    pdf_bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: true),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_pdf_exports", x => x.id);
                    table.ForeignKey(
                        name: "FK_resume_pdf_exports_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resume_pdf_exports_resume_builder_artifacts_artifact_id",
                        column: x => x.artifact_id,
                        principalSchema: "projects",
                        principalTable: "resume_builder_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                schema: "projects",
                table: "resume_builder_templates",
                columns: new[] { "template_id", "created_at", "description", "is_active", "is_default", "style_guide_json", "title", "updated_at" },
                values: new object[] { "deedy-one-page-two-column", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc), "Balanced one-page layout with a clean two-column structure for concise, high-signal resumes.", true, true, "{\"layout\":\"two_column\",\"pageLength\":\"one_page\",\"tone\":\"professional\",\"accentColor\":\"#0f766e\",\"sectionOrder\":[\"summary\",\"skills\",\"experience\",\"projects\",\"education\"]}", "Deedy - One Page Two Column Resume", new DateTime(2026, 4, 13, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_resume_builder_artifacts_project_id",
                schema: "projects",
                table: "resume_builder_artifacts",
                column: "project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resume_builder_artifacts_template_id",
                schema: "projects",
                table: "resume_builder_artifacts",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_resume_builder_templates_is_default",
                schema: "projects",
                table: "resume_builder_templates",
                column: "is_default");

            migrationBuilder.CreateIndex(
                name: "IX_resume_pdf_exports_artifact_id",
                schema: "projects",
                table: "resume_pdf_exports",
                column: "artifact_id");

            migrationBuilder.CreateIndex(
                name: "IX_resume_pdf_exports_project_id",
                schema: "projects",
                table: "resume_pdf_exports",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resume_pdf_exports",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "resume_builder_artifacts",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "resume_builder_templates",
                schema: "projects");
        }
    }
}
