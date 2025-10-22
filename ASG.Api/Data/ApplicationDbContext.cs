using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ASG.Api.Models;

namespace ASG.Api.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure User entity
            builder.Entity<User>(entity =>
            {
                entity.Property(e => e.FirstName)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.LastName)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Role)
                    .HasConversion<string>()
                    .HasDefaultValue(UserRole.User)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.Property(e => e.IsActive)
                    .HasDefaultValue(true);

                entity.ToTable("Users");
            });
        }
    }
}