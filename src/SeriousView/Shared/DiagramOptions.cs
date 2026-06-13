using CommunityToolkit.Mvvm.ComponentModel;
using SeriousView.Core.Settings;

namespace SeriousView.Shared;

/// <summary>
/// Diagram (Kroki) rendering options (M12), one instance per window — mirrors <see cref="EditorOptions"/>
/// and <see cref="LayoutOptions"/>. Observable so the preview re-renders live when toggled; mirrored
/// to and from the persisted <see cref="DiagramSettings"/>. OFF by default (diagram text is sent to an
/// external server).
/// </summary>
public partial class DiagramOptions : ObservableObject
{
    public const string DefaultKrokiUrl = "https://kroki.io";

    /// <summary>Render ```mermaid/```plantuml/… fences via Kroki. Default off (privacy: opt-in).</summary>
    [ObservableProperty]
    private bool _enabled;

    /// <summary>Kroki server base URL (public instance or self-hosted).</summary>
    [ObservableProperty]
    private string _krokiUrl = DefaultKrokiUrl;

    public DiagramSettings ToSettings() => new(Enabled, KrokiUrl);

    public static DiagramOptions FromSettings(DiagramSettings? s) => s is null
        ? new DiagramOptions()
        : new DiagramOptions
        {
            Enabled = s.Enabled,
            KrokiUrl = string.IsNullOrWhiteSpace(s.KrokiUrl) ? DefaultKrokiUrl : s.KrokiUrl.Trim(),
        };
}
