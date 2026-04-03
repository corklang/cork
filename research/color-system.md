# C64 Color System Reference

Comprehensive technical reference for the Commodore 64 color system, Color RAM, palette
characteristics, and color techniques across all graphics modes.


---

## 1. Overview

The Commodore 64's color system is built around the VIC-II chip's fixed 16-color palette.
Unlike later systems with programmable palettes, the C64's colors are generated entirely in
hardware by the VIC-II, which produces analog luminance and chrominance signals directly.
The palette was designed by Bob Yannes and the MOS Technology team for the YUV/YPbPr color
space used by television standards, not RGB.

### Color Sources in the System

The C64 provides color information from multiple sources:

- **VIC-II color registers** ($D020-$D02E) -- border, background, and sprite colors
- **Color RAM** ($D800-$DBE7) -- per-character foreground color, 4 bits wide
- **Screen RAM** (configurable, default $0400) -- encodes color data in bitmap modes and ECM
- **Bitmap data** -- pixel patterns that select between available colors per cell

### How Colors Flow Through the System

1. The VIC-II reads character/bitmap data and screen RAM from main memory via the 8-bit
   data bus during phi1 (its bus phase)
2. Simultaneously, it reads the 4-bit color nybble from Color RAM via its dedicated 4-bit
   color bus (bits D8-D11 of its 12-bit internal data bus)
3. The VIC-II combines this data with its internal color registers to determine the final
   color for each pixel
4. The color index (0-15) is converted to analog luma and chroma signals and output to the
   video port

### Color Registers

| Address | Decimal | Name | Description |
|---------|---------|------|-------------|
| $D020   | 53280   | EC   | Border (exterior) color |
| $D021   | 53281   | B0C  | Background color 0 |
| $D022   | 53282   | B1C  | Background color 1 (multicolor modes) |
| $D023   | 53283   | B2C  | Background color 2 (multicolor modes) |
| $D024   | 53284   | B3C  | Background color 3 (ECM mode) |
| $D025   | 53285   | MM0  | Sprite multicolor 0 (shared, bit pair %01) |
| $D026   | 53286   | MM1  | Sprite multicolor 1 (shared, bit pair %11) |
| $D027   | 53287   | M0C  | Sprite 0 individual color |
| $D028   | 53288   | M1C  | Sprite 1 individual color |
| $D029   | 53289   | M2C  | Sprite 2 individual color |
| $D02A   | 53290   | M3C  | Sprite 3 individual color |
| $D02B   | 53291   | M4C  | Sprite 4 individual color |
| $D02C   | 53292   | M5C  | Sprite 5 individual color |
| $D02D   | 53293   | M6C  | Sprite 6 individual color |
| $D02E   | 53294   | M7C  | Sprite 7 individual color |

All color registers use only the lower 4 bits (values 0-15). The upper 4 bits are ignored
on write and return undefined values on read.


---

## 2. The C64 Color Palette

### 2.1 The 16 Colors

| Index | Hex  | Name         | Luminance Group |
|-------|------|--------------|-----------------|
| 0     | $00  | Black        | 0 (darkest)     |
| 1     | $01  | White        | 8 (brightest)   |
| 2     | $02  | Red          | 3               |
| 3     | $03  | Cyan         | 6               |
| 4     | $04  | Purple       | 4               |
| 5     | $05  | Green        | 5               |
| 6     | $06  | Blue         | 2               |
| 7     | $07  | Yellow       | 7               |
| 8     | $08  | Orange       | 4               |
| 9     | $09  | Brown        | 2               |
| 10    | $0A  | Light Red    | 5               |
| 11    | $0B  | Dark Grey    | 3               |
| 12    | $0C  | Medium Grey  | 4               |
| 13    | $0D  | Light Green  | 7               |
| 14    | $0E  | Light Blue   | 4               |
| 15    | $0F  | Light Grey   | 6               |

Note: "Light Red" is sometimes called "Pink" in Commodore documentation.

### 2.2 Palette Design Philosophy

Robert Yannes (VIC-II designer) explained: "Since we had total control over hue, saturation
and luminance, we picked colors that we liked...many of the colors were simply the opposite
side of the color wheel."

The palette is structured around:

- **5 achromatic colors:** Black (0), Dark Grey (11), Medium Grey (12), Light Grey (15),
  White (1)
- **3 primary + 3 secondary colors:** Red (2), Green (5), Blue (6), Cyan (3), Purple (4),
  Yellow (7)
- **3 "light" variants:** Light Red (10), Light Green (13), Light Blue (14)
- **2 warm earth tones:** Orange (8), Brown (9)

The "light" colors (10, 13, 14) are not simply brighter versions of their base colors --
they have the same hue but both different luminance and reduced saturation, giving them a
pastel quality.

### 2.3 Luminance Values

The VIC-II generates luminance as discrete voltage levels. Different chip revisions use
different numbers of luminance levels, which is the single largest visual difference
between revisions.

#### Early Revision (6569R1) -- 5 Luminance Levels

| Level | Relative Value | Colors |
|-------|----------------|--------|
| 0     | 0              | Black |
| 1     | 8              | Blue, Brown, Red, Dark Grey, Purple, Orange, Green, Light Red |
| 2     | 16             | Medium Grey, Light Blue, Cyan, Light Grey |
| 3     | 24             | Yellow, Light Green |
| 4     | 32             | White |

The early revision crammed 8 colors onto a single luminance level and 4 onto another,
meaning many colors were indistinguishable on a black-and-white television. This was a
significant problem given that many households still had monochrome sets in the early 1980s.

#### Later Revisions (6569R3 and onward) -- 9 Luminance Levels

| Level | Relative Value | Colors |
|-------|----------------|--------|
| 0     | 0              | Black |
| 1     | 8              | Blue, Brown |
| 2     | 10             | Red, Dark Grey |
| 3     | 12             | Purple, Orange |
| 4     | 15             | Medium Grey, Light Blue |
| 5     | 16             | Green, Light Red |
| 6     | 20             | Cyan, Light Grey |
| 7     | 24             | Yellow, Light Green |
| 8     | 32             | White |

The later revision split the single crowded luminance level into four separate levels,
dramatically improving monochrome legibility and giving the palette much more tonal range.
All luminance values are divisible by a common factor and scale to 8-bit values via the
conversion factor 255/32 = 7.96875.

### 2.4 Hue Angles

Colors are arranged on the color wheel at angles divisible by 22.5 degrees. The VIC-II
encodes hue as a phase angle of the chrominance signal relative to the color burst
reference:

| Color        | Angle (degrees) | Complementary Color |
|--------------|-----------------|---------------------|
| Blue         | 0               | Yellow              |
| Light Blue   | 0               | Yellow              |
| Purple       | 45              | Green               |
| Red          | 112.5           | Cyan                |
| Light Red    | 112.5           | Cyan                |
| Orange       | 135             | --                  |
| Brown        | 157.5           | --                  |
| Yellow       | 180             | Blue                |
| Green        | 225             | Purple              |
| Light Green  | 225             | Purple              |
| Cyan         | 292.5           | Red                 |

Black, White, Dark Grey, Medium Grey, and Light Grey have no chrominance component
(saturation = 0). All chromatic colors share the same saturation level.

### 2.5 RGB Approximations

Because the VIC-II works natively in YUV color space (for PAL) or YIQ (for NTSC), there
is no single "correct" RGB representation. Different palette models account for different
gamma values, monitor characteristics, and viewing conditions.

#### Pepto Palette (2001, widely used in emulators)

Derived mathematically from the VIC-II's YUV output with gamma correction
(source gamma 2.8, target gamma 2.2, effective exponent 2.8/2.2 = ~1.27):

| Index | Color        | Hex      | R    | G    | B    |
|-------|--------------|----------|------|------|------|
| 0     | Black        | #000000  | 0    | 0    | 0    |
| 1     | White        | #FFFFFF  | 255  | 255  | 255  |
| 2     | Red          | #68372B  | 104  | 55   | 43   |
| 3     | Cyan         | #70A4B2  | 112  | 164  | 178  |
| 4     | Purple       | #6F3D86  | 111  | 61   | 134  |
| 5     | Green        | #588D43  | 88   | 141  | 67   |
| 6     | Blue         | #352879  | 53   | 40   | 121  |
| 7     | Yellow       | #B8C76F  | 184  | 199  | 111  |
| 8     | Orange       | #6F4F25  | 111  | 79   | 37   |
| 9     | Brown        | #433900  | 67   | 57   | 0    |
| 10    | Light Red    | #9A6759  | 154  | 103  | 89   |
| 11    | Dark Grey    | #444444  | 68   | 68   | 68   |
| 12    | Medium Grey  | #6C6C6C  | 108  | 108  | 108  |
| 13    | Light Green  | #9AD284  | 154  | 210  | 132  |
| 14    | Light Blue   | #6C5EB5  | 108  | 94   | 181  |
| 15    | Light Grey   | #959595  | 149  | 149  | 149  |

#### Colodore Palette (2017, updated Pepto model)

A recalculated model by Philip "Pepto" Timmermann with higher gamma values, producing
brighter, less muddy colors that better match measurements from real hardware:

| Index | Color        | Hex      | R    | G    | B    |
|-------|--------------|----------|------|------|------|
| 0     | Black        | #000000  | 0    | 0    | 0    |
| 1     | White        | #FFFFFF  | 255  | 255  | 255  |
| 2     | Red          | #813338  | 129  | 51   | 56   |
| 3     | Cyan         | #75CEC8  | 117  | 206  | 200  |
| 4     | Purple       | #8E3C97  | 142  | 60   | 151  |
| 5     | Green        | #56AC4D  | 86   | 172  | 77   |
| 6     | Blue         | #2E2C9B  | 46   | 44   | 155  |
| 7     | Yellow       | #EDF171  | 237  | 241  | 113  |
| 8     | Orange       | #8E5029  | 142  | 80   | 41   |
| 9     | Brown        | #553800  | 85   | 56   | 0    |
| 10    | Light Red    | #C46C71  | 196  | 108  | 113  |
| 11    | Dark Grey    | #4A4A4A  | 74   | 74   | 74   |
| 12    | Medium Grey  | #7B7B7B  | 123  | 123  | 123  |
| 13    | Light Green  | #A9FF9F  | 169  | 255  | 159  |
| 14    | Light Blue   | #706DEB  | 112  | 109  | 235  |
| 15    | Light Grey   | #B2B2B2  | 178  | 178  | 178  |

#### VICE Default Palette (common in emulation)

| Index | Color        | Hex      |
|-------|--------------|----------|
| 0     | Black        | #000000  |
| 1     | White        | #FFFFFF  |
| 2     | Red          | #894036  |
| 3     | Cyan         | #7ABFC7  |
| 4     | Purple       | #8A46AE  |
| 5     | Green        | #68A941  |
| 6     | Blue         | #3E31A2  |
| 7     | Yellow       | #D0DC71  |
| 8     | Orange       | #905F25  |
| 9     | Brown        | #5C4700  |
| 10    | Light Red    | #BB776D  |
| 11    | Dark Grey    | #555555  |
| 12    | Medium Grey  | #808080  |
| 13    | Light Green  | #ACEA88  |
| 14    | Light Blue   | #7C70DA  |
| 15    | Light Grey   | #ABABAB  |

### 2.6 YUV to RGB Conversion

The standard conversion from YUV (CCIR 601 / PAL) to RGB is:

```
R = Y + 1.140 * V
G = Y - 0.396 * U - 0.581 * V
B = Y + 2.029 * U
```

Where Y is the luminance, U and V are the chrominance components. The PAL encoding
formulas are:

```
Y = 0.299*R + 0.587*G + 0.114*B
U = -0.147*R - 0.289*G + 0.436*B
V = 0.615*R - 0.515*G - 0.100*B
```

For the VIC-II specifically, Pepto's model constrains the saturation scaling to
approximately 34.008, determined by brown (the color that first produces a negative blue
channel value during conversion, which must be clamped to zero).

### 2.7 PAL vs NTSC Color Differences

| Aspect | PAL (6569/8565) | NTSC (6567/8562) |
|--------|-----------------|-------------------|
| Color encoding | YUV (CCIR 601) | YIQ |
| Color subcarrier | 4.43361875 MHz | 3.579545 MHz |
| Hue stability | Excellent (PAL line alternation) | Prone to hue shifts |
| Perceived brightness | Slightly darker/muted | Brighter, more vivid |
| Color blending | Superior (PAL delay line) | More visible artifacts |
| Line frequency | 15625 Hz | 15734 Hz |

On NTSC systems, orange actually looks orange rather than the brownish tone seen on PAL.
Overall, NTSC colors appear brighter and more saturated, though the PAL signal is more
stable and consistent across different televisions.

The pixel-to-color-clock ratio also differs significantly:
- **PAL:** ~87.5% of color clock per pixel (14:16 ratio)
- **NTSC:** ~43.75% of color clock per pixel (7:16 ratio)

This means NTSC pixels are approximately half a color clock wide, causing more color
fringing and interference patterns when color transitions align poorly with the color clock.

### 2.8 VIC-II Revision Color Variations

| Revision | Luminance Levels | Key Differences |
|----------|------------------|-----------------|
| 6569R1   | 5 (old palette)  | Gold-plated package. Only 4 distinct non-black luma steps. Light pen IRQ trigger bug. Many colors share brightness. |
| 6569R3   | 9 (new palette)  | Most common PAL chip. 8 distinct non-black luma steps. Standard "reference" palette. |
| 6569R4   | 9                | Minor manufacturing refinement of R3. |
| 6569R5   | 9                | Only known "bug-free" VIC-II. Manufactured after C128 introduction. |
| 8565R2   | 9                | HMOS-II process, 5V only. Grey dot bug. Slightly different color saturation due to process change. |
| 6567R56A | 5 (old, NTSC)   | Early NTSC. 64 cycles per line (vs 65 in later). |
| 6567R8   | 9 (new, NTSC)   | Standard NTSC. 65 cycles per line. |
| 8562     | 9 (NTSC)        | HMOS-II NTSC variant. Grey dot bug. 5V only. |

The transition from 5 to 9 luminance levels is the most significant visual change between
revisions. Graphics designed for the old 5-level palette can look noticeably different on
9-level machines, and vice versa.


---

## 3. Color RAM

### 3.1 Physical Hardware

Color RAM is implemented using a dedicated **2114 (MM2114N-3)** static RAM chip on the C64
motherboard. This is physically separate from the 64KB of main DRAM.

Key characteristics of the 2114 SRAM:

- **Organization:** 1024 x 4 bits (1024 addressable locations, each 4 bits wide)
- **Total capacity:** 4096 bits = 512 bytes (but addressed as 1024 nybbles)
- **Type:** Static RAM -- does not require DRAM refresh cycles
- **Speed:** The -3 suffix indicates 300ns access time
- **Data width:** 4 bits only -- each location stores one color value (0-15)

### 3.2 Address Range and Layout

| Property | Value |
|----------|-------|
| Start address | $D800 (55296) |
| End address | $DBE7 (56295) |
| Used locations | 1000 (40 columns x 25 rows) |
| Unused locations | 24 ($DBE8-$DBFF / 56296-56319) |
| Total addressable | 1024 |

Color RAM is organized as a linear array of 1000 nybbles, with each position corresponding
to one character cell on the 40x25 text screen:

```
Color RAM offset = (row * 40) + column

Row 0:  $D800 - $D827  (columns 0-39)
Row 1:  $D828 - $D84F  (columns 0-39)
Row 2:  $D850 - $D877  (columns 0-39)
...
Row 24: $DBB8 - $DBDF  (columns 0-39, extends to $DBE7 with unused bytes)
```

The 24 bytes from $DBE8 to $DBFF are physically present in the 2114 chip (addresses
1000-1023) but are not used by the VIC-II for display. They can be used as general-purpose
4-bit storage.

### 3.3 Bus Architecture and VIC-II Wiring

The 2114 Color RAM chip is connected to the VIC-II through a **dedicated, private 4-bit
data bus**. This is entirely separate from the main 8-bit CPU data bus:

```
                     Main 8-bit bus
CPU <=======================================+======> 64KB DRAM
                                            |
VIC-II  D0-D7 <============================>
        D8-D11 <--private 4-bit bus--> 2114 Color RAM
        A0-A9  <--address lines------> (directly from VIC address bus)
```

The VIC-II's internal data bus is 12 bits wide:
- **Bits D0-D7:** Connected to the main memory data bus (shared with CPU)
- **Bits D8-D11:** Connected exclusively to Color RAM's 4 data pins

The Color RAM's address lines (A0-A9) are driven by the lower 10 bits of the VIC-II's
address bus. This means the VIC-II can access Color RAM independently, without any bus
arbitration with the CPU.

### 3.4 Why Color RAM Is at a Fixed Address

Color RAM always appears at $D800-$DBFF regardless of:
- VIC-II bank selection (controlled by CIA2 at $DD00)
- Memory configuration (controlled by $01 processor port)
- Any other banking scheme

This is because:

1. The VIC-II reads Color RAM through its private 4-bit bus, not through the main memory
   bus. VIC-II bank selection (bits 0-1 of $DD00) only affects the 14-bit address space
   the VIC-II uses for main memory fetches via D0-D7.

2. The CPU accesses Color RAM through the I/O address space. When the I/O region is
   mapped in ($D000-$DFFF), writes to $D800-$DBFF go to the Color RAM chip. The CPU's
   8-bit data bus connects to the Color RAM's 4 data pins on bits 0-3, with the upper
   4 bits floating.

3. The VIC-II's private color bus always reads Color RAM simultaneously during c-accesses
   (character/color matrix reads). There is no register to relocate this mapping.

### 3.5 Read Behavior: Random Upper Bits

Because the Color RAM chip is only 4 bits wide, reading from $D800-$DBFF through the CPU
produces:

- **Bits 0-3:** The actual stored color value (0-15)
- **Bits 4-7:** Random/undefined values from the floating data bus

```asm
; Reading Color RAM correctly
LDA $D800       ; A contains the color in bits 0-3, random garbage in bits 4-7
AND #$0F        ; Mask off the random upper bits to get the true color value
```

When writing, only bits 0-3 are actually stored:

```asm
; Writing Color RAM -- upper bits are ignored
LDA #$01        ; White
STA $D800       ; Only lower nybble ($01) is stored; upper nybble is discarded
```

This behavior means you cannot use Color RAM for general-purpose byte storage. It is
strictly 4-bit memory.

### 3.6 Color RAM and CPU/VIC-II Interaction

The VIC-II reads Color RAM during **c-accesses** (color/character matrix accesses), which
occur on badlines. During a c-access:

1. The VIC-II places a 10-bit address on its address bus
2. Main memory returns a screen matrix byte on D0-D7
3. Simultaneously, Color RAM returns a 4-bit color nybble on D8-D11
4. Both values are latched into the VIC-II's internal 40-entry line buffer

This simultaneous read is only possible because the two memory systems use separate data
buses. The VIC-II caches all 40 color values during the badline and reuses them for the
following 7 non-badlines in the same character row.

### 3.7 Color RAM Cannot Be Double-Buffered

Unlike screen RAM and bitmap data (which can be relocated within VIC-II banks), Color RAM
is permanently fixed at $D800. This creates a fundamental limitation:

- Screen RAM can be pointed to any 1KB-aligned address within the current VIC-II bank
  by writing to $D018
- Bitmap data can be pointed to either $0000 or $2000 within the current bank
- Character data can be pointed to any 2KB-aligned address within the current bank
- **Color RAM cannot be moved at all**

To "double-buffer" Color RAM, you must physically copy 1000 bytes to $D800-$DBE7 during
the vertical blank or at a carefully chosen raster position. At ~5 cycles per byte
(LDA abs,X / STA abs,X / DEX/INX / BNE), copying 1000 bytes takes approximately 5000
cycles -- a significant portion of the ~19656 cycles available per frame (PAL).

Workarounds include:

- **Split even/odd lines:** Use only even or odd character rows per buffer, reducing the
  copy to 500 bytes
- **Partial updates:** Only update the Color RAM cells that actually change
- **Use modes that minimize Color RAM dependence:** Standard bitmap mode uses Screen RAM
  for both colors, making Color RAM irrelevant


---

## 4. Color in Each Graphics Mode

The VIC-II supports five official graphics modes, controlled by three bits in two registers:

| Bit | Register | Name | Function |
|-----|----------|------|----------|
| ECM | $D011 bit 6 | Extended Color Mode | Selects from 4 background colors |
| BMM | $D011 bit 5 | Bitmap Mode | Uses bitmap instead of character ROM |
| MCM | $D016 bit 4 | Multicolor Mode | Enables multicolor (double-width pixels) |

### 4.1 Standard Character Mode (ECM=0, BMM=0, MCM=0)

The default mode at power-on. Each 8x8 pixel character cell has exactly 2 colors.

**Resolution:** 320 x 200 pixels (40 x 25 characters)

**Color sources per character cell:**

| Pixel Value | Color Source | Scope |
|-------------|--------------|-------|
| Bit = 0 | Background color 0 ($D021) | Global (shared by all cells) |
| Bit = 1 | Color RAM ($D800+) bits 0-3 | Per-cell (any of 16 colors) |

**Total colors on screen:** Up to 17 (1 shared background + up to 16 unique foregrounds)

**Color data flow:**
- Screen RAM byte selects which character (0-255) to display
- Character ROM/RAM provides the 8x8 pixel pattern
- Color RAM provides the foreground color for the "1" bits
- $D021 provides the background color for the "0" bits

### 4.2 Multicolor Character Mode (ECM=0, BMM=0, MCM=1)

Trades horizontal resolution for additional colors. Each character cell can display up to
4 colors using double-width pixels (2 pixels wide, interpreted as bit pairs).

**Resolution:** 160 x 200 pixels effective (40 x 25 characters, 4 double-wide pixels per
character horizontally)

**Color sources per character cell:**

| Bit Pair | Color Source | Scope |
|----------|--------------|-------|
| %00 | Background color 0 ($D021) | Global |
| %01 | Background color 1 ($D022) | Global |
| %10 | Background color 2 ($D023) | Global |
| %11 | Color RAM ($D800+) bits 0-2 | Per-cell, but limited to colors 0-7 only |

**Critical limitation:** The Color RAM value's bit 3 acts as a multicolor enable flag. If
bit 3 of the Color RAM value is 0 (color values 0-7), the character is displayed in
standard hires mode with that color as foreground. If bit 3 is 1 (color values 8-15), the
character uses multicolor mode, but the actual color used for bit pair %11 is only bits 0-2
(so colors 8-15 map to colors 0-7 for the pixel color).

Wait -- more precisely: Bit 3 of the Color RAM value determines whether *that specific
character* uses multicolor or hires mode. When multicolor is active for a character, only
bits 0-2 of Color RAM are used for the %11 pixel color, restricting it to colors 0-7.
Characters with Color RAM values 0-7 (bit 3 = 0) display in standard hires mode even when
MCM is globally enabled.

**Total colors on screen:** Up to 4 per cell (3 global + 1 per-cell from first 8 colors).
You can mix hires and multicolor characters on the same screen.

### 4.3 Extended Background Color Mode (ECM=1, BMM=0, MCM=0)

Provides 4 selectable background colors per character cell while keeping hires resolution.
The trade-off is that only 64 characters (0-63) are available instead of 256.

**Resolution:** 320 x 200 pixels (40 x 25 characters)

**Color sources per character cell:**

| Pixel Value | Color Source | Details |
|-------------|--------------|---------|
| Bit = 0 | Background color selected by bits 6-7 of Screen RAM | See table below |
| Bit = 1 | Color RAM ($D800+) bits 0-3 | Per-cell foreground (any of 16 colors) |

**Background selection via Screen RAM upper bits:**

| Screen RAM bits 7-6 | Background Register |
|----------------------|---------------------|
| %00 | $D021 (Background color 0) |
| %01 | $D022 (Background color 1) |
| %10 | $D023 (Background color 2) |
| %11 | $D024 (Background color 3) |

**Character index:** Only bits 0-5 of Screen RAM select the character (0-63), since bits
6-7 are used for background color selection.

**Total colors on screen:** Up to 5 distinct background colors (4 backgrounds + border) and
up to 16 foreground colors, but only 64 unique character shapes.

### 4.4 Standard Bitmap Mode (ECM=0, BMM=1, MCM=0)

Full bitmap control at hires resolution. Each 8x8 pixel cell has 2 independently chosen
colors, both from the full 16-color palette.

**Resolution:** 320 x 200 pixels

**Color sources per 8x8 cell:**

| Pixel Value | Color Source | Details |
|-------------|--------------|---------|
| Bit = 0 | Screen RAM bits 0-3 (lower nybble) | Per-cell background |
| Bit = 1 | Screen RAM bits 4-7 (upper nybble) | Per-cell foreground |

**Color data flow:**
- Bitmap data (8KB) provides the pixel patterns
- Screen RAM (1000 bytes) provides both colors for each 8x8 cell
- Color RAM is **not used** in standard bitmap mode

**Total colors on screen:** 2 per cell, both from the full 16-color palette. Color RAM is
free for other purposes.

### 4.5 Multicolor Bitmap Mode (ECM=0, BMM=1, MCM=1)

The most color-rich standard mode. Each 4x8 cell (double-width pixels) can display 4 colors,
3 of which are independently chosen per cell.

**Resolution:** 160 x 200 pixels effective

**Color sources per 4x8 cell:**

| Bit Pair | Color Source | Scope |
|----------|--------------|-------|
| %00 | Background color 0 ($D021) | Global |
| %01 | Screen RAM bits 4-7 (upper nybble) | Per-cell |
| %10 | Screen RAM bits 0-3 (lower nybble) | Per-cell |
| %11 | Color RAM ($D800+) bits 0-3 | Per-cell |

**Total colors on screen:** Up to 4 per cell. Three of the four are fully independent per
cell (from the full 16 colors), and one ($D021) is shared globally. Theoretically, up to
1000 x 3 + 1 unique color assignments.

This is the most common mode for detailed C64 artwork and the basis for advanced modes
like FLI.

### 4.6 Sprite Colors

Sprites have their own color system, independent of the background graphics mode.

#### Standard (Hires) Sprites

| Pixel Value | Color Source |
|-------------|--------------|
| Bit = 0 | Transparent (background shows through) |
| Bit = 1 | Sprite individual color ($D027-$D02E) |

**Resolution:** 24 x 21 pixels per sprite
**Colors:** 1 visible color per sprite + transparency

#### Multicolor Sprites

| Bit Pair | Color Source | Scope |
|----------|--------------|-------|
| %00 | Transparent | -- |
| %01 | Sprite multicolor 0 ($D025) | Shared by all 8 sprites |
| %10 | Sprite individual color ($D027-$D02E) | Per-sprite |
| %11 | Sprite multicolor 1 ($D026) | Shared by all 8 sprites |

**Resolution:** 12 x 21 pixels per sprite (double-width pixels)
**Colors:** 3 visible colors per sprite, but 2 are shared among all sprites

#### Sprite-Background Color Priority

Sprites and background graphics can be layered. Each sprite has a priority bit
($D01B, bit N for sprite N):
- Priority bit = 0: Sprite appears in front of background
- Priority bit = 1: Sprite appears behind background foreground pixels but in front of
  background color pixels

Sprite-sprite priority is fixed: Sprite 0 is highest priority (in front), Sprite 7 is
lowest.

### 4.7 Invalid/Undocumented Mode Combinations

Three mode combinations are "invalid" and produce unusual results:

| ECM | BMM | MCM | Result |
|-----|-----|-----|--------|
| 1   | 1   | 0   | Invalid bitmap mode -- displays black pixels only |
| 1   | 0   | 1   | Invalid multicolor mode -- displays black pixels only |
| 1   | 1   | 1   | Invalid multicolor bitmap -- displays black pixels only |

In all invalid modes, the foreground pixels are forced to black. The background color
and border color still function normally. These modes are sometimes used as a trick to
quickly blank the screen.


---

## 5. Color Techniques and Tricks

### 5.1 Raster Color Splits

The most fundamental color trick: change VIC-II color registers at specific raster lines
to display different colors in different screen regions.

**How it works:**

1. Set up a raster interrupt at the desired scanline by writing to $D012/$D011
2. In the interrupt handler, change color registers ($D020, $D021, etc.)
3. Set up the next raster interrupt for the next color change
4. Chain as many interrupts as needed

```asm
; Example: Two-zone background color split
irq_top:
    LDA #$00          ; Black
    STA $D021          ; Set background
    LDA #<irq_bottom
    STA $0314
    LDA #>irq_bottom
    STA $0315
    LDA #$80           ; Raster line 128
    STA $D012
    ASL $D019           ; Acknowledge interrupt
    JMP $EA31           ; Return via KERNAL

irq_bottom:
    LDA #$06          ; Blue
    STA $D021          ; Set background
    LDA #<irq_top
    STA $0314
    LDA #>irq_top
    STA $0315
    LDA #$00           ; Raster line 0
    STA $D012
    ASL $D019
    JMP $EA31
```

**Timing considerations:**
- A normal scanline takes 63 cycles (PAL) or 65 cycles (NTSC)
- A badline (every 8th line in the display area) leaves only ~23 CPU cycles
- Color register changes take effect immediately at the current beam position
- For cycle-exact color changes (e.g., mid-line splits), you must account for interrupt
  latency (7 cycles minimum) and stabilize the raster timing

**Common uses:**
- Split-screen: different background colors for status bar and game area
- Raster bars: rainbow-colored horizontal bands in the border or background
- Per-scanline background colors for gradient effects
- Changing graphics modes mid-screen (e.g., text status bar + bitmap game area)

### 5.2 Raster Bars

Raster bars take color splits to the extreme by changing the background and/or border color
on every single scanline, creating smooth gradients or animated color bands.

**Implementation:**
- A tightly timed loop writes new color values to $D020/$D021 each scanline
- The color table is rotated each frame to create animation (the "cycling" effect)
- By shifting the table index each frame, bars appear to move up or down

```asm
; Basic raster bar loop (must be cycle-exact)
    LDX #$00
raster_loop:
    LDA color_table,X  ; 4 cycles
    STA $D021          ; 4 cycles
    ; ... timing padding to fill 63 cycles ...
    INX                ; 2 cycles
    CPX #200           ; 2 cycles
    BNE raster_loop    ; 3/2 cycles
```

Badlines (every 8th line) steal 40 cycles from the CPU, leaving only ~23 cycles. This is
usually enough for one STA $D021 but not enough for elaborate per-line processing. Demo
coders often use pre-computed color tables and unrolled loops to handle this.

### 5.3 Color Cycling

Color cycling creates animation by rotating color values through registers or Color RAM
without changing any graphics data.

**Techniques:**
- **Register rotation:** Cycle $D021-$D024 values each frame for animated backgrounds
- **Color RAM rotation:** Shift colors through Color RAM locations for scrolling color
  effects on text or characters
- **Raster bar animation:** Rotate the color table used for raster bars

The key insight is that color changes are much cheaper (in CPU time) than redrawing
graphics. A single STA to a color register changes the appearance of all pixels using
that color.

### 5.4 Dithering

Dithering uses alternating pixel patterns of two colors to simulate a third, intermediate
color that does not exist in the palette.

#### Checkerboard Dithering (50% Pattern)

The simplest form: alternating pixels of two colors in a 2x2 repeating pattern.

```
AB    Where A and B are two different colors
BA    The eye perceives a blend of A and B
```

This works best on CRT displays where the phosphor dot pitch causes adjacent pixels to
physically overlap. On modern sharp displays, the individual pixels remain visible.

#### Ordered Dithering (Graded Patterns)

More sophisticated patterns provide multiple blend levels:

```
25% blend:   50% blend:   75% blend:
A A A A      A B A B      B A B A
A A A A      B A B A      B B B A
A B A A      A B A B      B A B B
A A A A      B A B A      B B B B
```

#### Best Luminance Combinations for Dithering

Dithering works best when the two colors have similar luminance values. Colors with
large luminance differences produce visible striping rather than smooth blending.

**Same-luminance pairs (ideal for dithering on later VIC-II):**

| Luminance Level | Color Pairs |
|-----------------|-------------|
| Level 2 | Blue (6) + Brown (9) |
| Level 3 | Red (2) + Dark Grey (11) |
| Level 4 | Purple (4) + Orange (8) + Medium Grey (12) + Light Blue (14) |
| Level 5 | Green (5) + Light Red (10) |
| Level 6 | Cyan (3) + Light Grey (15) |
| Level 7 | Yellow (7) + Light Green (13) |

Level 4 is particularly useful because it contains four colors, allowing six different
dither combinations at the same brightness level.

#### Multicolor vs Hires Dithering

- **Hires mode:** Pixels are very small (320 across), so dithering blends well even on
  sharp displays. But each cell only has 2 colors, limiting where dithering can occur.
- **Multicolor mode:** Pixels are twice as wide (160 effective), making individual pixels
  more visible. However, having 4 colors per cell allows more dithering combinations
  within a single character cell. Most C64 game art uses multicolor dithering.

### 5.5 PAL Color Blending (Non-Standard/Secret Colors)

A technique unique to PAL systems that exploits how PAL television encoding blends colors
on alternating scanlines.

**How it works:**

The PAL standard alternates the phase of the chrominance signal by 180 degrees on every
other scanline. A PAL decoder averages the chrominance from adjacent lines using a delay
line. When two colors of the same luminance are placed on alternating scanlines, the PAL
decoder blends their chrominance perfectly, producing a solid color that does not exist in
the C64 palette.

**Requirements:**
- The two colors must have the **same luminance level** for clean blending
- The blending only works on PAL systems with delay-line decoders
- On NTSC, composite monitors, or digital displays, visible striping occurs instead

**Example non-standard colors:**
- Blue (6) alternating with Brown (9) produces a dark neutral tone
- Purple (4) alternating with Orange (8) produces a warm medium tone
- Red (2) alternating with Dark Grey (11) produces a cool dark tone

**Phase asymmetry:** Because the VIC-II does not encode the chrominance signal with a
perfectly symmetrical 180-degree phase offset on alternating lines, swapping which color
is on even vs. odd lines produces a slightly different blended result. This effectively
doubles the number of non-standard colors: each pair creates two distinct blends depending
on the order.

**Achievable palette expansion:**
- The old VIC-II (5 luminance levels) could theoretically produce ~70 distinct colors
  (16 base + ~54 blended pairs)
- The new VIC-II (9 luminance levels) produces approximately 30 usable blended colors
  (fewer per-level pairs, but spread across more levels)
- In practice, many blended results look nearly identical, reducing the useful count

Games like *Mayhem in Monsterland* and *Creatures* famously used this technique to display
colors impossible in the standard palette.

### 5.6 FLI (Flexible Line Interpretation)

FLI is an advanced technique that forces the VIC-II to re-read screen RAM (and thus color
information) on every scanline instead of every 8th scanline. This allows unique color
assignments per scanline within each character column.

**Normal behavior:** The VIC-II reads screen RAM and Color RAM during badlines (every 8th
raster line). The fetched values are cached and reused for 8 lines.

**FLI behavior:** By changing the $D011 register's YSCROLL bits and the $D018 register's
screen matrix pointer at precisely cycle 14 of each non-badline, the CPU forces the
VIC-II to detect a "new" badline and re-read screen RAM. Each forced re-read uses a
different 1KB screen RAM area (pointed to by $D018), providing unique color data per line.

**Memory layout:**

```
$4000-$5FFF: Bitmap data (8KB)
$4000-$43E7: Screen RAM 0 (color map for line 0 of each cell)
$4400-$47E7: Screen RAM 1 (color map for line 1 of each cell)
$4800-$4BE7: Screen RAM 2 (color map for line 2 of each cell)
...
$5C00-$5FE7: Screen RAM 7 (color map for line 7 of each cell)
```

**The 3-column gap:** When the VIC-II is forced into a badline at cycle 14, it realizes
it should have started fetching data 3 cycles earlier (at cycle 11). It reads from an
unlatched data bus for those 3 missed cycles, producing $FF (light grey on light grey).
This creates an unusable 24-pixel (3-character) grey bar on the left side of the screen.

**Per-line register writes:**

```asm
; FLI inner loop (per-scanline, 7 iterations for lines 1-7)
    LDY #$06
inner:
    NOP                       ; timing padding
    NOP
    LDA d018_table,Y          ; load new screen matrix pointer
    STA $D018                 ; point to next color map
    LDA d011_table,Y          ; load new YSCROLL value
    STA $D011                 ; force badline at cycle 14
    DEY
    BPL inner
```

**Variants:**

| Mode | Description | Colors per Cell |
|------|-------------|-----------------|
| FLI | Multicolor bitmap + forced badlines | Up to 16 per 4x8 cell |
| AFLI | FLI in hires bitmap mode | 2 per 8x1 cell |
| IFLI | Interlaced FLI (two frames) | Simulates more colors via flicker |
| NUFLI | Optimized FLI with no left-side gap | Near-photographic quality |

### 5.7 IFLI and MCI (Interlace Techniques)

These techniques display two slightly offset frames in alternation (at 25 fps each on PAL),
exploiting persistence of vision to simulate higher color resolution.

**MCI (Multi-Color Interlaced):**
- Two multicolor bitmap frames offset by 1 pixel horizontally (using $D016 scroll)
- Alternated every frame (50 Hz display, each frame shown at 25 Hz)
- The eye blends the two frames, effectively seeing twice as many color options per area
- Requires two complete bitmaps (16KB) plus two sets of color data

**IFLI (Interlaced FLI):**
- Combines FLI with interlace
- Two FLI frames with horizontal pixel offset, alternated each frame
- Maximum color capability of any C64 display mode
- Requires massive amounts of memory and CPU time

**The flicker problem:**
- On CRT displays, phosphor persistence helps blend the alternating frames
- On LCD/LED displays, the alternation is visible as flicker because there is no
  phosphor decay to smooth the transition
- Modern flat-panel displays use de-interlacing algorithms that may not handle the
  frame-alternation pattern correctly, producing artifacts

**NUFLI (2009):**
- Developed by the Crest demo group
- Achieves near-IFLI color density without flicker
- Uses advanced VIC-II timing tricks including sprites to cover the FLI gap
- Considered the highest-quality static image format for the C64

### 5.8 Optimal Color Pair Combinations

For graphics work, knowing which colors share properties is essential:

**Complementary pairs (opposite on the color wheel):**
- Red (2) and Cyan (3)
- Purple (4) and Green (5)
- Blue (6) and Yellow (7)

**Same-hue pairs (bright/dark versions):**
- Red (2) and Light Red (10) -- hue 112.5 degrees
- Green (5) and Light Green (13) -- hue 225 degrees
- Blue (6) and Light Blue (14) -- hue 0 degrees

**Neutral pairs (grey scale):**
- Black (0), Dark Grey (11), Medium Grey (12), Light Grey (15), White (1)
- Provides 5 achromatic levels for smooth greyscale gradients

**Earth tone sequence:**
- Brown (9), Orange (8), Yellow (7) -- a natural warm progression


---

## 6. Hardcore Details

### 6.1 Color RAM: Physical Implementation

The Color RAM is wired to the VIC-II through a completely separate data path from main
memory. On the C64 motherboard:

- The 2114 SRAM chip sits between the VIC-II and the CPU, accessible to both
- The VIC-II addresses it through its own address lines (A0-A9, directly from the VIC-II
  address bus)
- Data flows to the VIC-II on dedicated pins corresponding to D8-D11 of the VIC-II's
  12-bit internal bus
- The CPU accesses it through the I/O memory map, with the 2114's 4 data pins mapped to
  CPU data bus bits 0-3

On the original C64 "breadbin" motherboards, the 2114 is a discrete DIP chip. On later
C64C "short board" revisions, the Color RAM function may be integrated differently or use
a compatible SRAM replacement, but the interface to the VIC-II remains identical.

### 6.2 VIC-II Color Fetch Timing

The VIC-II performs different types of memory accesses in a strict, repeating pattern across
each raster line. The access types relevant to color:

**c-access (Character/Color matrix access):**
- Occurs during badlines only (every 8th raster line in the display window)
- 40 c-accesses per badline, during cycles 15-54 (PAL)
- Each c-access simultaneously reads:
  - 1 byte from screen RAM (via D0-D7 on the main bus)
  - 1 nybble from Color RAM (via D8-D11 on the private bus)
- The VIC-II captures this during phi1 (its bus phase)
- Results are stored in an internal 40-position buffer (the "video matrix/color line")

**g-access (Graphics data access):**
- Occurs on every visible raster line, during cycles 15-54
- Reads 1 byte of character ROM/RAM or bitmap data per access
- Uses the previously cached screen matrix and color data to interpret the pixels

**Access timing within a PAL raster line (63 cycles):**

```
Cycle  1-10:  Sprite pointer and data fetches (p-access and s-access)
Cycle 11:     First refresh (DRAM refresh, no display data)
Cycle 12-14:  Additional refresh + BA goes low if badline upcoming
Cycle 15-54:  40 interleaved g-accesses (every line) and c-accesses (badlines only)
Cycle 55-57:  Sprite pointer fetches for next line
Cycle 58-63:  More sprite data fetches if sprites active
```

On a **badline**, the VIC-II pulls the BA (Bus Available) line low 3 cycles before cycle 15,
halting the CPU. It then takes over both phi1 and phi2 bus phases for cycles 15-54,
performing interleaved c-accesses (phi1) and g-accesses (phi2). This steals 40 cycles from
the CPU, leaving only ~23 cycles for CPU use on that line.

On a **normal line**, the VIC-II only performs g-accesses during phi1, reading from the data
cached during the previous badline. The CPU runs freely during phi2.

### 6.3 Why Color RAM Cannot Be Double-Buffered (Deep Dive)

The inability to double-buffer Color RAM stems from hardware constraints at multiple levels:

1. **Fixed address:** The VIC-II's color bus is hard-wired. There is no register to redirect
   it to a different memory chip or address range.

2. **No second color bus:** The VIC-II has exactly one 4-bit color input (D8-D11). There is
   no provision for a second Color RAM chip.

3. **Timing impossibility:** The VIC-II reads Color RAM during the phi1 phase of badline
   cycles. The CPU can only access memory during phi2. On a badline, the CPU is halted
   entirely during cycles 12-54. There is no point in the cycle where both the CPU could
   update Color RAM and the VIC-II could read a "new" buffer.

4. **Bus conflict:** If you attempted to add a second 2114 and switch between them, you
   would need hardware external to the VIC-II to perform the switching, as the VIC-II has
   no awareness of such a setup.

The practical workaround used in FLI -- switching the screen RAM pointer via $D018 --
only works because screen RAM goes through the 8-bit main bus (D0-D7), which is already
bank-switchable. Color RAM's dedicated 4-bit bus has no equivalent switching mechanism.

### 6.4 Luma/Chroma Encoding

The VIC-II generates video as separate luminance and chrominance signals internally:

**Luminance (Luma, Y):**
- Generated as one of 9 discrete voltage levels (later revisions) or 5 levels (early)
- Directly output on the luminance pin (pin 15 on 6569)
- Levels are determined by a resistor ladder DAC inside the VIC-II
- Vulnerable to coupling from control signals, especially the AEC line, which can cause
  bright or dark single-pixel artifacts

**Chrominance (Chroma, C):**
- Generated as a phase-modulated sine wave on the color subcarrier
- PAL subcarrier: 4.43361875 MHz
- NTSC subcarrier: 3.579545 MHz
- Hue is encoded as the phase angle relative to the color burst reference
- Saturation is encoded as the amplitude of the modulated signal
- All chromatic C64 colors share the same saturation level
- Output on the chroma pin (pin 14 on 6569)

**PAL phase alternation:**
- On PAL systems, the V (red difference) component's phase is inverted on alternate
  scanlines
- This is what allows PAL decoders to average out hue errors (and what enables the "secret
  color" blending trick)
- The VIC-II implements this phase alternation, but not with a perfect 180-degree offset,
  which is why swapping line order produces slightly different blended colors

### 6.5 Color Bleed on Real Hardware

Color bleed refers to visible color fringing where the chrominance signal "leaks" beyond
its intended pixel boundaries.

**Causes:**

1. **Bandwidth limitation:** The PAL chroma bandwidth is only 1.3 MHz (U) and 0.4 MHz (V),
   while the VIC-II pixel clock is 7.88 MHz (PAL). This means the color signal cannot
   change as fast as the pixel data, causing color to "smear" horizontally across several
   pixels.

2. **Composite encoding:** When luma and chroma are combined into composite video (via the
   RF modulator or composite output), they interfere with each other. This produces the
   characteristic "dot crawl" pattern -- fine diagonal moving lines visible on color
   transitions. The C64's composite output combines luma and chroma, causing the infamous
   red/green interference columns visible on many monitors.

3. **RF modulator:** The worst-quality output. The combined composite signal is further
   modulated onto an RF carrier for antenna input, adding noise and reducing both color
   and luminance resolution significantly.

4. **Chrominance leakage into luminance:** On the C64's video output circuitry, the chroma
   signal can couple into the luma output, causing visible color artifacts even on S-Video
   connections. The C64 outputs chroma at a higher level than the S-Video specification
   expects, as it was designed for Commodore's own monitors with dedicated luma+chroma
   inputs.

**Output quality hierarchy (best to worst):**

1. **Component video mod** (aftermarket) -- separates into Y/Pb/Pr, eliminates cross-talk
2. **S-Video** (separate luma + chroma) -- available on later VIC-II revisions with
   separated outputs; minimal cross-talk
3. **Composite video** (combined luma + chroma) -- standard output, significant artifacts
4. **RF modulator** (antenna) -- worst quality, heavy interference and color bleeding

### 6.6 S-Video vs Composite

**S-Video:**
- Carries luma and chroma as separate signals on separate wires
- Eliminates composite-specific artifacts (dot crawl, cross-color interference)
- Later VIC-II revisions output separated luma and chroma signals directly, providing
  native S-Video capability via the 8-pin DIN video connector
- Early VIC-II revisions do not output separated signals; S-Video requires a hardware
  modification

**Composite:**
- Luma and chroma are combined into a single signal
- The combination process adds interference because the high-frequency chroma signal
  bleeds into the luminance
- The RF modulator on the C64 takes this composite signal and modulates it further for
  antenna input
- Color accuracy is noticeably worse than S-Video, with visible color fringing on
  high-contrast edges

**Key difference in practice:**
- On S-Video, individual hires pixels (320 mode) are clearly visible with clean color
- On composite, hires pixels with different colors bleed into each other, and the color
  of thin vertical lines may be incorrect or appear as a different hue
- Many C64 games and artwork were designed with composite color bleed in mind, and may
  actually look "wrong" on sharp S-Video connections because the bleed was intentional

### 6.7 VIC-II Revision Differences Affecting Color

#### MOS 6569R1 (Early PAL)

- **Package:** Ceramic, often gold-plated pins
- **Power:** Requires +5V and +12V
- **Luminance:** 5 levels only (4 non-black steps)
- **Bugs:** Light pen IRQ trigger bug
- **Color character:** Darker, more saturated appearance. Many colors share brightness,
  making them harder to distinguish on monochrome displays, but the shared luminance
  levels enable more PAL blending combinations
- **Rarity:** Found in earliest C64 "breadbin" units (1982-early 1983)

#### MOS 6569R3 (Standard PAL)

- **Package:** Plastic DIP
- **Power:** Requires +5V and +12V
- **Luminance:** 9 levels (8 non-black steps)
- **Bugs:** None significant
- **Color character:** The "reference" C64 palette. Well-separated luminance levels.
  This is what most people think of as "C64 colors"
- **Prevalence:** Most common PAL VIC-II, found in majority of "breadbin" C64s

#### MOS 6569R5 (Late PAL NMOS)

- **Package:** Plastic DIP
- **Power:** Requires +5V and +12V
- **Luminance:** 9 levels
- **Bugs:** None -- the only known "bug-free" VIC-II revision
- **Prevalence:** Manufactured after C128 introduction. Relatively rare in C64s.

#### MOS 8565R2 (C64C PAL)

- **Package:** Plastic DIP
- **Power:** Requires +5V only (HMOS-II process)
- **Luminance:** 9 levels
- **Bugs:** **Grey dot bug** -- when a write to a color register ($D020-$D02E) occurs
  during a visible cycle, the first pixel of that cycle briefly displays as light grey
  ($0F). This is a race condition in the HMOS-II implementation and does not occur on
  NMOS chips.
- **Color character:** Slightly different saturation and color temperature compared to
  6569R3 due to the process change. Some users report colors appearing "colder" or
  "less saturated."
- **Prevalence:** All C64C / C64-II machines (the "flat" white case)

#### MOS 8562 (C64C NTSC)

- **Package:** Plastic DIP
- **Power:** Requires +5V only
- **Luminance:** 9 levels
- **Bugs:** Grey dot bug (same as 8565)
- **Color character:** NTSC encoding; brighter and more vivid than PAL equivalents

#### Impact of Grey Dot Bug on Color Tricks

The grey dot bug in 8565/8562 chips has practical consequences for raster color effects:

- Changing $D020 or $D021 mid-scanline causes a 1-pixel grey flash at the write point
- Raster bars on C64C machines show a faint grey dot at every color transition
- Workarounds include timing writes to occur during the horizontal blank (not visible)
  or accepting the dots as a minor visual artifact
- Demo coders must detect which VIC-II revision is present and adjust techniques
  accordingly

### 6.8 Video Signal Specifications

| Parameter | PAL (6569) | NTSC (6567R8) |
|-----------|------------|---------------|
| Pixel clock | 7.882 MHz | 8.182 MHz |
| Color subcarrier | 4.43362 MHz | 3.57955 MHz |
| Line frequency | 15625 Hz | 15734 Hz |
| Frame rate | ~50.125 Hz | ~59.826 Hz |
| Visible pixels/line | ~402 max | ~418 max |
| Visible lines | ~284 max | ~235 max |
| Video bandwidth | ~5.0 MHz | ~5.0 MHz |
| Chroma bandwidth (U) | 1.3 MHz | ~0.5 MHz |
| Chroma bandwidth (V) | 0.4 MHz | ~0.5 MHz |

The VIC-II's pixel clock is significantly faster than the chroma bandwidth of both PAL
and NTSC, which is why color information cannot change as fast as luminance information.
This is a fundamental property of analog television encoding, not a VIC-II limitation,
but it has a profound impact on how C64 graphics appear on real hardware.


---

## References

### Primary Technical Sources

- Pepto (Philip Timmermann), "Calculating the Color Palette of the VIC-II"
  https://www.pepto.de/projects/colorvic/

- Pepto, "All You Ever Wanted to Know About the Colors of the Commodore 64" (2001)
  https://www.pepto.de/projects/colorvic/2001/

- Christian Bauer, "The MOS 6567/6569 Video Controller (VIC-II) and Its Application in
  the Commodore 64"
  https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt

- Colodore Palette (Pepto, 2017)
  https://lospec.com/palette-list/colodore

### C64 Wiki References

- Color RAM: https://www.c64-wiki.com/wiki/Color_RAM
- Graphics Modes: https://www.c64-wiki.com/wiki/Graphics_Modes
- Color: https://www.c64-wiki.com/wiki/COLOR
- VIC-II: https://www.c64-wiki.com/wiki/VIC
- Standard Character Mode: https://www.c64-wiki.com/wiki/Standard_Character_Mode
- Multicolor Bitmap Mode: https://www.c64-wiki.com/wiki/Multicolor_Bitmap_Mode
- Extended Color Mode: https://www.c64-wiki.com/wiki/Extended_color_mode
- Raster Interrupt: https://www.c64-wiki.com/wiki/Raster_interrupt
- NUFLI: https://www.c64-wiki.com/wiki/NUFLI
- Sprite: https://www.c64-wiki.com/wiki/Sprite
- RAM: https://www.c64-wiki.com/wiki/RAM

### Display Mode References

- STA, "Commodore 64 Display Modes": https://sta.c64.org/cbm64disp.html
- STA, "Commodore 64 Color Codes": https://sta.c64.org/cbm64col.html
- Cosmigo, "C64 Graphic Mode Basics":
  https://www.cosmigo.com/promotion/docs/onlinehelp/gfxHardware-c64.htm

### VIC-II Timing and Architecture

- VIC-II and FLI Timing (3-part series):
  https://c64os.com/post/flitiming1
  https://c64os.com/post/flitiming2
- VIC-II Register Reference: https://www.oxyron.de/html/registers_vic2.html
- VIC-II Chip Variants: https://ist.uwaterloo.ca/~schepers/MJK/vic2.html
- MOS Technology VIC-II (Wikipedia):
  https://en.wikipedia.org/wiki/MOS_Technology_VIC-II

### Color Techniques and Tricks

- Bumbershoot Software, "FLI, Part 1: 16 Color Mode":
  https://bumbershootsoft.wordpress.com/2016/03/12/fli-part-1-16-color-mode/
- ilesj, "Old VIC-II Colors and Color Blending":
  https://ilesj.wordpress.com/2016/03/30/old-vic-ii-colors-and-color-blending/
- Aaron Bell, "Secret Colours of the Commodore 64":
  https://www.aaronbell.com/secret-colours-of-the-commodore-64/
- Kodiak64, "Non-Standard Hues: PAL & NTSC":
  https://kodiak64.com/blog/non-standard-hues-pal-ntsc-c64/
- krajzewicz, "Stretching the C64 Palette":
  http://www.krajzewicz.de/blog/stretching-the-c64-palette.php

### Video Output and Hardware

- Hitmen, "Accurately Reproducing the Video Output of a C64":
  https://hitmen.c02.at/temp/palstuff/
- C64 Video Enhancement Project:
  https://github.com/c0pperdragon/C64-Video-Enhancement
- FPT-Sokrates, "C64 Color RAM Double Buffer":
  https://github.com/FPT-Sokrates/C64ColorRamDoubleBuffer
- Raster Interrupts and Splitscreen:
  https://c64os.com/post/rasterinterruptsplitscreen
- Lars Haugseth, "C64 PAL Colour Mix":
  https://www.larshaugseth.com/c64/colmix.html
