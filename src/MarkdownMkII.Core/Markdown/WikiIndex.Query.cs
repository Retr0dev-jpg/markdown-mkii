using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Markdown;

public static partial class WikiIndex
{
    private static Func<string, bool>? FrontMatterValidator(FrontMatterMatchKind mode) => mode switch
    {
        FrontMatterMatchKind.Url => MarkdownEditing.LooksLikeWebUrl,
        FrontMatterMatchKind.Email => MarkdownEditing.LooksLikeEmail,
        FrontMatterMatchKind.Uuid => MarkdownEditing.LooksLikeUuid,
        FrontMatterMatchKind.Isbn => MarkdownEditing.LooksLikeIsbn,
        FrontMatterMatchKind.Issn => MarkdownEditing.LooksLikeIssn,
        FrontMatterMatchKind.Orcid => MarkdownEditing.LooksLikeOrcid,
        FrontMatterMatchKind.Doi => MarkdownEditing.LooksLikeDoi,
        FrontMatterMatchKind.Pmid => MarkdownEditing.LooksLikePmid,
        FrontMatterMatchKind.Arxiv => MarkdownEditing.LooksLikeArxiv,
        FrontMatterMatchKind.Pmcid => MarkdownEditing.LooksLikePmcid,
        FrontMatterMatchKind.Wikidata => MarkdownEditing.LooksLikeWikidata,
        FrontMatterMatchKind.Ror => MarkdownEditing.LooksLikeRor,
        FrontMatterMatchKind.Grid => MarkdownEditing.LooksLikeGrid,
        FrontMatterMatchKind.Handle => MarkdownEditing.LooksLikeHandle,
        FrontMatterMatchKind.Isni => MarkdownEditing.LooksLikeIsni,
        FrontMatterMatchKind.Viaf => MarkdownEditing.LooksLikeViaf,
        FrontMatterMatchKind.Lccn => MarkdownEditing.LooksLikeLccn,
        FrontMatterMatchKind.Oclc => MarkdownEditing.LooksLikeOclc,
        FrontMatterMatchKind.Gnd => MarkdownEditing.LooksLikeGnd,
        FrontMatterMatchKind.Bnf => MarkdownEditing.LooksLikeBnf,
        FrontMatterMatchKind.Sudoc => MarkdownEditing.LooksLikeSudoc,
        FrontMatterMatchKind.OpenAlex => MarkdownEditing.LooksLikeOpenAlex,
        FrontMatterMatchKind.Olid => MarkdownEditing.LooksLikeOlid,
        FrontMatterMatchKind.S2cid => MarkdownEditing.LooksLikeS2cid,
        FrontMatterMatchKind.Ndl => MarkdownEditing.LooksLikeNdl,
        FrontMatterMatchKind.Cinii => MarkdownEditing.LooksLikeCinii,
        FrontMatterMatchKind.Pubchem => MarkdownEditing.LooksLikePubchem,
        FrontMatterMatchKind.Cas => MarkdownEditing.LooksLikeCas,
        FrontMatterMatchKind.Inchi => MarkdownEditing.LooksLikeInchi,
        FrontMatterMatchKind.Smiles => MarkdownEditing.LooksLikeSmiles,
        FrontMatterMatchKind.Unii => MarkdownEditing.LooksLikeUnii,
        FrontMatterMatchKind.Chemspider => MarkdownEditing.LooksLikeChemspider,
        FrontMatterMatchKind.Chebi => MarkdownEditing.LooksLikeChebi,
        FrontMatterMatchKind.Ec => MarkdownEditing.LooksLikeEc,
        FrontMatterMatchKind.Mesh => MarkdownEditing.LooksLikeMesh,
        FrontMatterMatchKind.Hmdb => MarkdownEditing.LooksLikeHmdb,
        FrontMatterMatchKind.Kegg => MarkdownEditing.LooksLikeKegg,
        FrontMatterMatchKind.Chembl => MarkdownEditing.LooksLikeChembl,
        FrontMatterMatchKind.Inchikey => MarkdownEditing.LooksLikeInchiKey,
        FrontMatterMatchKind.Drugbank => MarkdownEditing.LooksLikeDrugbank,
        FrontMatterMatchKind.Pdb => MarkdownEditing.LooksLikePdb,
        FrontMatterMatchKind.Uniprot => MarkdownEditing.LooksLikeUniprot,
        FrontMatterMatchKind.Ensembl => MarkdownEditing.LooksLikeEnsembl,
        FrontMatterMatchKind.Rhea => MarkdownEditing.LooksLikeRhea,
        FrontMatterMatchKind.Lincs => MarkdownEditing.LooksLikeLincs,
        FrontMatterMatchKind.Hgnc => MarkdownEditing.LooksLikeHgnc,
        FrontMatterMatchKind.Go => MarkdownEditing.LooksLikeGo,
        FrontMatterMatchKind.Interpro => MarkdownEditing.LooksLikeInterpro,
        FrontMatterMatchKind.Pfam => MarkdownEditing.LooksLikePfam,
        FrontMatterMatchKind.Reactome => MarkdownEditing.LooksLikeReactome,
        FrontMatterMatchKind.Refseq => MarkdownEditing.LooksLikeRefseq,
        FrontMatterMatchKind.Mgi => MarkdownEditing.LooksLikeMgi,
        FrontMatterMatchKind.Flybase => MarkdownEditing.LooksLikeFlybase,
        FrontMatterMatchKind.Wormbase => MarkdownEditing.LooksLikeWormbase,
        FrontMatterMatchKind.Sgd => MarkdownEditing.LooksLikeSgd,
        FrontMatterMatchKind.Zfin => MarkdownEditing.LooksLikeZfin,
        FrontMatterMatchKind.Rgd => MarkdownEditing.LooksLikeRgd,
        FrontMatterMatchKind.Tair => MarkdownEditing.LooksLikeTair,
        FrontMatterMatchKind.Xenbase => MarkdownEditing.LooksLikeXenbase,
        FrontMatterMatchKind.Pombase => MarkdownEditing.LooksLikePomBase,
        FrontMatterMatchKind.Dictybase => MarkdownEditing.LooksLikeDictyBase,
        FrontMatterMatchKind.Ecocyc => MarkdownEditing.LooksLikeEcoCyc,
        FrontMatterMatchKind.Cgd => MarkdownEditing.LooksLikeCgd,
        FrontMatterMatchKind.Vectorbase => MarkdownEditing.LooksLikeVectorBase,
        FrontMatterMatchKind.Maizegdb => MarkdownEditing.LooksLikeMaizeGdb,
        FrontMatterMatchKind.Soybase => MarkdownEditing.LooksLikeSoyBase,
        FrontMatterMatchKind.Gramene => MarkdownEditing.LooksLikeGramene,
        FrontMatterMatchKind.Phytozome => MarkdownEditing.LooksLikePhytozome,
        FrontMatterMatchKind.Jgi => MarkdownEditing.LooksLikeJgi,
        FrontMatterMatchKind.Hp => MarkdownEditing.LooksLikeHp,
        FrontMatterMatchKind.Mondo => MarkdownEditing.LooksLikeMondo,
        FrontMatterMatchKind.Doid => MarkdownEditing.LooksLikeDoid,
        FrontMatterMatchKind.Uberon => MarkdownEditing.LooksLikeUberon,
        FrontMatterMatchKind.Efo => MarkdownEditing.LooksLikeEfo,
        FrontMatterMatchKind.Ncit => MarkdownEditing.LooksLikeNcit,
        FrontMatterMatchKind.Cl => MarkdownEditing.LooksLikeCl,
        FrontMatterMatchKind.Pr => MarkdownEditing.LooksLikePr,
        FrontMatterMatchKind.So => MarkdownEditing.LooksLikeSo,
        FrontMatterMatchKind.Obi => MarkdownEditing.LooksLikeObi,
        FrontMatterMatchKind.Envo => MarkdownEditing.LooksLikeEnvo,
        FrontMatterMatchKind.Pato => MarkdownEditing.LooksLikePato,
        FrontMatterMatchKind.Po => MarkdownEditing.LooksLikePo,
        FrontMatterMatchKind.Fma => MarkdownEditing.LooksLikeFma,
        FrontMatterMatchKind.Foodon => MarkdownEditing.LooksLikeFoodon,
        FrontMatterMatchKind.Fypo => MarkdownEditing.LooksLikeFypo,
        FrontMatterMatchKind.Nbo => MarkdownEditing.LooksLikeNbo,
        FrontMatterMatchKind.Chmo => MarkdownEditing.LooksLikeChmo,
        FrontMatterMatchKind.Mp => MarkdownEditing.LooksLikeMp,
        FrontMatterMatchKind.Zp => MarkdownEditing.LooksLikeZp,
        FrontMatterMatchKind.Maxo => MarkdownEditing.LooksLikeMaxo,
        FrontMatterMatchKind.Bfo => MarkdownEditing.LooksLikeBfo,
        FrontMatterMatchKind.Cmo => MarkdownEditing.LooksLikeCmo,
        FrontMatterMatchKind.To => MarkdownEditing.LooksLikeTo,
        FrontMatterMatchKind.Zfa => MarkdownEditing.LooksLikeZfa,
        FrontMatterMatchKind.Wbphenotype => MarkdownEditing.LooksLikeWbphenotype,
        FrontMatterMatchKind.Xpo => MarkdownEditing.LooksLikeXpo,
        FrontMatterMatchKind.Xao => MarkdownEditing.LooksLikeXao,
        FrontMatterMatchKind.Oba => MarkdownEditing.LooksLikeOba,
        FrontMatterMatchKind.Rs => MarkdownEditing.LooksLikeRs,
        FrontMatterMatchKind.Wbbt => MarkdownEditing.LooksLikeWbbt,
        FrontMatterMatchKind.Fbbt => MarkdownEditing.LooksLikeFbbt,
        FrontMatterMatchKind.Fbcv => MarkdownEditing.LooksLikeFbcv,
        FrontMatterMatchKind.Fbdv => MarkdownEditing.LooksLikeFbdv,
        FrontMatterMatchKind.Fbbi => MarkdownEditing.LooksLikeFbbi,
        FrontMatterMatchKind.Fbhh => MarkdownEditing.LooksLikeFbhh,
        FrontMatterMatchKind.Fbsp => MarkdownEditing.LooksLikeFbsp,
        FrontMatterMatchKind.Bto => MarkdownEditing.LooksLikeBto,
        _ => null
    };

    public static IReadOnlyList<WikiBacklink> QueryFrontMatter(
        IEnumerable<string> folders,
        string query,
        int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            return [];
        }

        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || !FrontMatter.TrySplit(text, out var fields, out _))
            {
                continue;
            }

            if (!MatchesWhereExpression(fields, query))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, FrontMatterPreview(fields, query)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    private enum FrontMatterMatchKind
    {
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        InList,
        Regex,
        Empty,
        NotEmpty,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        Between,
        Number,
        Date,
        True,
        False,
        Url,
        Email,
        Uuid,
        Isbn,
        Issn,
        Orcid,
        Doi,
        Pmid,
        Arxiv,
        Pmcid,
        Wikidata,
        Ror,
        Grid,
        Handle,
        Isni,
        Viaf,
        Lccn,
        Oclc,
        Gnd,
        Bnf,
        Sudoc,
        OpenAlex,
        Olid,
        S2cid,
        Ndl,
        Cinii,
        Pubchem,
        Cas,
        Inchi,
        Smiles,
        Unii,
        Chemspider,
        Chebi,
        Ec,
        Mesh,
        Hmdb,
        Kegg,
        Chembl,
        Inchikey,
        Drugbank,
        Pdb,
        Uniprot,
        Ensembl,
        Rhea,
        Lincs,
        Hgnc,
        Go,
        Interpro,
        Pfam,
        Reactome,
        Refseq,
        Mgi,
        Flybase,
        Wormbase,
        Sgd,
        Zfin,
        Rgd,
        Tair,
        Xenbase,
        Pombase,
        Dictybase,
        Ecocyc,
        Cgd,
        Vectorbase,
        Maizegdb,
        Soybase,
        Gramene,
        Phytozome,
        Jgi,
        Hp,
        Mondo,
        Doid,
        Uberon,
        Efo,
        Ncit,
        Cl,
        Pr,
        So,
        Obi,
        Envo,
        Pato,
        Po,
        Fma,
        Foodon,
        Fypo,
        Nbo,
        Chmo,
        Mp,
        Zp,
        Maxo,
        Bfo,
        Cmo,
        To,
        Zfa,
        Wbphenotype,
        Xpo,
        Xao,
        Oba,
        Rs,
        Wbbt,
        Fbbt,
        Fbcv,
        Fbdv,
        Fbbi,
        Fbhh,
        Fbsp,
        Bto
    }

    private static bool TryParseFrontMatterClause(
        string query,
        out string key,
        out string value,
        out FrontMatterMatchKind mode)
    {
        key = string.Empty;
        value = string.Empty;
        mode = FrontMatterMatchKind.Contains;
        query = (query ?? string.Empty).Trim();
        var isAt = query.LastIndexOf(" IS ", StringComparison.OrdinalIgnoreCase);
        if (isAt > 0)
        {
            var qualifier = query[(isAt + 4)..];
            var enumName = qualifier.Equals("NOT EMPTY", StringComparison.OrdinalIgnoreCase) ? "NotEmpty" : qualifier;
            if (Enum.TryParse<FrontMatterMatchKind>(enumName, ignoreCase: true, out var parsed) &&
                Enum.IsDefined(parsed) &&
                enumName.Equals(parsed.ToString(), StringComparison.OrdinalIgnoreCase) &&
                (parsed is FrontMatterMatchKind.Empty or FrontMatterMatchKind.NotEmpty || parsed >= FrontMatterMatchKind.Number))
            {
                key = query[..isAt].Trim();
                mode = parsed;
                return key.Length > 0;
            }
        }

        if (TrySplitFrontMatterOp(query, " ENDS ", out key, out value))
        {
            mode = FrontMatterMatchKind.EndsWith;
            return key.Length > 0;
        }

        if (TrySplitFrontMatterOp(query, " STARTS ", out key, out value))
        {
            mode = FrontMatterMatchKind.StartsWith;
            return key.Length > 0;
        }

        if (TrySplitFrontMatterOp(query, " CONTAINS ", out key, out value))
        {
            mode = FrontMatterMatchKind.Contains;
            return key.Length > 0;
        }

        if (TrySplitFrontMatterOp(query, " IN ", out key, out value))
        {
            mode = FrontMatterMatchKind.InList;
            return key.Length > 0;
        }

        if (TrySplitFrontMatterOp(query, " MATCHES ", out key, out value) ||
            TrySplitFrontMatterOp(query, " REGEX ", out key, out value))
        {
            mode = FrontMatterMatchKind.Regex;
            return key.Length > 0;
        }

        if (TrySplitFrontMatterOp(query, " BETWEEN ", out key, out value))
        {
            mode = FrontMatterMatchKind.Between;
            return key.Length > 0;
        }

        var separator = query.IndexOf("!=", StringComparison.Ordinal);
        if (separator > 0)
        {
            key = query[..separator].Trim();
            value = query[(separator + 2)..].Trim();
            mode = FrontMatterMatchKind.NotContains;
            return key.Length > 0;
        }

        if (TrySplitFrontMatterOp(query, " >= ", out key, out value) ||
            TrySplitFrontMatterOp(query, ">=", out key, out value))
        {
            mode = FrontMatterMatchKind.GreaterOrEqual;
            return key.Length > 0;
        }

        if (TrySplitFrontMatterOp(query, " <= ", out key, out value) ||
            TrySplitFrontMatterOp(query, "<=", out key, out value))
        {
            mode = FrontMatterMatchKind.LessOrEqual;
            return key.Length > 0;
        }

        if (TrySplitFrontMatterOp(query, " > ", out key, out value) ||
            TrySplitFrontMatterOp(query, ">", out key, out value))
        {
            mode = FrontMatterMatchKind.Greater;
            return key.Length > 0;
        }

        if (TrySplitFrontMatterOp(query, " < ", out key, out value) ||
            TrySplitFrontMatterOp(query, "<", out key, out value))
        {
            mode = FrontMatterMatchKind.Less;
            return key.Length > 0;
        }

        separator = query.IndexOfAny(['=', ':']);
        if (separator <= 0)
        {
            return false;
        }

        key = query[..separator].Trim();
        value = query[(separator + 1)..].Trim();
        mode = FrontMatterMatchKind.Contains;
        return key.Length > 0;
    }

    private static bool TrySplitFrontMatterOp(string query, string op, out string key, out string value)
    {
        var at = query.IndexOf(op, StringComparison.OrdinalIgnoreCase);
        if (at <= 0)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = query[..at].Trim();
        value = query[(at + op.Length)..].Trim();
        return true;
    }

    private static int CompareFrontMatterValues(string raw, string value)
    {
        raw = (raw ?? string.Empty).Trim().Trim('"', '\'');
        value = (value ?? string.Empty).Trim().Trim('"', '\'');
        if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var leftNumber) &&
            double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (TryParseFrontMatterDate(raw, out var leftDate) && TryParseFrontMatterDate(value, out var rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        return string.Compare(raw, value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseFrontMatterDate(string value, out DateTime date)
    {
        return DateTime.TryParseExact(
                   value,
                   ["yyyy-MM-dd", "yyyy-MM", "yyyy"],
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None,
                   out date);
    }

    private static bool MatchesFrontMatter(string raw, string value, FrontMatterMatchKind mode)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] is '\'' or '"' && value[^1] == value[0])
        {
            value = value[1..^1];
        }

        if (mode == FrontMatterMatchKind.Empty)
        {
            return string.IsNullOrWhiteSpace((raw ?? string.Empty).Trim().Trim('"', '\''));
        }

        if (mode == FrontMatterMatchKind.NotEmpty)
        {
            return !string.IsNullOrWhiteSpace((raw ?? string.Empty).Trim().Trim('"', '\''));
        }

        if (mode == FrontMatterMatchKind.Number)
        {
            var trimmed = (raw ?? string.Empty).Trim().Trim('"', '\'');
            return double.TryParse(
                trimmed,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out _);
        }

        if (mode == FrontMatterMatchKind.Date)
        {
            var trimmed = (raw ?? string.Empty).Trim().Trim('"', '\'');
            return TryParseFrontMatterDate(trimmed, out _);
        }

        if (mode == FrontMatterMatchKind.True)
        {
            return IsTruthy(raw);
        }

        if (mode == FrontMatterMatchKind.False)
        {
            return IsFalsy(raw);
        }

        if (FrontMatterValidator(mode) is { } validate)
        {
            return validate((raw ?? string.Empty).Trim().Trim('"', '\''));
        }

        if (mode == FrontMatterMatchKind.StartsWith)
        {
            return value.Length == 0 || raw.StartsWith(value, StringComparison.OrdinalIgnoreCase);
        }

        if (mode == FrontMatterMatchKind.EndsWith)
        {
            return value.Length == 0 || raw.EndsWith(value, StringComparison.OrdinalIgnoreCase);
        }

        if (mode is FrontMatterMatchKind.Greater or FrontMatterMatchKind.GreaterOrEqual
            or FrontMatterMatchKind.Less or FrontMatterMatchKind.LessOrEqual)
        {
            if (value.Length == 0)
            {
                return false;
            }

            var cmp = CompareFrontMatterValues(raw, value);
            return mode switch
            {
                FrontMatterMatchKind.Greater => cmp > 0,
                FrontMatterMatchKind.GreaterOrEqual => cmp >= 0,
                FrontMatterMatchKind.Less => cmp < 0,
                _ => cmp <= 0
            };
        }

        if (mode == FrontMatterMatchKind.InList)
        {
            foreach (var item in ParseInList(value))
            {
                if (item.Length > 0 && raw.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        if (mode == FrontMatterMatchKind.Regex)
        {
            value = (value ?? string.Empty).Trim().Trim('/');
            if (value.Length == 0)
            {
                return false;
            }

            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(
                    raw ?? string.Empty,
                    value,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(80));
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                return false;
            }
        }

        if (mode == FrontMatterMatchKind.Between)
        {
            var bounds = ParseInList(value).ToList();
            if (bounds.Count < 2)
            {
                return false;
            }

            return CompareFrontMatterValues(raw, bounds[0]) >= 0 &&
                   CompareFrontMatterValues(raw, bounds[^1]) <= 0;
        }

        var contains = value.Length == 0 ||
                       raw.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        return mode == FrontMatterMatchKind.NotContains ? !contains : contains;
    }

    private static IEnumerable<string> ParseInList(string value)
    {
        value = (value ?? string.Empty).Trim().Trim('(', ')');
        if (value.Length == 0)
        {
            yield break;
        }

        foreach (var piece in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var item = piece.Trim().Trim('"', '\'');
            if (item.Length > 0)
            {
                yield return item;
            }
        }
    }

    private static bool MatchesWhereExpression(IReadOnlyDictionary<string, string> fields, string query)
        => MatchesWhereOr(fields, query);

    private static bool MatchesWhereOr(IReadOnlyDictionary<string, string> fields, string query)
    {
        var parts = SplitWhere(query, " OR ");
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (var orPart in parts)
        {
            if (MatchesWhereAnd(fields, orPart))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesWhereAnd(IReadOnlyDictionary<string, string> fields, string query)
    {
        var parts = SplitWhere(query, " AND ");
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (var clause in parts)
        {
            if (!MatchesWherePrimary(fields, clause))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesWherePrimary(IReadOnlyDictionary<string, string> fields, string query)
    {
        var trimmed = (query ?? string.Empty).Trim();
        if (TryUnwrapParens(trimmed, out var inner))
        {
            return MatchesWhereOr(fields, inner);
        }

        if (!TryParseFrontMatterClause(trimmed, out var key, out var value, out var mode))
        {
            return false;
        }

        fields.TryGetValue(key, out var raw);
        raw ??= string.Empty;
        if (mode is FrontMatterMatchKind.Empty or FrontMatterMatchKind.NotEmpty)
        {
            return MatchesFrontMatter(raw, value, mode);
        }

        return fields.ContainsKey(key) && MatchesFrontMatter(raw, value, mode);
    }

    private static bool TryUnwrapParens(string text, out string inner)
    {
        inner = text;
        if (text.Length < 2 || text[0] != '(')
        {
            return false;
        }

        var close = IndexOfMatchingClose(text, 0);
        if (close != text.Length - 1)
        {
            return false;
        }

        inner = text[1..close].Trim();
        return inner.Length > 0;
    }

    private static int IndexOfMatchingClose(string text, int open)
    {
        var depth = 0;
        var quote = '\0';
        for (var i = open; i < text.Length; i++)
        {
            if (IsQuoted(text, ref i, ref quote))
            {
                continue;
            }

            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string[] SplitWhere(string query, string separator)
    {
        query ??= string.Empty;
        if (query.Length == 0 || string.IsNullOrEmpty(separator))
        {
            return [];
        }

        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i < query.Length; i++)
        {
            if (IsQuoted(query, ref i, ref quote))
            {
                continue;
            }

            var c = query[i];
            if (c == '(')
            {
                depth++;
                continue;
            }

            if (c == ')')
            {
                if (depth > 0)
                {
                    depth--;
                }

                continue;
            }

            if (depth != 0 || i + separator.Length > query.Length)
            {
                continue;
            }

            if (!query.AsSpan(i).StartsWith(separator, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var head = query[start..i].Trim();
            if (head.Length > 0)
            {
                parts.Add(head);
            }

            i += separator.Length - 1;
            start = i + 1;
        }

        var tail = query[start..].Trim();
        if (tail.Length > 0)
        {
            parts.Add(tail);
        }

        return parts.Count == 0 ? [] : [.. parts];
    }

    private static bool IsQuoted(string text, ref int index, ref char quote)
    {
        var current = text[index];
        if (quote != '\0')
        {
            if (current == '\\' && index + 1 < text.Length)
            {
                index++;
            }
            else if (current == quote)
            {
                quote = '\0';
            }
            return true;
        }

        if (current is '\'' or '"')
        {
            quote = current;
            return true;
        }

        return false;
    }

    private static string FrontMatterPreview(IReadOnlyDictionary<string, string> fields, string query)
    {
        var first = SplitWhere(query, " OR ").FirstOrDefault() ?? query;
        first = SplitWhere(first, " AND ").FirstOrDefault() ?? first;
        if (TryParseFrontMatterClause(first, out var key, out _, out _) &&
            fields.TryGetValue(key, out var raw))
        {
            return key + ": " + raw;
        }

        return query;
    }
}
