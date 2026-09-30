using POSApp.Core.Services;

namespace POSApp.Tests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void Hash_ThenVerify_Succeeds()
        {
            var stored = PasswordHasher.Hash("s3cret!");

            Assert.True(PasswordHasher.IsHashed(stored));
            Assert.True(PasswordHasher.Verify("s3cret!", stored, out var rehash));
            Assert.False(rehash);
        }

        [Fact]
        public void Verify_WrongPassword_Fails()
        {
            var stored = PasswordHasher.Hash("s3cret!");
            Assert.False(PasswordHasher.Verify("S3cret!", stored, out _));
        }

        [Fact]
        public void Hash_UsesRandomSalt()
        {
            Assert.NotEqual(PasswordHasher.Hash("same"), PasswordHasher.Hash("same"));
        }

        [Fact]
        public void Verify_LegacyPlainText_MatchesAndAsksForRehash()
        {
            Assert.True(PasswordHasher.Verify("admin123", "admin123", out var rehash));
            Assert.True(rehash);

            Assert.False(PasswordHasher.Verify("wrong", "admin123", out rehash));
            Assert.False(rehash);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("pbkdf2$abc$def$ghi")]
        [InlineData("pbkdf2$1000$not-base64$also-not")]
        public void Verify_MalformedStoredValue_Fails(string? stored)
        {
            Assert.False(PasswordHasher.Verify("anything", stored, out _));
        }
    }
}
