using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ResumeAI.ATSScore.API.Data;

#nullable disable

namespace ResumeAI.ATSScore.API.Migrations;

[DbContext(typeof(ProjectsDbContext))]
[Migration("20260411190000_AddSoftDeleteColumns")]
public partial class AddSoftDeleteColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_deleted",
            schema: "projects",
            table: "projects",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "is_deleted",
            schema: "projects",
            table: "resume_artifacts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "is_deleted",
            schema: "projects",
            table: "job_description_artifacts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "is_deleted",
            schema: "projects",
            table: "ats_results",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "is_deleted",
            schema: "projects",
            table: "wizard_state",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "is_deleted",
            schema: "projects",
            table: "projects");

        migrationBuilder.DropColumn(
            name: "is_deleted",
            schema: "projects",
            table: "resume_artifacts");

        migrationBuilder.DropColumn(
            name: "is_deleted",
            schema: "projects",
            table: "job_description_artifacts");

        migrationBuilder.DropColumn(
            name: "is_deleted",
            schema: "projects",
            table: "ats_results");

        migrationBuilder.DropColumn(
            name: "is_deleted",
            schema: "projects",
            table: "wizard_state");
    }
}
