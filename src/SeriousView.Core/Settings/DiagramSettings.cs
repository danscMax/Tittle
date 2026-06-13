namespace SeriousView.Core.Settings;

/// <summary>Diagram rendering preferences (M12). OFF by default: rendering sends the diagram text to
/// an external Kroki server, so it must be an explicit opt-in. <see cref="KrokiUrl"/> can point at the
/// public instance or a self-hosted one.</summary>
public sealed record DiagramSettings(bool Enabled = false, string KrokiUrl = "https://kroki.io");
