using System.Security.Cryptography;
using SecretShareApi.SupportingClasses;

namespace SecretShareApi.Tests.SupportingClasses
{
    public class EncryptionServiceTests
    {
        private const string Passphrase = "correct horse battery staple";

        private readonly EncryptionService _encryptionService = new EncryptionService();

        [Theory]
        [InlineData("hello")]
        [InlineData("")]
        [InlineData("wachtwoord met spaties en ëéï 🔐 日本語")]
        public void Decrypt_WithSamePassphrase_ReturnsOriginalText(string plainText)
        {
            var cipherText = _encryptionService.Encrypt(plainText, Passphrase);

            Assert.Equal(plainText, _encryptionService.Decrypt(cipherText, Passphrase));
        }

        [Fact]
        public void Decrypt_LongText_ReturnsOriginalText()
        {
            var plainText = string.Concat(Enumerable.Repeat("0123456789abcdef", 500)) + "!";

            var cipherText = _encryptionService.Encrypt(plainText, Passphrase);

            Assert.Equal(plainText, _encryptionService.Decrypt(cipherText, Passphrase));
        }

        [Fact]
        public void Encrypt_SameInputTwice_ProducesDifferentCipherTexts()
        {
            // Salt and IV are random per call, so identical input must never give identical output
            var first = _encryptionService.Encrypt("hello", Passphrase);
            var second = _encryptionService.Encrypt("hello", Passphrase);

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void Encrypt_ProducesBase64OfSaltIvAndCipherBlocks()
        {
            var cipherText = _encryptionService.Encrypt("hello", Passphrase);

            var bytes = Convert.FromBase64String(cipherText);

            // 16 bytes salt + 16 bytes IV + one 16 byte AES block (5 bytes of text, PKCS7 padded)
            Assert.Equal(48, bytes.Length);
        }

        [Fact]
        public void Encrypt_DoesNotContainPlainText()
        {
            var cipherText = _encryptionService.Encrypt("supersecret", Passphrase);

            Assert.DoesNotContain("supersecret", cipherText);
        }

        [Fact]
        public void Decrypt_WithWrongPassphrase_DoesNotReturnOriginalText()
        {
            var cipherText = _encryptionService.Encrypt("hello", Passphrase);

            Assert.NotEqual("hello", DecryptOrNull(cipherText, "wrong passphrase"));
        }

        [Fact]
        public void Decrypt_TamperedCipherText_DoesNotReturnOriginalText()
        {
            var bytes = Convert.FromBase64String(_encryptionService.Encrypt("hello", Passphrase));
            bytes[^1] ^= 0xFF;

            Assert.NotEqual("hello", DecryptOrNull(Convert.ToBase64String(bytes), Passphrase));
        }

        [Theory]
        [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
        [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
        public void ComputeSha256Hash_MatchesKnownVectors(string input, string expectedHash)
        {
            Assert.Equal(expectedHash, _encryptionService.ComputeSha256Hash(input));
        }

        [Fact]
        public void ComputeSha256Hash_ReturnsLowercaseHexOf64Characters()
        {
            var hash = _encryptionService.ComputeSha256Hash(Passphrase);

            Assert.Matches("^[0-9a-f]{64}$", hash);
        }

        [Fact]
        public void ComputeSha256Hash_IsDeterministic()
        {
            Assert.Equal(
                _encryptionService.ComputeSha256Hash(Passphrase),
                _encryptionService.ComputeSha256Hash(Passphrase));
        }

        // AES-CBC with a wrong key usually fails the padding check, but roughly 1 in 256 times the
        // padding happens to be valid and garbage comes out instead. Both count as "not decrypted".
        private string? DecryptOrNull(string cipherText, string passphrase)
        {
            try
            {
                return _encryptionService.Decrypt(cipherText, passphrase);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }
    }
}
