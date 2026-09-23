using System.Collections.Frozen;

namespace MarkdownMkII.Core.Highlight;

public enum LanguageId
{
    Plain,
    CSharp,
    Xml,
    Json,
    JavaScript,
    TypeScript,
    Python,
    PowerShell,
    Markdown,
    Sql,
    Bash,
    Rust,
    Go,
    Java,
    Yaml,
    Css,
    Diff
}

public static class LanguageCatalog
{
    private static readonly System.Collections.Frozen.FrozenSet<string> DiffAliases = """
        diff patch udiff status namestatus blame log stat numstat reflog stash tags remotes branches worktree
        worktrees submodule submodules config gitconfig shortlog contributors gitignore cherry hooks notes bisect
        describe showref show-ref sparse lsfiles ls-files lsremote ls-remote revlist rev-list countobjects count-
        objects namerev name-rev foreachref for-each-ref lstree ls-tree revparse rev-parse symbolicref symbolic-ref
        version gitversion mergebase merge-base checkignore check-ignore diffname name-only untracked cached
        diffcached diffstat stashshow clean showname modified diffcheck deleted cachedstat unmerged cachedns killed
        diffraw skipped assumed showstat oneline cachednum graphlog decorate cachedcheck firstparent ignorespace
        merges nomerge logrev logall logtopo logdate authordate logparents fmtpatch whatchanged logchildren nodecorate
        logskip noabbrev logsource mailmap logpretty nowalk logabbrev nocolor dateshort logencoding logfollow
        logboundary leftright cherrymark simpdec simpmerges fullhist ancestry remempty showpulls leftonly rightonly
        maxparents minparents invgrep fixedstr igncase allmatch extre perlre basicre grepreflog walkreflog logsize
        fulldiff fuller prettyfull prettyraw prettyemail prettyref dateiso daterel prettymedium prettymbox
        dateisostrict daterfc dateunix datelocal dateraw datehuman prettyshort prettyoneline dateisolocal datedefault
        dateiso8601 dateisostrictlocal daterfc3339 dateformat dateformatlocal prettyformat daterfclocal dateshortlocal
        datedefaultlocal prettytformat daterelativelocal prettyae dateiso8601local prettycn prettyce dateformatym
        prettyan dateformaty prettycname dateformatmd prettyaemail dateformathm prettycemail dateformathms prettyaname
        dateformata prettybody dateformatwd prettyf dateformatmon prettyd dateformatm prettyt dateformatd prettyp
        dateformath prettygd dateformati prettygn dateformatmin prettygs dateformats prettyge dateformatt prettyci
        dateformatr prettycd dateformatf prettycr dateformatz prettyct dateformatx prettyad dateformatp prettyat
        dateformatj prettyai dateformatu prettyas dateformatw prettycs dateformatv prettyn dateformatg prettye
        dateformatc prettytree dateformatwn prettygk dateformatb prettygg dateformatk prettygf dateformate prettygt
        dateformatns prettygp dateformatlc prettycl dateformattz prettyah dateformatlx prettych dateformatepoch
        prettyal dateformatampm prettyalm dateformatmdy prettyclm dateformatap prettygsigner dateformatweekday
        prettygstatus dateformatyy prettyghash dateformatusweek prettyglocal dateformattwelve prettygnm dateformatplus
        prettym dateformatn
        """.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static LanguageId FromInfo(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
        {
            return LanguageId.Plain;
        }

        var key = info.Trim().ToLowerInvariant();
        var space = key.AsSpan().IndexOfAny(" \t\r\n");
        if (space > 0)
        {
            key = key[..space];
        }

        return key switch
        {
            "cs" or "csharp" or "c#" => LanguageId.CSharp,
            "xml" or "xaml" or "html" or "csproj" => LanguageId.Xml,
            "json" => LanguageId.Json,
            "js" or "javascript" => LanguageId.JavaScript,
            "ts" or "typescript" => LanguageId.TypeScript,
            "py" or "python" => LanguageId.Python,
            "ps1" or "powershell" or "pwsh" => LanguageId.PowerShell,
            "md" or "markdown" or "mdx" => LanguageId.Markdown,
            "sql" or "tsql" => LanguageId.Sql,
            "sh" or "bash" or "zsh" or "shell" or "dockerfile" or "makefile" or "nix" => LanguageId.Bash,
            "rs" or "rust" => LanguageId.Rust,
            "go" or "golang" => LanguageId.Go,
            "java" => LanguageId.Java,
            "yml" or "yaml" or "toml" or "ini" or "graphql" or "tfvars" => LanguageId.Yaml,
            "css" or "scss" => LanguageId.Css,
            "c" or "h" or "cpp" or "c++" or "hpp" or "cc" or "cxx" => LanguageId.CSharp,
            "kt" or "kotlin" => LanguageId.Java,
            "rb" or "ruby" or "lua" => LanguageId.Python,
            "php" => LanguageId.CSharp,
            _ when DiffAliases.Contains(key) => LanguageId.Diff,
            "swift" => LanguageId.CSharp,
            "r" => LanguageId.Python,
            "ex" or "elixir" => LanguageId.Python,
            "hs" or "haskell" => LanguageId.Python,
            "scala" => LanguageId.Java,
            "dart" => LanguageId.Java,
            "vue" or "svelte" or "astro" => LanguageId.Xml,
            "terraform" or "tf" or "hcl" => LanguageId.Yaml,
            "zig" => LanguageId.Rust,
            "cmake" => LanguageId.Bash,
            "proto" or "protobuf" or "prisma" => LanguageId.CSharp,
            _ => LanguageId.Plain
        };
    }
}

