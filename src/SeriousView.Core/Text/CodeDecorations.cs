using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SeriousView.Core.Text;

/// <summary>What a decorated token is — drives the color in the view.</summary>
public enum CodeDecorationKind
{
    Timestamp,
    Uuid,
    Mac,
    Ip,
    Email,
    Hash,
    FilePath,
    Todo,
    LogLevel,
    HtmlEntity,
    Unit,
    Date,
}

/// <summary>One decorated token on a line. Offsets are line-relative.
/// Tooltip is the resolved/expanded value (decoded entity, byte count, relative date) or null.</summary>
public sealed record CodeDecoration(int Start, int Length, CodeDecorationKind Kind, string? Tooltip);

/// <summary>Pure cv-* token scanner for code-view lines (ported): timestamps, uuids, mac/ip,
/// emails, hashes, file:line:col paths, TODO markers, log levels, HTML entities, units and dates.
/// One composite alternation — earlier groups win (ISO timestamps beat bare dates).</summary>
public static class CodeDecorations
{
    /// <summary>ReDoS guard: the greedy email/path classes backtrack quadratically on token-less
    /// blobs, so overlong lines (minified JS, base64) are left undecorated — as in the original.</summary>
    public const int MaxLineLength = 2000;

    // Group order is priority order. 'ts' before 'date' so full ISO timestamps keep the
    // timestamp kind; hash sizes longest-first so a 64-hex digest isn't split into a 32.
    private static readonly Regex Token = new(
        @"(?<ts>(?<!\d)\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?(?:Z|[+-]\d{2}:?\d{2})?)" +
        @"|(?<uuid>\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b)" +
        @"|(?<mac>\b[0-9A-Fa-f]{2}(?::[0-9A-Fa-f]{2}){5}\b)" +
        @"|(?<ip>(?<!\d)(?:\d{1,3}\.){3}\d{1,3}(?!\d))" +
        @"|(?<email>\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b)" +
        @"|(?<hash>\b(?:[0-9a-fA-F]{64}|[0-9a-fA-F]{40}|[0-9a-fA-F]{32})\b)" +
        @"|(?<path>[\w./\\\-]+\.\w+:\d+(?::\d+)?)" +
        @"|(?<todo>\b(?:TODO|FIXME|XXX|HACK|BUG|NOTE)\b)" +              // uppercase only, intentional
        @"|(?<log>\b(?:ERROR|FATAL|CRITICAL|WARNING|WARN|INFO|DEBUG|TRACE)\b)" +
        @"|(?<entity>&(?:[A-Za-z][A-Za-z0-9]{1,30}|#\d{1,7}|#x[0-9a-fA-F]{1,6});)" +
        @"|(?<unit>[-+]?\d+(?:[.,]\d+)?\s?(?:%|‰|₽|\$|€|¥|£|млрд|млн|тыс|руб\.?|долл\.?|евро" +
        @"|KiB|MiB|GiB|TiB|KB|MB|GB|TB|PB|Кб|Мб|Гб|Тб|kbps|Mbps|Gbps|bps|ms|μs|mcs|ns" +
        // Lookahead, not \b: \b silently fails after symbol-terminated units (%, ₽, €).
        @"|°C|°F|px|pt|em|rem|vh|vw)(?![A-Za-z0-9а-яА-ЯёЁ]))" +
        @"|(?<date>(?<!\d)(?:\d{2}\.\d{2}\.\d{4}|\d{4}-\d{2}-\d{2})(?!\d)" +
        @"|\b(?:\d{1,2}\s+)?(?i:январ[ья]|феврал[ья]|март[а]?|апрел[ья]|ма[йя]|июн[ья]|июл[ья]" +
        @"|август[а]?|сентябр[ья]|октябр[ья]|ноябр[ья]|декабр[ья])\s+\d{4}\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromMilliseconds(200));

    // Group separator = plain space, decimal = comma (ru-style; safe under InvariantGlobalization).
    private static readonly NumberFormatInfo RuNumbers = new()
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ",",
    };

    private static readonly (string Name, CodeDecorationKind Kind)[] Groups =
    {
        ("ts", CodeDecorationKind.Timestamp),
        ("uuid", CodeDecorationKind.Uuid),
        ("mac", CodeDecorationKind.Mac),
        ("ip", CodeDecorationKind.Ip),
        ("email", CodeDecorationKind.Email),
        ("hash", CodeDecorationKind.Hash),
        ("path", CodeDecorationKind.FilePath),
        ("todo", CodeDecorationKind.Todo),
        ("log", CodeDecorationKind.LogLevel),
        ("entity", CodeDecorationKind.HtmlEntity),
        ("unit", CodeDecorationKind.Unit),
        ("date", CodeDecorationKind.Date),
    };

    /// <summary>Scans one line; <paramref name="today"/> anchors the relative-date tooltips.</summary>
    public static IReadOnlyList<CodeDecoration> ScanLine(string line, DateOnly today)
    {
        if (string.IsNullOrEmpty(line) || line.Length > MaxLineLength)
            return Array.Empty<CodeDecoration>();

        List<CodeDecoration>? hits = null;
        try
        {
            foreach (Match m in Token.Matches(line))
            {
                foreach (var (name, kind) in Groups)
                {
                    if (!m.Groups[name].Success)
                        continue;

                    var tooltip = ResolveTooltip(kind, m.Value, today, out var keep);
                    if (keep)
                        (hits ??= new List<CodeDecoration>()).Add(new CodeDecoration(m.Index, m.Length, kind, tooltip));
                    break;
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Defense in depth on pathological input: keep whatever was found before the timeout.
        }

        return (IReadOnlyList<CodeDecoration>?)hits ?? Array.Empty<CodeDecoration>();
    }

    private static string? ResolveTooltip(CodeDecorationKind kind, string raw, DateOnly today, out bool keep)
    {
        keep = true;
        switch (kind)
        {
            case CodeDecorationKind.HtmlEntity:
                var decoded = WebUtility.HtmlDecode(raw);
                // The decoder returns unknown entities unchanged — leave those as plain text.
                keep = decoded != raw;
                return keep ? decoded : null;
            case CodeDecorationKind.Unit:
                return ExpandUnit(raw);
            case CodeDecorationKind.Date:
                return DescribeDate(raw, today);
            default:
                return null;
        }
    }

    // ---- unit expansion ("5 MB" → "5 242 880 байт", "42%" → "0,42") ----

    private static readonly Dictionary<string, double> ByteMultipliers = new(StringComparer.Ordinal)
    {
        ["KB"] = 1024,
        ["Кб"] = 1024,
        ["KiB"] = 1024,
        ["MB"] = 1024d * 1024,
        ["Мб"] = 1024d * 1024,
        ["MiB"] = 1024d * 1024,
        ["GB"] = Math.Pow(1024, 3),
        ["Гб"] = Math.Pow(1024, 3),
        ["GiB"] = Math.Pow(1024, 3),
        ["TB"] = Math.Pow(1024, 4),
        ["Тб"] = Math.Pow(1024, 4),
        ["TiB"] = Math.Pow(1024, 4),
        ["PB"] = Math.Pow(1024, 5),
    };

    private static readonly Dictionary<string, double> ScaleMultipliers = new(StringComparer.Ordinal)
    {
        ["млрд"] = 1e9,
        ["млн"] = 1e6,
        ["тыс"] = 1e3,
    };

    private static string? ExpandUnit(string raw)
    {
        var i = 0;
        if (i < raw.Length && raw[i] is '-' or '+')
            i++;
        while (i < raw.Length && (char.IsAsciiDigit(raw[i]) || raw[i] is '.' or ','))
            i++;
        // Keep the optional leading sign in the parsed slice and let TryParse apply it
        // (NumberStyles.Float allows a leading sign) — no separate re-parse of the sign.
        var numText = raw[..i].Replace(',', '.');
        if (!double.TryParse(numText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;
        var unit = raw[i..].Trim();

        if (ByteMultipliers.TryGetValue(unit, out var bytes))
            return (value * bytes).ToString("#,0", RuNumbers) + " байт";
        if (ScaleMultipliers.TryGetValue(unit, out var scale))
            return (value * scale).ToString("#,0", RuNumbers);
        return unit switch
        {
            "%" => (value / 100).ToString("0.######", RuNumbers),
            "‰" => (value / 1000).ToString("0.######", RuNumbers),
            "ms" => value.ToString("0.###", RuNumbers) + "·10⁻³ с",
            "μs" or "mcs" => value.ToString("0.###", RuNumbers) + "·10⁻⁶ с",
            "ns" => value.ToString("0.###", RuNumbers) + "·10⁻⁹ с",
            // Currencies: group-separate big amounts; small ones gain nothing.
            "₽" or "$" or "€" or "¥" or "£" or "руб" or "руб." or "долл" or "долл." or "евро"
                => Math.Abs(value) >= 1000 ? value.ToString("#,0.##", RuNumbers) + " " + unit.TrimEnd('.') : null,
            _ => null,
        };
    }

    // ---- relative-date tooltip ("13.06.2026" → "через 2 дня") ----

    private static readonly (string Prefix, int Month)[] RuMonths =
    {
        ("янв", 1), ("фев", 2), ("мар", 3), ("апр", 4), ("ма", 5), ("июн", 6),
        ("июл", 7), ("авг", 8), ("сен", 9), ("окт", 10), ("ноя", 11), ("дек", 12),
    };

    private static string? DescribeDate(string raw, DateOnly today)
    {
        if (!TryParseDateToken(raw, out var date))
            return null;

        var days = date.DayNumber - today.DayNumber;
        return days switch
        {
            0 => "сегодня",
            > 0 => $"через {days} {PluralDays(days)}",
            _ => $"{-days} {PluralDays(-days)} назад",
        };
    }

    private static bool TryParseDateToken(string raw, out DateOnly date)
    {
        date = default;
        if (raw.Length == 10 && raw[2] == '.' && raw[5] == '.')
            return DateOnly.TryParseExact(raw, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        if (raw.Length == 10 && raw[4] == '-' && raw[7] == '-')
            return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        // "[12 ]февраля 2026" — optional day, month word, year.
        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (2 or 3))
            return false;
        var day = 1;
        var monthIdx = 0;
        if (parts.Length == 3)
        {
            if (!int.TryParse(parts[0], out day) || day is < 1 or > 31)
                return false;
            monthIdx = 1;
        }

        var word = parts[monthIdx].ToLowerInvariant();
        var month = 0;
        foreach (var (prefix, value) in RuMonths)
        {
            if (word.StartsWith(prefix, StringComparison.Ordinal))
            {
                month = value;
                break;
            }
        }

        if (month == 0 || !int.TryParse(parts[monthIdx + 1], out var year))
            return false;
        try
        {
            date = new DateOnly(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string PluralDays(int n)
    {
        var tail = n % 100;
        if (tail is >= 11 and <= 14)
            return "дней";
        return (n % 10) switch
        {
            1 => "день",
            2 or 3 or 4 => "дня",
            _ => "дней",
        };
    }
}
