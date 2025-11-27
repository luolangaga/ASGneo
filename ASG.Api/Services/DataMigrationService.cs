using Microsoft.EntityFrameworkCore;
using ASG.Api.Data;
using ASG.Api.Models;
using Microsoft.AspNetCore.Identity;
using System.Reflection;

namespace ASG.Api.Services
{
    public static class DataMigrationService
    {
        public static async Task MigrateSqliteToPostgres(string sqliteConn, string postgresConn)
        {
            var srcOpts = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(sqliteConn).Options;
            var dstOpts = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(postgresConn).Options;
            using var src = new ApplicationDbContext(srcOpts);
            using var dst = new ApplicationDbContext(dstOpts);
            await dst.Database.MigrateAsync();
            dst.ChangeTracker.AutoDetectChangesEnabled = false;

            await CopyIfExistsAsync(src, dst, dst.Set<IdentityRole>());
            await CopyIfExistsAsync(src, dst, dst.Set<IdentityRoleClaim<string>>());

            await CopyIfExistsAsync(src, dst, dst.Teams);
            await CopyIfExistsAsync(src, dst, dst.Users);
            await CopyIfExistsAsync(src, dst, dst.Set<IdentityUserClaim<string>>());
            await CopyIfExistsAsync(src, dst, dst.Set<IdentityUserLogin<string>>());
            await CopyIfExistsAsync(src, dst, dst.Set<IdentityUserRole<string>>());
            await CopyIfExistsAsync(src, dst, dst.Set<IdentityUserToken<string>>());

            await CopyIfExistsAsync(src, dst, dst.Players);
            await CopyIfExistsAsync(src, dst, dst.GameRoles);
            await CopyIfExistsAsync(src, dst, dst.Events);
            await CopyIfExistsAsync(src, dst, dst.TeamEvents);
            await CopyIfExistsAsync(src, dst, dst.Matches);
            await CopyIfExistsAsync(src, dst, dst.Articles);
            await CopyIfExistsAsync(src, dst, dst.Comments);
            await CopyIfExistsAsync(src, dst, dst.RecruitmentTasks);
            await CopyIfExistsAsync(src, dst, dst.RecruitmentApplications);
            await CopyIfExistsAsync(src, dst, dst.RecruitmentTaskMatches);
            await CopyIfExistsAsync(src, dst, dst.EventAdmins);
            await CopyIfExistsAsync(src, dst, dst.Conversations);
            await CopyIfExistsAsync(src, dst, dst.ConversationMembers);
            await CopyIfExistsAsync(src, dst, dst.Messages);
            await CopyIfExistsAsync(src, dst, dst.Notifications);
            await CopyIfExistsAsync(src, dst, dst.UserBlocks);
            await CopyIfExistsAsync(src, dst, dst.NeteaseHeroes);
            await CopyIfExistsAsync(src, dst, dst.NeteaseHeroStats);
        }

        static async Task<bool> SourceHasTable<T>(ApplicationDbContext src) where T : class
        {
            var et = src.Model.FindEntityType(typeof(T));
            var table = et?.GetTableName();
            if (string.IsNullOrWhiteSpace(table)) return false;
            await src.Database.OpenConnectionAsync();
            try
            {
                using var cmd = src.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name";
                var p = cmd.CreateParameter();
                p.ParameterName = "$name";
                p.Value = table;
                cmd.Parameters.Add(p);
                var r = await cmd.ExecuteScalarAsync();
                return r != null && r != DBNull.Value;
            }
            finally
            {
                await src.Database.CloseConnectionAsync();
            }
        }

        static async Task CopyIfExistsAsync<T>(ApplicationDbContext src, ApplicationDbContext dst, DbSet<T> target) where T : class
        {
            if (!await SourceHasTable<T>(src)) return;
            await CopyAsync(src.Set<T>(), dst, target);
        }

        static async Task CopyAsync<T>(IQueryable<T> source, ApplicationDbContext dst, DbSet<T> target) where T : class
        {
            var list = await source.AsNoTracking().ToListAsync();
            if (list.Count == 0) return;
            var et = dst.Model.FindEntityType(typeof(T));
            var pk = et?.FindPrimaryKey();
            foreach (var item in list)
            {
                NormalizeDateTimes(item);
                if (pk == null || pk.Properties.Count == 0)
                {
                    await target.AddAsync(item);
                    continue;
                }
                var keyValues = pk.Properties.Select(p => item.GetType().GetProperty(p.Name)?.GetValue(item)).ToArray();
                var existing = await target.FindAsync(keyValues);
                if (existing == null)
                {
                    await target.AddAsync(item);
                }
                else
                {
                    dst.Entry(existing).CurrentValues.SetValues(item);
                }
            }
            await dst.SaveChangesAsync();
        }

        static void NormalizeDateTimes(object obj)
        {
            if (obj == null) return;
            var t = obj.GetType();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || !p.CanWrite) continue;
                var pt = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                if (pt == typeof(DateTime))
                {
                    var val = (DateTime?)p.GetValue(obj);
                    if (val.HasValue)
                    {
                        var v = val.Value;
                        if (v.Kind == DateTimeKind.Unspecified) v = DateTime.SpecifyKind(v, DateTimeKind.Utc);
                        else if (v.Kind == DateTimeKind.Local) v = v.ToUniversalTime();
                        p.SetValue(obj, v);
                    }
                }
                else if (pt == typeof(DateTimeOffset))
                {
                    var val = (DateTimeOffset?)p.GetValue(obj);
                    if (val.HasValue)
                    {
                        var v = val.Value.ToUniversalTime();
                        p.SetValue(obj, v);
                    }
                }
            }
        }
    }
}
