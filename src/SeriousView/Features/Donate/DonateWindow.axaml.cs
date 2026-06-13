using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SeriousView.Core.Support;
using SeriousView.Core.Text;
using SeriousView.Shared;

namespace SeriousView.Features.Donate;

/// <summary>
/// "Поддержать автора" — premium donation modal. The static chrome (header badge, hero card link,
/// footer) lives in XAML; the crypto cards are appended from <see cref="DonationDirectory"/> so the
/// requisites stay single-sourced. Self-contained (like the other modals): URLs open through the
/// top-level launcher, addresses copy via the window's own clipboard — no DI. Esc-close comes from
/// <see cref="ModalWindow"/>.
/// </summary>
public partial class DonateWindow : ModalWindow
{
    public DonateWindow()
    {
        InitializeComponent();
        foreach (var method in DonationDirectory.Methods.Where(m => m.Kind == DonationKind.CryptoAddress))
            CryptoPanel.Children.Add(BuildCryptoCard(method));
    }

    // A bordered card with an accent leading edge: header (title · fee chip), the recommended-wallet
    // link, then the address in an inset monospace pill with an icon Copy button.
    private Control BuildCryptoCard(DonationMethod method)
    {
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock { Text = method.Title, VerticalAlignment = VerticalAlignment.Center };
        title.Classes.Add("cardtitle");
        titleRow.Children.Add(title);
        var chip = new Border { Child = new TextBlock { Text = method.Description } };
        chip.Classes.Add("chip");
        titleRow.Children.Add(chip);

        var content = new StackPanel { Spacing = 11, Margin = new(15, 13, 14, 14) };
        content.Children.Add(titleRow);

        if (method.WalletName is { } walletName && method.WalletUrl is { } walletUrl)
        {
            var linkRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
            linkRow.Children.Add(new TextBlock { Text = $"Кошелёк: {walletName}", VerticalAlignment = VerticalAlignment.Center });
            linkRow.Children.Add(new PathIcon { Data = Geo("IconExternalLink"), Width = 11, Height = 11, VerticalAlignment = VerticalAlignment.Center });
            var link = new Button { Content = linkRow };
            link.Classes.Add("link");
            link.Click += (_, _) => OpenUrl(walletUrl);
            content.Children.Add(link);
        }

        var address = new SelectableTextBlock { Text = method.Target };
        address.Classes.Add("addr");

        var copyIcon = new PathIcon { Data = Geo("IconCopy"), Width = 14, Height = 14 };
        var copy = new Button { Content = copyIcon };
        copy.Classes.Add("copy");
        ToolTip.SetTip(copy, "Скопировать адрес");
        copy.Click += (_, _) => CopyAddress(method.Target, copy, copyIcon);

        var pillGrid = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(copy, 1);
        pillGrid.Children.Add(address);
        pillGrid.Children.Add(copy);
        var pill = new Border { Child = pillGrid };
        pill.Classes.Add("addrpill");
        content.Children.Add(pill);

        var edge = new Border { Width = 3, VerticalAlignment = VerticalAlignment.Stretch };
        edge.Classes.Add("accentedge");

        var grid = new Grid { ColumnDefinitions = new("3,*") };
        Grid.SetColumn(content, 1);
        grid.Children.Add(edge);
        grid.Children.Add(content);

        var card = new Border { Child = grid };
        card.Classes.Add("cryptocard");
        return card;
    }

    private Geometry? Geo(string key) => this.TryFindResource(key, out var res) ? res as Geometry : null;

    // Av11-idiomatic opener: routes through the OS default browser, cross-platform, best-effort.
    private async void OpenUrl(string url)
    {
        try
        {
            // Gate through the same http/https/mailto allowlist as SafeHyperlinkCommand. The only
            // source today is the hard-coded DonationDirectory (https), but this keeps every shell
            // hand-off consistent and refuses an unexpected scheme should that ever change.
            if (MarkdownLink.IsSafe(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                await Launcher.LaunchUriAsync(uri);
        }
        catch
        {
            // Best-effort: a missing browser / sandboxed shell must not crash the dialog.
        }
    }

    private async void CopyAddress(string address, Button button, PathIcon icon)
    {
        try
        {
            if (Clipboard is not { } clipboard)
                return;
            await clipboard.SetTextAsync(address);
            button.Classes.Add("done");
            icon.Data = Geo("IconCheck");
            DispatcherTimer.RunOnce(() =>
            {
                button.Classes.Remove("done");
                icon.Data = Geo("IconCopy");
            }, TimeSpan.FromSeconds(1.6));
        }
        catch
        {
            // Best-effort: no clipboard available must not crash the dialog.
        }
    }

    private void OnHeroClick(object? sender, RoutedEventArgs e)
    {
        if (DonationDirectory.Methods.FirstOrDefault(m => m.Kind == DonationKind.Link) is { } link)
            OpenUrl(link.Target);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
