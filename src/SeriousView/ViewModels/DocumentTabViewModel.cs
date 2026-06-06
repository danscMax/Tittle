using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using SeriousView.Core.Text;
using SeriousView.Shared;

namespace SeriousView.ViewModels;

/// <summary>One open document = one tab. Holds its content, grammar, header and
/// per-document status metrics. Content is pushed to the editor via
/// <c>EditorBehavior.Text</c> (AvaloniaEdit can't bind Document directly).</summary>
public partial class DocumentTabViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _header;

    [ObservableProperty]
    private string? _filePath;

    /// <summary>Extension (e.g. ".cs") driving TextMate grammar in the View.</summary>
    [ObservableProperty]
    private string? _grammarExtension;

    /// <summary>Per-document status line (lines / chars), surfaced in the status bar.</summary>
    [ObservableProperty]
    private string _statusText = "";

    /// <summary>Document text, bound one-way into the editor. Named DocumentText (not
    /// Content) to avoid colliding with TabViewItem.Content when bound inside a TabView.</summary>
    public string DocumentText { get; }

    private DocumentTabViewModel(string header, string content)
    {
        _header = header;
        DocumentText = content;
    }

    public static DocumentTabViewModel FromFile(string text, string path)
    {
        var tab = new DocumentTabViewModel(Path.GetFileName(path), text)
        {
            FilePath = path,
            GrammarExtension = Path.GetExtension(path),
        };
        tab.StatusText = $"Строк: {TextMetrics.LineCount(text)}   ·   Символов: {TextMetrics.CharCount(text)}";
        return tab;
    }

    public static DocumentTabViewModel CreateSample()
    {
        var tab = new DocumentTabViewModel("Пример", Sample) { GrammarExtension = ".cs" };
        tab.StatusText = $"Строк: {TextMetrics.LineCount(Sample)}   ·   подсветка: C#";
        return tab;
    }

    private const string Sample = @"// SeriousView — нативный markdown/code viewer
// Движок: Avalonia 11 + AvaloniaEdit (TextMate / Dark+)
using System;
using System.Collections.Generic;

namespace Demo
{
    /// <summary>Демонстрация подсветки синтаксиса.</summary>
    public sealed class Greeter
    {
        private readonly string _name;
        public Greeter(string name) => _name = name;

        public IEnumerable<int> Fibonacci(int n)
        {
            int a = 0, b = 1;
            for (var i = 0; i < n; i++)
            {
                yield return a;
                (a, b) = (b, a + b);   // деконструкция кортежа
            }
        }

        public void Run() => Console.WriteLine($""Привет, {_name}! 3.14 == {Math.PI:F2}"");
    }
}

/* Проверь:  выделение мышью · Ctrl+F (поиск) · номера строк · скролл ·
   открой реальный файл командой:  SeriousView C:\path\to\file.rs  */
";
}
