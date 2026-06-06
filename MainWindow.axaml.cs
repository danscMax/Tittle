using System;
using System.IO;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace MdViewer;

public partial class MainWindow : Window
{
    private readonly RegistryOptions? _registryOptions;
    private readonly TextMate.Installation? _textMate;

    // Parameterless ctor for the XAML designer.
    public MainWindow() : this(Array.Empty<string>()) { }

    public MainWindow(string[] args)
    {
        InitializeComponent();

        // VS Code "Dark+" theme + TextMate grammars (TextMateSharp).
        // MDVIEWER_NOTM env var disables TextMate — for RAM isolation measurement.
        if (Environment.GetEnvironmentVariable("MDVIEWER_NOTM") is null)
        {
            _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            _textMate = Editor.InstallTextMate(_registryOptions);
        }

        var path = args.Length > 0 ? args[0] : null;
        if (path != null && File.Exists(path))
            OpenFile(path);
        else
            LoadSample();
    }

    private void OpenFile(string path)
    {
        try
        {
            Editor.Document.Text = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            Editor.Document.Text = "// Не удалось открыть файл:\n// " + ex.Message;
            StatusText.Text = "Ошибка чтения";
            return;
        }

        SetGrammarForExtension(Path.GetExtension(path));

        var name = Path.GetFileName(path);
        HeaderText.Text = name + "   —   " + path;
        Title = name + " — MdViewer";
        StatusText.Text = $"Строк: {Editor.Document.LineCount}   ·   Символов: {Editor.Document.TextLength}";
    }

    private void SetGrammarForExtension(string ext)
    {
        if (_registryOptions is null || _textMate is null) return;
        try
        {
            var lang = _registryOptions.GetLanguageByExtension(ext);
            if (lang != null)
                _textMate.SetGrammar(_registryOptions.GetScopeByLanguageId(lang.Id));
        }
        catch
        {
            // No grammar for this extension — plain text, fine.
        }
    }

    private void LoadSample()
    {
        Editor.Document.Text = Sample;
        if (_registryOptions is not null)
            _textMate?.SetGrammar(_registryOptions.GetScopeByLanguageId("csharp"));
        HeaderText.Text = "Встроенный пример   —   запусти как:  MdViewer <путь-к-файлу>";
        StatusText.Text = $"Строк: {Editor.Document.LineCount}   ·   подсветка: C# (Dark+)";
    }

    private const string Sample = @"// MdViewer — PoC нативного редактора класса Notepad++
// Движок: Avalonia 12 + AvaloniaEdit (TextMate / Dark+)
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
   открой реальный файл командой:  MdViewer C:\path\to\file.rs  */
";
}
