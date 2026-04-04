namespace Cork.CodeGen.Emit;

/// <summary>
/// Converts ASCII characters to C64 screen codes.
/// Supported: A-Z, a-z (→ uppercase), 0-9, space, and common punctuation.
/// Unsupported characters produce a compile error.
/// </summary>
public static class ScreenCodes
{
    public static byte[] FromString(string text)
    {
        var result = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            if (!TryCharToScreenCode(text[i], out var code))
                throw new InvalidOperationException(
                    $"Unsupported character '{text[i]}' (U+{(int)text[i]:X4}) in string literal. " +
                    $"Supported: A-Z, a-z, 0-9, space, and: !\"#$%&'()*+,-./:;<=>?@[]^_");
            result[i] = code;
        }
        return result;
    }

    public static bool TryCharToScreenCode(char c, out byte code)
    {
        code = c switch
        {
            >= 'A' and <= 'Z' => (byte)(c - 'A' + 1),
            >= 'a' and <= 'z' => (byte)(c - 'a' + 1),
            >= '0' and <= '9' => (byte)c,
            ' ' => 32,
            '!' => 33, '"' => 34, '#' => 35, '$' => 36,
            '%' => 37, '&' => 38, '\'' => 39,
            '(' => 40, ')' => 41, '*' => 42, '+' => 43,
            ',' => 44, '-' => 45, '.' => 46, '/' => 47,
            ':' => 58, ';' => 59, '<' => 60, '=' => 61,
            '>' => 62, '?' => 63, '@' => 0,
            '[' => 27, ']' => 29, '^' => 30, '_' => 100,
            _ => 0xFF // sentinel for unsupported
        };
        return code != 0xFF;
    }
}
