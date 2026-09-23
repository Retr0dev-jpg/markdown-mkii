using System.Text.RegularExpressions;
using MarkdownMkII.Core.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace MarkdownMkII.Storage;
internal sealed record ImportMetadata(Dictionary<string, string> Fields, IReadOnlyList<string> Tags, string? Warning)
{
    public static ImportMetadata Read(string markdown)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var header = Regex.Match(markdown, @"\A(?:\uFEFF)?---\r?\n(.*?)^(?:---|\.\.\.)[ \t]*(?:\r?\n|$)", RegexOptions.Singleline | RegexOptions.Multiline);
        var body = header.Success ? markdown[header.Length..] : markdown;
        var tags = new HashSet<string>(MarkdownTags.Extract(body), StringComparer.OrdinalIgnoreCase);
        if (!header.Success)
            return new(fields, tags.ToArray(), null);
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(header.Groups[1].Value));
            if (yaml.Documents.FirstOrDefault()?.RootNode is YamlMappingNode mapping)
                foreach (var pair in mapping.Children)
                {
                    if (pair.Key is not YamlScalarNode { Value: { } key })
                        continue;
                    if (pair.Value is YamlScalarNode scalar)
                        fields[key] = scalar.Value ?? "";
                    if (!key.Equals("tags", StringComparison.OrdinalIgnoreCase) && !key.Equals("tag", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (pair.Value is YamlSequenceNode sequence)
                        foreach (var tag in sequence.Children.OfType<YamlScalarNode>())
                        {
                            if (!string.IsNullOrWhiteSpace(tag.Value))
                                tags.Add(tag.Value.TrimStart('#'));
                        }
                    else if (pair.Value is YamlScalarNode { Value: { } value })
                        foreach (var tag in value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
                            tags.Add(tag.TrimStart('#'));
                }

            return new(fields, tags.ToArray(), null);
        }
        catch (YamlException ex)
        {
            return new(fields, tags.ToArray(), ex.Message);
        }
    }
}
