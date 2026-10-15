using System;
using System.IO;
using System.Text;

namespace Interstellar.Utilities
{
    public class FileUtilities
    {
        public static string GetGamePath() =>
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        public static string GetMenuPath(string relativePath = "")
        {
            string root = Path.GetFullPath(Path.Combine(GetGamePath(), PluginInfo.BaseDirectory));
            if (Path.IsPathRooted(relativePath)) throw new ArgumentException("The menu path must be relative.", nameof(relativePath));
            string[] parts = relativePath.Replace('\\', '/').Split('/');
            string current = root;
            RejectLink(current);
            foreach (string part in parts)
            {
                if (part.Length == 0 && relativePath.Length == 0) continue;
                if (string.IsNullOrWhiteSpace(part) || part == "." || part == ".." ||
                    part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal) ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.IndexOf(':') >= 0)
                    throw new ArgumentException("The menu path is invalid.", nameof(relativePath));
                current = Path.Combine(current, part);
                RejectLink(current);
            }
            return current;
        }

        private static void RejectLink(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Menu storage cannot use a redirected file or folder.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }

        public static string ReadBoundedText(string path, int maximumBytes)
        {
            if (maximumBytes <= 0 || maximumBytes > 65536) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > maximumBytes) throw new InvalidDataException("The menu data file is too large.");
            var bytes = new byte[maximumBytes + 1];
            int count = 0;
            while (count < bytes.Length)
            {
                int read = stream.Read(bytes, count, bytes.Length - count);
                if (read == 0) break;
                count += read;
            }
            if (count > maximumBytes) throw new InvalidDataException("The menu data file is too large.");
            using var reader = new StreamReader(new MemoryStream(bytes, 0, count, false), new UTF8Encoding(false, true), true);
            return reader.ReadToEnd();
        }

        public static void WriteMenuText(string relativePath, string content)
        {
            string path = GetMenuPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            path = GetMenuPath(relativePath);
            string temporary = Path.Combine(Path.GetDirectoryName(path), ".interstellar-" + Guid.NewGuid().ToString("N") + ".tmp");
            bool created = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    byte[] bytes = new UTF8Encoding(false, true).GetBytes(content);
                    if (bytes.Length > 65536) throw new InvalidDataException("The menu data file is too large.");
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                GetMenuPath(relativePath);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                created = false;
            }
            finally
            {
                if (created) File.Delete(temporary);
            }
        }
    }
}
