namespace MarkdownMkII.Core.Highlight;

public static class CodeTokenizer
{
    private static readonly HashSet<string> CSharpKeywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
        "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
        "virtual", "void", "volatile", "while", "record", "var", "await", "async", "required",
        "init", "file", "scoped", "when", "with", "yield"
    ];

    private static readonly HashSet<string> JsKeywords =
    [
        "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete",
        "do", "else", "export", "extends", "false", "finally", "for", "function", "if", "import",
        "in", "instanceof", "let", "new", "null", "return", "super", "switch", "this", "throw",
        "true", "try", "typeof", "var", "void", "while", "with", "yield", "async", "await",
        "of", "from", "as", "static"
    ];

    private static readonly HashSet<string> PythonKeywords =
    [
        "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del",
        "elif", "else", "except", "False", "finally", "for", "from", "global", "if", "import",
        "in", "is", "lambda", "None", "nonlocal", "not", "or", "pass", "raise", "return",
        "True", "try", "while", "with", "yield"
    ];

    private static readonly HashSet<string> SqlKeywords =
    [
        "select", "from", "where", "insert", "update", "delete", "create", "table", "index",
        "join", "left", "right", "inner", "outer", "on", "group", "by", "order", "having",
        "and", "or", "not", "null", "as", "into", "values", "set", "distinct", "limit",
        "offset", "union", "all", "case", "when", "then", "else", "end", "exists", "in"
    ];

    private static readonly HashSet<string> BashKeywords =
    [
        "if", "then", "else", "elif", "fi", "for", "while", "until", "do", "done", "case",
        "esac", "function", "return", "in", "select", "time", "coproc", "export", "local",
        "readonly", "declare", "unset", "shift", "break", "continue", "echo", "printf", "test"
    ];

    private static readonly HashSet<string> RustKeywords =
    [
        "as", "async", "await", "break", "const", "continue", "crate", "dyn", "else", "enum",
        "extern", "false", "fn", "for", "if", "impl", "in", "let", "loop", "match", "mod",
        "move", "mut", "pub", "ref", "return", "self", "Self", "static", "struct", "super",
        "trait", "true", "type", "unsafe", "use", "where", "while"
    ];

    private static readonly HashSet<string> GoKeywords =
    [
        "break", "case", "chan", "const", "continue", "default", "defer", "else", "fallthrough",
        "for", "func", "go", "goto", "if", "import", "interface", "map", "package", "range",
        "return", "select", "struct", "switch", "type", "var"
    ];

    private static readonly HashSet<string> JavaKeywords =
    [
        "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char", "class",
        "const", "continue", "default", "do", "double", "else", "enum", "extends", "final",
        "finally", "float", "for", "goto", "if", "implements", "import", "instanceof", "int",
        "interface", "long", "native", "new", "package", "private", "protected", "public",
        "return", "short", "static", "strictfp", "super", "switch", "synchronized", "this",
        "throw", "throws", "transient", "try", "void", "volatile", "while", "var", "record",
        "yield", "sealed", "permits"
    ];

    private static readonly HashSet<string> YamlKeywords =
    [
        "true", "false", "null", "yes", "no", "on", "off"
    ];

    private static readonly HashSet<string> CssKeywords =
    [
        "color", "background", "margin", "padding", "display", "flex", "grid", "position",
        "width", "height", "border", "font", "text", "align", "justify", "content", "important"
    ];

    private static readonly HashSet<string> PowerShellKeywords =
    [
        "begin", "break", "catch", "class", "continue", "data", "define", "do", "dynamicparam",
        "else", "elseif", "end", "exit", "filter", "finally", "for", "foreach", "from",
        "function", "if", "in", "param", "process", "return", "switch", "throw", "trap",
        "try", "until", "using", "var", "while"
    ];

    public static IReadOnlyList<Preview.CodeToken> Tokenize(string code, string? languageInfo)
        => Tokenize(code, LanguageCatalog.FromInfo(languageInfo));

    public static IReadOnlyList<Preview.CodeToken> Tokenize(string code, LanguageId language)
    {
        code ??= string.Empty;
        if (code.Length == 0 || language == LanguageId.Plain)
        {
            return code.Length == 0
                ? []
                : [new Preview.CodeToken(0, code.Length, Preview.CodeTokenKind.Plain)];
        }

        return language switch
        {
            LanguageId.Xml => TokenizeXml(code),
            LanguageId.Json => TokenizeJson(code),
            LanguageId.CSharp => TokenizeCLike(code, CSharpKeywords, "//", "/*", "*/"),
            LanguageId.JavaScript or LanguageId.TypeScript => TokenizeCLike(code, JsKeywords, "//", "/*", "*/"),
            LanguageId.Python => TokenizePython(code),
            LanguageId.Sql => TokenizeCLike(code, SqlKeywords, "--", "/*", "*/", caseInsensitive: true),
            LanguageId.PowerShell => TokenizeCLike(code, PowerShellKeywords, "#", "<#", "#>", caseInsensitive: true),
            LanguageId.Bash => TokenizeCLike(code, BashKeywords, "#", null, null),
            LanguageId.Rust => TokenizeCLike(code, RustKeywords, "//", "/*", "*/"),
            LanguageId.Go => TokenizeCLike(code, GoKeywords, "//", "/*", "*/"),
            LanguageId.Java => TokenizeCLike(code, JavaKeywords, "//", "/*", "*/"),
            LanguageId.Yaml => TokenizeCLike(code, YamlKeywords, "#", null, null),
            LanguageId.Css => TokenizeCLike(code, CssKeywords, null, "/*", "*/"),
            LanguageId.Markdown => TokenizeMarkdownLite(code),
            LanguageId.Diff => TokenizeDiff(code),
            _ => [new Preview.CodeToken(0, code.Length, Preview.CodeTokenKind.Plain)]
        };
    }

    private static List<Preview.CodeToken> TokenizeCLike(
        string code,
        HashSet<string> keywords,
        string? lineComment,
        string? blockOpen,
        string? blockClose,
        bool caseInsensitive = false)
    {
        var tokens = new List<Preview.CodeToken>();
        var i = 0;
        while (i < code.Length)
        {
            if (!string.IsNullOrEmpty(lineComment) && StartsWith(code, i, lineComment))
            {
                var lineBreak = code.AsSpan(i).IndexOfAny('\r', '\n');
                var end = lineBreak < 0 ? code.Length : i + lineBreak;
                if (end < 0)
                {
                    end = code.Length;
                }

                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.Comment));
                i = end;
                continue;
            }

            if (blockOpen is not null && blockClose is not null && StartsWith(code, i, blockOpen))
            {
                var end = code.IndexOf(blockClose, i + blockOpen.Length, StringComparison.Ordinal);
                end = end < 0 ? code.Length : end + blockClose.Length;
                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.Comment));
                i = end;
                continue;
            }

            var ch = code[i];
            if (ch is '"' or '\'' or '`')
            {
                var end = ScanString(code, i, ch);
                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.String));
                i = end;
                continue;
            }

            if (char.IsDigit(ch))
            {
                var end = i + 1;
                while (end < code.Length && (char.IsDigit(code[end]) || code[end] is '.' or '_' or 'x' or 'X' or 'b' or 'B'))
                {
                    end++;
                }

                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.Number));
                i = end;
                continue;
            }

            if (IsIdentStart(ch))
            {
                var end = i + 1;
                while (end < code.Length && IsIdentPart(code[end]))
                {
                    end++;
                }

                var word = code[i..end];
                var isKeyword = caseInsensitive
                    ? keywords.Contains(word.ToLowerInvariant())
                    : keywords.Contains(word);
                var kind = isKeyword
                    ? Preview.CodeTokenKind.Keyword
                    : char.IsUpper(word[0])
                        ? Preview.CodeTokenKind.TypeName
                        : Preview.CodeTokenKind.Plain;
                tokens.Add(new Preview.CodeToken(i, end - i, kind));
                i = end;
                continue;
            }

            if ("(){}[];,.:?=<>!&|+-*/%".Contains(ch))
            {
                tokens.Add(new Preview.CodeToken(i, 1, Preview.CodeTokenKind.Punctuation));
                i++;
                continue;
            }

            tokens.Add(new Preview.CodeToken(i, 1, Preview.CodeTokenKind.Plain));
            i++;
        }

        return MergePlain(tokens);
    }

    private static List<Preview.CodeToken> TokenizePython(string code)
    {
        var tokens = new List<Preview.CodeToken>();
        var i = 0;
        while (i < code.Length)
        {
            if (code[i] == '#')
            {
                var lineBreak = code.AsSpan(i).IndexOfAny('\r', '\n');
                var end = lineBreak < 0 ? code.Length : i + lineBreak;
                if (end < 0)
                {
                    end = code.Length;
                }

                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.Comment));
                i = end;
                continue;
            }

            if (StartsWith(code, i, "\"\"\"") || StartsWith(code, i, "'''"))
            {
                var quote = code[i..(i + 3)];
                var end = code.IndexOf(quote, i + 3, StringComparison.Ordinal);
                end = end < 0 ? code.Length : end + 3;
                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.String));
                i = end;
                continue;
            }

            if (code[i] is '"' or '\'')
            {
                var end = ScanString(code, i, code[i]);
                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.String));
                i = end;
                continue;
            }

            if (char.IsDigit(code[i]))
            {
                var end = i + 1;
                while (end < code.Length && (char.IsDigit(code[end]) || code[end] == '.'))
                {
                    end++;
                }

                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.Number));
                i = end;
                continue;
            }

            if (IsIdentStart(code[i]))
            {
                var end = i + 1;
                while (end < code.Length && IsIdentPart(code[end]))
                {
                    end++;
                }

                var word = code[i..end];
                var kind = PythonKeywords.Contains(word)
                    ? Preview.CodeTokenKind.Keyword
                    : Preview.CodeTokenKind.Plain;
                tokens.Add(new Preview.CodeToken(i, end - i, kind));
                i = end;
                continue;
            }

            tokens.Add(new Preview.CodeToken(i, 1, Preview.CodeTokenKind.Plain));
            i++;
        }

        return MergePlain(tokens);
    }

    private static List<Preview.CodeToken> TokenizeJson(string code)
    {
        var tokens = new List<Preview.CodeToken>();
        var i = 0;
        while (i < code.Length)
        {
            if (code[i] == '"')
            {
                var end = ScanString(code, i, '"');
                var kind = Preview.CodeTokenKind.String;
                var look = end;
                while (look < code.Length && char.IsWhiteSpace(code[look]))
                {
                    look++;
                }

                if (look < code.Length && code[look] == ':')
                {
                    kind = Preview.CodeTokenKind.TypeName;
                }

                tokens.Add(new Preview.CodeToken(i, end - i, kind));
                i = end;
                continue;
            }

            if (char.IsDigit(code[i]) || (code[i] == '-' && i + 1 < code.Length && char.IsDigit(code[i + 1])))
            {
                var end = i + 1;
                while (end < code.Length && (char.IsDigit(code[end]) || code[end] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    end++;
                }

                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.Number));
                i = end;
                continue;
            }

            if (StartsWith(code, i, "true") || StartsWith(code, i, "false") || StartsWith(code, i, "null"))
            {
                var word = StartsWith(code, i, "true") ? 4 : StartsWith(code, i, "false") ? 5 : 4;
                tokens.Add(new Preview.CodeToken(i, word, Preview.CodeTokenKind.Keyword));
                i += word;
                continue;
            }

            tokens.Add(new Preview.CodeToken(i, 1, "{}[],:".Contains(code[i]) ? Preview.CodeTokenKind.Punctuation : Preview.CodeTokenKind.Plain));
            i++;
        }

        return MergePlain(tokens);
    }

    private static List<Preview.CodeToken> TokenizeXml(string code)
    {
        var tokens = new List<Preview.CodeToken>();
        var i = 0;
        while (i < code.Length)
        {
            if (StartsWith(code, i, "<!--"))
            {
                var end = code.IndexOf("-->", i + 4, StringComparison.Ordinal);
                end = end < 0 ? code.Length : end + 3;
                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.Comment));
                i = end;
                continue;
            }

            if (code[i] == '<')
            {
                var end = code.IndexOf('>', i);
                end = end < 0 ? code.Length : end + 1;
                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.Keyword));
                i = end;
                continue;
            }

            if (code[i] == '"')
            {
                var end = ScanString(code, i, '"');
                tokens.Add(new Preview.CodeToken(i, end - i, Preview.CodeTokenKind.String));
                i = end;
                continue;
            }

            tokens.Add(new Preview.CodeToken(i, 1, Preview.CodeTokenKind.Plain));
            i++;
        }

        return MergePlain(tokens);
    }

    private static List<Preview.CodeToken> TokenizeMarkdownLite(string code)
        => [new Preview.CodeToken(0, code.Length, Preview.CodeTokenKind.Plain)];

    private static List<Preview.CodeToken> TokenizeDiff(string code)
    {
        var tokens = new List<Preview.CodeToken>();
        var i = 0;
        while (i < code.Length)
        {
            var lineStart = i;
            var newline = code.IndexOf('\n', i);
            var lineEnd = newline < 0 ? code.Length : newline;
            var line = code[lineStart..lineEnd];
            var kind = Preview.CodeTokenKind.Plain;
            if (line.StartsWith("diff ", StringComparison.Ordinal) ||
                line.StartsWith("index ", StringComparison.Ordinal) ||
                line.StartsWith("+++", StringComparison.Ordinal) ||
                line.StartsWith("---", StringComparison.Ordinal) ||
                line.StartsWith("@@", StringComparison.Ordinal))
            {
                kind = Preview.CodeTokenKind.Keyword;
            }
            else if (line.StartsWith('+'))
            {
                kind = Preview.CodeTokenKind.TypeName;
            }
            else if (line.StartsWith('-'))
            {
                kind = Preview.CodeTokenKind.Comment;
            }

            if (line.Length > 0 && kind != Preview.CodeTokenKind.Plain)
            {
                tokens.Add(new Preview.CodeToken(lineStart, line.Length, kind));
            }

            i = newline < 0 ? code.Length : newline + 1;
        }

        return MergePlain(tokens);
    }

    private static int ScanString(string code, int start, char quote)
    {
        var i = start + 1;
        while (i < code.Length)
        {
            if (code[i] == '\\' && i + 1 < code.Length)
            {
                i += 2;
                continue;
            }

            if (code[i] == quote)
            {
                return i + 1;
            }

            i++;
        }

        return code.Length;
    }

    private static bool StartsWith(string code, int index, string value)
        => index + value.Length <= code.Length && code.AsSpan(index, value.Length).SequenceEqual(value);

    private static bool IsIdentStart(char ch) => char.IsLetter(ch) || ch == '_';

    private static bool IsIdentPart(char ch) => char.IsLetterOrDigit(ch) || ch == '_';

    private static List<Preview.CodeToken> MergePlain(List<Preview.CodeToken> tokens)
    {
        if (tokens.Count < 2)
        {
            return tokens;
        }

        var merged = new List<Preview.CodeToken>(tokens.Count);
        var current = tokens[0];
        for (var i = 1; i < tokens.Count; i++)
        {
            var next = tokens[i];
            if (current.Kind == next.Kind && current.Start + current.Length == next.Start)
            {
                current = new Preview.CodeToken(current.Start, current.Length + next.Length, current.Kind);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }
}
