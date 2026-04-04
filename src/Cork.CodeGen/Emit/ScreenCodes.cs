namespace Cork.CodeGen.Emit;

/// <summary>
/// Converts ASCII characters to C64 screen codes.
/// </summary>
public static class ScreenCodes
{
    public static byte[] FromString(string text)
    {
        var result = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
            result[i] = CharToScreenCode(text[i]);
        return result;
    }

    public static byte CharToScreenCode(char c) => c switch
    {
        // Uppercase letters A-Z → 1-26
        >= 'A' and <= 'Z' => (byte)(c - 'A' + 1),
        // Lowercase letters a-z → 1-26 (C64 treats same as uppercase)
        >= 'a' and <= 'z' => (byte)(c - 'a' + 1),
        // Digits 0-9 → 48-57 (same as ASCII)
        >= '0' and <= '9' => (byte)c,
        // Space
        ' ' => 32,
        // Common punctuation (same positions as ASCII in screen codes)
        '!' => 33,
        '"' => 34,
        '#' => 35,
        '$' => 36,
        '%' => 37,
        '&' => 38,
        '\'' => 39,
        '(' => 40,
        ')' => 41,
        '*' => 42,
        '+' => 43,
        ',' => 44,
        '-' => 45,
        '.' => 46,
        '/' => 47,
        ':' => 58,
        ';' => 59,
        '<' => 60,
        '=' => 61,
        '>' => 62,
        '?' => 63,
        '@' => 0,
        '[' => 27,
        ']' => 29,
        '^' => 30,
        '_' => 100,
        // Default: use as-is (may not display correctly)
        _ => (byte)c
    };
}
