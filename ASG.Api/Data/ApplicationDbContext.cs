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

        // DbSet 定义
        public DbSet<Team> Teams { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<GameRole> GameRoles { get; set; }

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

                // 配置与 Team 的关系
                entity.HasOne(u => u.OwnedTeam)
                    .WithOne(t => t.Owner)
                    .HasForeignKey<User>(u => u.TeamId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.ToTable("Users");
            });

            // Configure Team entity
            builder.Entity<Team>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Password)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.Description)
                    .HasMaxLength(1000);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("datetime('now')");

                // 配置与 Player 的一对多关系
                entity.HasMany(t => t.Players)
                    .WithOne(p => p.Team)
                    .HasForeignKey(p => p.TeamId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable("Teams");
            });

            // Configure Player entity
            builder.Entity<Player>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.GameId)
                    .HasMaxLength(100);

                entity.Property(e => e.GameRank)
                    .HasMaxLength(50);

                entity.Property(e => e.Description)
                    .HasMaxLength(500);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.ToTable("Players");
            });

            // Configure GameRole entity
            builder.Entity<GameRole>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.GameId)
                    .HasMaxLength(100);

                entity.Property(e => e.GameRank)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Description)
                    .HasMaxLength(500);

                // 自定义字段配置
                entity.Property(e => e.CustomField1)
                    .HasMaxLength(200);

                entity.Property(e => e.CustomField2)
                    .HasMaxLength(200);

                entity.Property(e => e.CustomField3)
                    .HasMaxLength(200);

                entity.Property(e => e.CustomTextArea)
                    .HasMaxLength(500);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.ToTable("GameRoles");
            });
        }
    }
}