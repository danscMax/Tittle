using System;
using System.Linq;
using SeriousView.Core.Support;
using Xunit;

namespace SeriousView.Tests.Core;

/// <summary>The Support window renders <see cref="DonationDirectory"/> verbatim, so these tests pin
/// the canonical requisites and the per-kind invariants the UI relies on (a Link is an openable URL;
/// a CryptoAddress carries a non-empty address and an https wallet link).</summary>
public class DonationDirectoryTests
{
    [Fact]
    public void Requisites_match_the_canonical_values()
    {
        Assert.Equal("https://pay.cloudtips.ru/p/9b14d4f1", DonationDirectory.CloudTipsUrl);
        Assert.Equal("UQBMEMUpZZmrnnZoFseXuewWD1RkyVYw5EuBqTAOIl-AuOgM", DonationDirectory.TonAddress);
        Assert.Equal("TLuHigjqe8gjwfidfi2F7SZ4z27e4uShS6", DonationDirectory.UsdtTrc20Address);
    }

    [Fact]
    public void Link_target_is_an_absolute_https_url()
    {
        var link = DonationDirectory.Methods.Single(m => m.Kind == DonationKind.Link);

        Assert.True(Uri.TryCreate(link.Target, UriKind.Absolute, out var uri));
        Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
    }

    [Fact]
    public void Crypto_methods_have_a_non_empty_address_and_an_https_wallet_link()
    {
        var crypto = DonationDirectory.Methods.Where(m => m.Kind == DonationKind.CryptoAddress).ToList();

        Assert.Equal(2, crypto.Count); // TON + USDT
        foreach (var m in crypto)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Target));
            Assert.False(string.IsNullOrWhiteSpace(m.WalletName));
            Assert.True(Uri.TryCreate(m.WalletUrl, UriKind.Absolute, out var wallet));
            Assert.Equal(Uri.UriSchemeHttps, wallet!.Scheme);
        }
    }

    [Fact]
    public void Every_method_carries_a_title_and_description()
    {
        Assert.NotEmpty(DonationDirectory.Methods);
        Assert.All(DonationDirectory.Methods, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Title));
            Assert.False(string.IsNullOrWhiteSpace(m.Description));
        });
    }
}
