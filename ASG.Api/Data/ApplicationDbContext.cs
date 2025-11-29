using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ASG.Api.Models;
using Microsoft.EntityFrameworkCore.Metadata;

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
    public DbSet<TeamReview> TeamReviews { get; set; }
    public DbSet<RecruitmentTask> RecruitmentTasks { get; set; }
    public DbSet<RecruitmentApplication> RecruitmentApplications { get; set; }
    public DbSet<RecruitmentTaskMatch> RecruitmentTaskMatches { get; set; }
    public DbSet<EventAdmin> EventAdmins { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ConversationMember> ConversationMembers { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<UserBlock> UserBlocks { get; set; }
    public DbSet<NeteaseHero> NeteaseHeroes { get; set; }
    public DbSet<NeteaseHeroStat> NeteaseHeroStats { get; set; }
    public DbSet<DeviceToken> DeviceTokens { get; set; }
    public DbSet<EventRuleRevision> EventRuleRevisions { get; set; }
    public DbSet<EventRegistrationAnswer> EventRegistrationAnswers { get; set; }
    public DbSet<OperationLog> OperationLogs { get; set; }
    public DbSet<PlayerEvent> PlayerEvents { get; set; }

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
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.IsActive)
                    .HasDefaultValue(true);

                entity.Property(e => e.EmailCredits)
                    .HasDefaultValue(0);

                
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

                entity.Property(e => e.QqNumber)
                    .HasMaxLength(20);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .IsConcurrencyToken();

                entity.Property(e => e.HidePlayers)
                    .HasDefaultValue(false);

                entity.Property(e => e.HasDispute)
                    .HasDefaultValue(false);

                entity.Property<bool>(nameof(Team.IsTemporary))
                    .HasDefaultValue(false);

                entity.Property<Guid?>(nameof(Team.TemporaryEventId));

                entity.HasOne(t => t.Owner)
                    .WithOne(u => u.OwnedTeam)
                    .HasForeignKey<Team>(t => t.OwnerId)
                    .OnDelete(DeleteBehavior.SetNull);

                // 配置与 Player 的一对多关系（玩家可游离，删除战队不删除玩家）
                entity.HasMany(t => t.Players)
                    .WithOne(p => p.Team)
                    .HasForeignKey(p => p.TeamId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.ToTable("Teams");
            });

            builder.Entity<TeamReview>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Rating).HasDefaultValue(0);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.HasOne(e => e.Team)
                    .WithMany()
                    .HasForeignKey(e => e.TeamId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Event)
                    .WithMany()
                    .HasForeignKey(e => e.EventId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.ToTable("TeamReviews");
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
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

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
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

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

                entity.Property(e => e.QqGroup)
                    .HasMaxLength(50);

                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .IsRequired();

                entity.Property(e => e.RegistrationMode)
                    .HasConversion<string>()
                    .HasDefaultValue(RegistrationMode.Team)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.CustomData)
                    .IsRequired()
                    .HasDefaultValue("{}");

                // 冠军战队关系（可为空；冠军删除或战队删除时不影响赛事）
                entity.HasOne(e => e.ChampionTeam)
                    .WithMany()
                    .HasForeignKey(e => e.ChampionTeamId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.ToTable("Events");
            });

            // Configure EventRuleRevision entity
            builder.Entity<EventRuleRevision>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChangeNotes).HasMaxLength(500);
                entity.HasOne(e => e.Event)
                    .WithMany()
                    .HasForeignKey(e => e.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.EventId, e.Version }).IsUnique();
                entity.ToTable("EventRuleRevisions");
            });

            // Configure EventRegistrationAnswer entity
            builder.Entity<EventRegistrationAnswer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Event)
                    .WithMany()
                    .HasForeignKey(e => e.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Team)
                    .WithMany()
                    .HasForeignKey(e => e.TeamId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.EventId, e.TeamId }).IsUnique();
                entity.ToTable("EventRegistrationAnswers");
            });

            // Configure PlayerEvent entity (solo registrations)
            builder.Entity<PlayerEvent>(entity =>
            {
                entity.HasKey(pe => new { pe.PlayerId, pe.EventId });
                entity.Property(pe => pe.Status)
                    .HasConversion<string>()
                    .IsRequired();
                entity.Property(pe => pe.Notes)
                    .HasMaxLength(500);
                entity.Property(pe => pe.RegistrationTime)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(pe => pe.Player)
                    .WithMany()
                    .HasForeignKey(pe => pe.PlayerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pe => pe.Event)
                    .WithMany()
                    .HasForeignKey(pe => pe.EventId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable("PlayerEvents");
            });

            // Configure OperationLog entity
            builder.Entity<OperationLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).HasMaxLength(64);
                entity.Property(e => e.EntityType).HasMaxLength(64);
                entity.ToTable("OperationLogs");
            });

            builder.Entity<EventAdmin>(entity =>
            {
                entity.HasKey(ea => new { ea.EventId, ea.UserId });
                entity.Property(ea => ea.AddedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne(ea => ea.Event)
                    .WithMany(e => e.EventAdmins)
                    .HasForeignKey(ea => ea.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(ea => ea.User)
                    .WithMany()
                    .HasForeignKey(ea => ea.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable("EventAdmins");
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
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

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
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

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
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

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
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Author)
                    .WithMany()
                    .HasForeignKey(e => e.AuthorUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Replies)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable("Comments");
            });

            builder.Entity<RecruitmentTask>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PositionType).HasConversion<string>().IsRequired();
                entity.Property(e => e.Status).HasConversion<string>().IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne<Event>()
                    .WithMany()
                    .HasForeignKey(e => e.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable("RecruitmentTasks");
            });

            builder.Entity<RecruitmentApplication>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).HasConversion<string>().IsRequired();
                entity.Property(e => e.Note).HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne<RecruitmentTask>()
                    .WithMany()
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable("RecruitmentApplications");
            });

            builder.Entity<RecruitmentTaskMatch>(entity =>
            {
                entity.HasKey(e => new { e.TaskId, e.MatchId });
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.ToTable("RecruitmentTaskMatches");
            });
            builder.Entity<Conversation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IsDirect).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.ToTable("Conversations");
            });

            builder.Entity<ConversationMember>(entity =>
            {
                entity.HasKey(e => new { e.ConversationId, e.UserId });
                entity.Property(e => e.JoinedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.HasOne<Conversation>()
                    .WithMany()
                    .HasForeignKey(e => e.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable("ConversationMembers");
            });

            builder.Entity<Message>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne<Conversation>()
                    .WithMany()
                    .HasForeignKey(e => e.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable("Messages");
            });

            builder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Payload).HasDefaultValue("");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsRead).HasDefaultValue(false);
                entity.ToTable("Notifications");
            });

            builder.Entity<UserBlock>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BlockerUserId).IsRequired();
                entity.Property(e => e.BlockedUserId).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(e => new { e.BlockerUserId, e.BlockedUserId }).IsUnique();
                entity.ToTable("UserBlocks");
            });

            builder.Entity<DeviceToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.Token).IsRequired();
                entity.Property(e => e.Platform).HasMaxLength(20).HasDefaultValue("");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasIndex(e => new { e.UserId, e.Token }).IsUnique();
                entity.ToTable("DeviceTokens");
            });

            builder.Entity<NeteaseHero>(entity =>
            {
                entity.HasKey(e => e.HeroId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CampId).IsRequired();
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.ToTable("NeteaseHeroes");
            });

            builder.Entity<NeteaseHeroStat>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.HeroId).IsRequired();
                entity.Property(e => e.CampId).IsRequired();
                entity.Property(e => e.Season).IsRequired();
                entity.Property(e => e.Part).IsRequired();
                entity.Property(e => e.WinRate).IsRequired();
                entity.Property(e => e.PingRate).IsRequired();
                entity.Property(e => e.UseRate).IsRequired();
                entity.Property(e => e.BanRate).IsRequired();
                entity.Property(e => e.StartDate).IsRequired();
                entity.Property(e => e.EndDate).IsRequired();
                entity.Property(e => e.Position).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Source).IsRequired().HasMaxLength(50);
                entity.Property(e => e.FetchedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne<NeteaseHero>()
                    .WithMany()
                    .HasForeignKey(e => e.HeroId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.Source, e.HeroId, e.CampId, e.Season, e.Part, e.WeekNum, e.StartDate, e.EndDate, e.Position }).IsUnique();
                entity.ToTable("NeteaseHeroStats");
            });

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                var dateProps = entityType.GetProperties().Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?));
                foreach (var p in dateProps)
                {
                    p.SetColumnType("timestamp with time zone");
                }
            }
        }
    }
}
