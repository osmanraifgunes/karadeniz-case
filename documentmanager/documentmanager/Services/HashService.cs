using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DocumentManager.Services
{
    /// <summary>
    /// Dosya içeriği hash'lemesi. SHA-256 — collision olasılığı pratikte sıfır,
    /// 8K DAU + 100MB altı dosyalar için CPU maliyeti ihmal edilebilir.
    /// </summary>
    public static class HashService
    {
        public static string ComputeSha256(Stream stream)
        {
            if (stream.CanSeek) stream.Position = 0;
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(stream);
                return ToHex(bytes);
            }
        }

        public static string ComputeSha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(bytes));
            }
        }

        public static string ComputeSha256OfFile(string filePath)
        {
            using (var fs = File.OpenRead(filePath))
            {
                return ComputeSha256(fs);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
