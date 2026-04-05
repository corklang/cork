using Cork.CodeGen.Emit;

namespace Cork.CodeGen.Tests;

public class SpritePatternCompilerTests
{
    [Test]
    public async Task HiRes_all_dots_produces_zero_bytes()
    {
        var pattern = new string('.', 504);
        var bytes = SpritePatternCompiler.Compile(pattern, multicolor: false);
        await Assert.That(bytes).Count().IsEqualTo(63);
        await Assert.That(bytes.All(b => b == 0)).IsTrue();
    }

    [Test]
    public async Task HiRes_all_hashes_produces_0xFF_bytes()
    {
        var pattern = new string('#', 504);
        var bytes = SpritePatternCompiler.Compile(pattern, multicolor: false);
        await Assert.That(bytes).Count().IsEqualTo(63);
        await Assert.That(bytes.All(b => b == 0xFF)).IsTrue();
    }

    [Test]
    public async Task HiRes_first_pixel_sets_bit_7_of_first_byte()
    {
        var pattern = "#" + new string('.', 503);
        var bytes = SpritePatternCompiler.Compile(pattern, multicolor: false);
        await Assert.That(bytes[0]).IsEqualTo((byte)0x80);
    }

    [Test]
    public async Task HiRes_wrong_length_throws()
    {
        await Assert.That(() => SpritePatternCompiler.Compile("...", multicolor: false))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task HiRes_invalid_char_throws()
    {
        var pattern = new string('X', 504);
        await Assert.That(() => SpritePatternCompiler.Compile(pattern, multicolor: false))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Multicolor_all_dots_produces_zero_bytes()
    {
        var pattern = new string('.', 252);
        var bytes = SpritePatternCompiler.Compile(pattern, multicolor: true);
        await Assert.That(bytes).Count().IsEqualTo(63);
        await Assert.That(bytes.All(b => b == 0)).IsTrue();
    }

    [Test]
    public async Task Multicolor_all_3s_produces_0xFF()
    {
        var pattern = new string('3', 252);
        var bytes = SpritePatternCompiler.Compile(pattern, multicolor: true);
        await Assert.That(bytes.All(b => b == 0xFF)).IsTrue();
    }

    [Test]
    public async Task Multicolor_first_pixel_sets_high_bits()
    {
        // First pixel = '2' → bits 7-6 = 10 → 0x80
        var pattern = "2" + new string('.', 251);
        var bytes = SpritePatternCompiler.Compile(pattern, multicolor: true);
        await Assert.That(bytes[0]).IsEqualTo((byte)0x80);
    }

    [Test]
    public async Task Multicolor_wrong_length_throws()
    {
        await Assert.That(() => SpritePatternCompiler.Compile("...", multicolor: true))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Multicolor_invalid_char_throws()
    {
        var pattern = new string('#', 252);
        await Assert.That(() => SpritePatternCompiler.Compile(pattern, multicolor: true))
            .Throws<InvalidOperationException>();
    }
}
