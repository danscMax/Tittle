using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace Tittle.Platform;

/// <summary>
/// Bundled STATIC Inter (separate file per weight/style in <c>Assets/Fonts</c>), registered as a
/// proper font collection so the family is indexed by BOTH weight and style.
/// <para>
/// Workaround for the open Avalonia 11.3.x regression #18875: the new variable-font code path
/// mis-resolves <c>FontWeight</c>, so <c>.WithInterFont()</c> (which ships a VARIABLE Inter) renders
/// bold at normal weight — and an avares-folder URI used only as the default family resolves weight
/// but not italic. Separate-file faces (Regular/SemiBold/Bold/Italic/BoldItalic) are unaffected, and
/// registering them via <see cref="EmbeddedFontCollection"/> indexes the italic faces too.
/// </para>
/// Reference as <c>fonts:Inter#Inter</c>. Revert to <c>.WithInterFont()</c> once #18875 ships fixed.
/// </summary>
internal sealed class InterFontCollection : EmbeddedFontCollection
{
    public InterFontCollection() : base(
        new Uri("fonts:Inter", UriKind.Absolute),
        new Uri("avares://Tittle/Assets/Fonts", UriKind.Absolute))
    {
    }
}

/// <summary>
/// Registers the bundled static Inter (see <see cref="InterFontCollection"/>) and pins it as the
/// default family. Public so EVERY AppBuilder entry point applies it identically — the desktop app
/// (<c>Program</c>), the headless test harness (<c>TestAppBuilder</c>) and the render tool
/// (<c>tools/HeadlessRender</c>) — otherwise <c>fonts:Inter#Inter</c> (referenced by the preview
/// style) fails to resolve and headless renders throw.
/// </summary>
public static class BundledFonts
{
    public static AppBuilder WithBundledInterFont(this AppBuilder builder)
        => builder
            .ConfigureFonts(fontManager => fontManager.AddFontCollection(new InterFontCollection()))
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" });
}
