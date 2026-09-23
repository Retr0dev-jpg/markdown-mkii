namespace MarkdownMkII.Services.Localization;

internal static class ResourceKey
{
    public static string ToPath(string key) => string.Create(key.Length, key, (path, source) =>
    {
        var inNamespace = false;
        for (var i = 0; i < source.Length; i++)
        {
            var character = source[i];
            if (character == '[') inNamespace = true;
            else if (character == ']') inNamespace = false;

            // MakePRI preserves dots inside [using:Namespace] in attached properties.
            path[i] = character == '.' && !inNamespace ? '/' : character;
        }
    });
}
