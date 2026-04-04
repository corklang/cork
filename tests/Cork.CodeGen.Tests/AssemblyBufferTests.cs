using Cork.CodeGen.Emit;

namespace Cork.CodeGen.Tests;

public class AssemblyBufferTests
{
    [Test]
    public async Task LDA_immediate_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitLdaImmediate(42);
        var bytes = buf.ToArray();
        await Assert.That(bytes).Count().IsEqualTo(2);
        await Assert.That(bytes[0]).IsEqualTo((byte)0xA9);
        await Assert.That(bytes[1]).IsEqualTo((byte)42);
    }

    [Test]
    public async Task STA_absolute_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitStaAbsolute(0xD020);
        var bytes = buf.ToArray();
        await Assert.That(bytes).Count().IsEqualTo(3);
        await Assert.That(bytes[0]).IsEqualTo((byte)0x8D);
        await Assert.That(bytes[1]).IsEqualTo((byte)0x20); // low byte
        await Assert.That(bytes[2]).IsEqualTo((byte)0xD0); // high byte
    }

    [Test]
    public async Task LDA_zero_page_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitLdaZeroPage(0x02);
        var bytes = buf.ToArray();
        await Assert.That(bytes).Count().IsEqualTo(2);
        await Assert.That(bytes[0]).IsEqualTo((byte)0xA5);
        await Assert.That(bytes[1]).IsEqualTo((byte)0x02);
    }

    [Test]
    public async Task STA_zero_page_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitStaZeroPage(0x10);
        var bytes = buf.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0x85);
        await Assert.That(bytes[1]).IsEqualTo((byte)0x10);
    }

    [Test]
    public async Task Branch_BNE_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitBne(unchecked((sbyte)-7));
        var bytes = buf.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0xD0);
        await Assert.That(bytes[1]).IsEqualTo((byte)0xF9); // -7 as unsigned byte
    }

    [Test]
    public async Task Branch_BEQ_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitBeq(5);
        var bytes = buf.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0xF0);
        await Assert.That(bytes[1]).IsEqualTo((byte)5);
    }

    [Test]
    public async Task JMP_absolute_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitJmpAbsolute(0x1234);
        var bytes = buf.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0x4C);
        await Assert.That(bytes[1]).IsEqualTo((byte)0x34); // low byte
        await Assert.That(bytes[2]).IsEqualTo((byte)0x12); // high byte
    }

    [Test]
    public async Task JSR_absolute_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitJsrAbsolute(0xABCD);
        var bytes = buf.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0x20);
        await Assert.That(bytes[1]).IsEqualTo((byte)0xCD);
        await Assert.That(bytes[2]).IsEqualTo((byte)0xAB);
    }

    [Test]
    public async Task RTS_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitRts();
        var bytes = buf.ToArray();
        await Assert.That(bytes).Count().IsEqualTo(1);
        await Assert.That(bytes[0]).IsEqualTo((byte)0x60);
    }

    [Test]
    public async Task CurrentAddress_tracks_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        await Assert.That(buf.CurrentAddress).IsEqualTo((ushort)0x0800);
        buf.EmitLdaImmediate(0);
        await Assert.That(buf.CurrentAddress).IsEqualTo((ushort)0x0802);
        buf.EmitStaAbsolute(0xD020);
        await Assert.That(buf.CurrentAddress).IsEqualTo((ushort)0x0805);
    }

    [Test]
    public async Task DefineLabel_and_GetLabel_work()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitNop();
        buf.DefineLabel("test_label");
        var addr = buf.GetLabel("test_label");
        await Assert.That(addr).IsEqualTo((ushort)0x0801);
    }

    [Test]
    public async Task Forward_reference_resolves_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitJmpForward("target");  // 3 bytes: JMP $0000
        buf.EmitNop();                  // 1 byte at $0803
        buf.EmitNop();                  // 1 byte at $0804
        buf.DefineLabel("target");     // target = $0805
        buf.ResolveFixups();

        var bytes = buf.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0x4C);
        await Assert.That(bytes[1]).IsEqualTo((byte)0x05); // low byte of $0805
        await Assert.That(bytes[2]).IsEqualTo((byte)0x08); // high byte of $0805
    }

    [Test]
    public async Task ADC_immediate_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitAdcImmediate(10);
        var bytes = buf.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0x69);
        await Assert.That(bytes[1]).IsEqualTo((byte)10);
    }

    [Test]
    public async Task SBC_immediate_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitSbcImmediate(5);
        var bytes = buf.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0xE9);
        await Assert.That(bytes[1]).IsEqualTo((byte)5);
    }

    [Test]
    public async Task CMP_immediate_encodes_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitCmpImmediate(255);
        var bytes = buf.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0xC9);
        await Assert.That(bytes[1]).IsEqualTo((byte)0xFF);
    }

    [Test]
    public async Task SEI_and_CLI_encode_correctly()
    {
        var buf = new AssemblyBuffer(0x0800);
        buf.EmitSei();
        buf.EmitCli();
        var bytes = buf.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0x78);
        await Assert.That(bytes[1]).IsEqualTo((byte)0x58);
    }
}
