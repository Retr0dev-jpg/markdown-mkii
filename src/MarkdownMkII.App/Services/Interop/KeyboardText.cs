using System.Runtime.InteropServices;

namespace MarkdownMkII.Services.Interop;

/// <summary>Reads the character a key would produce without changing keyboard composition state.</summary>
internal static class KeyboardText
{
    private const uint PreserveKeyboardState = 4;

    public static char? FromCurrentKey(uint virtualKey, uint scanCode)
    {
        var state = new byte[256];
        return GetKeyboardState(state)
            ? Translate(virtualKey, scanCode, state, GetKeyboardLayout(0))
            : null;
    }

    internal static char? Translate(uint virtualKey, uint scanCode, byte[] state, nint layout)
    {
        // Space can finish a dead-key composition; process/packet keys belong to IME or Unicode input.
        if (virtualKey is <= 0x20 or > 0xFF or 0xE5 or 0xE7 || state.Length < 256 || layout == 0 ||
            IsDown(state, 0x11) || IsDown(state, 0x12) || IsDown(state, 0x5B) || IsDown(state, 0x5C))
        {
            return null;
        }

        var characters = new char[8];
        // Bit 2 prevents ToUnicodeEx from consuming or introducing a pending dead key.
        var count = ToUnicodeEx(virtualKey, scanCode, state, characters, characters.Length, PreserveKeyboardState, layout);
        return count == 1 && !char.IsControl(characters[0]) && !char.IsSurrogate(characters[0])
            ? characters[0]
            : null;
    }

    internal static MarkdownMkII.Core.Editor.CommandGesture ForCurrentLayout(MarkdownMkII.Core.Editor.CommandGesture gesture)
    {
        var character = gesture.Key switch { 191 => '/', 219 => '[', 221 => ']', 187 => '+', 189 => '-', 188 => ',', 190 => '.', _ => '\0' };
        if (character == '\0') return gesture;
        var translated = VkKeyScanEx(character, GetKeyboardLayout(0));
        if (translated == -1) return gesture;
        var modifiers = gesture.Modifiers;
        if ((translated & 0x100) != 0) modifiers |= MarkdownMkII.Core.Editor.GestureModifiers.Shift;
        if ((translated & 0x600) != 0) return default; // Never borrow AltGr from text entry.
        return new(translated & 0xff, modifiers);
    }

    [DllImport("user32.dll", EntryPoint = "VkKeyScanExW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanEx(char character, nint layout);

    private static bool IsDown(byte[] state, int key) => (state[key] & 0x80) != 0;

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState([Out] byte[] state);

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint virtualKey, uint scanCode, byte[] state,
        [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2)] char[] characters,
        int capacity, uint flags, nint layout);
}
