using System;
using System.Collections.Generic;
using System.Text;

namespace SeriousView.Core.Diagnostics;

/// <summary>
/// Pure protocol for the single-instance channel: encodes the forwarded command-line file arguments
/// to bytes and back, and derives the per-user Mutex + pipe names. No I/O, no Avalonia, no pipe types —
/// fully unit-testable; the Mutex/NamedPipe plumbing lives in <c>Platform/SingleInstanceGate</c> (the
/// same pure-core / platform-impl split as <c>Core/Diagnostics/CrashLog</c> + <c>Platform/CrashLogger</c>).
/// </summary>
public static class SingleInstanceMessage
{
    private const string Header = "SV1";          // version/magic so a format change is ignored, not mis-parsed
    private const char Separator = '';      // Unit Separator: illegal in paths; safer than a space

    /// <summary>Encodes args as one UTF-8 header+separator line. Empty/whitespace args are dropped, and
    /// the separator / newlines are stripped from each arg so one path can't split into two.</summary>
    public static byte[] Encode(IReadOnlyList<string> args)
    {
        var sb = new StringBuilder(Header);
        foreach (var a in args)
        {
            if (string.IsNullOrWhiteSpace(a))
                continue;
            sb.Append(Separator).Append(a.Replace(Separator, ' ').Replace('\n', ' ').Replace('\r', ' '));
        }
        sb.Append('\n');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Decodes a payload from <see cref="Encode"/> back to the path list. Null / garbage /
    /// wrong-header input yields an empty list (never throws — a malformed or hostile message must not
    /// crash the running primary).</summary>
    public static IReadOnlyList<string> Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
            return Array.Empty<string>();

        string text;
        try
        {
            text = Encoding.UTF8.GetString(payload).TrimEnd('\n', '\r');
        }
        catch
        {
            return Array.Empty<string>();
        }

        var parts = text.Split(Separator);
        if (parts.Length == 0 || parts[0] != Header)
            return Array.Empty<string>();

        var result = new List<string>(parts.Length - 1);
        for (var i = 1; i < parts.Length; i++)
            if (!string.IsNullOrWhiteSpace(parts[i]))
                result.Add(parts[i]);
        return result;
    }

    /// <summary>Stable per-user identity. Includes the user name so two users on one machine
    /// (fast-user-switching / Terminal Server) stay independent.</summary>
    public static string MutexName(string userName) => $"SeriousView.{Sanitize(userName)}.mutex";

    public static string PipeName(string userName) => $"SeriousView.{Sanitize(userName)}.pipe";

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.Length == 0 ? "default" : sb.ToString();
    }
}
