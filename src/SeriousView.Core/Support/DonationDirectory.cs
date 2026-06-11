namespace SeriousView.Core.Support;

/// <summary>How a supporter can tip the author — drives the Support window's UI per entry:
/// a <see cref="Link"/> opens in the browser; a <see cref="CryptoAddress"/> shows a copyable
/// wallet address plus a link to the recommended wallet.</summary>
public enum DonationKind
{
    Link,
    CryptoAddress,
}

/// <summary>One way to support the author. For <see cref="DonationKind.Link"/> the
/// <see cref="Target"/> is a URL to open; for <see cref="DonationKind.CryptoAddress"/> it is the
/// receive address (copied to the clipboard), with <see cref="WalletName"/>/<see cref="WalletUrl"/>
/// pointing at a recommended wallet to send from.</summary>
public sealed record DonationMethod(
    DonationKind Kind,
    string Title,
    string Description,
    string Target,
    string? WalletName = null,
    string? WalletUrl = null);

/// <summary>
/// Canonical donation requisites for SeriousView — the single source of truth the Support window
/// renders. These are the author's PUBLIC receive endpoints (a tip link + crypto receive
/// addresses), so shipping them in source is intentional and safe; nothing here is a secret.
/// Mirrors the same author's browser-extension "Support" tab so the two stay in sync.
/// </summary>
public static class DonationDirectory
{
    /// <summary>Russian-card tip page (CloudTips). Opens in the default browser.</summary>
    public const string CloudTipsUrl = "https://pay.cloudtips.ru/p/9b14d4f1";

    /// <summary>Toncoin (TON) receive address.</summary>
    public const string TonAddress = "UQBMEMUpZZmrnnZoFseXuewWD1RkyVYw5EuBqTAOIl-AuOgM";

    /// <summary>USDT on the TRON network (TRC20) receive address.</summary>
    public const string UsdtTrc20Address = "TLuHigjqe8gjwfidfi2F7SZ4z27e4uShS6";

    /// <summary>The supported tip methods, in display order: card link first, then crypto.</summary>
    public static IReadOnlyList<DonationMethod> Methods { get; } = new[]
    {
        new DonationMethod(
            DonationKind.Link,
            "Картой РФ",
            "CloudTips · откроется в браузере",
            CloudTipsUrl),
        new DonationMethod(
            DonationKind.CryptoAddress,
            "Toncoin (TON)",
            "Бесплатно · ~5 сек",
            TonAddress,
            WalletName: "Tonkeeper",
            WalletUrl: "https://tonkeeper.com/"),
        new DonationMethod(
            DonationKind.CryptoAddress,
            "USDT (TRC20)",
            "~$1–3 комиссия · ~3 сек",
            UsdtTrc20Address,
            WalletName: "Trust Wallet",
            WalletUrl: "https://trustwallet.com/"),
    };
}
