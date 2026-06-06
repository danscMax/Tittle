using System;
using Avalonia;                 // AttachDevTools extension lives in the Avalonia namespace
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Windowing;
using SeriousView.ViewModels;

namespace SeriousView.Views;

public partial class MainWindow : AppWindow
{
    // Parameterless ctor for the XAML designer.
    public MainWindow()
    {
        InitializeComponent();

        // Single title-bar strip: our content (brand + tabs + buttons) is drawn into the
        // title-bar area; the system min/max/close buttons sit on top of it (Windows).
        // Complex hit-testing lets the tabs/buttons receive clicks instead of dragging.
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

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

        // Reserve room on the right for the system caption buttons so the title-bar content
        // (the Open button) never sits under min/max/close. RightInset is valid once shown.
        CaptionReserve.Width = TitleBar.RightInset;

        // No OS blur (older Windows / some Linux): Background=Transparent would show the
        // desktop, so fall back to a solid window background.
        if (ActualTransparencyLevel == WindowTransparencyLevel.None
            && this.TryFindResource("WindowBackgroundBrush", out var res) && res is IBrush solid)
        {
            Background = solid;
        }
    }
}
