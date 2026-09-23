namespace MarkdownMkII.Core.Editor;
[Flags]
public enum GestureModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4
}

public readonly record struct CommandGesture(int Key, GestureModifiers Modifiers)
{
    private static readonly Dictionary<string, int> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Enter"] = 13,
        ["Tab"] = 9,
        ["Esc"] = 27,
        ["Escape"] = 27,
        ["Space"] = 32,
        ["Left"] = 37,
        ["Up"] = 38,
        ["Right"] = 39,
        ["Down"] = 40,
        ["PageUp"] = 33,
        ["PageDown"] = 34,
        ["Home"] = 36,
        ["End"] = 35,
        ["Delete"] = 46,
        ["Backspace"] = 8,
        ["/"] = 191,
        ["["] = 219,
        ["]"] = 221,
        ["Plus"] = 187,
        ["Minus"] = 189,
        ["Comma"] = 188,
        ["Period"] = 190
    };
    public static bool TryParse(string? text, out CommandGesture result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;
        var modifiers = GestureModifiers.None;
        foreach (var part in parts[..^1])
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                modifiers |= GestureModifiers.Control;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= GestureModifiers.Shift;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= GestureModifiers.Alt;
            else
                return false;
        }

        var last = parts[^1];
        int key;
        if (last.Length == 1 && char.IsAsciiLetterOrDigit(last[0]))
            key = char.ToUpperInvariant(last[0]);
        else if (last.Length > 1 && last.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(last[1..], out var function) && function is >= 1 and <= 12)
            key = 111 + function;
        else if (!NamedKeys.TryGetValue(last, out key))
            return false;
        result = new(key, modifiers);
        return true;
    }

    public bool CanAssign => Key != 0 && !(Modifiers.HasFlag(GestureModifiers.Control) && Modifiers.HasFlag(GestureModifiers.Alt)) && !(Modifiers.HasFlag(GestureModifiers.Alt) && Key is 9 or 27 or 32 or 115) && !(Modifiers == GestureModifiers.Control && Key is 65 or 67 or 86 or 88) && (Modifiers.HasFlag(GestureModifiers.Control) || Modifiers.HasFlag(GestureModifiers.Alt) || Key is >= 112 and <= 123) && !(Key == 9 || Key == 27 || Key == 32 && Modifiers.HasFlag(GestureModifiers.Alt)) && !(Modifiers.HasFlag(GestureModifiers.Control) && Key is 8 or 46 or 37 or 39 or 36 or 35);

    public override string ToString()
    {
        if (Key == 0)
            return "";
        var key = Key;
        var prefix = (Modifiers.HasFlag(GestureModifiers.Control) ? "Ctrl+" : "") + (Modifiers.HasFlag(GestureModifiers.Shift) ? "Shift+" : "") + (Modifiers.HasFlag(GestureModifiers.Alt) ? "Alt+" : "");
        var name = Key is >= 48 and <= 90 ? ((char)Key).ToString() : Key is >= 112 and <= 123 ? "F" + (Key - 111) : NamedKeys.FirstOrDefault(p => p.Value == key).Key ?? Key.ToString();
        return prefix + name;
    }
}
