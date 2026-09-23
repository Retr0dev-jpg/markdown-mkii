using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class TextFileCodecTests
{
    [Fact]
    public void Roundtrips_Utf8Bom_And_Crlf()
    {
        var bytes = TextFileCodec.Encode("uno\ndue", new TextFileProfile(TextEncodingKind.Utf8Bom, NewlineKind.Crlf));
        Assert.Equal(0xEF, bytes[0]);
        Assert.Contains(bytes, b => b == (byte)'\r');
        var (text, profile) = TextFileCodec.Decode(bytes);
        Assert.Equal("uno\ndue", text);
        Assert.Equal(TextEncodingKind.Utf8Bom, profile.Encoding);
        Assert.Equal(NewlineKind.Crlf, profile.Newline);
    }

    [Fact]
    public void Detects_Utf16Le_Bom()
    {
        var bytes = TextFileCodec.Encode("# Titolo", new TextFileProfile(TextEncodingKind.Utf16Le, NewlineKind.Lf));
        Assert.Equal(0xFF, bytes[0]);
        var (text, profile) = TextFileCodec.Decode(bytes);
        Assert.Equal("# Titolo", text);
        Assert.Equal(TextEncodingKind.Utf16Le, profile.Encoding);
    }
}
