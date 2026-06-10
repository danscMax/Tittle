using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SeriousView.Features.Help;

/// <summary>Static shortcuts reference (ported help modal).</summary>
public partial class HelpWindow : Window
{
    private static readonly (string Keys, string Action)[] Shortcuts =
    {
        ("Ctrl+O", "Открыть файл(ы)"),
        ("Ctrl+K", "Палитра команд"),
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

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
