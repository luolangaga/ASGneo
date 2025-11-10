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
    public DbSet<Event> Events { get; set; }
    public DbSet<TeamEvent> TeamEvents { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<Article> Articles { get; set; }
    public DbSet<Comment> Comments { get; set; }

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
                    .HasConversion(
                        v => v.ToString(),
                        v => (UserRole)Enum.Parse(typeof(UserRole), v))
                    .HasDefaultValue(UserRole.User)
                    .HasSentinel(UserRole.None)
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

            // Configure Event entity
            builder.Entity<Event>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Description)
                    .HasMaxLength(1000);

                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("datetime('now')");

                // 冠军战队关系（可为空；冠军删除或战队删除时不影响赛事）
                entity.HasOne(e => e.ChampionTeam)
                    .WithMany()
                    .HasForeignKey(e => e.ChampionTeamId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.ToTable("Events");
            });

            // Configure Match entity
            builder.Entity<Match>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.MatchTime)
                    .IsRequired();

                entity.Property(e => e.LiveLink)
                    .HasMaxLength(500);

                entity.Property(e => e.CustomData)
                    .IsRequired()
                    .HasDefaultValue("{}");

                entity.Property(e => e.Commentator)
                    .HasMaxLength(100);

                entity.Property(e => e.Director)
                    .HasMaxLength(100);

                entity.Property(e => e.Referee)
                    .HasMaxLength(100);

                entity.Property(e => e.Likes)
                    .HasDefaultValue(0);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("datetime('now')");

                // Relationships
                entity.HasOne(m => m.HomeTeam)
                    .WithMany()
                    .HasForeignKey(m => m.HomeTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.AwayTeam)
                    .WithMany()
                    .HasForeignKey(m => m.AwayTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Event)
                    .WithMany(e => e.Matches)
                    .HasForeignKey(m => m.EventId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable("Matches");
            });

            // Configure TeamEvent entity (many-to-many relationship)
            builder.Entity<TeamEvent>(entity =>
            {
                entity.HasKey(te => new { te.TeamId, te.EventId });

                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .IsRequired();

                entity.Property(e => e.Notes)
                    .HasMaxLength(500);

                entity.Property(e => e.RegistrationTime)
                    .HasDefaultValueSql("datetime('now')");

                // Configure relationships
                entity.HasOne(te => te.Team)
                    .WithMany(t => t.TeamEvents)
                    .HasForeignKey(te => te.TeamId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(te => te.Event)
                    .WithMany(e => e.TeamEvents)
                    .HasForeignKey(te => te.EventId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable("TeamEvents");
            });

            // Configure Article entity
            builder.Entity<Article>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.ContentMarkdown)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.Property(e => e.Likes)
                    .HasDefaultValue(0);

                entity.Property(e => e.Views)
                    .HasDefaultValue(0);

                entity.HasOne(e => e.Author)
                    .WithMany()
                    .HasForeignKey(e => e.AuthorUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Comments)
                    .WithOne(c => c.Article)
                    .HasForeignKey(c => c.ArticleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable("Articles");
            });

            // Configure Comment entity
            builder.Entity<Comment>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Content)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("datetime('now')");

                entity.HasOne(e => e.Author)
                    .WithMany()
                    .HasForeignKey(e => e.AuthorUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable("Comments");
            });
        }
    }
}