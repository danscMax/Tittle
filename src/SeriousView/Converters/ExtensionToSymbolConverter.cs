using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentAvalonia.UI.Controls;

namespace SeriousView.Converters;

/// <summary>
/// Maps a file extension (".cs", ".md", ".json", …) to a FluentAvalonia <see cref="Symbol"/>
/// used as the per-tab file-type icon. Unknown/empty extensions fall back to a document glyph.
/// </summary>
public sealed class ExtensionToSymbolConverter : IValueConverter
{
    public static readonly ExtensionToSymbolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ext = (value as string)?.TrimStart('.').ToLowerInvariant() ?? string.Empty;
        return ext switch
        {
            "cs" or "js" or "ts" or "jsx" or "tsx" or "py" or "go" or "rs" or "c" or "cpp" or "cc"
                or "h" or "hpp" or "java" or "kt" or "rb" or "php" or "swift" or "lua" or "sh"
                or "ps1" or "sql" or "html" or "htm" or "css" or "scss" or "xaml" or "axaml"
                => Symbol.Code,
            "json" or "yaml" or "yml" or "toml" or "ini" or "xml" or "config" or "csproj"
                or "props" or "targets" or "editorconfig" or "gitignore"
                => Symbol.Settings,
            "png" or "jpg" or "jpeg" or "gif" or "bmp" or "svg" or "webp" or "ico"
                => Symbol.Image,
            _ => Symbol.Document,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
