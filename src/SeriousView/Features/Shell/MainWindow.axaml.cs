using System;
using System.Linq;
using Avalonia;                 // AttachDevTools extension lives in the Avalonia namespace
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Windowing;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Settings;

namespace SeriousView.Features.Shell;

public partial class MainWindow : AppWindow
{
    private readonly IAppSettingsService? _settings;

    // Window-state persistence. AppWindow with ExtendsContentIntoTitleBar reports Height as
    // (content + title-bar) but the Height *setter* takes the content height — so a naive
    // save/restore grows the window by the title-bar height on every launch. We measure that
    // constant chrome offset once the window is shown (in the Normal state) and compensate on
    // save, giving an exact, drift-free round-trip.
    private Size _normalSize;            // last actual size seen while Normal
    private PixelPoint _normalPosition;  // last position seen while Normal
    private bool _haveNormal;
    private double _requestedWidth;      // the size we asked for (XAML default or restored value)
    private double _requestedHeight;
    private Size _chromeOffset;          // actual − requested, measured once while Normal
    private bool _offsetMeasured;
    private bool _opened;                // gate: ignore property changes during InitializeComponent/restore

    // Parameterless ctor for the XAML designer.
    public MainWindow()
    {
        InitializeComponent();

        // Single title-bar strip: our content (brand + tabs + buttons) is drawn into the
        // title-bar area; the system min/max/close buttons sit on top of it (Windows).
        // Complex hit-testing lets the tabs/buttons receive clicks instead of dragging.
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

        _requestedWidth = Width;
        _requestedHeight = Height;

        PositionChanged += (_, _) => TrackNormalBounds();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        // Tunnel both so the shortcuts win over the focused editor (which otherwise swallows
        // Ctrl+L / Alt+Z and consumes the wheel for scrolling).
        AddHandler(PointerWheelChangedEvent, OnPointerWheel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
#if DEBUG
        this.AttachDevTools();
#endif
    }

    // Central keyboard-shortcut dispatcher. Tunnelling (preview) runs before the AvaloniaEdit editor
    // processes the key, so app shortcuts fire regardless of where focus sits; non-matching keys are
    // left untouched for the editor.
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        var command = (ctrl, shift, alt, e.Key) switch
        {
            (true, false, false, Key.O) => vm.OpenFileCommand,
            (true, false, false, Key.W) => vm.CloseActiveTabCommand,
            (true, false, false, Key.Tab) => vm.SelectNextTabCommand,
            (true, true, false, Key.Tab) => vm.SelectPreviousTabCommand,
            (true, false, false, Key.OemPlus or Key.Add) => vm.ZoomInCommand,
            (true, false, false, Key.OemMinus or Key.Subtract) => vm.ZoomOutCommand,
            (true, false, false, Key.D0 or Key.NumPad0) => vm.ZoomResetCommand,
            (true, false, false, Key.L) => vm.ToggleLineNumbersCommand,
            (false, false, true, Key.Z) => vm.ToggleWordWrapCommand,
            _ => (System.Windows.Input.ICommand?)null,
        };

        if (command is null)
            return;

        if (command.CanExecute(null))
            command.Execute(null);
        e.Handled = true;
    }

    // Ctrl + mouse wheel zooms the editor font (like VS Code).
    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || DataContext is not MainWindowViewModel vm)
            return;

        if (e.Delta.Y > 0)
            vm.Editor.ZoomIn();
        else if (e.Delta.Y < 0)
            vm.Editor.ZoomOut();
        e.Handled = true;
    }

    public MainWindow(MainWindowViewModel viewModel, IAppSettingsService settings) : this()
    {
        _settings = settings;
        DataContext = viewModel;
        RestoreWindow();
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

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _opened = true; // from here on, size/position changes are real (not XAML/restore writes)

        // Reserve room on the right for the system caption buttons so the title-bar content
        // (the Open button) never sits under min/max/close. RightInset is valid once shown.
        CaptionReserve.Width = TitleBar.RightInset;

        MeasureChromeOffset();

        // A restored position may land on a monitor that has since been disconnected — pull the
        // window back onto a visible screen. Screens are populated once the window is shown.
        EnsureOnScreen();
        TrackNormalBounds();

        // No OS blur (older Windows / some Linux): Background=Transparent would show the
        // desktop, so fall back to a solid window background.
        if (ActualTransparencyLevel == WindowTransparencyLevel.None
            && this.TryFindResource("WindowBackgroundBrush", out var res) && res is IBrush solid)
        {
            Background = solid;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        SaveOnClose();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WidthProperty || change.Property == HeightProperty
            || change.Property == ClientSizeProperty)
        {
            MeasureChromeOffset();
            TrackNormalBounds();
        }
        else if (change.Property == WindowStateProperty)
        {
            TrackNormalBounds();
        }
    }

    /// <summary>Restores the saved size/position/maximized state, or keeps the XAML default on first run.</summary>
    private void RestoreWindow()
    {
        if (_settings?.Current.Window is not { } w)
            return; // first run: keep the XAML default (1100×750, centred)

        if (w.Width < 200 || w.Height < 150)
            return; // ignore an implausibly small saved size

        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = _requestedWidth = w.Width;
        Height = _requestedHeight = w.Height;
        Position = new PixelPoint(w.X, w.Y);
        _normalSize = new Size(w.Width, w.Height);
        _normalPosition = new PixelPoint(w.X, w.Y);
        _haveNormal = true;

        if (w.Maximized)
            WindowState = WindowState.Maximized;
    }

    /// <summary>Re-centres the window if its current rectangle isn't reachable on any monitor.</summary>
    private void EnsureOnScreen()
    {
        if (_settings?.Current.Window is null || WindowState == WindowState.Maximized)
            return;

        var screens = Screens.All
            .Select(s => new ScreenArea(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height))
            .ToList();
        if (screens.Count == 0)
            return;

        var here = new WindowPlacement(Width, Height, Position.X, Position.Y, Maximized: false);
        if (WindowPlacementValidator.IsVisible(here, screens))
            return;

        var area = (Screens.Primary ?? Screens.All[0]).WorkingArea;
        Position = new PixelPoint(
            area.X + Math.Max(0, (area.Width - (int)Width) / 2),
            area.Y + Math.Max(0, (area.Height - (int)Height) / 2));
    }

    // Capture the constant chrome offset (title-bar height) once the window is laid out Normal.
    private void MeasureChromeOffset()
    {
        if (!_opened || _offsetMeasured || WindowState != WindowState.Normal
            || double.IsNaN(Width) || double.IsNaN(Height))
            return;

        _chromeOffset = new Size(
            Math.Max(0, Width - _requestedWidth),
            Math.Max(0, Height - _requestedHeight));
        _offsetMeasured = true;
    }

    private void TrackNormalBounds()
    {
        if (!_opened || WindowState != WindowState.Normal || double.IsNaN(Width) || double.IsNaN(Height))
            return;

        _normalSize = new Size(Width, Height);
        _normalPosition = Position;
        _haveNormal = true;
    }

    // Persist window placement and the open-tab session together in one atomic write on close.
    private void SaveOnClose()
    {
        if (_settings is null)
            return;

        var maximized = WindowState == WindowState.Maximized;
        // When maximized, Width/Height are the maximized rectangle — persist the tracked Normal bounds.
        var size = maximized && _haveNormal ? _normalSize : CurrentSize();
        var pos = maximized && _haveNormal ? _normalPosition : Position;

        // Undo the title-bar inflation so a later restore reproduces this exact window.
        var w = Math.Max(1, size.Width - _chromeOffset.Width);
        var h = Math.Max(1, size.Height - _chromeOffset.Height);

        var session = (DataContext as MainWindowViewModel)?.GetSession();
        _settings.Update(_settings.Current with
        {
            Window = new WindowPlacement(w, h, pos.X, pos.Y, maximized),
            Session = session,
        });
    }

    private Size CurrentSize() => new(
        double.IsNaN(Width) ? ClientSize.Width : Width,
        double.IsNaN(Height) ? ClientSize.Height : Height);
}
