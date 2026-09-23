using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class PaletteFilterTests
{
    [Fact]
    public void Matches_All_Words_Case_Insensitive()
    {
        Assert.True(PaletteFilter.Matches("Salva con nome", ""));
        Assert.True(PaletteFilter.Matches("Salva con nome", "salva"));
        Assert.True(PaletteFilter.Matches("Salva con nome", "con NOME"));
        Assert.False(PaletteFilter.Matches("Salva con nome", "apri"));
        Assert.True(PaletteFilter.MatchesAny("salva", "saveAs", "Salva con nome", "Ctrl+Shift+S"));
        Assert.True(PaletteFilter.MatchesAny("saveas", "saveAs", "Salva con nome"));
        var filtered = PaletteFilter.Apply(["Nuovo", "Apri", "Salva"], item => item, "sa");
        Assert.Equal(["Salva"], filtered);
    }
}
