using System;
using System.IO;
using System.Threading.Tasks;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Export;
using SeriousView.Core.Support;

namespace SeriousView.Features.Shell;

/// <summary>
/// Document export collaborator (extracted from <see cref="MainWindowViewModel"/>): turns a tab's
/// raw markdown into a self-contained themed HTML file (M13), opens it in the browser for printing,
/// or puts it on the clipboard as rich text. Pure work — the VM keeps the file dialog, status bar
/// and error-bar plumbing and just delegates here; each method returns the status line to show.
/// </summary>
public sealed class DocumentExportService
{
    private readonly IThemeService _theme;
    private readonly IShellService _shell;
    private readonly IClipboardService _clipboard;

    public DocumentExportService(IThemeService theme, IShellService shell, IClipboardService clipboard)
    {
        _theme = theme;
        _shell = shell;
        _clipboard = clipboard;
    }

    /// <summary>Export the tab as one self-contained HTML file to <paramref name="target"/>. The
    /// theme follows the app; wiki links resolve against the document's folder, like the preview.</summary>
    public async Task<string> ExportHtmlAsync(DocumentTabViewModel tab, string target)
    {
        var html = HtmlExporter.Export(
            tab.DocumentText, tab.Header, IsAppEffectivelyDark(_theme.Mode), tab.BuildWikiResolver());
        await AtomicFile.WriteAllTextAsync(target, html);
        return $"Экспортировано: {Path.GetFileName(target)}";
    }

    /// <summary>Print / save-as-PDF (M13): the LIGHT-theme HTML goes to a per-print random temp dir
    /// and opens in the default browser — its print dialog (Ctrl+P) covers paper and selectable PDF.</summary>
    public async Task<string> PrintViaBrowserAsync(DocumentTabViewModel tab)
    {
        var html = HtmlExporter.Export(tab.DocumentText, tab.Header, darkTheme: false, tab.BuildWikiResolver());
        // A per-print random subdirectory makes the path unpredictable (no co-process can pre-create
        // or symlink it) while keeping a readable file name for the browser tab.
        var dir = Path.Combine(Path.GetTempPath(), "SeriousView", Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, Path.GetFileNameWithoutExtension(tab.Header) + ".print.html");
        await AtomicFile.WriteAllTextAsync(target, html);
        _shell.OpenWithDefaultApp(target);
        ScheduleTempCleanup(dir);
        return "Открыто в браузере — печать: Ctrl+P";
    }

    /// <summary>Copy-as-rich-text (M13): the themed HTML goes onto the clipboard as HTML (CF_HTML on
    /// Windows) with the raw markdown as the plain fallback.</summary>
    public async Task<string> CopyAsRichTextAsync(DocumentTabViewModel tab)
    {
        var html = HtmlExporter.Export(
            tab.DocumentText, tab.Header, IsAppEffectivelyDark(_theme.Mode), tab.BuildWikiResolver());
        await _clipboard.SetHtmlAsync(html, tab.DocumentText);
        return "Скопировано как форматированный текст";
    }

    /// <summary>One "is the app dark" answer for every export flavour: the dark family is dark, Light
    /// is light, and Auto resolves against the variant the OS gave us — so export and copy-as-rich-text
    /// can never disagree about the same screen.</summary>
    private static bool IsAppEffectivelyDark(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => false,
        ThemeMode.Auto => Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark,
        _ => true, // Dark, Midnight, Ocean
    };

    /// <summary>Best-effort: after the browser has had time to load the print HTML, remove its temp
    /// directory so exports don't accumulate in %TEMP%. Failures (the browser still holds the file)
    /// are ignored — the next launch's OS temp cleanup catches stragglers.</summary>
    private static void ScheduleTempCleanup(string dir)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2));
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // best effort
            }
        });
    }
}
