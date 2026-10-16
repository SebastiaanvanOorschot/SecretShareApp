using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecretShareApi.Entities;
using SecretShareApi.SupportingClasses;

namespace SecretShareApi.Tests.SupportingClasses
{
    public sealed class ExpiredSecretCleanupServiceTests : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly TestDatabase _database = new TestDatabase();
        private readonly ListLogger<ExpiredSecretCleanupService> _logger = new ListLogger<ExpiredSecretCleanupService>();

        public void Dispose() => _database.Dispose();

        [Fact]
        public async Task FirstRun_DeletesExpiredSecrets_AndKeepsValidOnes()
        {
            Seed(("expired-hour", DateTime.UtcNow.AddHours(-1)),
                 ("expired-day", DateTime.UtcNow.AddDays(-1)),
                 ("valid", DateTime.UtcNow.AddHours(1)));
            var scopes = new SignallingScopeFactory(BuildProvider());
            using var service = new ExpiredSecretCleanupService(scopes, _logger);

            await service.StartAsync(CancellationToken.None);
            await scopes.FirstScopeDisposed.WaitAsync(Timeout);
            await Stop(service);

            Assert.Equal(new[] { "valid" }, RemainingUrlExtensions());
            Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Information && e.Message == "Deleted 2 expired secrets.");
        }

        [Fact]
        public async Task FirstRun_NothingExpired_DeletesNothingAndLogsNoDeletion()
        {
            Seed(("valid", DateTime.UtcNow.AddHours(1)));
            var scopes = new SignallingScopeFactory(BuildProvider());
            using var service = new ExpiredSecretCleanupService(scopes, _logger);

            await service.StartAsync(CancellationToken.None);
            await scopes.FirstScopeDisposed.WaitAsync(Timeout);
            await Stop(service);

            Assert.Equal(new[] { "valid" }, RemainingUrlExtensions());
            Assert.DoesNotContain(_logger.Entries, e => e.Message.StartsWith("Deleted"));
        }

        [Fact]
        public async Task Exception_IsLogged_AndServiceKeepsRunning()
        {
            // No SebasDbContext registered, so resolving it inside the iteration throws
            var scopes = new SignallingScopeFactory(new ServiceCollection().BuildServiceProvider());
            using var service = new ExpiredSecretCleanupService(scopes, _logger);

            await service.StartAsync(CancellationToken.None);
            var error = await _logger.FirstError.WaitAsync(Timeout);

            Assert.Equal("Error during expired secret cleanup.", error.Message);
            Assert.IsType<InvalidOperationException>(error.Exception);
            Assert.False(service.ExecuteTask!.IsCompleted, "the service should survive a failed iteration");

            await Stop(service);
        }

        [Fact]
        public async Task Stop_DuringTheWaitBetweenRuns_EndsPromptly()
        {
            var scopes = new SignallingScopeFactory(BuildProvider());
            using var service = new ExpiredSecretCleanupService(scopes, _logger);
            await service.StartAsync(CancellationToken.None);
            await scopes.FirstScopeDisposed.WaitAsync(Timeout);

            // The service is now in its 30 minute delay; stopping must not wait that out
            await Stop(service);

            Assert.True(service.ExecuteTask!.IsCompleted);
            Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
        }

        // ---- helpers ---------------------------------------------------------

        // Bounded, so a service that ignores the stop token fails the test instead of hanging the run
        private static Task Stop(ExpiredSecretCleanupService service) =>
            service.StopAsync(CancellationToken.None).WaitAsync(Timeout);

        private IServiceProvider BuildProvider() =>
            new ServiceCollection()
                .AddDbContext<SebasDbContext>(_database.Configure)
                .BuildServiceProvider();

        private void Seed(params (string UrlExtension, DateTime Expire)[] secrets)
        {
            using var context = _database.CreateContext();
            foreach (var (urlExtension, expire) in secrets)
            {
                context.SharedSecrets.Add(new SharedSecret { Urlextension = urlExtension, Expire = expire });
            }
            context.SaveChanges();
        }

        private List<string?> RemainingUrlExtensions()
        {
            using var context = _database.CreateContext();
            return context.SharedSecrets.OrderBy(s => s.Urlextension).Select(s => s.Urlextension).ToList();
        }

        /// Wraps the real scope factory and signals when the first scope is disposed. The service
        /// disposes its scope at the end of every iteration, so this marks "first run finished".
        private sealed class SignallingScopeFactory : IServiceScopeFactory
        {
            private readonly IServiceScopeFactory _inner;
            private readonly TaskCompletionSource _firstScopeDisposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public SignallingScopeFactory(IServiceProvider provider)
            {
                _inner = provider.GetRequiredService<IServiceScopeFactory>();
            }

            public Task FirstScopeDisposed => _firstScopeDisposed.Task;

            public IServiceScope CreateScope() => new SignallingScope(_inner.CreateScope(), () => _firstScopeDisposed.TrySetResult());

            private sealed class SignallingScope : IServiceScope
            {
                private readonly IServiceScope _inner;
                private readonly Action _onDispose;

                public SignallingScope(IServiceScope inner, Action onDispose)
                {
                    _inner = inner;
                    _onDispose = onDispose;
                }

                public IServiceProvider ServiceProvider => _inner.ServiceProvider;

                public void Dispose()
                {
                    _inner.Dispose();
                    _onDispose();
                }
            }
        }

        private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

        /// Collects log entries and exposes the first error as a task to await.
        private sealed class ListLogger<T> : ILogger<T>
        {
            private readonly List<LogEntry> _entries = new();
            private readonly TaskCompletionSource<LogEntry> _firstError = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public IReadOnlyList<LogEntry> Entries
            {
                get { lock (_entries) return _entries.ToList(); }
            }

            public Task<LogEntry> FirstError => _firstError.Task;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var entry = new LogEntry(logLevel, formatter(state, exception), exception);
                lock (_entries) _entries.Add(entry);

                if (logLevel == LogLevel.Error)
                {
                    _firstError.TrySetResult(entry);
                }
            }
        }
    }
}
