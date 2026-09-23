namespace MarkdownMkII.Core.Text;

public static partial class MarkdownEditing
{
    public static bool LooksLikeHgnc(string value)
    {
        value = NormalizeIdentifier(value, "https://www.genenames.org/data/gene-symbol-report/#!/hgnc_id/");

        if (value.StartsWith("hgnc:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["hgnc:".Length..].Trim();
        }

        if (value.Length is < 1 or > 8)
        {
            return false;
        }

        if (!char.IsAsciiDigit(value[0]) || value[0] == '0')
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeGo(string value)
    {
        value = NormalizeIdentifier(value, "https://amigo.geneontology.org/amigo/term/", "https://www.ebi.ac.uk/QuickGO/term/");

        if (value.StartsWith("go:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["go:".Length..].Trim();
        }

        if (value.Length != 7)
        {
            return false;
        }

        for (var i = 0; i < 7; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeInterpro(string value)
    {
        value = NormalizeIdentifier(value, "https://www.ebi.ac.uk/interpro/entry/InterPro/");

        if (value.StartsWith("interpro:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["interpro:".Length..].Trim();
        }

        if (value.StartsWith("IPR", StringComparison.OrdinalIgnoreCase))
        {
            value = value[3..];
        }

        if (value.Length != 6)
        {
            return false;
        }

        for (var i = 0; i < 6; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikePfam(string value)
    {
        value = NormalizeIdentifier(value, "https://www.ebi.ac.uk/interpro/entry/pfam/", "https://pfam.xfam.org/family/");

        if (value.StartsWith("pfam:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["pfam:".Length..].Trim();
        }

        if (value.StartsWith("PF", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        if (value.Length != 5)
        {
            return false;
        }

        for (var i = 0; i < 5; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeReactome(string value)
    {
        value = NormalizeIdentifier(value, "https://reactome.org/content/detail/");

        if (value.StartsWith("reactome:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["reactome:".Length..].Trim();
        }

        if (!value.StartsWith("R-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[2..];
        if (value.Length < 5)
        {
            return false;
        }

        for (var i = 0; i < 3; i++)
        {
            if (!char.IsAsciiLetter(value[i]))
            {
                return false;
            }
        }

        if (value[3] != '-')
        {
            return false;
        }

        var digits = value[4..];
        if (digits.Length is < 1 or > 8 || digits[0] == '0')
        {
            return false;
        }

        foreach (var ch in digits)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeRefseq(string value)
    {
        value = NormalizeIdentifier(value, "https://www.ncbi.nlm.nih.gov/nuccore/", "https://www.ncbi.nlm.nih.gov/protein/");

        if (value.StartsWith("refseq:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["refseq:".Length..].Trim();
        }

        var dot = value.LastIndexOf('.');
        if (dot > 0)
        {
            var ver = value[(dot + 1)..];
            var allDigits = ver.Length > 0;
            foreach (var ch in ver)
            {
                if (!char.IsAsciiDigit(ch))
                {
                    allDigits = false;
                    break;
                }
            }

            if (allDigits)
            {
                value = value[..dot];
            }
        }

        if (value.Length < 9)
        {
            return false;
        }

        if (!char.IsAsciiLetter(value[0]) || !char.IsAsciiLetter(value[1]) || value[2] != '_')
        {
            return false;
        }

        var digits = value[3..];
        if (digits.Length < 6)
        {
            return false;
        }

        foreach (var ch in digits)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeMgi(string value)
    {
        value = NormalizeIdentifier(value, "https://www.informatics.jax.org/marker/", "https://www.informatics.jax.org/accession/");

        if (value.StartsWith("mgi:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["mgi:".Length..].Trim();
        }

        if (value.StartsWith("MGI:", StringComparison.OrdinalIgnoreCase))
        {
            value = value[4..].Trim();
        }

        if (value.Length is < 1 or > 8 || value[0] == '0')
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeFlybase(string value)
    {
        value = NormalizeIdentifier(value, "https://flybase.org/reports/");

        if (value.StartsWith("flybase:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["flybase:".Length..].Trim();
        }

        if (value.StartsWith("FBgn", StringComparison.OrdinalIgnoreCase))
        {
            value = value[4..];
        }

        if (value.Length != 7)
        {
            return false;
        }

        for (var i = 0; i < 7; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeWormbase(string value)
    {
        value = NormalizeIdentifier(value, "https://www.wormbase.org/species/c_elegans/gene/", "https://wormbase.org/species/c_elegans/gene/");

        if (value.StartsWith("wormbase:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["wormbase:".Length..].Trim();
        }

        if (value.StartsWith("WBGene", StringComparison.OrdinalIgnoreCase))
        {
            value = value[6..];
        }

        if (value.Length != 8)
        {
            return false;
        }

        for (var i = 0; i < 8; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeSgd(string value)
    {
        value = NormalizeIdentifier(value, "https://www.yeastgenome.org/locus/", "https://yeastgenome.org/locus/");

        if (value.StartsWith("sgd:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["sgd:".Length..].Trim();
        }

        if (value.Length == 10 && value.StartsWith("S", StringComparison.OrdinalIgnoreCase))
        {
            value = value[1..];
        }

        if (value.Length != 9)
        {
            return false;
        }

        for (var i = 0; i < 9; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeZfin(string value)
    {
        value = NormalizeIdentifier(value, "https://www.zfin.org/", "https://zfin.org/");

        if (value.StartsWith("zfin:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["zfin:".Length..].Trim();
        }

        const string ZdbGene = "ZDB-GENE-";
        if (!value.StartsWith(ZdbGene, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[ZdbGene.Length..];
        if (value.Length < 8 || value.Length > 15 || value[6] != '-')
        {
            return false;
        }

        for (var i = 0; i < 6; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        var suffix = value[7..];
        if (suffix.Length < 1 || suffix.Length > 8)
        {
            return false;
        }

        for (var i = 0; i < suffix.Length; i++)
        {
            if (!char.IsAsciiDigit(suffix[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeRgd(string value)
    {
        value = NormalizeIdentifier(value, "https://www.rgd.mcw.edu/rgdweb/report/gene/main.html?id=", "https://rgd.mcw.edu/rgdweb/report/gene/main.html?id=");

        if (value.StartsWith("rgd:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["rgd:".Length..].Trim();
        }

        if (value.StartsWith("RGD:", StringComparison.OrdinalIgnoreCase))
        {
            value = value[4..].Trim();
        }

        if (value.Length is < 1 or > 9 || value[0] == '0')
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeTair(string value)
    {
        value = NormalizeIdentifier(value, "https://www.arabidopsis.org/servlets/TairObject?type=locus&name=", "https://arabidopsis.org/servlets/TairObject?type=locus&name=");

        if (value.StartsWith("tair:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["tair:".Length..].Trim();
        }

        if (value.Length != 9 ||
            !value.StartsWith("AT", StringComparison.OrdinalIgnoreCase) ||
            char.ToUpperInvariant(value[3]) != 'G')
        {
            return false;
        }

        var chromosome = char.ToUpperInvariant(value[2]);
        if (chromosome is not ('1' or '2' or '3' or '4' or '5' or 'C' or 'M'))
        {
            return false;
        }

        for (var i = 4; i < 9; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeXenbase(string value)
    {
        value = NormalizeIdentifier(value, "https://www.xenbase.org/entry/", "https://xenbase.org/entry/");

        if (value.StartsWith("xenbase:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["xenbase:".Length..].Trim();
        }

        const string XbGene = "XB-GENE-";
        if (!value.StartsWith(XbGene, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[XbGene.Length..];
        if (value.Length is < 1 or > 9 || value[0] == '0')
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikePomBase(string value)
    {
        value = NormalizeIdentifier(value, "https://www.pombase.org/gene/", "https://pombase.org/gene/");

        if (value.StartsWith("pombase:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["pombase:".Length..].Trim();
        }

        if (value.Length < 7 ||
            !value.StartsWith("SP", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = value[2..];
        var chromosome = char.ToUpperInvariant(rest[0]);
        if (chromosome is not ('A' or 'B' or 'C'))
        {
            return false;
        }

        rest = rest[1..];
        var dot = rest.IndexOf('.');
        if (dot < 1)
        {
            return false;
        }

        var left = rest[..dot];
        var right = rest[(dot + 1)..];
        foreach (var ch in left)
        {
            if (!char.IsAsciiLetterOrDigit(ch))
            {
                return false;
            }
        }

        if (right.EndsWith('c') || right.EndsWith('C'))
        {
            right = right[..^1];
        }

        if (right.Length is < 1 or > 4)
        {
            return false;
        }

        foreach (var ch in right)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeDictyBase(string value)
    {
        value = NormalizeIdentifier(value, "https://www.dictybase.org/gene/", "https://dictybase.org/gene/");

        if (value.StartsWith("dictybase:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["dictybase:".Length..].Trim();
        }

        const string DdbG = "DDB_G";
        if (!value.StartsWith(DdbG, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[DdbG.Length..];
        if (value.Length != 7)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeEcoCyc(string value)
    {
        value = NormalizeIdentifier(value, "https://www.ecocyc.org/gene?orgid=ECOLI&id=", "https://ecocyc.org/gene?orgid=ECOLI&id=");

        if (value.StartsWith("ecocyc:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["ecocyc:".Length..].Trim();
        }

        if (!value.StartsWith("EG", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[2..];
        if (value.Length != 5)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeCgd(string value)
    {
        value = NormalizeIdentifier(value, "https://www.candidagenome.org/cgi-bin/locus.pl?locus=", "https://candidagenome.org/cgi-bin/locus.pl?locus=");

        if (value.StartsWith("cgd:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["cgd:".Length..].Trim();
        }

        if (!value.StartsWith("CAL", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[3..];
        if (value.Length != 7)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeVectorBase(string value)
    {
        value = NormalizeIdentifier(value, "https://www.vectorbase.org/gene/", "https://vectorbase.org/gene/");

        if (value.StartsWith("vectorbase:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["vectorbase:".Length..].Trim();
        }

        if (!value.StartsWith("AGAP", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[4..];
        if (value.Length != 6)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeMaizeGdb(string value)
    {
        value = NormalizeIdentifier(value, "https://www.maizegdb.org/gene_center/gene/", "https://maizegdb.org/gene_center/gene/");

        if (value.StartsWith("maizegdb:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["maizegdb:".Length..].Trim();
        }

        const string Grmzm2g = "GRMZM2G";
        if (!value.StartsWith(Grmzm2g, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[Grmzm2g.Length..];
        if (value.Length != 6)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeSoyBase(string value)
    {
        value = NormalizeIdentifier(value, "https://www.soybase.org/gene/", "https://soybase.org/gene/");

        if (value.StartsWith("soybase:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["soybase:".Length..].Trim();
        }

        const string Glyma = "Glyma.";
        if (!value.StartsWith(Glyma, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[Glyma.Length..];
        if (value.Length != 9)
        {
            return false;
        }

        if (!char.IsAsciiDigit(value[0]) || !char.IsAsciiDigit(value[1]))
        {
            return false;
        }

        if (char.ToUpperInvariant(value[2]) != 'G')
        {
            return false;
        }

        for (var i = 3; i < 9; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeGramene(string value)
    {
        value = NormalizeIdentifier(value, "https://www.gramene.org/gene/", "https://gramene.org/gene/");

        if (value.StartsWith("gramene:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["gramene:".Length..].Trim();
        }

        const string Os = "Os";
        if (!value.StartsWith(Os, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[Os.Length..];
        if (value.Length != 10)
        {
            return false;
        }

        if (!char.IsAsciiDigit(value[0]) || !char.IsAsciiDigit(value[1]))
        {
            return false;
        }

        if (char.ToUpperInvariant(value[2]) != 'G')
        {
            return false;
        }

        for (var i = 3; i < 10; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikePhytozome(string value)
    {
        value = NormalizeIdentifier(value, "https://www.phytozome.net/gene/", "https://phytozome.net/gene/");

        if (value.StartsWith("phytozome:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["phytozome:".Length..].Trim();
        }

        const string Potri = "Potri.";
        if (!value.StartsWith(Potri, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[Potri.Length..];
        if (value.Length != 10)
        {
            return false;
        }

        if (!char.IsAsciiDigit(value[0]) || !char.IsAsciiDigit(value[1]) || !char.IsAsciiDigit(value[2]))
        {
            return false;
        }

        if (char.ToUpperInvariant(value[3]) != 'G')
        {
            return false;
        }

        for (var i = 4; i < 10; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeJgi(string value)
    {
        value = NormalizeIdentifier(value, "https://www.jgi.doe.gov/gene/", "https://jgi.doe.gov/gene/");

        if (value.StartsWith("jgi:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["jgi:".Length..].Trim();
        }

        const string Ga = "Ga";
        if (!value.StartsWith(Ga, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value[Ga.Length..];
        if (value.Length != 8)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return true;
    }
}
