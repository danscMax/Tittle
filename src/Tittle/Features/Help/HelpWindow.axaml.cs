using Avalonia.Controls;
using Tittle.Shared;

namespace Tittle.Features.Help;

/// <summary>Static shortcuts reference (ported help modal). Esc-close comes from <see cref="ModalWindow"/>.</summary>
public partial class HelpWindow : ModalWindow
{
    private static readonly (string Keys, string Action)[] Shortcuts =
    {
        ("Ctrl+O", "Открыть файл(ы)"),
        ("Ctrl+K · Ctrl+Shift+P", "Палитра команд"),
        ("Ctrl+F", "Найти в документе"),
        ("Ctrl+G", "Перейти к строке"),
        ("Ctrl+W", "Закрыть вкладку"),
        ("Ctrl+Tab / Ctrl+Shift+Tab", "Следующая / предыдущая вкладка"),
        ("Ctrl+± / Ctrl+0 / Ctrl+колесо", "Масштаб шрифта"),
        ("Ctrl+L", "Номера строк"),
        ("Alt+Z", "Перенос строк"),
        ("F1", "Эта справка"),
        ("Esc", "Закрыть поиск / окно"),
    };

    public HelpWindow()
    {
        InitializeComponent();
        foreach (var (keys, action) in Shortcuts)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("210,*"), Margin = new(0, 3) };
            var key = new TextBlock { Text = keys };
            key.Classes.Add("key");
            var label = new TextBlock { Text = action };
            Grid.SetColumn(label, 1);
            row.Children.Add(key);
            row.Children.Add(label);
            ShortcutList.Items.Add(row);
        }
    }
}
