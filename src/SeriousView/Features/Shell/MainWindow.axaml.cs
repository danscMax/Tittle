using System;
using System.Linq;
using Avalonia;                 // AttachDevTools extension lives in the Avalonia namespace
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Reactive;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Windowing;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Editing;
using SeriousView.Core.Settings;
using SeriousView.Features.Palette;
using SeriousView.Features.Settings;
using SeriousView.Platform;
using SeriousView.Shared;

namespace SeriousView.Features.Shell;

public partial class MainWindow : AppWindow
{
    private readonly IAppSettingsService? _settings;
    private bool _saved;

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

    // Outline sidebar width. The GridSplitter drives OutlineColumn live; we mirror the latest shown
    // width here and commit it to the (persisted) LayoutOptions once on close — routing every drag
    // pixel through Layout.PropertyChanged would rewrite settings.json continuously.
    private double _outlineWidth = LayoutOptions.DefaultOutlineWidth;

    // Tab drag-reorder: the tab grabbed on press, the press point, and whether we've passed the movement
    // threshold. The Tabs collection reorders live as the cursor crosses neighbours — the dragged tab is
    // itself the visual feedback, so there's no separate insertion adorner.
    private DocumentTabViewModel? _dragTab;
    private Point _dragStart;
    private bool _dragging;
    private const double DragThreshold = 5;

    // The outline sidebar is column [0] of the body grid. A named ColumnDefinition gets no generated
    // code-behind field (it isn't a control), so reach it through the named grid instead.
    private ColumnDefinition OutlineColumn => BodyGrid.ColumnDefinitions[0];

    // Parameterless ctor for the XAML designer.
    public MainWindow()
    {
        InitializeComponent();

        // Single title-bar strip: our content (brand + tabs + buttons) is drawn into the
        // title-bar area; the system min/max/close buttons sit on top of it (Windows).
        // Complex hit-testing lets the tabs/buttons receive clicks instead of dragging.
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

        // Match the caption-button band to our 40px strip so min/max/close align vertically with the
        // tabs instead of sitting high, and pin the glyph colour across active/inactive (FluentAvalonia
        // dims the buttons by window-activation state otherwise — reads as the colour "changing").
        TitleBar.Height = 40;
        ActualThemeVariantChanged += (_, _) => ApplyCaptionColors();
        ApplyCaptionColors();

        _requestedWidth = Width;
        _requestedHeight = Height;

        PositionChanged += (_, _) => TrackNormalBounds();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        // Tunnel both so the shortcuts win over the focused editor (which otherwise swallows
        // Ctrl+L / Alt+Z and consumes the wheel for scrolling).
        AddHandler(PointerWheelChangedEvent, OnPointerWheel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        // Go-to-line input (status bar): Enter jumps, Esc closes.
        GoToLineBox.KeyDown += OnGoToLineKeyDown;
        // Omnibar address field: Enter opens the typed path, Esc reverts to the active tab's path.
        OmnibarBox.KeyDown += OnOmnibarKeyDown;
        // Tab drag-reorder. Tunnel so we observe the gesture before the ListBox's own pointer handling.
        TabStrip.AddHandler(PointerPressedEvent, OnTabPointerPressed, RoutingStrategies.Tunnel);
        TabStrip.AddHandler(PointerMovedEvent, OnTabPointerMoved, RoutingStrategies.Tunnel);
        TabStrip.AddHandler(PointerReleasedEvent, OnTabPointerReleased, RoutingStrategies.Tunnel);
        // Entrance fade on new tabs (#23). Programmatic, not a styled animation: drag-reorder's
        // Move() recreates containers mid-gesture, and a styled entrance would replay on every swap.
        TabStrip.ContainerPrepared += OnTabContainerPrepared;
#if DEBUG
        this.AttachDevTools();
#endif
    }

    // Pin the caption-button glyph colour to the chrome foreground for the current theme, so the
    // min/max/close buttons don't change colour with window activation (see the ctor note).
    private void ApplyCaptionColors()
    {
        if (this.TryGetResource("ChromeForegroundBrush", ActualThemeVariant, out var res)
            && res is ISolidColorBrush brush)
        {
            TitleBar.ButtonForegroundColor = brush.Color;
            TitleBar.ButtonInactiveForegroundColor = brush.Color;
        }
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

        // Ctrl+K (and the VS Code-style Ctrl+Shift+P alias) open the command palette — a separate
        // top-level window, not a VM command.
        if ((ctrl && !shift && !alt && e.Key == Key.K) ||
            (ctrl && shift && !alt && e.Key == Key.P))
        {
            OpenCommandPalette(vm);
            e.Handled = true;
            return;
        }

        // Line operations carry a LineOp parameter, so they bypass the parameterless command switch below.
        var lineOp = (ctrl, shift, alt, e.Key) switch
        {
            (true, false, false, Key.D) => (LineOp?)LineOp.Duplicate,
            (false, false, true, Key.Up) => LineOp.MoveUp,
            (false, false, true, Key.Down) => LineOp.MoveDown,
            _ => null,
        };
        if (lineOp is { } op && vm.SelectedTab?.ApplyLineOpCommand is { } lineCmd && lineCmd.CanExecute(op))
        {
            lineCmd.Execute(op);
            e.Handled = true;
            return;
        }

        var command = (ctrl, shift, alt, e.Key) switch
        {
            (true, false, false, Key.O) => vm.OpenFileCommand,
            (true, false, false, Key.S) => vm.SaveActiveTabCommand, // M15 save
            (true, false, false, Key.W) => vm.CloseActiveTabCommand,
            (true, false, false, Key.Tab) => vm.SelectNextTabCommand,
            (true, true, false, Key.Tab) => vm.SelectPreviousTabCommand,
            (true, false, false, Key.OemPlus or Key.Add) => vm.ZoomInCommand,
            (true, false, false, Key.OemMinus or Key.Subtract) => vm.ZoomOutCommand,
            (true, false, false, Key.D0 or Key.NumPad0) => vm.ZoomResetCommand,
            (true, false, false, Key.L) => vm.ToggleLineNumbersCommand,
            (true, false, false, Key.G) => vm.OpenGoToLineCommand,
            (true, false, false, Key.F) => vm.OpenSearchCommand,
            // The physical "\|" key reports as Key.OemPipe on Windows (VK_OEM_5); OemBackslash is the
            // separate VK_OEM_102 "<>" key. Accept both so Ctrl+\ works across layouts.
            (true, false, false, Key.OemPipe or Key.OemBackslash) => vm.SelectedTab?.ToggleSplitCommand, // split (markdown; CanExecute gates)
            (false, false, false, Key.F1) => vm.ShowHelpCommand,
            (false, false, true, Key.Z) => vm.ToggleWordWrapCommand,
            _ => (System.Windows.Input.ICommand?)null,
        };

        if (command is null)
            return;

        if (command.CanExecute(null))
            command.Execute(null);
        e.Handled = true;

        // Ctrl+G just opened the go-to-line input — move focus into it.
        if (e.Key == Key.G && vm.SelectedTab?.IsGoToLineOpen == true)
            Dispatcher.UIThread.Post(() => { GoToLineBox.Focus(); GoToLineBox.SelectAll(); });
    }

    // Open the Ctrl+K command palette as a fresh owned top-level window (rebuilt each time so it reflects
    // the current commands / recent files / active tab). It closes itself on run / Esc / click-away.
    private void OpenCommandPalette(MainWindowViewModel vm)
    {
        var paletteVm = new CommandPaletteViewModel(vm.BuildPaletteItems());
        var window = new CommandPaletteWindow { DataContext = paletteVm };
        paletteVm.Closed += window.Close;
        window.Show(this);
    }

    // The omnibar ⌘ button opens the palette (same seam as Ctrl+K).
    private void OnPaletteButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            OpenCommandPalette(vm);
    }

    // Layout settings window (☰ ▸ Раскладка / palette). A single instance, kept open while the user toggles
    // knobs and watches the chrome update live; reactivated rather than re-created.
    private LayoutSettingsWindow? _layoutSettings;

    private void OpenLayoutSettings()
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        if (_layoutSettings is not null)
        {
            _layoutSettings.Activate();
            return;
        }

        _layoutSettings = new LayoutSettingsWindow { DataContext = vm.Layout, Diagrams = vm.Diagrams };
        _layoutSettings.Closed += (_, _) => _layoutSettings = null;
        _layoutSettings.Show(this);
    }

    private void OnStatsRequested(SeriousView.Core.Text.TextStats stats)
        => new Features.Stats.StatsWindow { DataContext = stats }.ShowDialog(this);

    private void OnHelpRequested()
        => new Features.Help.HelpWindow().ShowDialog(this);

    private void OnDonateRequested()
        => new Features.Donate.DonateWindow().ShowDialog(this);

    // Omnibar address field: Enter opens the typed path (reusing the open tab if any), Esc reverts to the
    // active tab's path. async-void event handler — OpenPathAsync is itself guarded (a bad path becomes an
    // error message, never a crash), so nothing can escape to the global backstop.
    private async void OnOmnibarKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        // async-void handler: contain any throw so it can't escape to the global crash backstop,
        // matching OnDrop. OpenPathAsync is itself guarded, so this is defense-in-depth.
        try
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                // Tolerate an Explorer "Copy as path" value (wrapped in quotes) and stray whitespace.
                var path = (vm.OmnibarText ?? string.Empty).Trim().Trim('"');
                if (path.Length > 0)
                    await vm.OpenPathAsync(path);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                vm.ResetOmnibar();
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Write(ex, "Omnibar");
        }
    }

    // Go-to-line input (status bar): Enter submits the jump, Esc closes.
    private void OnGoToLineKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { SelectedTab: { } tab })
            return;

        if (e.Key == Key.Enter)
        {
            tab.SubmitGoToLineCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            tab.CloseGoToLineCommand.Execute(null);
            e.Handled = true;
        }
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

    // --- Tab drag-reorder (#18). Press a tab and drag horizontally; Tabs reorders live as the cursor
    //     crosses each neighbour's midpoint. A plain click still selects, the close button still closes
    //     (we skip presses on it), and a right-click still opens the context menu (left button only). ---

    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _dragTab = null;
        _dragging = false;
        if (!e.GetCurrentPoint(TabStrip).Properties.IsLeftButtonPressed)
            return;
        // Leave the close button (and any future in-tab button) to its own click.
        if (e.Source is Visual v && (v is Button || v.GetVisualAncestors().OfType<Button>().Any()))
            return;
        if (TabItemFrom(e.Source)?.DataContext is DocumentTabViewModel tab)
        {
            _dragTab = tab;
            _dragStart = e.GetPosition(TabStrip);
        }
    }

    private void OnTabPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragTab is null || DataContext is not MainWindowViewModel vm)
            return;
        if (!e.GetCurrentPoint(TabStrip).Properties.IsLeftButtonPressed)
        {
            _dragTab = null; // the button came up outside the strip
            _dragging = false;
            return;
        }

        var x = e.GetPosition(TabStrip).X;
        if (!_dragging)
        {
            if (Math.Abs(x - _dragStart.X) < DragThreshold)
                return;
            _dragging = true; // crossed the threshold — this is a drag, not a click
        }

        vm.MoveTab(_dragTab, TargetIndexAt(x));
    }

    private void OnTabPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragTab = null;
        _dragging = false;
    }

    // Entrance fade for a freshly realized tab container (#23). Opacity only — Avalonia 11
    // keyframes have no animator for the composite RenderTransform (see Animations.axaml),
    // so a slide variant would crash. Close stays instant by design (matches VS Code).
    private static readonly Animation TabEntranceAnimation = new()
    {
        Duration = TimeSpan.FromMilliseconds(180),
        Easing = new CubicEaseOut(),
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, 0d) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, 1d) } },
        },
    };

    private void OnTabContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (!_dragging)
            _ = TabEntranceAnimation.RunAsync(e.Container);
    }

    // The slot the cursor is over: the first tab whose horizontal midpoint is right of the cursor, else
    // the last. The tab strip uses a (non-virtualizing) StackPanel, so every container is realized.
    private int TargetIndexAt(double x)
    {
        for (var i = 0; i < TabStrip.ItemCount; i++)
        {
            if (TabStrip.ContainerFromIndex(i) is Control c
                && x < (c.TranslatePoint(default, TabStrip)?.X ?? 0) + c.Bounds.Width / 2)
                return i;
        }
        return Math.Max(0, TabStrip.ItemCount - 1);
    }

    private static ListBoxItem? TabItemFrom(object? source)
        => source as ListBoxItem
           ?? (source as Visual)?.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();

    public MainWindow(MainWindowViewModel viewModel, IAppSettingsService settings) : this()
    {
        _settings = settings;
        DataContext = viewModel;
        RestoreWindow();
        WireOutlineSidebar(viewModel);
        // Named handlers (not lambdas) so SaveOnClose can detach them before disposing the VM —
        // harmless today (both are app-lifetime singletons) but a leak if the window ever recycles.
        viewModel.LayoutSettingsRequested += OpenLayoutSettings;
        viewModel.StatsRequested += OnStatsRequested;
        viewModel.HelpRequested += OnHelpRequested;
        viewModel.DonateRequested += OnDonateRequested;
        // The tab strip lays out horizontally; translate a vertical wheel into sideways scroll so
        // overflowing tabs are reachable by the wheel (Avalonia doesn't flip the wheel axis itself).
        TabStrip.PointerWheelChanged += OnTabStripWheel;
    }

    private ScrollViewer? _tabScroll;

    private void OnTabStripWheel(object? sender, PointerWheelEventArgs e)
    {
        _tabScroll ??= TabStrip.FindDescendantOfType<ScrollViewer>();
        if (_tabScroll is not { } sv || sv.Extent.Width <= sv.Viewport.Width + 0.5)
            return;

        var delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
        var max = sv.Extent.Width - sv.Viewport.Width;
        sv.Offset = new Vector(Math.Clamp(sv.Offset.X - delta * 48, 0, max), sv.Offset.Y);
        e.Handled = true;
    }

    // Restore the persisted outline width, follow live drags into a field, and expand/collapse the
    // column with the pane's visibility (a hidden pane must not leave a dead 240px gutter).
    private void WireOutlineSidebar(MainWindowViewModel vm)
    {
        _outlineWidth = LayoutOptions.ClampOutlineWidth(vm.Layout.OutlineWidth);
        OutlineColumn.GetObservable(ColumnDefinition.WidthProperty).Subscribe(new AnonymousObserver<GridLength>(w =>
        {
            if (vm.IsOutlinePaneVisible && w.IsAbsolute && w.Value >= 1)
                _outlineWidth = w.Value;
        }));
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsOutlinePaneVisible))
                ApplyOutlineColumn(vm.IsOutlinePaneVisible);
        };
        ApplyOutlineColumn(vm.IsOutlinePaneVisible);
    }

    // Shown → pixel width within [min,max]; hidden → collapse to 0 (no reserved gutter, no splitter).
    private void ApplyOutlineColumn(bool visible)
    {
        OutlineColumn.MinWidth = visible ? LayoutOptions.MinOutlineWidth : 0;
        OutlineColumn.MaxWidth = visible ? LayoutOptions.MaxOutlineWidth : double.PositiveInfinity;
        OutlineColumn.Width = new GridLength(
            visible ? LayoutOptions.ClampOutlineWidth(_outlineWidth) : 0, GridUnitType.Pixel);
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

        // async-void event handler: contain any throw (e.g. from TryGetLocalPath) so it can't escape
        // to the global crash backstop. OpenPathAsync is itself guarded (surfaces a status message).
        try
        {
            foreach (var file in files)
            {
                if (file.TryGetLocalPath() is { } path)
                    await vm.OpenPathAsync(path);
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Write(ex, "Drop");
        }
    }
#pragma warning restore CS0618

    // Size Col3 to the exact system caption-button width (DIPs). Sizing the ColumnDefinition (not a
    // child Border's Width) is what makes the star omnibar column reflow and keep the right-hand
    // cluster clear of min/max/close. (x:Name on a ColumnDefinition does not generate a field, so we
    // reach it by index on the named grid — same pattern as OutlineColumn.)
    private ColumnDefinition CaptionReserveColumn => TitleGrid.ColumnDefinitions[3];

    private void ApplyCaptionReserve() =>
        CaptionReserveColumn.Width = new GridLength(TitleBar.RightInset);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _opened = true; // from here on, size/position changes are real (not XAML/restore writes)

        // Reserve the system caption-button band on the COLUMN itself (see the XAML note) so the
        // right-hand cluster never sits under min/max/close. RightInset is valid once shown and is
        // already in DIPs for FluentAvalonia (no scale division). Re-apply on resize because RightInset
        // can change when the window moves to a monitor with a different DPI (WM_DPICHANGED resizes the
        // window) and the property raises no change notification of its own.
        ApplyCaptionReserve();
        Resized += (_, _) => ApplyCaptionReserve();

        MeasureChromeOffset();

        // A restored position may land on a monitor that has since been disconnected — pull the
        // window back onto a visible screen. Screens are populated once the window is shown.
        EnsureOnScreen();
        TrackNormalBounds();
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
    // Idempotent (the _saved guard): a programmatic desktop.Shutdown() / OS session-end fires
    // ShutdownRequested which also calls this (R5), and that may or may not be paired with OnClosing.
    internal void SaveOnClose()
    {
        if (_settings is null || _saved)
            return;
        _saved = true;

        // Land any debounced editor-option change (e.g. a last-moment zoom) before the window goes away.
        (DataContext as MainWindowViewModel)?.FlushEditorSettings();

        var maximized = WindowState == WindowState.Maximized;
        // When maximized, Width/Height are the maximized rectangle — persist the tracked Normal bounds.
        var size = maximized && _haveNormal ? _normalSize : CurrentSize();
        var pos = maximized && _haveNormal ? _normalPosition : Position;

        // Undo the title-bar inflation so a later restore reproduces this exact window.
        var w = Math.Max(1, size.Width - _chromeOffset.Width);
        var h = Math.Max(1, size.Height - _chromeOffset.Height);

        // One atomic write on close: fold the last shown outline width into the layout (tracked in a
        // field so dragging the splitter never rewrites settings.json per pixel), plus window + session.
        var vm = DataContext as MainWindowViewModel;
        var layout = vm is not null
            ? vm.Layout.ToSettings() with { OutlineWidth = LayoutOptions.ClampOutlineWidth(_outlineWidth) }
            : _settings.Current.Layout;
        _settings.Update(_settings.Current with
        {
            Layout = layout,
            Window = new WindowPlacement(w, h, pos.X, pos.Y, maximized),
            Session = vm?.GetSession(),
        });
        vm?.FlushViewState(); // accumulated visited marks persist alongside the session

        // Detach the VM->window event subscriptions wired in the constructor before disposing it.
        if (vm is not null)
        {
            vm.LayoutSettingsRequested -= OpenLayoutSettings;
            vm.StatsRequested -= OnStatsRequested;
            vm.HelpRequested -= OnHelpRequested;
            vm.DonateRequested -= OnDonateRequested;
        }

        // Detach every VM subscription and stop its timer now the window is going away. Dispose()
        // re-flushes editor settings internally (idempotent with the call above) so nothing is lost.
        vm?.Dispose();
    }

    private Size CurrentSize() => new(
        double.IsNaN(Width) ? ClientSize.Width : Width,
        double.IsNaN(Height) ? ClientSize.Height : Height);
}
