using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace UniWebKit
{
    internal static class UniWebSdkArchive
    {
        internal static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        internal static void Extract(byte[] archive, string staging, Func<string, bool> include)
        {
            using (var content = new MemoryStream())
            {
                using (var gzip = new GZipStream(new MemoryStream(archive), CompressionMode.Decompress))
                    gzip.CopyTo(content);
                var bytes = content.ToArray();
                var entries = new Dictionary<string, (int Start, int Size)>();
                for (int i = 0; i + 512 <= bytes.Length;)
                {
                    string entry = Encoding.UTF8.GetString(bytes, i, 100).TrimEnd('\0');
                    if (entry.Length == 0) break;
                    int length = Convert.ToInt32(Encoding.ASCII.GetString(bytes, i + 124, 12).Trim('\0', ' '), 8);
                    int start = i + 512;
                    if (length > bytes.Length - start)
                        throw new InvalidDataException("Truncated Unity SDK archive.");
                    entries.Add(entry, (start, length));
                    i = start + (length + 511) / 512 * 512;
                }

                foreach (var entry in entries)
                {
                    if (!entry.Key.EndsWith("/pathname", StringComparison.Ordinal)) continue;
                    string path = Encoding.UTF8.GetString(bytes, entry.Value.Start, entry.Value.Size).Trim();
                    if (!include(path)) continue;
                    if (Path.IsPathRooted(path) || path.Contains("\\") || path.Contains(":") ||
                        Array.Exists(path.Split('/'), part => part == ".." || part == "." || part.Length == 0))
                        throw new InvalidDataException("Unexpected SDK asset path: " + path);
                    string name = entry.Key.Substring(0, entry.Key.Length - "pathname".Length);
                    string destination = Path.Combine(staging, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    if (entries.TryGetValue(name + "asset", out var file))
                        WriteEntry(destination, bytes, file);
                    else
                        Directory.CreateDirectory(destination);
                    if (entries.TryGetValue(name + "asset.meta", out var meta))
                        WriteEntry(destination + ".meta", bytes, meta);
                }
            }
        }

        private static void WriteEntry(string path, byte[] bytes, (int Start, int Size) entry)
        {
            using (var file = File.Create(path))
                file.Write(bytes, entry.Start, entry.Size);
        }
    }
}
