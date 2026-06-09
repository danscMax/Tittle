using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using SeriousView.Core.Settings;
using SeriousView.Features.Settings;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Features;

public class LayoutSettingsWindowTests
{
    // Exercises the real window + the two-way EnumRadioConverter binding end-to-end: the radios reflect
    // the current ToolbarMode, and selecting one writes the new mode back to the shared LayoutOptions.
    // Read from the logical tree (no Show/render) — rendering trips FluentAvalonia's Symbols glyph font
    // (checkbox/radio glyphs) under the headless font manager; bindings are active from InitializeComponent.
    [AvaloniaFact]
    public void ToolbarRadios_ReflectAndSet_ToolbarMode()
    {
        var layout = new LayoutOptions { ToolbarMode = ToolbarMode.Contextual };
        var window = new LayoutSettingsWindow { DataContext = layout };

        var radios = window.GetLogicalDescendants().OfType<RadioButton>().ToList();
        Assert.Equal(3, radios.Count);
        Assert.Single(radios, r => r.IsChecked == true); // exactly one selected (Contextual)

        // Selecting "Выключена" writes ToolbarMode.Off back through the two-way enum converter.
        var off = radios.First(r => (r.Content as string)!.Contains("Выключена"));
        off.IsChecked = true;

        Assert.Equal(ToolbarMode.Off, layout.ToolbarMode);
    }
}
