using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tittle.Features.Shell;
using Xunit;

namespace Tittle.Tests.Features;

public class AccessibilityTests
{
    // The chrome's glyph buttons (↵ # A− A+ 📂 ⌘) and the go-to-line box read as their raw glyph (or
    // nothing) to a screen reader without an explicit name. Assert the accessible names are wired.
    // Read from the logical tree right after construction — no Show()/RunJobs(), so the FluentAvalonia
    // FontIcon render (which the headless font manager can't shape) never runs.
    [AvaloniaFact]
    public void ChromeGlyphControls_ExposeAccessibleNames()
    {
        var window = new MainWindow();

        var names = window.GetLogicalDescendants()
            .OfType<Control>()
            .Select(AutomationProperties.GetName)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

        Assert.Contains("Перенос строк", names);   // status bar ↵
        Assert.Contains("Уменьшить шрифт", names);  // status bar A−
        Assert.Contains("Размер шрифта", names);    // status bar {FontSize}
        Assert.Contains("Открыть файл", names);     // omnibar 📂
        Assert.Contains("Палитра команд", names);   // omnibar ⌘
        Assert.Contains("Номер строки", names);     // go-to-line box
    }
}
