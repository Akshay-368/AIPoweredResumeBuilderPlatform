using Microsoft.EntityFrameworkCore;
using ResumeAI.Auth.API.Models;
/*
For creating the database with the help of migrations :
1. Add a migration:
   dotnet ef migrations add InitialCreate -p ResumeAI.Auth.API -s ResumeAI.Auth.API
2. Update the database:
   dotnet ef database update -p ResumeAI.Auth.API -s ResumeAI.Auth.API
where -p specifies the project containing the DbContext and -s specifies the startup project. If both are the same, you can omit -s.

I used these commands :
From the root of the solution:
dotnet ef migrations add UpdatedUserTableWithTokensAndIsActive --project ResumeAI.Auth.API
dotnet ef database update --project ResumeAI.Auth.API
*/

namespace ResumeAI.Auth.API.Data;

/// <summary>
/// EF Core database context for authentication and user identity data.
/// </summary>
public class AuthDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="AuthDbContext"/>.
    /// </summary>
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    /// <summary>
    /// Application users.
    /// </summary>
    public DbSet<User> Users { get; set; }

    /// <summary>
    /// Configures entity mappings and constraints for the model.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Enforce unique email addresses to prevent duplicate identities.
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.PhoneNumber).IsUnique();
    }
}