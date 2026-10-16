using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SecretShareApi.Controllers;
using SecretShareApi.Controllers.DataTransferObjects;
using SecretShareApi.Repositories;

namespace SecretShareApi.Tests.Controllers
{
    public class SecretControllerTests
    {
        // The repository's public methods are virtual, so it can be substituted without a database
        private readonly SharedSecretRepository _repository = Substitute.For<SharedSecretRepository>(null!, null!, null!);
        private readonly SecretController _controller;

        public SecretControllerTests()
        {
            _controller = new SecretController(_repository);
        }

        [Fact]
        public async Task Store_ReturnsPickUpUrlAsJson()
        {
            var dto = new StoreSecretDto { Secret = "my secret", Lifetime = 3600, Passphrase = "phrase" };
            _repository.EncryptAndSaveSecret(dto).Returns("https://pickup.test/secretPickup/abc");

            var result = await _controller.Store(dto);

            Assert.Equal("https://pickup.test/secretPickup/abc", result.Value);
            await _repository.Received(1).EncryptAndSaveSecret(dto);
        }

        [Fact]
        public async Task Retrieve_SecretFound_ReturnsSecretAsJson()
        {
            var dto = new RetrieveSecretDto { UrlExtension = "abc", Passphrase = "phrase" };
            _repository.VerifyAndDecryptSecret(dto).Returns("my secret");

            var result = await _controller.Retrieve(dto);

            var json = Assert.IsType<JsonResult>(result);
            Assert.Equal("my secret", json.Value);
        }

        [Fact]
        public async Task Retrieve_NothingFound_ReturnsNotFound()
        {
            // The repository returns "" for unknown, expired, already retrieved or wrong passphrase alike
            var dto = new RetrieveSecretDto { UrlExtension = "abc", Passphrase = "phrase" };
            _repository.VerifyAndDecryptSecret(dto).Returns("");

            var result = await _controller.Retrieve(dto);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
