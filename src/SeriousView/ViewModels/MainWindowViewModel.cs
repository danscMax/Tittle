using System;
using System.IO;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeriousView.Core.Abstractions;

namespace SeriousView.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialog;
    private readonly IFileReader _fileReader;

    [ObservableProperty]
    private string _title = "SeriousView";

    [ObservableProperty]
    private string _headerText = "SeriousView";

    [ObservableProperty]
    private string _statusText = "Готово";

    /// <summary>Extension (e.g. ".cs") driving TextMate grammar selection in the View.</summary>
    [ObservableProperty]
    private string? _grammarExtension;

    /// <summary>The editor document, bound to <c>TextEditor.Document</c> in the View.</summary>
    public TextDocument Document { get; } = new();

    public MainWindowViewModel(IFileDialogService fileDialog, IFileReader fileReader, string[] args)
    {
        _fileDialog = fileDialog;
        _fileReader = fileReader;

        var startupPath = args.Length > 0 ? args[0] : null;
        if (startupPath is not null && _fileReader.Exists(startupPath))
            LoadFromText(_fileReader.ReadAllText(startupPath), startupPath);
        else
            LoadSample();
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = await _fileDialog.PickFileAsync();
        if (path is null)
            return;

        try
        {
            var text = await _fileReader.ReadAllTextAsync(path);
            LoadFromText(text, path);
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка чтения: " + ex.Message;
        }
    }

    private void LoadFromText(string text, string path)
    {
        Document.Text = text;
        GrammarExtension = Path.GetExtension(path);

        var name = Path.GetFileName(path);
        HeaderText = name + "   —   " + path;
        Title = name + " — SeriousView";
        StatusText = $"Строк: {Document.LineCount}   ·   Символов: {Document.TextLength}";
    }

    private void LoadSample()
    {
        Document.Text = Sample;
        GrammarExtension = ".cs"; // C# highlighting for the built-in sample
        HeaderText = "Встроенный пример   —   запусти как:  SeriousView <путь-к-файлу>";
        StatusText = $"Строк: {Document.LineCount}   ·   подсветка: C# (Dark+)";
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
