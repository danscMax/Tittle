using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SeriousView.Core.Abstractions;

namespace SeriousView.Platform;

/// <summary>
/// <see cref="IFileDialogService"/> implemented with the Avalonia 11 StorageProvider
/// (the modern replacement for the obsolete OpenFileDialog).
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    private readonly Func<TopLevel?> _topLevel;

    public FileDialogService(Func<TopLevel?> topLevel) => _topLevel = topLevel;

    public async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        var top = _topLevel();
        if (top is null)
            return Array.Empty<string>();

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Открыть файлы",
            AllowMultiple = true,
        });

        // TryGetLocalPath returns null for virtual/cloud locations; only local files are loadable.
        return files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
    }

    public async Task<string?> SaveFileAsync(string suggestedFileName)
    {
        var top = _topLevel();
        if (top is null)
            return null;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Экспорт в HTML",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "html",
            FileTypeChoices = new[] { new FilePickerFileType("HTML") { Patterns = new[] { "*.html" } } },
        });

        return file?.TryGetLocalPath();
    }
}
