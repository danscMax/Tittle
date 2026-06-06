using System.IO;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SeriousView.ViewModels;

/// <summary>One open document = one tab. Owns its <see cref="TextDocument"/>,
/// grammar, and per-document status metrics.</summary>
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

    public TextDocument Document { get; } = new();

    private DocumentTabViewModel(string header) => _header = header;

    public static DocumentTabViewModel FromFile(string text, string path)
    {
        var tab = new DocumentTabViewModel(Path.GetFileName(path))
        {
            FilePath = path,
            GrammarExtension = Path.GetExtension(path),
        };
        tab.Document.Text = text;
        tab.StatusText = $"Строк: {tab.Document.LineCount}   ·   Символов: {tab.Document.TextLength}";
        return tab;
    }

    public static DocumentTabViewModel CreateSample()
    {
        var tab = new DocumentTabViewModel("Пример") { GrammarExtension = ".cs" };
        tab.Document.Text = Sample;
        tab.StatusText = $"Строк: {tab.Document.LineCount}   ·   подсветка: C# (Dark+)";
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
