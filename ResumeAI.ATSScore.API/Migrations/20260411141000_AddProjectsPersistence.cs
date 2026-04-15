using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResumeAI.ATSScore.API.Migrations;

public partial class AddProjectsPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "projects");

        migrationBuilder.CreateTable(
            name: "projects",
            schema: "projects",
            columns: table => new
            {
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<int>(type: "integer", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                type = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                current_step = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_projects", x => x.project_id);
            });

        migrationBuilder.CreateTable(
            name: "ats_results",
            schema: "projects",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                job_role = table.Column<string>(type: "text", nullable: false),
                custom_role = table.Column<string>(type: "text", nullable: true),
                ats_result_json = table.Column<string>(type: "jsonb", nullable: false),
                overall_score = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ats_results", x => x.id);
                table.ForeignKey(
                    name: "FK_ats_results_projects_project_id",
                    column: x => x.project_id,
                    principalSchema: "projects",
                    principalTable: "projects",
                    principalColumn: "project_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "job_description_artifacts",
            schema: "projects",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                raw_text = table.Column<string>(type: "text", nullable: true),
                parsed_jd_json = table.Column<string>(type: "jsonb", nullable: false),
                source_type = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_job_description_artifacts", x => x.id);
                table.ForeignKey(
                    name: "FK_job_description_artifacts_projects_project_id",
                    column: x => x.project_id,
                    principalSchema: "projects",
                    principalTable: "projects",
                    principalColumn: "project_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "resume_artifacts",
            schema: "projects",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                raw_text = table.Column<string>(type: "text", nullable: true),
                parsed_resume_json = table.Column<string>(type: "jsonb", nullable: false),
                source_type = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_resume_artifacts", x => x.id);
                table.ForeignKey(
                    name: "FK_resume_artifacts_projects_project_id",
                    column: x => x.project_id,
                    principalSchema: "projects",
                    principalTable: "projects",
                    principalColumn: "project_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "wizard_state",
            schema: "projects",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                module = table.Column<string>(type: "text", nullable: false),
                current_step = table.Column<int>(type: "integer", nullable: false),
                state_json = table.Column<string>(type: "jsonb", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_wizard_state", x => x.id);
                table.ForeignKey(
                    name: "FK_wizard_state_projects_project_id",
                    column: x => x.project_id,
                    principalSchema: "projects",
                    principalTable: "projects",
                    principalColumn: "project_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ats_results_project_id_created_at",
            schema: "projects",
            table: "ats_results",
            columns: new[] { "project_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "IX_job_description_artifacts_project_id",
            schema: "projects",
            table: "job_description_artifacts",
            column: "project_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_projects_user_id",
            schema: "projects",
            table: "projects",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_projects_user_id_updated_at",
            schema: "projects",
            table: "projects",
            columns: new[] { "user_id", "updated_at" });

        migrationBuilder.CreateIndex(
            name: "IX_resume_artifacts_project_id",
            schema: "projects",
            table: "resume_artifacts",
            column: "project_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_wizard_state_project_id",
            schema: "projects",
            table: "wizard_state",
            column: "project_id",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ats_results", schema: "projects");
        migrationBuilder.DropTable(name: "job_description_artifacts", schema: "projects");
        migrationBuilder.DropTable(name: "resume_artifacts", schema: "projects");
        migrationBuilder.DropTable(name: "wizard_state", schema: "projects");
        migrationBuilder.DropTable(name: "projects", schema: "projects");
    }
}
