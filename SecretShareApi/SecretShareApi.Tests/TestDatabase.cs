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
            var builder = new DbContextOptionsBuilder<SebasDbContext>();
            Configure(builder);

            return new SebasDbContext(builder.Options);
        }

        /// Points a context at this database, e.g. for AddDbContext in a test service collection.
        public void Configure(DbContextOptionsBuilder builder) => builder.UseSqlite(_connection);

        public void Dispose() => _connection.Dispose();
    }
}
