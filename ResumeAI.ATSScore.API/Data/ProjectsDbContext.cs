using Microsoft.EntityFrameworkCore;
using ResumeAI.ATSScore.API.Persistence;

namespace ResumeAI.ATSScore.API.Data;

public class ProjectsDbContext : DbContext
{
    public ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : base(options)
    {
    }

    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<ResumeArtifactEntity> ResumeArtifacts => Set<ResumeArtifactEntity>();
    public DbSet<JobDescriptionArtifactEntity> JobDescriptionArtifacts => Set<JobDescriptionArtifactEntity>();
    public DbSet<AtsResultEntity> AtsResults => Set<AtsResultEntity>();
    public DbSet<WizardStateEntity> WizardStates => Set<WizardStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("projects");

        modelBuilder.Entity<ProjectEntity>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.ProjectId);
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Type).HasColumnName("type");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.CurrentStep).HasColumnName("current_step");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.UserId, x.UpdatedAt });
        });

        modelBuilder.Entity<ResumeArtifactEntity>(entity =>
        {
            entity.ToTable("resume_artifacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.RawText).HasColumnName("raw_text");
            entity.Property(x => x.ParsedResumeJson).HasColumnName("parsed_resume_json").HasColumnType("jsonb");
            entity.Property(x => x.SourceType).HasColumnName("source_type");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.HasIndex(x => x.ProjectId).IsUnique();
            entity.HasOne(x => x.Project).WithOne(x => x.ResumeArtifact).HasForeignKey<ResumeArtifactEntity>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobDescriptionArtifactEntity>(entity =>
        {
            entity.ToTable("job_description_artifacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.RawText).HasColumnName("raw_text");
            entity.Property(x => x.ParsedJdJson).HasColumnName("parsed_jd_json").HasColumnType("jsonb");
            entity.Property(x => x.SourceType).HasColumnName("source_type");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.HasIndex(x => x.ProjectId).IsUnique();
            entity.HasOne(x => x.Project).WithOne(x => x.JobDescriptionArtifact).HasForeignKey<JobDescriptionArtifactEntity>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AtsResultEntity>(entity =>
        {
            entity.ToTable("ats_results");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.JobRole).HasColumnName("job_role");
            entity.Property(x => x.CustomRole).HasColumnName("custom_role");
            entity.Property(x => x.AtsResultJson).HasColumnName("ats_result_json").HasColumnType("jsonb");
            entity.Property(x => x.OverallScore).HasColumnName("overall_score");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAt });
            entity.HasOne(x => x.Project).WithMany(x => x.AtsResults).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WizardStateEntity>(entity =>
        {
            entity.ToTable("wizard_state");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.Module).HasColumnName("module");
            entity.Property(x => x.CurrentStep).HasColumnName("current_step");
            entity.Property(x => x.StateJson).HasColumnName("state_json").HasColumnType("jsonb");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.HasIndex(x => x.ProjectId).IsUnique();
            entity.HasOne(x => x.Project).WithOne(x => x.WizardState).HasForeignKey<WizardStateEntity>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
