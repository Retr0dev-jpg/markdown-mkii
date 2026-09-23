using System.Text;

namespace MarkdownMkII.Core.Text;

public enum TextEncodingKind
{
    Utf8,
    Utf8Bom,
    Utf16Le,
    Utf16Be
}

public enum NewlineKind
{
    Lf,
    Crlf
}

public sealed record TextFileProfile(TextEncodingKind Encoding, NewlineKind Newline)
{
    public static TextFileProfile Utf8Lf { get; } = new(TextEncodingKind.Utf8, NewlineKind.Lf);

    public string StatusLabel
        => EncodingLabel(Encoding) + " · " + (Newline == NewlineKind.Crlf ? "CRLF" : "LF");

    public TextFileProfile WithNewline(NewlineKind newline) => this with { Newline = newline };

    public TextFileProfile NextEncoding()
        => this with
        {
            Encoding = Encoding switch
            {
                TextEncodingKind.Utf8 => TextEncodingKind.Utf8Bom,
                TextEncodingKind.Utf8Bom => TextEncodingKind.Utf16Le,
                TextEncodingKind.Utf16Le => TextEncodingKind.Utf16Be,
                _ => TextEncodingKind.Utf8
            }
        };

    private static string EncodingLabel(TextEncodingKind encoding)
        => encoding switch
        {
            TextEncodingKind.Utf8Bom => "UTF-8 BOM",
            TextEncodingKind.Utf16Le => "UTF-16 LE",
            TextEncodingKind.Utf16Be => "UTF-16 BE",
            _ => "UTF-8"
        };
}

public static class TextFileCodec
{
    public static (string Text, TextFileProfile Profile) Decode(ReadOnlySpan<byte> bytes)
    {
        var encoding = DetectEncoding(bytes, out var preamble);
        var payload = preamble > 0 && preamble <= bytes.Length ? bytes[preamble..] : bytes;
        var text = encoding switch
        {
            TextEncodingKind.Utf16Le => Encoding.Unicode.GetString(payload),
            TextEncodingKind.Utf16Be => Encoding.BigEndianUnicode.GetString(payload),
            _ => Encoding.UTF8.GetString(payload)
        };
        var newline = DetectNewline(text);
        text = DocumentStoreNormalize(text);
        return (text, new TextFileProfile(encoding, newline));
    }

    public static byte[] Encode(string text, TextFileProfile? profile = null)
    {
        profile ??= TextFileProfile.Utf8Lf;
        text ??= string.Empty;
        text = DocumentStoreNormalize(text);
        if (profile.Newline == NewlineKind.Crlf)
        {
            text = text.Replace("\n", "\r\n", StringComparison.Ordinal);
        }

        var body = profile.Encoding switch
        {
            TextEncodingKind.Utf16Le => Encoding.Unicode.GetBytes(text),
            TextEncodingKind.Utf16Be => Encoding.BigEndianUnicode.GetBytes(text),
            _ => Encoding.UTF8.GetBytes(text)
        };

        var preamble = profile.Encoding switch
        {
            TextEncodingKind.Utf8Bom => Encoding.UTF8.GetPreamble(),
            TextEncodingKind.Utf16Le => Encoding.Unicode.GetPreamble(),
            TextEncodingKind.Utf16Be => Encoding.BigEndianUnicode.GetPreamble(),
            _ => []
        };

        if (preamble.Length == 0)
        {
            return body;
        }

        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    private static TextEncodingKind DetectEncoding(ReadOnlySpan<byte> bytes, out int preamble)
    {
        preamble = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            preamble = 3;
            return TextEncodingKind.Utf8Bom;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            preamble = 2;
            return TextEncodingKind.Utf16Le;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            preamble = 2;
            return TextEncodingKind.Utf16Be;
        }

        if (LooksLikeUtf16Le(bytes))
        {
            return TextEncodingKind.Utf16Le;
        }

        return TextEncodingKind.Utf8;
    }

    private static bool LooksLikeUtf16Le(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || bytes.Length % 2 != 0)
        {
            return false;
        }

        var nuls = 0;
        var limit = Math.Min(bytes.Length, 64);
        for (var i = 1; i < limit; i += 2)
        {
            if (bytes[i] == 0)
            {
                nuls++;
            }
        }

        return nuls >= limit / 4;
    }

    private static NewlineKind DetectNewline(string text)
    {
        var crlf = 0;
        var lf = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            if (i > 0 && text[i - 1] == '\r')
            {
                crlf++;
            }
            else
            {
                lf++;
            }
        }

        return crlf > lf ? NewlineKind.Crlf : NewlineKind.Lf;
    }

    private static string DocumentStoreNormalize(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
