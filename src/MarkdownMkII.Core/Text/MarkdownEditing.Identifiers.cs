namespace MarkdownMkII.Core.Text;

public static partial class MarkdownEditing
{
    private static string NormalizeIdentifier(string value, params ReadOnlySpan<string> urls)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        foreach (var url in urls)
        {
            if (value.StartsWith(url, StringComparison.OrdinalIgnoreCase))
            {
                value = value[url.Length..];
                break;
            }
        }

        return value.Trim('/');
    }

    private static bool LooksLikeNumericIdentifier(string value, string prefix, int digits, string firstUrl, string secondUrl)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        if (value.StartsWith(firstUrl, StringComparison.OrdinalIgnoreCase))
        {
            value = value[firstUrl.Length..];
        }
        else if (value.StartsWith(secondUrl, StringComparison.OrdinalIgnoreCase))
        {
            value = value[secondUrl.Length..];
        }

        value = value.Trim('/');
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[prefix.Length..].Trim();
        }

        return value.Length == digits && value.All(char.IsAsciiDigit);
    }

    public static bool LooksLikeWebUrl(string value)
    {
        value = (value ?? string.Empty).Trim();
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme == Uri.UriSchemeMailto);
    }

    public static bool LooksLikeEmail(string value)
    {
        value = (value ?? string.Empty).Trim();
        if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["mailto:".Length..];
        }

        var at = value.IndexOf('@');
        if (at <= 0 || at != value.LastIndexOf('@') || at >= value.Length - 1)
        {
            return false;
        }

        if (value.Contains(' ') || value.Contains('\t'))
        {
            return false;
        }

        var domain = value[(at + 1)..];
        var dot = domain.IndexOf('.');
        return dot > 0 && dot < domain.Length - 1;
    }

    public static bool LooksLikeUuid(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        return Guid.TryParse(value, out _);
    }

    public static bool LooksLikeIsbn(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        if (value.StartsWith("isbn:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["isbn:".Length..].Trim();
        }
        else if (value.StartsWith("isbn ", StringComparison.OrdinalIgnoreCase))
        {
            value = value["isbn ".Length..].Trim();
        }

        var chars = new List<char>();
        foreach (var ch in value)
        {
            if (char.IsDigit(ch) || ch is 'X' or 'x')
            {
                chars.Add(char.ToUpperInvariant(ch));
            }
        }

        if (chars.Count == 13)
        {
            return chars.TrueForAll(char.IsDigit);
        }

        if (chars.Count == 10)
        {
            for (var i = 0; i < 9; i++)
            {
                if (!char.IsDigit(chars[i]))
                {
                    return false;
                }
            }

            return char.IsDigit(chars[9]) || chars[9] == 'X';
        }

        return false;
    }

    public static bool LooksLikeIssn(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        if (value.StartsWith("issn:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["issn:".Length..].Trim();
        }
        else if (value.StartsWith("issn ", StringComparison.OrdinalIgnoreCase))
        {
            value = value["issn ".Length..].Trim();
        }

        var chars = new List<char>();
        foreach (var ch in value)
        {
            if (char.IsDigit(ch) || ch is 'X' or 'x')
            {
                chars.Add(char.ToUpperInvariant(ch));
            }
        }

        if (chars.Count != 8)
        {
            return false;
        }

        for (var i = 0; i < 7; i++)
        {
            if (!char.IsDigit(chars[i]))
            {
                return false;
            }
        }

        return char.IsDigit(chars[7]) || chars[7] == 'X';
    }

    public static bool LooksLikeOrcid(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        const string prefix = "https://orcid.org/";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[prefix.Length..].Trim();
        }
        else if (value.StartsWith("orcid.org/", StringComparison.OrdinalIgnoreCase))
        {
            value = value["orcid.org/".Length..].Trim();
        }

        var chars = new List<char>();
        foreach (var ch in value)
        {
            if (char.IsDigit(ch) || ch is 'X' or 'x')
            {
                chars.Add(char.ToUpperInvariant(ch));
            }
        }

        if (chars.Count != 16)
        {
            return false;
        }

        for (var i = 0; i < 15; i++)
        {
            if (!char.IsDigit(chars[i]))
            {
                return false;
            }
        }

        return char.IsDigit(chars[15]) || chars[15] == 'X';
    }

    public static bool LooksLikeDoi(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://doi.org/",
            "http://doi.org/",
            "https://dx.doi.org/",
            "http://dx.doi.org/",
            "doi.org/",
            "dx.doi.org/",
            "doi:"
        ];
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].Trim();
                break;
            }
        }

        if (!value.StartsWith("10.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = value[3..];
        var slash = rest.IndexOf('/');
        if (slash < 4)
        {
            return false;
        }

        var registrant = rest[..slash];
        var suffix = rest[(slash + 1)..].Trim();
        if (suffix.Length == 0 || suffix.Contains(' '))
        {
            return false;
        }

        if (registrant.Length < 4)
        {
            return false;
        }

        foreach (var ch in registrant)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikePmid(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://pubmed.ncbi.nlm.nih.gov/",
            "http://pubmed.ncbi.nlm.nih.gov/",
            "pubmed.ncbi.nlm.nih.gov/",
            "pmid:",
            "pubmed:"
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
        if (value.Length is < 7 or > 8)
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

    public static bool LooksLikeArxiv(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://arxiv.org/abs/",
            "http://arxiv.org/abs/",
            "https://arxiv.org/pdf/",
            "http://arxiv.org/pdf/",
            "arxiv.org/abs/",
            "arxiv.org/pdf/",
            "arxiv:",
            "arXiv:"
        ];
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].Trim();
                break;
            }
        }

        if (value.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^4];
        }

        value = value.Trim('/');
        var version = value.LastIndexOf('v');
        if (version > 0)
        {
            var tag = value[(version + 1)..];
            if (tag.Length > 0 && tag.All(char.IsDigit))
            {
                value = value[..version];
            }
        }

        var slash = value.IndexOf('/');
        if (slash > 0)
        {
            var archive = value[..slash];
            var number = value[(slash + 1)..];
            if (archive.Length == 0 || number.Length != 7)
            {
                return false;
            }

            foreach (var ch in archive)
            {
                if (!char.IsLetter(ch) && ch != '-')
                {
                    return false;
                }
            }

            foreach (var ch in number)
            {
                if (!char.IsDigit(ch))
                {
                    return false;
                }
            }

            return true;
        }

        var dot = value.IndexOf('.');
        if (dot != 4)
        {
            return false;
        }

        var yearMonth = value[..4];
        var seq = value[5..];
        if (seq.Length is < 4 or > 5)
        {
            return false;
        }

        foreach (var ch in yearMonth)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        foreach (var ch in seq)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikePmcid(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://www.ncbi.nlm.nih.gov/pmc/articles/",
            "http://www.ncbi.nlm.nih.gov/pmc/articles/",
            "https://ncbi.nlm.nih.gov/pmc/articles/",
            "pmc.ncbi.nlm.nih.gov/articles/",
            "pmcid:",
            "pmc:"
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
        if (value.StartsWith("PMC", StringComparison.OrdinalIgnoreCase))
        {
            value = value[3..];
        }

        if (value.Length is < 7 or > 8)
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

    public static bool LooksLikeWikidata(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://www.wikidata.org/wiki/",
            "http://www.wikidata.org/wiki/",
            "https://wikidata.org/wiki/",
            "wikidata.org/wiki/",
            "wikidata:",
            "wd:"
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
        if (value.Length < 2 || (value[0] is not 'Q' and not 'q' and not 'P' and not 'p'))
        {
            return false;
        }

        var digits = value[1..];
        if (digits.Length is < 1 or > 12)
        {
            return false;
        }

        foreach (var ch in digits)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeRor(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://ror.org/",
            "http://ror.org/",
            "ror.org/",
            "ror:"
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
        if (value.Length != 9 || value[0] != '0')
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static bool LooksLikeGrid(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://www.grid.ac/institutes/",
            "http://www.grid.ac/institutes/",
            "https://grid.ac/institutes/",
            "http://grid.ac/institutes/",
            "grid.ac/institutes/",
            "grid:"
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
        if (!value.StartsWith("grid.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length < 3)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                return false;
            }

            foreach (var ch in part)
            {
                if (!char.IsLetterOrDigit(ch))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static bool LooksLikeHandle(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://hdl.handle.net/",
            "http://hdl.handle.net/",
            "https://handle.net/",
            "http://handle.net/",
            "hdl.handle.net/",
            "hdl:"
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
        if (value.StartsWith("10.", StringComparison.Ordinal))
        {
            return false;
        }

        var slash = value.IndexOf('/');
        if (slash <= 0 || slash == value.Length - 1)
        {
            return false;
        }

        var prefixPart = value[..slash];
        var suffix = value[(slash + 1)..];
        if (suffix.Length == 0)
        {
            return false;
        }

        var parts = prefixPart.Split('.');
        if (parts.Length < 2)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                return false;
            }

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

    public static bool LooksLikeIsni(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://isni.org/isni/",
            "http://isni.org/isni/",
            "https://www.isni.org/isni/",
            "http://www.isni.org/isni/",
            "isni.org/isni/",
            "isni:"
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
        if (value.Contains('-', StringComparison.Ordinal))
        {
            return false;
        }

        var chars = new List<char>();
        foreach (var ch in value)
        {
            if (char.IsDigit(ch) || ch is 'X' or 'x')
            {
                chars.Add(char.ToUpperInvariant(ch));
            }
            else if (!char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        if (chars.Count != 16)
        {
            return false;
        }

        for (var i = 0; i < 15; i++)
        {
            if (!char.IsDigit(chars[i]))
            {
                return false;
            }
        }

        return char.IsDigit(chars[15]) || chars[15] == 'X';
    }

    public static bool LooksLikeViaf(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://viaf.org/viaf/",
            "http://viaf.org/viaf/",
            "https://www.viaf.org/viaf/",
            "http://www.viaf.org/viaf/",
            "viaf.org/viaf/",
            "viaf:"
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
        if (value.Length is < 2 or > 16)
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

    public static bool LooksLikeLccn(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://lccn.loc.gov/",
            "http://lccn.loc.gov/",
            "https://www.loc.gov/item/",
            "lccn.loc.gov/",
            "lccn:"
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
        var hyphen = 0;
        var digits = 0;
        var letters = 0;
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                digits++;
            }
            else if (char.IsLetter(ch))
            {
                letters++;
            }
            else if (ch == '-')
            {
                hyphen++;
            }
            else if (!char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        return digits >= 6 && letters <= 3 && hyphen <= 1;
    }

    public static bool LooksLikeOclc(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://www.worldcat.org/oclc/",
            "http://www.worldcat.org/oclc/",
            "https://worldcat.org/oclc/",
            "http://worldcat.org/oclc/",
            "worldcat.org/oclc/",
            "oclc:"
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
        var digits = 0;
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                digits++;
            }
            else if (!char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        return digits >= 8 && digits <= 14;
    }

    public static bool LooksLikeGnd(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://d-nb.info/gnd/",
            "http://d-nb.info/gnd/",
            "d-nb.info/gnd/",
            "gnd:"
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
        var digits = 0;
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                digits++;
            }
            else if (!char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        return digits >= 8 && digits <= 11;
    }

    public static bool LooksLikeBnf(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://catalogue.bnf.fr/ark:/12148/",
            "http://catalogue.bnf.fr/ark:/12148/",
            "https://data.bnf.fr/ark:/12148/",
            "http://data.bnf.fr/ark:/12148/",
            "catalogue.bnf.fr/ark:/12148/",
            "data.bnf.fr/ark:/12148/",
            "ark:/12148/",
            "bnf:"
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
        var digits = 0;
        var letters = 0;
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                digits++;
            }
            else if (char.IsLetter(ch))
            {
                letters++;
            }
            else if (!char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        return digits >= 6 && letters >= 1 && letters <= 4;
    }

    public static bool LooksLikeSudoc(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://www.sudoc.fr/",
            "http://www.sudoc.fr/",
            "https://sudoc.fr/",
            "http://sudoc.fr/",
            "sudoc.fr/",
            "https://www.idref.fr/",
            "http://www.idref.fr/",
            "https://idref.fr/",
            "idref.fr/",
            "sudoc:",
            "idref:"
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
        var digits = 0;
        var letters = 0;
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                digits++;
            }
            else if (char.IsLetter(ch))
            {
                letters++;
            }
            else if (!char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        return digits >= 8 && digits <= 10 && letters <= 1;
    }

    public static bool LooksLikeOpenAlex(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://openalex.org/",
            "http://openalex.org/",
            "openalex.org/",
            "openalex:"
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
        if (value.Length < 5)
        {
            return false;
        }

        var kind = char.ToUpperInvariant(value[0]);
        if (kind is not ('W' or 'A' or 'S' or 'I' or 'C' or 'P' or 'F' or 'T'))
        {
            return false;
        }

        var digits = 0;
        foreach (var ch in value[1..])
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }

            digits++;
        }

        return digits >= 4 && digits <= 12;
    }

    public static bool LooksLikeOlid(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://openlibrary.org/works/",
            "http://openlibrary.org/works/",
            "https://openlibrary.org/books/",
            "http://openlibrary.org/books/",
            "https://openlibrary.org/authors/",
            "http://openlibrary.org/authors/",
            "https://openlibrary.org/",
            "http://openlibrary.org/",
            "openlibrary.org/",
            "olid:",
            "ol:"
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
        if (!value.StartsWith("OL", StringComparison.OrdinalIgnoreCase) || value.Length < 4)
        {
            return false;
        }

        var rest = value[2..];
        var last = char.ToUpperInvariant(rest[^1]);
        if (last is not ('W' or 'M' or 'A' or 'T'))
        {
            return false;
        }

        var digits = 0;
        foreach (var ch in rest[..^1])
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }

            digits++;
        }

        return digits >= 1 && digits <= 10;
    }

    public static bool LooksLikeS2cid(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://www.semanticscholar.org/paper/",
            "http://www.semanticscholar.org/paper/",
            "https://semanticscholar.org/paper/",
            "http://semanticscholar.org/paper/",
            "https://api.semanticscholar.org/",
            "http://api.semanticscholar.org/",
            "www.semanticscholar.org/paper/",
            "semanticscholar.org/paper/",
            "api.semanticscholar.org/",
            "s2cid:",
            "corpusid:"
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
        if (value.Length < 7 || value.Length > 12)
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

    public static bool LooksLikeNdl(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://id.ndl.go.jp/bib/",
            "http://id.ndl.go.jp/bib/",
            "https://iss.ndl.go.jp/books/",
            "http://iss.ndl.go.jp/books/",
            "id.ndl.go.jp/bib/",
            "iss.ndl.go.jp/books/",
            "ndl:"
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
        if (value.Length < 8 || value.Length > 12)
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

    public static bool LooksLikeCinii(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        string[] prefixes =
        [
            "https://ci.nii.ac.jp/naid/",
            "http://ci.nii.ac.jp/naid/",
            "https://ci.nii.ac.jp/nrid/",
            "http://ci.nii.ac.jp/nrid/",
            "ci.nii.ac.jp/naid/",
            "ci.nii.ac.jp/nrid/",
            "cinii:",
            "naid:"
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
        if (value.Length < 8 || value.Length > 12)
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

    public static bool LooksLikeHp(string value)
        => LooksLikeNumericIdentifier(value, "hp:", 7, "https://www.hpo.jax.org/app/browse/term/", "https://hpo.jax.org/app/browse/term/");

    public static bool LooksLikeMondo(string value)
        => LooksLikeNumericIdentifier(value, "mondo:", 7, "https://www.monarchinitiative.org/disease/", "https://monarchinitiative.org/disease/");

    public static bool LooksLikeDoid(string value)
        => LooksLikeNumericIdentifier(value, "doid:", 7, "https://www.disease-ontology.org/term/", "https://disease-ontology.org/term/");

    public static bool LooksLikeUberon(string value)
        => LooksLikeNumericIdentifier(value, "uberon:", 7, "https://www.ebi.ac.uk/ols4/ontologies/uberon/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/uberon/terms?obo_id=");

    public static bool LooksLikeEfo(string value)
        => LooksLikeNumericIdentifier(value, "efo:", 7, "https://www.ebi.ac.uk/ols4/ontologies/efo/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/efo/terms?obo_id=");

    public static bool LooksLikeNcit(string value)
    {
        value = NormalizeIdentifier(value, "https://www.ebi.ac.uk/ols4/ontologies/ncit/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/ncit/terms?obo_id=");

        if (value.StartsWith("ncit:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["ncit:".Length..].Trim();
        }

        if (value.Length != 8 || char.ToUpperInvariant(value[0]) != 'C')
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

    public static bool LooksLikeCl(string value)
        => LooksLikeNumericIdentifier(value, "cl:", 7, "https://www.ebi.ac.uk/ols4/ontologies/cl/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/cl/terms?obo_id=");

    public static bool LooksLikePr(string value)
        => LooksLikeNumericIdentifier(value, "pr:", 9, "https://www.ebi.ac.uk/ols4/ontologies/pr/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/pr/terms?obo_id=");

    public static bool LooksLikeSo(string value)
        => LooksLikeNumericIdentifier(value, "so:", 7, "https://www.ebi.ac.uk/ols4/ontologies/so/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/so/terms?obo_id=");

    public static bool LooksLikeObi(string value)
        => LooksLikeNumericIdentifier(value, "obi:", 7, "https://www.ebi.ac.uk/ols4/ontologies/obi/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/obi/terms?obo_id=");

    public static bool LooksLikeEnvo(string value)
        => LooksLikeNumericIdentifier(value, "envo:", 8, "https://www.ebi.ac.uk/ols4/ontologies/envo/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/envo/terms?obo_id=");

    public static bool LooksLikePato(string value)
        => LooksLikeNumericIdentifier(value, "pato:", 7, "https://www.ebi.ac.uk/ols4/ontologies/pato/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/pato/terms?obo_id=");

    public static bool LooksLikePo(string value)
        => LooksLikeNumericIdentifier(value, "po:", 7, "https://www.ebi.ac.uk/ols4/ontologies/po/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/po/terms?obo_id=");

    public static bool LooksLikeFma(string value)
        => LooksLikeNumericIdentifier(value, "fma:", 5, "https://www.ebi.ac.uk/ols4/ontologies/fma/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/fma/terms?obo_id=");

    public static bool LooksLikeFoodon(string value)
        => LooksLikeNumericIdentifier(value, "foodon:", 8, "https://www.ebi.ac.uk/ols4/ontologies/foodon/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/foodon/terms?obo_id=");

    public static bool LooksLikeFypo(string value)
        => LooksLikeNumericIdentifier(value, "fypo:", 7, "https://www.ebi.ac.uk/ols4/ontologies/fypo/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/fypo/terms?obo_id=");

    public static bool LooksLikeNbo(string value)
        => LooksLikeNumericIdentifier(value, "nbo:", 7, "https://www.ebi.ac.uk/ols4/ontologies/nbo/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/nbo/terms?obo_id=");

    public static bool LooksLikeChmo(string value)
        => LooksLikeNumericIdentifier(value, "chmo:", 7, "https://www.ebi.ac.uk/ols4/ontologies/chmo/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/chmo/terms?obo_id=");

    public static bool LooksLikeMp(string value)
        => LooksLikeNumericIdentifier(value, "mp:", 7, "https://www.ebi.ac.uk/ols4/ontologies/mp/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/mp/terms?obo_id=");

    public static bool LooksLikeZp(string value)
        => LooksLikeNumericIdentifier(value, "zp:", 7, "https://www.ebi.ac.uk/ols4/ontologies/zp/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/zp/terms?obo_id=");

    public static bool LooksLikeMaxo(string value)
        => LooksLikeNumericIdentifier(value, "maxo:", 7, "https://www.ebi.ac.uk/ols4/ontologies/maxo/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/maxo/terms?obo_id=");

    public static bool LooksLikeBfo(string value)
        => LooksLikeNumericIdentifier(value, "bfo:", 7, "https://www.ebi.ac.uk/ols4/ontologies/bfo/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/bfo/terms?obo_id=");

    public static bool LooksLikeCmo(string value)
        => LooksLikeNumericIdentifier(value, "cmo:", 7, "https://www.ebi.ac.uk/ols4/ontologies/cmo/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/cmo/terms?obo_id=");

    public static bool LooksLikeTo(string value)
        => LooksLikeNumericIdentifier(value, "to:", 7, "https://www.ebi.ac.uk/ols4/ontologies/to/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/to/terms?obo_id=");

    public static bool LooksLikeZfa(string value)
        => LooksLikeNumericIdentifier(value, "zfa:", 7, "https://www.ebi.ac.uk/ols4/ontologies/zfa/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/zfa/terms?obo_id=");

    public static bool LooksLikeWbphenotype(string value)
        => LooksLikeNumericIdentifier(value, "wbphenotype:", 7, "https://www.ebi.ac.uk/ols4/ontologies/wbphenotype/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/wbphenotype/terms?obo_id=");

    public static bool LooksLikeXpo(string value)
        => LooksLikeNumericIdentifier(value, "xpo:", 7, "https://www.ebi.ac.uk/ols4/ontologies/xpo/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/xpo/terms?obo_id=");

    public static bool LooksLikeXao(string value)
        => LooksLikeNumericIdentifier(value, "xao:", 7, "https://www.ebi.ac.uk/ols4/ontologies/xao/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/xao/terms?obo_id=");

    public static bool LooksLikeOba(string value)
        => LooksLikeNumericIdentifier(value, "oba:", 7, "https://www.ebi.ac.uk/ols4/ontologies/oba/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/oba/terms?obo_id=");

    public static bool LooksLikeRs(string value)
        => LooksLikeNumericIdentifier(value, "rs:", 7, "https://www.ebi.ac.uk/ols4/ontologies/rs/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/rs/terms?obo_id=");

    public static bool LooksLikeWbbt(string value)
        => LooksLikeNumericIdentifier(value, "wbbt:", 7, "https://www.ebi.ac.uk/ols4/ontologies/wbbt/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/wbbt/terms?obo_id=");

    public static bool LooksLikeFbbt(string value)
        => LooksLikeNumericIdentifier(value, "fbbt:", 8, "https://www.ebi.ac.uk/ols4/ontologies/fbbt/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/fbbt/terms?obo_id=");

    public static bool LooksLikeFbcv(string value)
        => LooksLikeNumericIdentifier(value, "fbcv:", 7, "https://www.ebi.ac.uk/ols4/ontologies/fbcv/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/fbcv/terms?obo_id=");

    public static bool LooksLikeFbdv(string value)
        => LooksLikeNumericIdentifier(value, "fbdv:", 8, "https://www.ebi.ac.uk/ols4/ontologies/fbdv/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/fbdv/terms?obo_id=");

    public static bool LooksLikeFbbi(string value)
        => LooksLikeNumericIdentifier(value, "fbbi:", 8, "https://www.ebi.ac.uk/ols4/ontologies/fbbi/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/fbbi/terms?obo_id=");

    public static bool LooksLikeFbhh(string value)
        => LooksLikeNumericIdentifier(value, "fbhh:", 7, "https://www.ebi.ac.uk/ols4/ontologies/fbhh/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/fbhh/terms?obo_id=");

    public static bool LooksLikeFbsp(string value)
        => LooksLikeNumericIdentifier(value, "fbsp:", 8, "https://www.ebi.ac.uk/ols4/ontologies/fbsp/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/fbsp/terms?obo_id=");

    public static bool LooksLikeBto(string value)
        => LooksLikeNumericIdentifier(value, "bto:", 7, "https://www.ebi.ac.uk/ols4/ontologies/bto/terms?obo_id=", "https://www.ebi.ac.uk/ols/ontologies/bto/terms?obo_id=");
}
