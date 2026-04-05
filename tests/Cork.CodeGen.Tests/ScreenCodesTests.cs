using Cork.CodeGen.Emit;

namespace Cork.CodeGen.Tests;

public class ScreenCodesTests
{
    [Test]
    public async Task Uppercase_letters_map_to_1_through_26()
    {
        var codes = ScreenCodes.FromString("AZ");
        await Assert.That(codes[0]).IsEqualTo((byte)1);  // A
        await Assert.That(codes[1]).IsEqualTo((byte)26); // Z
    }

    [Test]
    public async Task Lowercase_maps_to_uppercase()
    {
        var lower = ScreenCodes.FromString("a");
        var upper = ScreenCodes.FromString("A");
        await Assert.That(lower[0]).IsEqualTo(upper[0]);
    }

    [Test]
    public async Task Digits_map_to_PETSCII_values()
    {
        var codes = ScreenCodes.FromString("09");
        await Assert.That(codes[0]).IsEqualTo((byte)'0'); // 48
        await Assert.That(codes[1]).IsEqualTo((byte)'9'); // 57
    }

    [Test]
    public async Task Space_maps_to_32()
    {
        var codes = ScreenCodes.FromString(" ");
        await Assert.That(codes[0]).IsEqualTo((byte)32);
    }

    [Test]
    public async Task At_sign_maps_to_0()
    {
        var codes = ScreenCodes.FromString("@");
        await Assert.That(codes[0]).IsEqualTo((byte)0);
    }

    [Test]
    public async Task Unsupported_character_throws()
    {
        await Assert.That(() => ScreenCodes.FromString("{"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Full_string_encodes_correctly()
    {
        var codes = ScreenCodes.FromString("HELLO");
        await Assert.That(codes).Count().IsEqualTo(5);
        await Assert.That(codes[0]).IsEqualTo((byte)8);  // H
        await Assert.That(codes[1]).IsEqualTo((byte)5);  // E
        await Assert.That(codes[2]).IsEqualTo((byte)12); // L
        await Assert.That(codes[3]).IsEqualTo((byte)12); // L
        await Assert.That(codes[4]).IsEqualTo((byte)15); // O
    }
}
