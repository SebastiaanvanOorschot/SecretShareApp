using Microsoft.EntityFrameworkCore;

namespace SecretShareApi.SupportingClasses
{
    public class ExpiredSecretCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExpiredSecretCleanupService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(30);

        public ExpiredSecretCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<ExpiredSecretCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Expired secret cleanup service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SebasDbContext>();

                    var deleted = await db.SharedSecrets
                        .Where(s => s.Expire < DateTime.UtcNow)
                        .ExecuteDeleteAsync(stoppingToken);

                    if (deleted > 0)
                        _logger.LogInformation("Deleted {Count} expired secrets.", deleted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during expired secret cleanup.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
