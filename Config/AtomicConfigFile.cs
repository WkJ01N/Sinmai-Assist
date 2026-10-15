using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace SinmaiAssist.Config;

internal static class AtomicConfigFile
{
    internal static void WriteMany(IDictionary<string, string> files)
    {
        var originals = new Dictionary<string, string>();
        foreach (var path in files.Keys) originals[path] = File.Exists(path) ? File.ReadAllText(path) : null;
        var changed = new List<string>();
        try
        {
            foreach (var item in files) { Write(item.Key, item.Value); changed.Add(item.Key); }
        }
        catch (Exception failure)
        {
            var errors = new List<Exception> { failure };
            for (int i = changed.Count - 1; i >= 0; i--)
            {
                var path = changed[i];
                try { if (originals[path] == null) File.Delete(path); else Write(path, originals[path]); }
                catch (Exception rollback) { errors.Add(rollback); }
            }
            if (errors.Count > 1) throw new AggregateException("Save failed, including rollback; see individual errors.", errors);
            throw;
        }
    }
    internal static void Write(string path, string text)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
