using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SecretShareApi.Controllers.DataTransferObjects;
using SecretShareApi.Entities;
using SecretShareApi.Repositories;
using SecretShareApi.SupportingClasses;

namespace SecretShareApi.Tests.Repositories
{
    public sealed class SharedSecretRepositoryTests : IDisposable
    {
        private const string PickUpUrl = "https://pickup.test/secretPickup";
        private const string Passphrase = "correct horse battery staple";
        private const long OneHour = 3600;

        private readonly TestDatabase _database = new TestDatabase();
        private readonly SebasDbContext _context;
        private readonly EncryptionService _encryptionService = new EncryptionService();
        private readonly SharedSecretRepository _repository;

        public SharedSecretRepositoryTests()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PasswordShare:PickUpUrl"] = PickUpUrl
                })
                .Build();

            _context = _database.CreateContext();
            _repository = new SharedSecretRepository(_context, config, _encryptionService);
        }

        public void Dispose()
        {
            _context.Dispose();
            _database.Dispose();
        }

        // ---- EncryptAndSaveSecret ------------------------------------------

        [Fact]
        public async Task EncryptAndSaveSecret_ReturnsPickUpUrlWithHashExtension()
        {
            var url = await Store("my secret");

            Assert.Matches("^" + PickUpUrl + "/[0-9a-f]{64}$", url);
        }

        [Fact]
        public async Task EncryptAndSaveSecret_StoresRowMatchingUrlExtension()
        {
            var url = await Store("my secret");

            var row = Assert.Single(Rows());
            Assert.Equal(ExtensionOf(url), row.Urlextension);
            Assert.Equal(0, row.Tries);
        }

        [Fact]
        public async Task EncryptAndSaveSecret_StoresCipherTextAndPassphraseHash_NeverPlainValues()
        {
            await Store("my secret");

            var row = Assert.Single(Rows());
            Assert.NotEqual("my secret", row.Secret);
            Assert.DoesNotContain("my secret", row.Secret);
            Assert.NotEqual(Passphrase, row.Token);
            Assert.Equal(_encryptionService.ComputeSha256Hash(Passphrase), row.Token);
            Assert.Equal("my secret", _encryptionService.Decrypt(row.Secret!, Passphrase));
        }

        [Fact]
        public async Task EncryptAndSaveSecret_TrimsSecret()
        {
            await Store("  my secret \n");

            var row = Assert.Single(Rows());
            Assert.Equal("my secret", _encryptionService.Decrypt(row.Secret!, Passphrase));
        }

        [Fact]
        public async Task EncryptAndSaveSecret_SetsExpireToNowPlusLifetime()
        {
            var before = DateTime.UtcNow;
            await Store("my secret", lifetime: OneHour);
            var after = DateTime.UtcNow;

            var row = Assert.Single(Rows());
            Assert.InRange(row.Expire, before.AddSeconds(OneHour), after.AddSeconds(OneHour));
        }

        [Fact]
        public async Task EncryptAndSaveSecret_SameSecretTwice_GivesDifferentUrls()
        {
            var first = await Store("my secret");
            var second = await Store("my secret");

            Assert.NotEqual(first, second);
            Assert.Equal(2, Rows().Count);
        }

        // ---- VerifyAndDecryptSecret ----------------------------------------

        [Fact]
        public async Task VerifyAndDecryptSecret_CorrectPassphrase_ReturnsSecretAndDeletesIt()
        {
            var url = await Store("my secret");

            var secret = await Retrieve(url, Passphrase);

            Assert.Equal("my secret", secret);
            Assert.Empty(Rows());
        }

        [Fact]
        public async Task VerifyAndDecryptSecret_SecondRetrieval_ReturnsEmpty()
        {
            // The core promise of the app: a secret can be read exactly once
            var url = await Store("my secret");
            await Retrieve(url, Passphrase);

            var secondTry = await Retrieve(url, Passphrase);

            Assert.Equal("", secondTry);
        }

        [Fact]
        public async Task VerifyAndDecryptSecret_OnlyDeletesTheRetrievedSecret()
        {
            var url = await Store("my secret");
            var otherUrl = await Store("other secret");

            await Retrieve(url, Passphrase);

            var remaining = Assert.Single(Rows());
            Assert.Equal(ExtensionOf(otherUrl), remaining.Urlextension);
        }

        [Fact]
        public async Task VerifyAndDecryptSecret_UnknownUrlExtension_ReturnsEmpty()
        {
            await Store("my secret");

            var secret = await _repository.VerifyAndDecryptSecret(new RetrieveSecretDto
            {
                UrlExtension = new string('0', 64),
                Passphrase = Passphrase
            });

            Assert.Equal("", secret);
            Assert.Single(Rows());
        }

        [Fact]
        public async Task VerifyAndDecryptSecret_ExpiredSecret_ReturnsEmptyAndDeletesIt()
        {
            var url = await Store("my secret");
            SetExpire(DateTime.UtcNow.AddMinutes(-1));

            var secret = await Retrieve(url, Passphrase);

            Assert.Equal("", secret);
            Assert.Empty(Rows());
        }

        [Fact]
        public async Task VerifyAndDecryptSecret_WrongPassphrase_ReturnsEmptyAndCountsTry()
        {
            var url = await Store("my secret");

            var secret = await Retrieve(url, "wrong passphrase");

            Assert.Equal("", secret);
            var row = Assert.Single(Rows());
            Assert.Equal(1, row.Tries);
        }

        [Fact]
        public async Task VerifyAndDecryptSecret_ThreeWrongTries_CorrectPassphraseStillWorks()
        {
            var url = await Store("my secret");
            for (var i = 0; i < 3; i++)
            {
                await Retrieve(url, "wrong passphrase");
            }

            var secret = await Retrieve(url, Passphrase);

            Assert.Equal("my secret", secret);
        }

        [Fact]
        public async Task VerifyAndDecryptSecret_FourWrongTries_DeletesSecret()
        {
            var url = await Store("my secret");
            for (var i = 0; i < 4; i++)
            {
                await Retrieve(url, "wrong passphrase");
            }

            Assert.Empty(Rows());
            Assert.Equal("", await Retrieve(url, Passphrase));
        }

        // ---- helpers ---------------------------------------------------------

        // Store and Retrieve clear the change tracker afterwards: in the API every request gets its
        // own context, so no entity should carry over from one call to the next.
        private async Task<string> Store(string secret, long lifetime = OneHour, string passphrase = Passphrase)
        {
            var url = await _repository.EncryptAndSaveSecret(new StoreSecretDto
            {
                Secret = secret,
                Lifetime = lifetime,
                Passphrase = passphrase
            });
            _context.ChangeTracker.Clear();

            return url;
        }

        private async Task<string> Retrieve(string url, string passphrase)
        {
            var secret = await _repository.VerifyAndDecryptSecret(new RetrieveSecretDto
            {
                UrlExtension = ExtensionOf(url),
                Passphrase = passphrase
            });
            _context.ChangeTracker.Clear();

            return secret;
        }

        private static string ExtensionOf(string url) => url[(url.LastIndexOf('/') + 1)..];

        /// Reads through a fresh context, so the result reflects the database and not the
        /// repository's change tracker.
        private List<SharedSecret> Rows()
        {
            using var context = _database.CreateContext();
            return context.SharedSecrets.AsNoTracking().ToList();
        }

        private void SetExpire(DateTime expire)
        {
            using var context = _database.CreateContext();
            context.SharedSecrets.ExecuteUpdate(s => s.SetProperty(e => e.Expire, expire));
        }
    }
}
