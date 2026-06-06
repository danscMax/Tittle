using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SeriousView.Core.Abstractions;

namespace SeriousView.Services;

/// <summary>
/// <see cref="IFileDialogService"/> implemented with the Avalonia 11 StorageProvider
/// (the modern replacement for the obsolete OpenFileDialog).
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    private readonly Func<TopLevel?> _topLevel;

    public FileDialogService(Func<TopLevel?> topLevel) => _topLevel = topLevel;

    public async Task<string?> PickFileAsync()
    {
        var top = _topLevel();
        if (top is null)
            return null;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Открыть файл",
            AllowMultiple = false,
        });

        // TryGetLocalPath returns null for virtual/cloud locations; M1 only needs local files.
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
