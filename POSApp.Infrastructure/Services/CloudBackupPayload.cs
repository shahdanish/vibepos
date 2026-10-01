using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace POSApp.Infrastructure.Services
{
    /// <summary>
    /// The wire format of a cloud database snapshot — SHA-256 of the raw file, GZip, Base64,
    /// split into chunk strings that fit a Firestore document — kept apart from the Firestore
    /// calls in <see cref="CloudBackupService"/> so a full backup → restore round trip can be
    /// tested without touching any client's cloud project.
    /// </summary>
    public static class CloudBackupPayload
    {
        /// <summary>Base64 characters per chunk document (Firestore's per-document limit is ~1 MB).</summary>
        public const int ChunkCharSize = 700_000;

        public sealed record Encoded(IReadOnlyList<string> Chunks, string Sha256, long SizeBytes, long CompressedBytes);

        public static Encoded Encode(byte[] raw, int chunkCharSize = ChunkCharSize)
        {
            var sha256 = Convert.ToHexString(SHA256.HashData(raw));
            var compressed = Gzip(raw);
            var base64 = Convert.ToBase64String(compressed);

            var chunks = new List<string>((base64.Length / chunkCharSize) + 1);
            for (int i = 0; i < base64.Length; i += chunkCharSize)
                chunks.Add(base64.Substring(i, Math.Min(chunkCharSize, base64.Length - i)));

            return new Encoded(chunks, sha256, raw.LongLength, compressed.LongLength);
        }

        /// <summary>Reassembles the chunks (in order) into the raw file. Throws if they cannot be decoded.</summary>
        public static byte[] Decode(IEnumerable<string> chunksInOrder)
        {
            var sb = new StringBuilder();
            foreach (var chunk in chunksInOrder)
                sb.Append(chunk);
            return Gunzip(Convert.FromBase64String(sb.ToString()));
        }

        /// <summary>True when the decoded file has the size and SHA-256 recorded at backup time.</summary>
        public static bool Verify(byte[] raw, long expectedSize, string expectedSha256) =>
            raw.LongLength == expectedSize &&
            string.Equals(Convert.ToHexString(SHA256.HashData(raw)), expectedSha256, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The schema guard: a snapshot may be restored when this build knows its last migration
        /// (it is the same or older — start-up migrates it forward). A snapshot from a newer
        /// build, or one without a recorded schema from an old build, is judged by that rule.
        /// </summary>
        public static bool IsSchemaKnown(IEnumerable<string> knownMigrations, string? schemaVersion) =>
            string.IsNullOrEmpty(schemaVersion) || knownMigrations.Contains(schemaVersion);

        private static byte[] Gzip(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gz = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                gz.Write(data, 0, data.Length);
            return output.ToArray();
        }

        private static byte[] Gunzip(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gz.CopyTo(output);
            return output.ToArray();
        }
    }
}
