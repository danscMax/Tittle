using Avalonia.Headless.XUnit;
using Tittle.Features.Shell;
using Xunit;

namespace Tittle.Tests.Features;

/// <summary>Guards the search-debounce fix (A5/M1 VM part): typing into the find bar re-scans the
/// whole document on every keystroke, so a large document coalesces the burst onto a short debounce.
/// Small documents stay synchronous (the scan is instant and the set-query→results contract holds).</summary>
public class DocumentTabSearchDebounceTests
{
    [AvaloniaFact]
    public void SearchQuery_LargeDocument_DefersScanToDebounce()
    {
        var big = new string('a', 250_000) + "\nneedle";
        var vm = DocumentTabViewModel.FromFile(big, "/big.txt");

        vm.SearchQuery = "needle";

        // The scan is coalesced onto the debounce timer (not pumped here) → no synchronous result.
        Assert.Equal(0, vm.SearchMatchCount);
        Assert.True(vm.SearchDebouncePending);
    }

    [AvaloniaFact]
    public void Dispose_StopsThePendingDebouncedScan()
    {
        // Audit V12: disposing the tab (on close) must stop the debounce timer so it can't keep the
        // closed VM and its document string rooted on the dispatcher timer queue.
        var big = new string('a', 250_000) + "\nneedle";
        var vm = DocumentTabViewModel.FromFile(big, "/big.txt");
        vm.SearchQuery = "needle";
        Assert.True(vm.SearchDebouncePending);

        vm.Dispose();

        Assert.False(vm.SearchDebouncePending);
    }

    [AvaloniaFact]
    public void SearchQuery_SmallDocument_ScansSynchronously()
    {
        var vm = DocumentTabViewModel.FromFile("alpha needle beta", "/small.txt");

        vm.SearchQuery = "needle";

        Assert.Equal(1, vm.SearchMatchCount);
        Assert.False(vm.SearchDebouncePending);
    }
}
