using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SeriousView.Shared;

/// <summary>
/// Maps a file extension (".cs", ".md", ".json", …) to a vector icon <see cref="Geometry"/> from
/// <c>Themes/Icons.axaml</c>, used as the per-tab file-type glyph via a <c>PathIcon</c>. A vector,
/// not a FluentAvalonia <c>SymbolIcon</c>: the Symbols font can lose its load race and render a
/// <c>.notdef</c> box, while a PathIcon is geometry and always draws. Unknown/empty → document.
/// </summary>
public sealed class ExtensionToIconConverter : IValueConverter
{
    public static readonly ExtensionToIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ext = (value as string)?.TrimStart('.').ToLowerInvariant() ?? string.Empty;
        var key = ext switch
        {
            "cs" or "js" or "ts" or "jsx" or "tsx" or "py" or "go" or "rs" or "c" or "cpp" or "cc"
                or "h" or "hpp" or "java" or "kt" or "rb" or "php" or "swift" or "lua" or "sh"
                or "ps1" or "sql" or "html" or "htm" or "css" or "scss" or "xaml" or "axaml"
                => "IconCode",
            "json" or "yaml" or "yml" or "toml" or "ini" or "xml" or "config" or "csproj"
                or "props" or "targets" or "editorconfig" or "gitignore"
                => "IconSettings",
            "png" or "jpg" or "jpeg" or "gif" or "bmp" or "svg" or "webp" or "ico"
                => "IconImage",
            _ => "IconDocument",
        };

        return Application.Current?.TryGetResource(key, null, out var res) == true ? res as Geometry : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
