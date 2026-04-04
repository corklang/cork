namespace Cork.Output.Prg;

/// <summary>
/// Writes a C64 .prg file with a BASIC stub that auto-starts the machine code.
/// </summary>
public static class PrgWriter
{
    /// <summary>
    /// Calculates the address where machine code will land after the BASIC stub.
    /// Use this as the codeBase for the code generator.
    /// </summary>
    public static ushort CalculateCodeStart(ushort loadAddress = 0x0801)
    {
        // Stub size is deterministic for any SYS address in the $0800-$FFFF range (4-5 digit decimal)
        // Calculate using a dummy address first, then recalculate with the real one
        var dummyStub = GenerateBasicStub(0x0900, loadAddress);
        var codeStart = (ushort)(loadAddress + dummyStub.Length);
        // Recalculate in case the address string length differs
        var realStub = GenerateBasicStub(codeStart, loadAddress);
        return (ushort)(loadAddress + realStub.Length);
    }

    /// <summary>
    /// Creates a .prg file: 2-byte load address + BASIC stub + machine code.
    /// The BASIC stub is: 10 SYS {codeStart}
    /// machineCode must have been assembled at the address returned by CalculateCodeStart().
    /// </summary>
    public static byte[] Create(byte[] machineCode, ushort loadAddress = 0x0801)
    {
        var codeStart = CalculateCodeStart(loadAddress);
        var stub = GenerateBasicStub(codeStart, loadAddress);
        var result = new byte[2 + stub.Length + machineCode.Length];

        // 2-byte load address header (little-endian)
        result[0] = (byte)(loadAddress & 0xFF);
        result[1] = (byte)(loadAddress >> 8);

        // BASIC stub
        stub.CopyTo(result, 2);

        // Machine code
        machineCode.CopyTo(result, 2 + stub.Length);

        return result;
    }

    /// <summary>
    /// Generate a BASIC stub: 10 SYS {address}
    /// Format: [next-line-ptr:2] [line-number:2] [SYS-token:1] [space:1] [address-ascii] [0x00] [0x00 0x00]
    /// </summary>
    private static byte[] GenerateBasicStub(ushort sysAddress, ushort loadAddress)
    {
        var addressStr = sysAddress.ToString();
        var stubBytes = new List<byte>();

        // Next line pointer (points to the terminating 0x00 0x00 after this line)
        // We'll calculate and patch this after building the line
        var nextLinePtrOffset = stubBytes.Count;
        stubBytes.Add(0x00); // placeholder low
        stubBytes.Add(0x00); // placeholder high

        // Line number: 10
        stubBytes.Add(0x0A); // 10 low byte
        stubBytes.Add(0x00); // 10 high byte

        // SYS token
        stubBytes.Add(0x9E); // BASIC token for SYS

        // Space
        stubBytes.Add(0x20);

        // Address as PETSCII digits
        foreach (var c in addressStr)
            stubBytes.Add((byte)c);

        // End of line
        stubBytes.Add(0x00);

        // End of program (null pointer = no more lines)
        stubBytes.Add(0x00);
        stubBytes.Add(0x00);

        // Patch the next-line pointer: points to the end-of-program marker
        var nextLineAddr = (ushort)(loadAddress + stubBytes.Count - 2);
        stubBytes[nextLinePtrOffset] = (byte)(nextLineAddr & 0xFF);
        stubBytes[nextLinePtrOffset + 1] = (byte)(nextLineAddr >> 8);

        return [.. stubBytes];
    }

    public static void WriteToFile(string path, byte[] machineCode, ushort loadAddress = 0x0801)
    {
        var prg = Create(machineCode, loadAddress);
        File.WriteAllBytes(path, prg);
    }
}
