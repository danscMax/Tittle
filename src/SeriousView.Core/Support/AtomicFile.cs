using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SeriousView.Core.Support;

/// <summary>
/// Crash-safe text file writes: write a sibling temp file, then atomically swap it into place
/// (Windows ReplaceFile / Unix rename). A crash, disk-full or power loss mid-write can therefore
/// never truncate or empty the target — the live file is either the old content or the new, never
/// a half-written ruin. This is the same pattern <see cref="Abstractions.ISettingsStore"/>'s JSON
/// store proved for settings; here it guards the user's actual documents (Ctrl+S, checkbox toggle,
/// HTML export). UTF-8 without a BOM, matching the app's prior <c>File.WriteAllTextAsync</c> policy.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Write <paramref name="text"/> to <paramref name="path"/> atomically. The temp file
    /// shares the target's directory (same volume → the swap is atomic). On failure the stray temp
    /// is removed and the original exception is rethrown so callers can surface it.</summary>
    public static async Task WriteAllTextAsync(string path, string text)
    {
        var temp = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, text, Utf8NoBom).ConfigureAwait(false);
            // temp and target share the directory/filesystem, so this is atomic (Windows ReplaceFile,
            // Unix rename) and surfaces to a directory watcher as a single Changed on the target.
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    /// <summary>Atomically write raw <paramref name="bytes"/> — used by document save when a non-UTF-8
    /// encoding or a BOM is chosen (the text path stays UTF-8-no-BOM for everything else).</summary>
    public static async Task WriteAllBytesAsync(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(false);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup of the temp file
        }
    }
}
