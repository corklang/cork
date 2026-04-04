namespace Cork.CodeGen.Emit;

/// <summary>
/// Converts inline sprite pattern strings to 63 bytes of C64 sprite data.
/// Hi-res: 24×21 (504 chars), 1 bit per pixel using '.' and '#'.
/// Multicolor: 12×21 (252 chars), 2 bits per pixel using '.', '1', '2', '3'.
/// </summary>
public static class SpritePatternCompiler
{
    public static byte[] Compile(string pattern, bool multicolor)
    {
        return multicolor ? CompileMulticolor(pattern) : CompileHiRes(pattern);
    }

    private static byte[] CompileHiRes(string pattern)
    {
        if (pattern.Length != 504)
            throw new InvalidOperationException(
                $"Hi-res sprite pattern must have exactly 504 pixels (24×21), got {pattern.Length}");

        foreach (var c in pattern)
            if (c is not ('.' or '#'))
                throw new InvalidOperationException(
                    $"Hi-res sprite pattern allows only '.' and '#', got '{c}'");

        var bytes = new byte[63];
        for (var row = 0; row < 21; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                byte b = 0;
                for (var bit = 0; bit < 8; bit++)
                {
                    if (pattern[row * 24 + col * 8 + bit] == '#')
                        b |= (byte)(0x80 >> bit);
                }
                bytes[row * 3 + col] = b;
            }
        }
        return bytes;
    }

    private static byte[] CompileMulticolor(string pattern)
    {
        if (pattern.Length != 252)
            throw new InvalidOperationException(
                $"Multicolor sprite pattern must have exactly 252 pixels (12×21), got {pattern.Length}");

        foreach (var c in pattern)
            if (c is not ('.' or '1' or '2' or '3'))
                throw new InvalidOperationException(
                    $"Multicolor sprite pattern allows only '.', '1', '2', '3', got '{c}'");

        var bytes = new byte[63];
        for (var row = 0; row < 21; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                byte b = 0;
                for (var pair = 0; pair < 4; pair++)
                {
                    var value = pattern[row * 12 + col * 4 + pair] switch
                    {
                        '1' => 1, '2' => 2, '3' => 3, _ => 0
                    };
                    b |= (byte)(value << (6 - pair * 2));
                }
                bytes[row * 3 + col] = b;
            }
        }
        return bytes;
    }
}
