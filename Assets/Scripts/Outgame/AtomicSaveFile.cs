using System;
using System.IO;
using System.Text;

/// <summary>Commits a complete save document without truncating the last good file.</summary>
public static class AtomicSaveFile
{
    public static void Write(string path, string contents)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(nameof(path));
        if (contents == null) throw new ArgumentNullException(nameof(contents));
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(contents);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally
        {
            // Cleanup must not turn a successful commit into a reported failure.
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
