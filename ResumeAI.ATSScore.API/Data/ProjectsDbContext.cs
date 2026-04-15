using Microsoft.EntityFrameworkCore;
using ResumeAI.ATSScore.API.Persistence;

namespace ResumeAI.ATSScore.API.Data;

public class ProjectsDbContext : DbContext
{
    public ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : base(options)
    {
    }

    public DbSet<ResumeBuilderTemplateEntity> ResumeBuilderTemplates => Set<ResumeBuilderTemplateEntity>();
    public DbSet<ResumeTemplateAssetEntity> ResumeTemplateAssets => Set<ResumeTemplateAssetEntity>();
    public DbSet<ResumeBuilderArtifactEntity> ResumeBuilderArtifacts => Set<ResumeBuilderArtifactEntity>();
    public DbSet<ResumePdfExportEntity> ResumePdfExports => Set<ResumePdfExportEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<ResumeArtifactEntity> ResumeArtifacts => Set<ResumeArtifactEntity>();
    public DbSet<JobDescriptionArtifactEntity> JobDescriptionArtifacts => Set<JobDescriptionArtifactEntity>();
    public DbSet<AtsResultEntity> AtsResults => Set<AtsResultEntity>();
    public DbSet<WizardStateEntity> WizardStates => Set<WizardStateEntity>();
    public DbSet<UserResumePreferenceEntity> UserResumePreferences => Set<UserResumePreferenceEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<NotificationUserStateEntity> NotificationUserStates => Set<NotificationUserStateEntity>();
    public DbSet<FeedbackEntity> Feedback => Set<FeedbackEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("projects");

        modelBuilder.Entity<ResumeBuilderTemplateEntity>(entity =>
        {
            entity.ToTable("resume_builder_templates");
            entity.HasKey(x => x.TemplateId);
            entity.Property(x => x.TemplateId).HasColumnName("template_id");
            entity.Property(x => x.Title).HasColumnName("title");
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.Category).HasColumnName("category");
            entity.Property(x => x.PreviewThumbnailBase64).HasColumnName("preview_thumbnail_base64");
            entity.Property(x => x.AssetGroupKey).HasColumnName("asset_group_key");
            entity.Property(x => x.RenderContractJson).HasColumnName("render_contract_json").HasColumnType("jsonb");
            entity.Property(x => x.StyleGuideJson).HasColumnName("style_guide_json").HasColumnType("jsonb");
            entity.Property(x => x.IsDefault).HasColumnName("is_default");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.IsDefault);
            entity.HasData(
                new ResumeBuilderTemplateEntity
                {
                    TemplateId = "deedy-one-page-two-column",
                    Title = "Deedy - One Page Two Column Resume",
                    Description = "Balanced one-page layout with a strong two-column hierarchy for concise, high-signal resumes.",
                    Category = "professional",
                    AssetGroupKey = "deedy-default",
                    RenderContractJson = "{\"layoutType\":\"two_column\",\"page\":{\"size\":\"A4\",\"margin\":24},\"columns\":{\"leftRatio\":0.34,\"rightRatio\":0.66,\"gap\":16},\"typography\":{\"fontFamily\":\"Arial\",\"nameSize\":20,\"roleSize\":10,\"sectionTitleSize\":11,\"bodySize\":9,\"smallTextSize\":8},\"colors\":{\"primary\":\"#0f766e\",\"secondary\":\"#111827\",\"muted\":\"#4b5563\"},\"sectionOrder\":{\"left\":[\"summary\",\"skills\",\"education\"],\"right\":[\"experience\",\"projects\"]},\"limits\":{\"maxPages\":1,\"truncateOverflow\":true,\"maxBulletsPerJob\":4,\"maxExperienceItems\":3,\"maxProjectItems\":3}}",
                    StyleGuideJson = "{\"layout\":\"two_column\",\"pageLength\":\"one_page\",\"tone\":\"professional\",\"accentColor\":\"#0f766e\",\"sectionOrder\":[\"summary\",\"skills\",\"experience\",\"projects\",\"education\"]}",
                    IsDefault = true,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
                },
                new ResumeBuilderTemplateEntity
                {
                    TemplateId = "jakes-template",
                    Title = "Jake's Resume Template",
                    Description = "Single-column modern professional template with high readability and strong section rhythm.",
                    Category = "professional",
                    AssetGroupKey = "jakes-default",
                    RenderContractJson = "{\"layoutType\":\"single_column\",\"page\":{\"size\":\"A4\",\"margin\":28},\"typography\":{\"fontFamily\":\"Arial\",\"nameSize\":22,\"roleSize\":10,\"sectionTitleSize\":12,\"bodySize\":9,\"smallTextSize\":8},\"colors\":{\"primary\":\"#0f172a\",\"secondary\":\"#1f2937\",\"muted\":\"#6b7280\"},\"sectionOrder\":{\"main\":[\"summary\",\"experience\",\"projects\",\"education\",\"skills\"]},\"limits\":{\"maxPages\":1,\"truncateOverflow\":true,\"maxBulletsPerJob\":4,\"maxExperienceItems\":4,\"maxProjectItems\":3}}",
                    StyleGuideJson = "{\"layout\":\"single_column\",\"pageLength\":\"one_page\",\"tone\":\"professional\",\"accentColor\":\"#0f172a\"}",
                    IsDefault = false,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
                },
                new ResumeBuilderTemplateEntity
                {
                    TemplateId = "simple-hipster",
                    Title = "Simple Hipster",
                    Description = "Clean casual layout with icon-assisted contact header and lightweight visual accents.",
                    Category = "creative",
                    AssetGroupKey = "simple-hipster-icons",
                    RenderContractJson = "{\"layoutType\":\"hipster\",\"page\":{\"size\":\"A4\",\"margin\":26},\"typography\":{\"fontFamily\":\"Arial\",\"nameSize\":21,\"roleSize\":10,\"sectionTitleSize\":11,\"bodySize\":9,\"smallTextSize\":8},\"colors\":{\"primary\":\"#155e75\",\"secondary\":\"#0f172a\",\"muted\":\"#475569\"},\"sectionOrder\":{\"main\":[\"summary\",\"experience\",\"projects\",\"education\",\"skills\"]},\"assets\":{\"iconKeys\":[\"phone\",\"email\",\"linkedin\",\"github\"]},\"limits\":{\"maxPages\":1,\"truncateOverflow\":true,\"maxBulletsPerJob\":3,\"maxExperienceItems\":3,\"maxProjectItems\":3}}",
                    StyleGuideJson = "{\"layout\":\"creative_clean\",\"pageLength\":\"one_page\",\"tone\":\"modern\",\"accentColor\":\"#155e75\"}",
                    IsDefault = false,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        modelBuilder.Entity<ResumeTemplateAssetEntity>(entity =>
        {
            entity.ToTable("resume_template_assets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TemplateId).HasColumnName("template_id");
            entity.Property(x => x.AssetKey).HasColumnName("asset_key");
            entity.Property(x => x.MimeType).HasColumnName("mime_type");
            entity.Property(x => x.Base64Data).HasColumnName("base64_data");
            entity.Property(x => x.Width).HasColumnName("width");
            entity.Property(x => x.Height).HasColumnName("height");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => new { x.TemplateId, x.AssetKey }).IsUnique();
            entity.HasOne(x => x.Template)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                new ResumeTemplateAssetEntity
                {
                    Id = new Guid("f702a82e-6a2b-4ddf-a0ad-f7f0535db001"),
                    TemplateId = "simple-hipster",
                    AssetKey = "phone",
                    MimeType = "image/svg+xml",
                    Base64Data = "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxNiAxNiI+PGNpcmNsZSBjeD0iOCIgY3k9IjgiIHI9IjciIGZpbGw9IiMwZjc2NmUiLz48cGF0aCBkPSJNNSA0aDJsMSAyLTEgMWMuNSAxIDEuNSAyIDIuNSAyLjVsMS0xIDIgMXYyYy0zLjUuNS03LTMtNy41LTcuNXoiIGZpbGw9IiNmZmYiLz48L3N2Zz4=",
                    Width = 16,
                    Height = 16,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
                },
                new ResumeTemplateAssetEntity
                {
                    Id = new Guid("f702a82e-6a2b-4ddf-a0ad-f7f0535db002"),
                    TemplateId = "simple-hipster",
                    AssetKey = "email",
                    MimeType = "image/svg+xml",
                    Base64Data = "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxNiAxNiI+PHJlY3QgeD0iMSIgeT0iMyIgd2lkdGg9IjE0IiBoZWlnaHQ9IjEwIiByeD0iMiIgZmlsbD0iIzBmNzY2ZSIvPjxwYXRoIGQ9Ik0yIDVsNiA0IDYtNCIgc3Ryb2tlPSIjZmZmIiBzdHJva2Utd2lkdGg9IjEuMiIgZmlsbD0ibm9uZSIvPjwvc3ZnPg==",
                    Width = 16,
                    Height = 16,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
                },
                new ResumeTemplateAssetEntity
                {
                    Id = new Guid("f702a82e-6a2b-4ddf-a0ad-f7f0535db003"),
                    TemplateId = "simple-hipster",
                    AssetKey = "linkedin",
                    MimeType = "image/svg+xml",
                    Base64Data = "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxNiAxNiI+PHJlY3QgeD0iMSIgeT0iMSIgd2lkdGg9IjE0IiBoZWlnaHQ9IjE0IiByeD0iMiIgZmlsbD0iIzBmNzY2ZSIvPjxjaXJjbGUgY3g9IjUiIGN5PSI1IiByPSIxIiBmaWxsPSIjZmZmIi8+PHJlY3QgeD0iNC4yIiB5PSI2LjUiIHdpZHRoPSIxLjYiIGhlaWdodD0iNSIgZmlsbD0iI2ZmZiIvPjxwYXRoIGQ9Ik04IDYuNWgxLjV2LjhjLjMtLjUuOS0uOSAxLjctLjkgMS4zIDAgMiAuOCAyIDIuNHYyLjdoLTEuNlY5LjFjMC0uOC0uMy0xLjItMS0xLjItLjcgMC0xLjEuNS0xLjEgMS4zdjIuM0g4eiIgZmlsbD0iI2ZmZiIvPjwvc3ZnPg==",
                    Width = 16,
                    Height = 16,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
                },
                new ResumeTemplateAssetEntity
                {
                    Id = new Guid("f702a82e-6a2b-4ddf-a0ad-f7f0535db004"),
                    TemplateId = "simple-hipster",
                    AssetKey = "github",
                    MimeType = "image/svg+xml",
                    Base64Data = "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxNiAxNiI+PGNpcmNsZSBjeD0iOCIgY3k9IjgiIHI9IjciIGZpbGw9IiMxMTE4MjciLz48cGF0aCBkPSJNOCAzLjVhNC41IDQuNSAwIDAgMC0xLjQgOC44di0xLjdjLTEuNi4zLTItLjctMi0uNy0uMy0uNy0uOC0uOS0uOC0uOS0uNy0uNSAwLS41IDAtLjUuOC4xIDEuMi44IDEuMi44LjcgMS4xIDEuOC44IDIuMi42LjEtLjUuMy0uOC41LTEtMS4zLS4xLTIuNy0uNy0yLjctMyAwLS43LjItMS4zLjctMS43LS4xLS4yLS4zLS45LjEtMS45IDAgMCAuNi0uMiAxLjkuN2E2LjUgNi41IDAgMCAxIDMuNCAwYzEuMy0uOSAxLjktLjcgMS45LS43LjQgMSAuMiAxLjcuMSAxLjkuNC41LjcgMSAuNyAxLjcgMCAyLjMtMS40IDIuOS0yLjcgMyAuMy4yLjUuNy41IDEuM3YxLjlBNC41IDQuNSAwIDAgMCA4IDMuNXoiIGZpbGw9IiNmZmYiLz48L3N2Zz4=",
                    Width = 16,
                    Height = 16,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        modelBuilder.Entity<ResumeBuilderArtifactEntity>(entity =>
        {
            entity.ToTable("resume_builder_artifacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.TemplateId).HasColumnName("template_id");
            entity.Property(x => x.BuilderSnapshotJson).HasColumnName("builder_snapshot_json").HasColumnType("jsonb");
            entity.Property(x => x.GeneratedResumeJson).HasColumnName("generated_resume_json").HasColumnType("jsonb");
            entity.Property(x => x.GenerationModel).HasColumnName("generation_model");
            entity.Property(x => x.LastChangeRequest).HasColumnName("last_change_request");
            entity.Property(x => x.RevisionCount).HasColumnName("revision_count");
            entity.Property(x => x.IsFinalized).HasColumnName("is_finalized");
            entity.Property(x => x.FinalizedAt).HasColumnName("finalized_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.HasIndex(x => x.ProjectId).IsUnique();
            entity.HasOne(x => x.Project).WithOne(x => x.ResumeBuilderArtifact).HasForeignKey<ResumeBuilderArtifactEntity>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Template).WithMany(x => x.Artifacts).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ResumePdfExportEntity>(entity =>
        {
            entity.ToTable("resume_pdf_exports");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.ArtifactId).HasColumnName("artifact_id");
            entity.Property(x => x.TemplateId).HasColumnName("template_id");
            entity.Property(x => x.RenderOptionsJson).HasColumnName("render_options_json").HasColumnType("jsonb");
            entity.Property(x => x.PdfBytes).HasColumnName("pdf_bytes").HasColumnType("bytea");
            entity.Property(x => x.Sha256).HasColumnName("sha256");
            entity.Property(x => x.FileName).HasColumnName("file_name");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.HasIndex(x => x.ProjectId);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Artifact).WithMany(x => x.PdfExports).HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.SetNull);
        });

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

        modelBuilder.Entity<UserResumePreferenceEntity>(entity =>
        {
            entity.ToTable("user_resume_preferences");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.DefaultResumeRefType).HasColumnName("default_resume_ref_type");
            entity.Property(x => x.DefaultResumeRefId).HasColumnName("default_resume_ref_id");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.SenderUserId).HasColumnName("sender_user_id");
            entity.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id");
            entity.Property(x => x.Subject).HasColumnName("subject");
            entity.Property(x => x.Body).HasColumnName("body");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.HasIndex(x => x.SenderUserId);
            entity.HasIndex(x => x.RecipientUserId);
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<NotificationUserStateEntity>(entity =>
        {
            entity.ToTable("notification_user_states");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.NotificationId).HasColumnName("notification_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.IsRead).HasColumnName("is_read");
            entity.Property(x => x.ReadAt).HasColumnName("read_at");
            entity.Property(x => x.IsDeletedForUser).HasColumnName("is_deleted_for_user");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => new { x.NotificationId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.Notification)
                .WithMany(x => x.UserStates)
                .HasForeignKey(x => x.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FeedbackEntity>(entity =>
        {
            entity.ToTable("feedback");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Rating).HasColumnName("rating");
            entity.Property(x => x.FeedbackText).HasColumnName("feedback_text");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.CreatedAt);
        });

        base.OnModelCreating(modelBuilder);
    }
}
