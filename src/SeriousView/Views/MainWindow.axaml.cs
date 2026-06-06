using System;
using Avalonia;                 // AttachDevTools extension lives in the Avalonia namespace
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SeriousView.ViewModels;

namespace SeriousView.Views;

public partial class MainWindow : Window
{
    // Parameterless ctor for the XAML designer.
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
#if DEBUG
        this.AttachDevTools();
#endif
    }

    // NOTE: Avalonia 11.3 marks DragEventArgs.Data / DataFormats.Files obsolete in favour of the
    // newer DataTransfer API. The classic API still works; migrating to DataTransfer is follow-up.
#pragma warning disable CS0618
    private static void OnDragOver(object? sender, DragEventArgs e)
        => e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || e.Data.GetFiles() is not { } files)
            return;

        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is { } path)
                await vm.OpenPathAsync(path);
        }
    }
#pragma warning restore CS0618

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // No OS blur (older Windows / some Linux): Background=Transparent would show the
        // desktop, so fall back to a solid window background.
        if (ActualTransparencyLevel == WindowTransparencyLevel.None
            && this.TryFindResource("WindowBackgroundBrush", out var res) && res is IBrush solid)
        {
            Background = solid;
        }
    }
}
