using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SecretShareApi.Tests
{
    /// In-memory SQLite database that lives as long as this object. Every context created from it
    /// shares the same open connection, so a fresh context sees what another one saved.
    public sealed class TestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;

        public TestDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public SebasDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<SebasDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new SebasDbContext(options);
        }

        public void Dispose() => _connection.Dispose();
    }
}
