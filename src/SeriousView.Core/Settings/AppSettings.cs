using System.Collections.Generic;
using SeriousView.Core.Abstractions;

namespace SeriousView.Core.Settings;

/// <summary>
/// Persisted application settings — one typed record saved atomically as <c>settings.json</c> and
/// held in memory by <see cref="IAppSettingsService"/>. Every field is optional so a missing or
/// blank file deserializes to sensible defaults: Dark theme, default-centred window, no session.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Last chosen theme, re-applied at startup. Defaults to Dark.</summary>
    public ThemeMode Theme { get; init; } = ThemeMode.Dark;

    /// <summary>Last window placement, or null to use the default size centred on first run.</summary>
    public WindowPlacement? Window { get; init; }

    /// <summary>Open tabs to reopen at startup, or null/empty for the welcome screen.</summary>
    public SessionState? Session { get; init; }

    /// <summary>Editor view options (font zoom, wrap, line numbers), or null for defaults.</summary>
    public EditorSettings? Editor { get; init; }
}

/// <summary>Window geometry in screen pixels plus whether it was maximized. When maximized, the
/// width/height/position are the <em>normal</em> (restore) bounds, not the maximized rectangle.</summary>
public sealed record WindowPlacement(double Width, double Height, int X, int Y, bool Maximized);

/// <summary>The set of documents open at the last clean exit and which one was active.</summary>
public sealed record SessionState(List<string> OpenFiles, int ActiveIndex);

/// <summary>Editor display options shared across tabs (source view only).</summary>
public sealed record EditorSettings(double FontSize, bool WordWrap, bool ShowLineNumbers);
