using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Reactive;
using Tittle.Core.Settings;
using Tittle.Shared;

namespace Tittle.Features.Viewer;

// Split-view geometry: drives SplitGrid's track sizes, the Horizontal/Vertical orientation and the
// splitter visibility so the SINGLE Source/Preview subtrees serve Source-only, Preview-only AND
// side-by-side split (never duplicated or reparented). Mirrors MainWindow's code-managed outline
// column: a star/star GridSplitter rewrites the track lengths live, and we read the ratio back via an
// observable (no fragile GridLength↔double binding). Split out of the DocumentView core; same class.
public partial class DocumentView
{
    // Guards the programmatic definition rebuild from being read back as a user drag.
    private bool _suppressRatioCapture;
    // Set while writing Layout.SplitRatio from a drag, so the Layout.PropertyChanged echo doesn't
    // rebuild the grid (the splitter already moved the tracks; only persistence needs the value).
    private bool _ratioFromDrag;
    // Subscription to the source track's length — re-pointed on orientation flip (Column↔Row).
    private IDisposable? _splitTrackSub;

    /// <summary>Wire the split layout to the current VM's shared Layout and lay it out once.</summary>
    private void WireSplitLayout()
    {
        if (_vm?.Layout is { } layout)
            layout.PropertyChanged += OnLayoutPropertyChanged;
        ApplySplitLayout();
    }

    /// <summary>Tear down the Layout subscription + the track observable (called from Unsubscribe).</summary>
    private void UnwireSplitLayout()
    {
        if (_vm?.Layout is { } layout)
            layout.PropertyChanged -= OnLayoutPropertyChanged;
        _splitTrackSub?.Dispose();
        _splitTrackSub = null;
    }

    private void OnLayoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutOptions.SplitOrientation))
            ApplySplitLayout();
        else if (e.PropertyName == nameof(LayoutOptions.SplitRatio) && !_ratioFromDrag)
            ApplySplitLayout(); // external ratio change (import/settings) → re-apply; a drag echo is skipped
    }

    /// <summary>(Re)build the split grid: track lengths for source/splitter/preview, the orientation
    /// (Columns vs Rows) and the splitter visibility, then re-point the ratio read-back subscription.</summary>
    private void ApplySplitLayout()
    {
        if (_vm is null)
            return;

        var split = _vm.ShowSplit;
        var horizontal = _vm.Layout is not { SplitOrientation: SplitOrientation.Vertical };
        var ratio = LayoutOptions.ClampSplitRatio(_vm.Layout?.SplitRatio ?? LayoutOptions.DefaultSplitRatio);

        // Track lengths. In split both panes share the space by ratio with the splitter between; in a
        // single mode the shown pane takes all the space and the other collapses to 0 (its host is also
        // hidden via ShowSourcePane/ShowPreviewPane, so it leaves layout entirely).
        GridLength src, sp, prev;
        if (split)
        {
            src = new GridLength(ratio, GridUnitType.Star);
            sp = GridLength.Auto;
            prev = new GridLength(1 - ratio, GridUnitType.Star);
        }
        else if (_vm.ShowPreviewPane && !_vm.ShowSourcePane)
        {
            src = new GridLength(0);
            sp = new GridLength(0);
            prev = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            src = new GridLength(1, GridUnitType.Star);
            sp = new GridLength(0);
            prev = new GridLength(0);
        }

        _suppressRatioCapture = true;
        _splitTrackSub?.Dispose();
        _splitTrackSub = null;

        SplitGrid.ColumnDefinitions.Clear();
        SplitGrid.RowDefinitions.Clear();

        if (horizontal)
        {
            SplitGrid.ColumnDefinitions.Add(new ColumnDefinition(src));
            SplitGrid.ColumnDefinitions.Add(new ColumnDefinition(sp));
            SplitGrid.ColumnDefinitions.Add(new ColumnDefinition(prev));
            SplitGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            SetCell(SourceHost, col: 0);
            SetCell(SplitSplitter, col: 1);
            SetCell(PreviewHost, col: 2);
            SplitSplitter.ResizeDirection = GridResizeDirection.Columns;
            SplitSplitter.Width = 5;
            SplitSplitter.Height = double.NaN;
            SplitSplitter.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        }
        else
        {
            SplitGrid.RowDefinitions.Add(new RowDefinition(src));
            SplitGrid.RowDefinitions.Add(new RowDefinition(sp));
            SplitGrid.RowDefinitions.Add(new RowDefinition(prev));
            SplitGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            SetCell(SourceHost, row: 0);
            SetCell(SplitSplitter, row: 1);
            SetCell(PreviewHost, row: 2);
            SplitSplitter.ResizeDirection = GridResizeDirection.Rows;
            SplitSplitter.Height = 5;
            SplitSplitter.Width = double.NaN;
            SplitSplitter.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
        }

        SplitSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
        SplitSplitter.VerticalAlignment = VerticalAlignment.Stretch;
        SplitSplitter.IsVisible = split;

        if (split)
        {
            // The splitter rewrites the two star tracks live; read the source fraction back so a drag
            // persists (LayoutOptions.SplitRatio → settings.json on close). Re-pointed each rebuild
            // because the orientation flip swaps a ColumnDefinition for a RowDefinition.
            _splitTrackSub = horizontal
                ? SplitGrid.ColumnDefinitions[0].GetObservable(ColumnDefinition.WidthProperty)
                    .Subscribe(new AnonymousObserver<GridLength>(_ => CaptureSplitRatioFromDrag()))
                : SplitGrid.RowDefinitions[0].GetObservable(RowDefinition.HeightProperty)
                    .Subscribe(new AnonymousObserver<GridLength>(_ => CaptureSplitRatioFromDrag()));
        }

        _suppressRatioCapture = false;
    }

    private void CaptureSplitRatioFromDrag()
    {
        if (_suppressRatioCapture || _vm is not { ShowSplit: true, Layout: { } layout })
            return;

        var horizontal = layout.SplitOrientation != SplitOrientation.Vertical;
        var a = horizontal ? SplitGrid.ColumnDefinitions[0].Width.Value : SplitGrid.RowDefinitions[0].Height.Value;
        var b = horizontal ? SplitGrid.ColumnDefinitions[2].Width.Value : SplitGrid.RowDefinitions[2].Height.Value;
        if (a <= 0 || b <= 0)
            return;

        var ratio = LayoutOptions.ClampSplitRatio(a / (a + b));
        _ratioFromDrag = true;
        try { layout.SplitRatio = ratio; }
        finally { _ratioFromDrag = false; }
    }

    private static void SetCell(Control c, int col = 0, int row = 0)
    {
        Grid.SetColumn(c, col);
        Grid.SetRow(c, row);
    }

    // Test seams (headless): assert the split geometry without synthesizing pointer drags.
    internal bool SplitSplitterVisibleForTest => SplitSplitter.IsVisible;

    internal bool SplitIsHorizontalForTest => SplitGrid.ColumnDefinitions.Count == 3;

    internal (double Source, double Preview) SplitTracksForTest => SplitGrid.ColumnDefinitions.Count == 3
        ? (SplitGrid.ColumnDefinitions[0].Width.Value, SplitGrid.ColumnDefinitions[2].Width.Value)
        : (SplitGrid.RowDefinitions[0].Height.Value, SplitGrid.RowDefinitions[2].Height.Value);

    // Test seam: simulate the splitter dragging the source track to a fraction (no real pointer).
    // The preview track is set first so the source track's change observable (the one we subscribe to,
    // mirroring how a real GridSplitter settles both tracks) reads a consistent pair.
    internal void SimulateSplitDragForTest(double sourceFraction)
    {
        if (SplitGrid.ColumnDefinitions.Count == 3)
        {
            SplitGrid.ColumnDefinitions[2].Width = new GridLength(1 - sourceFraction, GridUnitType.Star);
            SplitGrid.ColumnDefinitions[0].Width = new GridLength(sourceFraction, GridUnitType.Star);
        }
        else
        {
            SplitGrid.RowDefinitions[2].Height = new GridLength(1 - sourceFraction, GridUnitType.Star);
            SplitGrid.RowDefinitions[0].Height = new GridLength(sourceFraction, GridUnitType.Star);
        }
    }
}
