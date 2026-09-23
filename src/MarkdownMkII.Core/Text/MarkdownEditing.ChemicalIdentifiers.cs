namespace MarkdownMkII.Core.Text;

public static partial class MarkdownEditing
{
    public static bool LooksLikePubchem(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://pubchem.ncbi.nlm.nih.gov/compound/",
            "http://pubchem.ncbi.nlm.nih.gov/compound/",
            "pubchem.ncbi.nlm.nih.gov/compound/",
            "pubchem:",
            "cid:"
        ];
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].Trim();
                break;
            }
        }

        value = value.Trim('/');
        if (value.Length < 2 || value.Length > 10)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeCas(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://commonchemistry.cas.org/detail?cas_rn=",
            "http://commonchemistry.cas.org/detail?cas_rn=",
            "https://commonchemistry.cas.org/detail?cas=",
            "http://commonchemistry.cas.org/detail?cas=",
            "commonchemistry.cas.org/detail?cas_rn=",
            "commonchemistry.cas.org/detail?cas=",
            "casrn:",
            "cas:"
        ];
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].Trim();
                break;
            }
        }

        var parts = value.Split('-');
        if (parts.Length != 3)
        {
            return false;
        }

        if (parts[0].Length < 2 || parts[0].Length > 7 ||
            parts[1].Length != 2 ||
            parts[2].Length != 1)
        {
            return false;
        }

        foreach (var part in parts)
        {
            foreach (var ch in part)
            {
                if (!char.IsDigit(ch))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static bool LooksLikeInchi(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://www.ebi.ac.uk/chembl/inchi/",
            "http://www.ebi.ac.uk/chembl/inchi/",
            "inchi="
        ];
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("InChI=", StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].Trim();
                break;
            }
        }

        if (value.StartsWith("inchi:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["inchi:".Length..].Trim();
        }

        if (!value.StartsWith("InChI=", StringComparison.OrdinalIgnoreCase) || value.Length < 10)
        {
            return false;
        }

        var rest = value["InChI=".Length..];
        if (rest.Length == 0 || !char.IsDigit(rest[0]) || !rest.Contains('/'))
        {
            return false;
        }

        return true;
    }

    public static bool LooksLikeSmiles(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        if (value.StartsWith("smiles:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["smiles:".Length..].Trim();
        }

        if (value.Length < 2 || value.Length > 80 || value.Contains(' ') || value.Contains("--", StringComparison.Ordinal))
        {
            return false;
        }

        if (value.Contains('-') && !value.Contains('['))
        {
            return false;
        }

        if (value.StartsWith("Q", StringComparison.OrdinalIgnoreCase))
        {
            var rest = value[1..];
            var digits = true;
            foreach (var ch in rest)
            {
                if (!char.IsDigit(ch))
                {
                    digits = false;
                    break;
                }
            }

            if (digits)
            {
                return false;
            }
        }

        var organic = false;
        foreach (var ch in value)
        {
            if (ch is 'C' or 'c' or 'N' or 'n' or 'O' or 'o' or 'S' or 's' or 'P' or 'F' or 'B' or 'I' or 'H')
            {
                organic = true;
            }

            if (char.IsLetterOrDigit(ch) || ch is '@' or '+' or '-' or '[' or ']' or '(' or ')' or '=' or '#' or '/' or '\\' or '.' or '%')
            {
                continue;
            }

            return false;
        }

        return organic;
    }

    public static bool LooksLikeUnii(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        const string Fda = "https://precision.fda.gov/uniisearch/srs/unii/";
        if (value.StartsWith(Fda, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Fda.Length..];
        }

        if (value.StartsWith("unii:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["unii:".Length..].Trim();
        }

        if (value.Length != 10)
        {
            return false;
        }

        var hasLetter = false;
        var hasDigit = false;
        foreach (var ch in value)
        {
            if (char.IsAsciiLetter(ch))
            {
                hasLetter = true;
            }
            else if (char.IsAsciiDigit(ch))
            {
                hasDigit = true;
            }
            else
            {
                return false;
            }
        }

        return hasLetter && hasDigit;
    }

    public static bool LooksLikeChemspider(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        const string HttpsWww = "https://www.chemspider.com/Chemical-Structure.";
        const string HttpWww = "http://www.chemspider.com/Chemical-Structure.";
        const string Https = "https://chemspider.com/Chemical-Structure.";
        const string Http = "http://chemspider.com/Chemical-Structure.";
        if (value.StartsWith(HttpsWww, StringComparison.OrdinalIgnoreCase))
        {
            value = value[HttpsWww.Length..];
        }
        else if (value.StartsWith(HttpWww, StringComparison.OrdinalIgnoreCase))
        {
            value = value[HttpWww.Length..];
        }
        else if (value.StartsWith(Https, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Https.Length..];
        }
        else if (value.StartsWith(Http, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Http.Length..];
        }

        if (value.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^".html".Length];
        }

        if (value.StartsWith("chemspider:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["chemspider:".Length..].Trim();
        }

        if (value.StartsWith("csid:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["csid:".Length..].Trim();
        }

        if (value.Length is < 1 or > 10)
        {
            return false;
        }

        if (value[0] == '0')
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

    public static bool LooksLikeChebi(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        const string Search = "https://www.ebi.ac.uk/chebi/searchId.do?chebiId=";
        const string Entity = "https://www.ebi.ac.uk/chebi/CHEBI:";
        if (value.StartsWith(Search, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Search.Length..];
        }
        else if (value.StartsWith(Entity, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Entity.Length..];
        }

        if (value.StartsWith("chebi:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["chebi:".Length..].Trim();
        }

        if (value.Length is < 1 or > 7)
        {
            return false;
        }

        if (value[0] == '0')
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

    public static bool LooksLikeEc(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        const string Expasy = "https://enzyme.expasy.org/EC/";
        const string Brenda = "https://www.brenda-enzymes.org/enzyme.php?ecno=";
        if (value.StartsWith(Expasy, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Expasy.Length..];
        }
        else if (value.StartsWith(Brenda, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Brenda.Length..];
        }

        if (value.StartsWith("ec:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["ec:".Length..].Trim();
        }

        var parts = value.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (part.Length is < 1 or > 3)
            {
                return false;
            }

            foreach (var ch in part)
            {
                if (!char.IsAsciiDigit(ch))
                {
                    return false;
                }
            }
        }

        return parts[0][0] != '0';
    }

    public static bool LooksLikeMesh(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        const string Id = "https://id.nlm.nih.gov/mesh/";
        const string Browser = "https://meshb.nlm.nih.gov/record/ui?ui=";
        if (value.StartsWith(Id, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Id.Length..];
        }
        else if (value.StartsWith(Browser, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Browser.Length..];
        }

        if (value.StartsWith("mesh:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["mesh:".Length..].Trim();
        }

        if (value.Length != 7)
        {
            return false;
        }

        if (value[0] is not ('D' or 'd' or 'C' or 'c' or 'Q' or 'q'))
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

    public static bool LooksLikeHmdb(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        const string Site = "https://hmdb.ca/metabolites/";
        if (value.StartsWith(Site, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Site.Length..];
        }

        if (value.StartsWith("hmdb:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["hmdb:".Length..].Trim();
        }

        if (!value.StartsWith("HMDB", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var digits = value[4..];
        if (digits.Length is < 5 or > 7)
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

    public static bool LooksLikeKegg(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        const string KeggEntry = "https://www.kegg.jp/entry/";
        const string KeggBget = "https://www.kegg.jp/dbget-bin/www_bget?";
        const string GenomeEntry = "https://www.genome.jp/entry/";
        const string GenomeBget = "https://www.genome.jp/dbget-bin/www_bget?";
        if (value.StartsWith(KeggEntry, StringComparison.OrdinalIgnoreCase))
        {
            value = value[KeggEntry.Length..];
        }
        else if (value.StartsWith(KeggBget, StringComparison.OrdinalIgnoreCase))
        {
            value = value[KeggBget.Length..];
        }
        else if (value.StartsWith(GenomeEntry, StringComparison.OrdinalIgnoreCase))
        {
            value = value[GenomeEntry.Length..];
        }
        else if (value.StartsWith(GenomeBget, StringComparison.OrdinalIgnoreCase))
        {
            value = value[GenomeBget.Length..];
        }

        if (value.StartsWith("kegg:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["kegg:".Length..].Trim();
        }

        if (value.Length != 6)
        {
            return false;
        }

        var kind = char.ToUpperInvariant(value[0]);
        if (kind is not ('C' or 'D' or 'G' or 'H' or 'K' or 'R'))
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

    public static bool LooksLikeChembl(string value)
    {
        value = NormalizeIdentifier(value, "https://www.ebi.ac.uk/chembl/compound_report_card/", "https://www.ebi.ac.uk/chembl/explore/compound/");

        if (value.StartsWith("chembl:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["chembl:".Length..].Trim();
        }

        if (!value.StartsWith("CHEMBL", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var digits = value[6..];
        if (digits.Length is < 1 or > 8)
        {
            return false;
        }

        if (digits[0] == '0')
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

    public static bool LooksLikeInchiKey(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        const string Pubchem = "https://pubchem.ncbi.nlm.nih.gov/#query=";
        if (value.StartsWith(Pubchem, StringComparison.OrdinalIgnoreCase))
        {
            value = value[Pubchem.Length..];
        }

        if (value.StartsWith("inchikey:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["inchikey:".Length..].Trim();
        }

        if (value.Length != 27 || value[14] != '-' || value[25] != '-')
        {
            return false;
        }

        for (var i = 0; i < 14; i++)
        {
            if (!char.IsAsciiLetterOrDigit(value[i]))
            {
                return false;
            }
        }

        for (var i = 15; i < 25; i++)
        {
            if (!char.IsAsciiLetterOrDigit(value[i]))
            {
                return false;
            }
        }

        return char.IsAsciiLetter(value[26]);
    }

    public static bool LooksLikeDrugbank(string value)
    {
        value = NormalizeIdentifier(value, "https://go.drugbank.com/drugs/");

        if (value.StartsWith("drugbank:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["drugbank:".Length..].Trim();
        }

        if (!value.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var digits = value[2..];
        if (digits.Length != 5)
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

    public static bool LooksLikePdb(string value)
    {
        value = NormalizeIdentifier(value, "https://www.rcsb.org/structure/", "https://www.ebi.ac.uk/pdbe/entry/pdb/");

        if (value.StartsWith("pdb:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["pdb:".Length..].Trim();
        }

        if (value.Length != 4)
        {
            return false;
        }

        if (!char.IsAsciiDigit(value[0]) || value[0] == '0')
        {
            return false;
        }

        for (var i = 1; i < 4; i++)
        {
            if (!char.IsAsciiLetterOrDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeUniprot(string value)
    {
        value = NormalizeIdentifier(value, "https://www.uniprot.org/uniprotkb/", "https://www.uniprot.org/uniprot/");

        if (value.StartsWith("uniprot:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["uniprot:".Length..].Trim();
        }

        if (value.Length != 6)
        {
            return false;
        }

        var first = char.ToUpperInvariant(value[0]);
        if (first is not ('O' or 'P' or 'Q'))
        {
            return false;
        }

        if (!char.IsAsciiDigit(value[1]))
        {
            return false;
        }

        for (var i = 2; i < 5; i++)
        {
            if (!char.IsAsciiLetterOrDigit(value[i]))
            {
                return false;
            }
        }

        return char.IsAsciiDigit(value[5]);
    }

    public static bool LooksLikeEnsembl(string value)
    {
        value = NormalizeIdentifier(value, "https://www.ensembl.org/id/", "https://ensembl.org/id/");

        if (value.StartsWith("ensembl:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["ensembl:".Length..].Trim();
        }

        var versionAt = value.LastIndexOf('.');
        if (versionAt > 0)
        {
            var version = value[(versionAt + 1)..];
            var digits = version.Length > 0;
            foreach (var ch in version)
            {
                if (!char.IsAsciiDigit(ch))
                {
                    digits = false;
                    break;
                }
            }

            if (digits)
            {
                value = value[..versionAt];
            }
        }

        if (value.Length < 15 ||
            !value.StartsWith("ENS", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var i = 3;
        while (i < value.Length && char.IsAsciiLetter(value[i]))
        {
            i++;
        }

        if (i == 3)
        {
            return false;
        }

        if (value.Length - i != 11)
        {
            return false;
        }

        for (; i < value.Length; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeRhea(string value)
    {
        value = NormalizeIdentifier(value, "https://www.rhea-db.org/rhea/");

        if (value.StartsWith("rhea:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["rhea:".Length..].Trim();
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

    public static bool LooksLikeLincs(string value)
    {
        value = NormalizeIdentifier(value, "https://lincs.hms.harvard.edu/db/sm/", "https://lincsportal.ccs.miami.edu/SmallMolecules/view/");

        if (value.StartsWith("lincs:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["lincs:".Length..].Trim();
        }

        if (!value.StartsWith("LSM-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value["LSM-".Length..];
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
}
