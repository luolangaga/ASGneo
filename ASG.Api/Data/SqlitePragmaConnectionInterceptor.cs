using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ASG.Api.Data
{
    public class SqlitePragmaConnectionInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            if (connection is SqliteConnection sqlite)
            {
                using var cmd1 = sqlite.CreateCommand();
                cmd1.CommandText = "PRAGMA journal_mode=WAL;";
                cmd1.ExecuteScalar();

                using var cmd2 = sqlite.CreateCommand();
                cmd2.CommandText = "PRAGMA synchronous=NORMAL;";
                cmd2.ExecuteNonQuery();

                using var cmd3 = sqlite.CreateCommand();
                cmd3.CommandText = "PRAGMA busy_timeout=5000;";
                cmd3.ExecuteNonQuery();

                using var cmd4 = sqlite.CreateCommand();
                cmd4.CommandText = "PRAGMA temp_store=MEMORY;";
                cmd4.ExecuteNonQuery();
            }
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (connection is SqliteConnection sqlite)
            {
                using var cmd1 = sqlite.CreateCommand();
                cmd1.CommandText = "PRAGMA journal_mode=WAL;";
                await cmd1.ExecuteScalarAsync(cancellationToken);

                using var cmd2 = sqlite.CreateCommand();
                cmd2.CommandText = "PRAGMA synchronous=NORMAL;";
                await cmd2.ExecuteNonQueryAsync(cancellationToken);

                using var cmd3 = sqlite.CreateCommand();
                cmd3.CommandText = "PRAGMA busy_timeout=5000;";
                await cmd3.ExecuteNonQueryAsync(cancellationToken);

                using var cmd4 = sqlite.CreateCommand();
                cmd4.CommandText = "PRAGMA temp_store=MEMORY;";
                await cmd4.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }
}
