using System.Security.Cryptography;
using System.Text;

namespace POSApp.Core.Services
{
    /// <summary>
    /// PBKDF2-SHA256 password hashing. Stored format:
    /// <c>pbkdf2$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 hash&gt;</c>.
    ///
    /// Older databases stored passwords in plain text; <see cref="Verify"/> still accepts those
    /// and reports <c>needsRehash</c> so the login path can upgrade them in place.
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "pbkdf2$";
        private const int Iterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static string Hash(string password)
        {
            ArgumentNullException.ThrowIfNull(password);
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool IsHashed(string? stored) => stored != null && stored.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>Checks <paramref name="password"/> against a stored value.</summary>
        /// <param name="needsRehash">True when the stored value is legacy plain text (or weaker settings) and should be replaced by <see cref="Hash"/>.</param>
        public static bool Verify(string password, string? stored, out bool needsRehash)
        {
            needsRehash = false;
            if (string.IsNullOrEmpty(stored) || password == null) return false;

            if (!IsHashed(stored))
            {
                // Legacy plain-text record.
                var ok = CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(stored));
                needsRehash = ok;
                return ok;
            }

            var parts = stored.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
                return false;

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            var match = CryptographicOperations.FixedTimeEquals(actual, expected);
            needsRehash = match && iterations < Iterations;
            return match;
        }
    }
}
