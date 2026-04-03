# Commodore 64 Bitmap Graphics Modes

## 1. Overview

The Commodore 64's VIC-II video chip (MOS 6567 for NTSC, MOS 6569 for PAL) supports
two native bitmap graphics modes in addition to its character-based text modes:

- **Standard Bitmap Mode (Hires)** -- 320x200 pixels, 2 colors per 8x8 cell
- **Multicolor Bitmap Mode** -- 160x200 pixels (double-width pixels), 4 colors per 8x8 cell

In character mode, the VIC-II reads character codes from screen RAM and looks up their
pixel patterns in a character generator (ROM or RAM). The screen is a grid of 40x25
characters, each 8x8 pixels. The programmer does not control individual pixels -- only
which character appears in each cell.

In bitmap mode, every pixel (or pixel pair, in multicolor) is individually addressable.
The VIC-II reads pixel data directly from an 8000-byte bitmap in RAM. The programmer has
full control over every dot on screen. However, color information is still organized on
an 8x8 cell grid, which imposes the characteristic "color cell" restriction: you can
only use a limited number of colors within each 8x8 pixel block.

The bitmap modes are selected by three control bits spread across two VIC-II registers:

| Bit | Register | Name | Purpose |
|-----|----------|------|---------|
| 5   | $D011    | BMM  | Bitmap Mode (0=character, 1=bitmap) |
| 4   | $D016    | MCM  | Multicolor Mode (0=hires, 1=multicolor) |
| 6   | $D011    | ECM  | Extended Color Mode (must be 0 for valid bitmap modes) |

The five valid VIC-II graphics modes:

| ECM | BMM | MCM | Mode |
|-----|-----|-----|------|
| 0   | 0   | 0   | Standard Character Mode |
| 0   | 0   | 1   | Multicolor Character Mode |
| 0   | 1   | 0   | Standard Bitmap Mode (Hires) |
| 0   | 1   | 1   | Multicolor Bitmap Mode |
| 1   | 0   | 0   | Extended Background Color Mode |


## 2. Standard Bitmap Mode (Hires)

### Enabling the Mode

Set bit 5 (BMM) of $D011 and clear bit 4 (MCM) of $D016. Bit 6 (ECM) of $D011 must
also be clear.

```
    ; Enable standard bitmap mode
    lda $d011
    ora #$20        ; set bit 5 (BMM=1)
    and #$bf        ; clear bit 6 (ECM=0)
    sta $d011

    lda $d016
    and #$ef        ; clear bit 4 (MCM=0)
    sta $d016
```

A common shorthand for the full $D011 value is $3B: this sets BMM=1, display enable,
25-row mode, and YSCROLL=3.

### Resolution and Color

- **320 x 200 pixels**, each pixel mapped to a single bit in the bitmap
- **2 colors per 8x8 cell**: one foreground, one background
- All 16 VIC-II colors are available, but each 8x8 block can only contain 2 of them
- Foreground color = high nybble of the corresponding screen RAM byte
- Background color = low nybble of the corresponding screen RAM byte

In bitmap data, a **1-bit** selects the foreground color and a **0-bit** selects the
background color for that cell.

### Memory Layout

The bitmap requires 8000 bytes (40 columns x 25 rows x 8 bytes per cell). Additionally,
1000 bytes of screen RAM provide color data (one byte per 8x8 cell).

The bitmap is NOT organized as linear scanlines. Instead, it is organized in a
**cell-interleaved** pattern that mirrors the character generator layout. The 8000 bytes
are arranged as 1000 groups of 8 consecutive bytes, where each group of 8 defines one
8x8 cell (rows 0-7 of that cell, top to bottom):

```
    Byte 0:    cell (0,0) row 0    (top-left cell, top pixel row)
    Byte 1:    cell (0,0) row 1
    Byte 2:    cell (0,0) row 2
    ...
    Byte 7:    cell (0,0) row 7
    Byte 8:    cell (1,0) row 0    (second cell in top row, top pixel row)
    Byte 9:    cell (1,0) row 1
    ...
    Byte 15:   cell (1,0) row 7
    Byte 16:   cell (2,0) row 0    (third cell in top row)
    ...
    Byte 319:  cell (39,0) row 7   (last cell in top row, bottom pixel row)
    Byte 320:  cell (0,1) row 0    (first cell in second character row)
    ...
    Byte 7999: cell (39,24) row 7  (last cell, bottom pixel row)
```

The first 320 bytes cover the top character row (40 cells x 8 bytes). The next 320
bytes cover the second character row, and so on for all 25 rows.

### Pixel Address Calculation

Given pixel coordinates (X, Y) where X = 0..319, Y = 0..199:

```
    CharColumn = X / 8          (integer division, 0..39)
    CharRow    = Y / 8          (integer division, 0..24)
    CellLine   = Y AND 7        (row within the 8x8 cell, 0..7)

    ByteOffset = CharRow * 320 + CharColumn * 8 + CellLine
    ByteAddress = BitmapBase + ByteOffset

    BitPosition = 7 - (X AND 7)     (bit 7 = leftmost pixel)
```

The classic formula from the Programmer's Reference Guide:

```
    BYTE = BASE + INT(Y/8)*320 + INT(X/8)*8 + (Y AND 7)
    BIT  = 7 - (X AND 7)
```

To set a pixel:
```
    POKE BYTE, PEEK(BYTE) OR (2 ^ BIT)
```

To clear a pixel:
```
    POKE BYTE, PEEK(BYTE) AND (255 - 2 ^ BIT)
```

### Screen RAM Color Mapping

Each byte of screen RAM (1000 bytes, one per cell) encodes two colors:

```
    Bits 7-4 (high nybble) = foreground color (used where bitmap bit = 1)
    Bits 3-0 (low nybble)  = background color (used where bitmap bit = 0)
```

Screen RAM for cell at column C, row R:

```
    ColorAddress = ScreenBase + R * 40 + C
```


## 3. Multicolor Bitmap Mode

### Enabling the Mode

Set bit 5 (BMM) of $D011 AND bit 4 (MCM) of $D016. Bit 6 (ECM) must be clear.

```
    ; Enable multicolor bitmap mode
    lda $d011
    ora #$20        ; set bit 5 (BMM=1)
    and #$bf        ; clear bit 6 (ECM=0)
    sta $d011

    lda $d016
    ora #$10        ; set bit 4 (MCM=1)
    sta $d016
```

A common shorthand: $D011=$3B, $D016=$18.

### Resolution and Color

- **160 x 200 pixels** -- each pixel is two hires pixels wide (double-width)
- **4 colors per 8x8 cell** (technically per 4x8 multicolor cell)
- All 16 VIC-II colors available across the screen
- Three color sources are per-cell; one is global

The horizontal resolution is halved because each pixel is now encoded by 2 bits instead
of 1. The bitmap still occupies the same 8000 bytes. Within each byte, bits are read in
pairs from left to right.

### Bit Pair Encoding

Each pair of bits in the bitmap data selects one of four color sources:

| Bit Pair | Color Source |
|----------|-------------|
| %00      | Background color register $D021 (global, shared by all cells) |
| %01      | High nybble (bits 4-7) of screen RAM byte for this cell |
| %10      | Low nybble (bits 0-3) of screen RAM byte for this cell |
| %11      | Low nybble (bits 0-3) of Color RAM byte for this cell ($D800+) |

So for each 4x8 pixel cell, you get:
- 1 color from $D021 (background, same across entire screen)
- 2 colors from screen RAM (the same byte provides both, split by nybble)
- 1 color from Color RAM

This gives 4 colors per cell, with one constrained to be the same everywhere.

### Pixel Address Calculation

For multicolor pixel coordinates (X, Y) where X = 0..159, Y = 0..199:

```
    CharColumn = X / 4          (integer division, 0..39)
    CharRow    = Y / 8          (integer division, 0..24)
    CellLine   = Y AND 7        (row within the cell, 0..7)

    ByteOffset = CharRow * 320 + CharColumn * 8 + CellLine
    ByteAddress = BitmapBase + ByteOffset
```

Within the byte, the 4 pixel pairs are at bit positions:
```
    Pixel 0 (leftmost):  bits 7-6
    Pixel 1:             bits 5-4
    Pixel 2:             bits 3-2
    Pixel 3 (rightmost): bits 1-0
```

To extract the color index for multicolor pixel P (0..3) within a byte:
```
    shift = (3 - P) * 2
    colorIndex = (byteValue >> shift) AND 3
```


## 4. Advanced Bitmap Techniques

### FLI (Flexible Line Interpretation)

**Invented:** July 1989, by the C64 demoscene.

FLI overcomes the fundamental 8x8 color cell restriction of bitmap mode. Normally, each
8x8 cell gets one screen RAM byte defining its colors. This means the same 2 (hires) or
4 (multicolor) colors must serve all 8 rows within a cell.

FLI changes the screen RAM pointer on every single raster line, so each pixel row within
a cell can reference a different color table. This shrinks the effective color cell from
8x8 to **8x1** (hires) or **4x1** (multicolor) vertically.

#### How It Works

The technique exploits VIC-II "badlines." Normally, a badline occurs on the first
raster line of each 8-pixel character row. During a badline, the VIC-II halts the CPU
for up to 40 cycles to read 40 bytes of screen RAM (the "c-access") and cache them for
the next 8 raster lines.

The key insight: a badline is triggered when the lowest 3 bits of the current raster
line match the YSCROLL value in $D011 bits 0-2. By changing YSCROLL every line, you can
force a badline on every single raster line. Each forced badline causes the VIC-II to
re-read screen RAM, picking up new color data.

Simultaneously, you change the screen RAM pointer in $D018 each line to point to one of
up to 8 different screen RAM tables. This gives each raster line its own unique set of
color attributes.

The core FLI loop, executed once per raster line:

```
    ; Typical FLI loop (runs 7 times for lines 1-7 of each char row)
    ; Must be cycle-exact -- the STA $D018 must occur before cycle 14
    lda d018_table,y    ; 4 cycles -- load new screen pointer
    sta $d018           ; 4 cycles -- switch screen RAM pointer
    lda d011_table,y    ; 4 cycles -- load new YSCROLL
    sta $d011           ; 4 cycles -- force badline by matching YSCROLL
    ; ...VIC-II now steals ~40 cycles for the badline...
```

A typical PAL FLI timing budget is about 23 free CPU cycles per line (out of 63 total),
since the VIC-II steals 40 cycles during each forced badline.

**Memory requirements:** FLI needs 8 separate screen RAM tables (8 x 1024 = 8192 bytes)
plus the 8000-byte bitmap, requiring most of a 16KB VIC bank.

#### The FLI Bug

When a badline is forced, the VIC-II first waits up to 3 cycles for the CPU to finish
its current instruction before seizing the bus. During these 3 cycles, the VIC-II
continues to render pixels but cannot yet read valid color data -- it reads garbage
($FF, which decodes to light grey on light grey).

This produces a characteristic **24-pixel wide grey block** on the left side of the
screen (3 character positions x 8 pixels each). This artifact is called the "FLI bug"
and appears on every raster line where a badline is forced.

Common workarounds:
- Accept it as part of the aesthetic (reducing usable width to 296 pixels)
- Cover it with sprites (as done in AFLI and NUFLI)
- Use the leftmost sprite column as a solid color border

#### Color Resolution Improvement

Standard bitmap: 2 colors per 8x8 cell = same 2 colors for all 8 rows.
FLI bitmap: 2 colors per 8x1 strip = different color pair on every row.

This dramatically increases the effective color count. A single 8x8 block can now
display up to 16 different color pairs (2 per row x 8 rows), allowing much more
detailed color gradients and reducing the visible "attribute clash" artifacts.

### IFLI (Interlaced FLI)

**Invented:** ~1991.

IFLI combines two FLI images and alternates between them every frame (every 1/50th of a
second on PAL, 1/60th on NTSC). Additionally, one image is shifted horizontally by one
hires pixel using the hardware scroll register ($D016 bits 0-2).

The visual effect: the human eye blends the two alternating frames, perceiving a merged
image with effectively doubled color resolution. Where one frame shows color A and the
other shows color B at the same position, the eye sees an intermediate blended color.
The 1-pixel horizontal shift also creates the illusion of half-pixel detail.

**Downsides:**
- Visible flicker, especially on modern displays (CRTs blended more naturally)
- Requires double the memory (two full FLI images)
- The CPU is almost entirely consumed maintaining the display
- The horizontal shift can create a "shimmering" effect on vertical edges

### AFLI (Advanced FLI)

**Invented:** April 1990.

AFLI addresses the FLI bug by using sprites to cover the 3-character grey artifact on
the left side. Typically one sprite covers the FLI bug area, and the remaining sprites
can provide additional color data or simply mask the artifact with a chosen solid color.

In AFLI, the 3 leftmost character positions are always rendered as light grey (the FLI
bug), so sprites are positioned there to display the intended image content instead.

### UFLI (Underlayed FLI)

**Invented:** April 1996.

UFLI is based on Hires FLI combined with a low-priority sprite layer underneath. It uses
7 sprites: one to cover the FLI bug, and 6 X-expanded sprites covering 288 pixels of
screen width. The sprites act as an "underlay" -- where the bitmap has the background
color ($D021), the sprite color shows through instead, providing additional color options.

Due to timing constraints from the heavy sprite usage, UFLI can only perform FLI on
every 2nd raster line. The colorfulness lost from this limitation is partially
compensated by the extra colors from the sprite layer.

### MUFLI (Multicolor Underlayed FLI)

**Invented:** July 2006.

MUFLI extends UFLI by adding sprite color splits to the timing code. Sprite colors are
changed at precise raster positions, allowing even more color variation across the
display. MUIFLI is a variant that adds interlacing to MUFLI.

### NUFLI (New Underlayed FLI)

**Invented:** July 2009, by the Crest demoscene group. First showcased in the "Crest
Slide Story" slideshow in 2010.

NUFLI achieves near-photographic quality on the C64 without flickering. It is based on
AFLI but combines FLI with sprites in a more sophisticated way:

- **Resolution:** Full 320x200 pixels
- **Color density:** ~3 colors per 8x2 pixel area (vs. 2 per 8x8 in standard hires)
- **Sprite layer:** 6 double-width hires sprites cover columns 4-39 (288 pixels)
- **Sprite crunching:** Exploits a VIC-II bug where toggling Y-expansion at precise
  moments causes the sprite line counter to misalign, allowing sprite data to be
  displayed repeatedly with different data pointers
- **FLI on alternating lines:** FLI triggers on every 2nd scanline to balance CPU
  availability with sprite multiplexing
- **Sprite recoloring:** Sprite colors are changed on nearly every scanline

The FLI bug area (left 24 pixels) uses a multicolor sprite for up to 3 additional
colors at half resolution. The rightmost 8 pixels are bitmap-only.

Memory footprint: unpacked NUFLI images occupy RAM from $2000 to $7A00.

### NUFLIX

NUFLIX is a modern refinement of NUFLI (2024) that further improves color quality:

- Pre-generates optimized machine code instead of interpreting tables at runtime
- Allows per-scanline sprite color changes (vs. NUFLI's 2-scanline blocks)
- Uses GPU-accelerated conversion that tests 256 sprite color combinations per block
- Achieves 10-12 distinct colors per scanline in optimal conditions

### Dithering Techniques for Bitmap Images

Dithering is essential for creating the illusion of more colors within the C64's
palette and cell restrictions:

- **Ordered dithering (Bayer matrix):** Uses a predefined pattern (2x2, 4x4, or 8x8)
  to threshold pixels. Produces a regular, grid-like pattern. Well-suited for the C64
  because the pattern aligns with the 8x8 cell structure. Bayer 4x4 is a common default.

- **Floyd-Steinberg error diffusion:** Distributes the quantization error from each
  pixel to its neighbors. Produces smoother gradients but can create "wormy" artifacts.
  Must be applied with awareness of cell color constraints -- error diffusion across
  cell boundaries can cause problems when the available colors change.

- **Pattern dithering:** Pre-defined 8x8 fill patterns mixing two colors. Simple to
  implement and predictable. Common in early C64 paint programs.

- **Cell-aware dithering:** Modified algorithms that constrain error diffusion within
  8x8 cell boundaries, or that select cell colors first and then dither within each
  cell. This avoids the common artifact where error diffusion "wants" a color that is
  not available in the adjacent cell.


## 5. Converting Images to C64 Format

### Color Reduction

The C64 has a fixed palette of exactly 16 colors. Converting a photographic image
requires reducing potentially millions of colors down to these 16, subject to the
additional constraint that only 2-4 of them can appear in any given 8x8 cell.

The conversion process generally involves:

1. **Map source colors to the nearest C64 palette entries** (using perceptual color
   distance metrics -- simple Euclidean RGB distance often gives poor results; CIE
   Lab or weighted RGB formulas work better)
2. **Analyze each 8x8 cell** to determine which 2 (hires) or 4 (multicolor) palette
   colors best represent that cell
3. **Apply dithering** within each cell using only the selected colors
4. **Iterate and optimize** -- some tools use simulated annealing or other optimization
   to find the best global color assignment

### The C64 Color Palette

The C64 generates colors in the analog domain via YUV encoding. There is no single
"correct" RGB palette -- different VIC-II chip revisions, video standards (PAL vs NTSC),
and monitor types produce different results. The Pepto/Colodore palette (by Philip
"Pepto" Timmermann, 2017 revision) is widely considered the most accurate digital
approximation.

**Colodore palette (Pepto, 2017):**

| Index | Name        | Hex       | RGB              |
|-------|-------------|-----------|------------------|
| 0     | Black       | `#000000` | (0, 0, 0)        |
| 1     | White       | `#ffffff` | (255, 255, 255)  |
| 2     | Red         | `#813338` | (129, 51, 56)    |
| 3     | Cyan        | `#75cec8` | (117, 206, 200)  |
| 4     | Purple      | `#8e3c97` | (142, 60, 151)   |
| 5     | Green       | `#56ac4d` | (86, 172, 77)    |
| 6     | Blue        | `#2e2c9b` | (46, 44, 155)    |
| 7     | Yellow      | `#edf171` | (237, 241, 113)  |
| 8     | Orange      | `#8e5029` | (142, 80, 41)    |
| 9     | Brown       | `#553800` | (85, 56, 0)      |
| 10    | Light Red   | `#c46c71` | (196, 108, 113)  |
| 11    | Dark Grey   | `#4a4a4a` | (74, 74, 74)     |
| 12    | Medium Grey | `#7b7b7b` | (123, 123, 123)  |
| 13    | Light Green | `#a9ff9f` | (169, 255, 159)  |
| 14    | Light Blue  | `#706deb` | (112, 109, 235)  |
| 15    | Light Grey  | `#b2b2b2` | (178, 178, 178)  |

Note: the palette is NOT ordered by luminance. The five grey/white shades form a
luminance ramp: Black(0) < Dark Grey(11) < Medium Grey(12) < Light Grey(15) < White(1).
The colors are organized with four grey levels (0,11,12,15,1) and distinct hues that
fall into luminance groups.

Colodore is not a static table but an algorithm that models the VIC-II's YUV color
generation. The values above are for default contrast/saturation settings matching a
Commodore 1084S monitor. Adjustable at https://www.colodore.com.

### Dealing with Color Cell Restrictions

The cell restriction is the central challenge of C64 image conversion:

- **Hires mode:** Only 2 colors per 8x8 cell. High-contrast photographic images
  inevitably show "attribute clash" (blocky color artifacts at cell boundaries).
- **Multicolor mode:** 4 colors per cell (one global), but half the horizontal
  resolution. Better for colorful images, but details are softer.

Strategies:
- **Pre-processing:** Adjust contrast, blur details that will cause cell conflicts
- **Smart color selection:** Choose cell colors that minimize total error across all
  8 pixels rows, not just the majority color
- **Cross-cell optimization:** When a cell boundary falls in a gradient, choose colors
  on both sides that make the transition smooth
- **Manual touch-up:** Automated conversion rarely matches hand-pixeled art quality.
  Most serious C64 artists hand-edit converted images.

### Dithering Algorithms Suited for C64

- **Bayer ordered dither** -- regular pattern, aligns well with 8x8 cells
- **Floyd-Steinberg** -- smooth but must be cell-constrained
- **Atkinson dithering** -- diffuses less error than Floyd-Steinberg, produces lighter
  images with more open areas. Good for the limited C64 palette.
- **Clustered dot dithering** -- simulates halftone printing, can look appropriate at
  C64 resolution

### Common Converter Tools

- **RetroPixels** -- Cross-platform command-line converter by Michel de Bree. Supports
  hires, multicolor, FLI. Multiple dithering modes (Bayer 2x2, 4x4, 8x8).
- **Pixcen** -- Windows GUI editor/converter by John "CRT" Hammarberg. Full-featured
  pixel editor with conversion capabilities.
- **Multipaint** -- Cross-platform (Windows/Linux/Mac) paint program designed for
  retro platforms including C64. Enforces color cell restrictions in real time.
- **NUFLIX Studio** -- NUFLI/NUFLIX converter by Cobbpg. GPU-accelerated optimization
  for near-photographic conversions.
- **IMG2C64MC** -- Web-based multicolor converter with multiple dithering algorithms
  and palette options.
- **Timanthes** -- Classic C64 graphics editor supporting multiple formats.
- **Project One** -- C64 graphics editor with import capabilities.
- **GIMP with C64 plugins** -- Various GIMP scripts exist for exporting to C64 formats.


## 6. Hardcore Details

### Bitmap Data Memory Layout Byte-by-Byte

The bitmap occupies 8000 bytes. Consider the screen as a 40x25 grid of 8x8 cells,
numbered left-to-right, top-to-bottom:

```
    Cell(col, row) where col = 0..39, row = 0..24
    Cell number = row * 40 + col  (0..999)

    Each cell = 8 consecutive bytes in the bitmap:
        Byte 0: pixel row 0 of the cell (topmost)
        Byte 1: pixel row 1
        ...
        Byte 7: pixel row 7 (bottommost)

    Byte offset of cell(col, row), pixel row R:
        offset = (row * 40 + col) * 8 + R
             = row * 320 + col * 8 + R
```

Visual representation of the first 640 bytes (first 2 character rows):

```
    Offset  Cell    Row   Screen position
    ------  ------  ---   ---------------
    0       (0,0)   0     Pixels 0-7 of screen line 0
    1       (0,0)   1     Pixels 0-7 of screen line 1
    2       (0,0)   2     Pixels 0-7 of screen line 2
    ...
    7       (0,0)   7     Pixels 0-7 of screen line 7
    8       (1,0)   0     Pixels 8-15 of screen line 0
    9       (1,0)   1     Pixels 8-15 of screen line 1
    ...
    15      (1,0)   7     Pixels 8-15 of screen line 7
    16      (2,0)   0     Pixels 16-23 of screen line 0
    ...
    312     (39,0)  0     Pixels 312-319 of screen line 0
    313     (39,0)  1     Pixels 312-319 of screen line 1
    ...
    319     (39,0)  7     Pixels 312-319 of screen line 7
    320     (0,1)   0     Pixels 0-7 of screen line 8
    ...
```

This means that consecutive bytes in memory do NOT represent consecutive pixels on a
scanline. To traverse a single scanline left to right, you must skip 8 bytes between
cells: bytes 0, 8, 16, 24, ..., 312 form scanline 0.

### Relationship Between Bitmap Address, Screen RAM, and VIC Bank

The VIC-II sees a 16KB window of memory, selected by the CIA-2 register $DD00:

```
    $DD00 bits 1-0    VIC Bank    Address Range
    ──────────────    ────────    ─────────────
    %11               Bank 0      $0000-$3FFF
    %10               Bank 1      $4000-$7FFF
    %01               Bank 2      $8000-$BFFF
    %00               Bank 3      $C000-$FFFF
```

Note: the bits are **inverted** -- %11 selects the lowest bank, %00 the highest.

Within the selected 16KB bank, the bitmap and screen RAM locations are set by $D018:

```
    $D018 bit 3 (CB13):   Bitmap base address
        0 = bitmap at bank + $0000
        1 = bitmap at bank + $2000

    $D018 bits 7-4 (VM13-VM10):  Screen RAM base address
        Value (0-15) * $0400 = offset within bank
        Example: $D018 = $38 -> screen at bank + $0C00, bitmap at bank + $2000
```

**Common configurations:**

| VIC Bank | $DD00 | Bitmap     | Screen RAM  | $D018 |
|----------|-------|------------|-------------|-------|
| 0        | $03   | $0000      | $0400       | $18   |
| 0        | $03   | $2000      | $0400       | $18   |
| 1        | $02   | $6000      | $4400       | $18   |
| 3        | $00   | $E000      | $C400       | $18   |

A frequently used setup for bitmap graphics:

```
    ; Use VIC bank 1 ($4000-$7FFF)
    lda $dd00
    and #$fc
    ora #$02        ; select bank 1
    sta $dd00

    ; Bitmap at $6000, screen RAM at $4000
    lda #$80        ; bits 7-4 = %1000 -> screen at $4000+$0000=$4000
                    ; bit 3 = 0 -> bitmap at $4000+$0000=$4000 ... WRONG
    ; Actually: bitmap at $6000, screen at $4400:
    lda #$18        ; bits 7-4 = %0001 -> screen at $4000+$0400=$4400
                    ; bit 3 = 1 -> bitmap at $4000+$2000=$6000
    sta $d018
```

**Important:** In VIC banks 0 and 2, addresses $1000-$1FFF and $9000-$9FFF map to
character ROM instead of RAM. If you place bitmap or screen data at those offsets, the
VIC-II will read character ROM instead. Banks 1 and 3 ($4000 and $C000) do not have
this issue.

### How $D018 Works in Bitmap Mode

In character mode, $D018 bits 1-3 select the character generator base address and bits
4-7 select screen RAM. In bitmap mode, the interpretation changes:

- **Bits 4-7 (VM13-VM10):** Still select screen RAM base. This is where color data is
  read from. The value (0-15) is multiplied by $0400 to get the offset within the VIC
  bank.

- **Bit 3 (CB13):** Selects the bitmap base address. In character mode, bits 1-3
  together select the character generator. In bitmap mode, only bit 3 matters -- it
  selects between the lower 8KB ($0000) or upper 8KB ($2000) within the VIC bank.
  Bits 1-2 are ignored.

The VIC-II internally constructs addresses using these pointer bits:

**G-access (graphics/bitmap data):**
```
    Bit 13:      CB13 (from $D018 bit 3)
    Bits 12-3:   VC9-VC0 (video counter, 0-999, selects cell)
    Bits 2-0:    RC2-RC0 (row counter, 0-7, selects row within cell)
```

**C-access (color/screen RAM):**
```
    Bits 13-10:  VM13-VM10 (from $D018 bits 7-4)
    Bits 9-0:    VC9-VC0 (video counter, selects cell)
```

### Why the Memory Layout Is "Weird"

The cell-based bitmap layout is a direct consequence of VIC-II hardware design:

1. **The VIC-II was designed primarily for character mode.** Its internal address
   generation circuit uses a Video Counter (VC, 10 bits, 0-999) to step through 1000
   character cells, and a Row Counter (RC, 3 bits, 0-7) to step through the 8 rows
   of each character.

2. **Bitmap mode reuses the same address generation circuit.** Instead of reading
   character ROM at address (CharBase + CharCode*8 + RC), it reads bitmap data at
   address (BitmapBase + VC*8 + RC). The same VC and RC counters are used; the only
   difference is that VC directly addresses the bitmap instead of indirectly through
   character codes.

3. **This was an engineering optimization.** Adding a true linear framebuffer would
   have required a completely separate address generation circuit and more silicon. By
   reusing the character mode circuitry, MOS Technology added bitmap capability with
   minimal additional transistors.

4. **Memory bandwidth is the fundamental constraint.** At 1 MHz, the VIC-II cannot
   fetch both pixel data and per-pixel color data for every pixel. The cell-based
   scheme allows color data to be fetched once per cell (during badlines) and reused
   for 8 rows, dramatically reducing the required bandwidth. This is also why color
   is restricted to cell granularity.

### FLI Technique Exact Timing Requirements

**PAL system (6569 VIC-II):** 63 cycles per raster line, 312 lines per frame.
**NTSC system (6567 VIC-II):** 65 cycles per raster line, 263 lines per frame.

FLI timing on PAL:

```
    Cycle  Event
    ─────  ─────
    1-11   Sprite pointer fetches, DRAM refresh (VIC-II internal)
    12     VIC-II checks for badline condition
             (if rasterline[2:0] == YSCROLL[2:0], it's a badline)
    12-14  VIC-II pulls BA low, waits up to 3 cycles for CPU to finish
    15-54  40 stolen cycles: VIC-II fetches 40 c-accesses (screen RAM)
             interleaved with g-accesses (bitmap data)
    55-63  CPU resumes execution (9 free cycles at end of line)
```

The FLI loop must write to $D018 (to change the screen RAM pointer) and $D011 (to
change YSCROLL and force the next badline) within the available CPU cycles. The
critical write to $D018 must happen before cycle 14 of the next line, which is when
the VIC-II performs the badline check.

A typical FLI loop uses about 23 cycles per iteration:

```
    ; 23-cycle FLI loop (PAL), runs once per raster line
    lda d018tab,y       ; 4 -- new screen RAM pointer
    sta $d018           ; 4 -- must land before cycle 14 of NEXT line
    lda d011tab,y       ; 4 -- new YSCROLL value
    sta $d011           ; 4 -- forces badline when value matches line
    dey                 ; 2
    bne loop            ; 3 (taken) / 2 (not taken)
                        ; = 21 cycles + branch overhead
```

After the STA $D011 triggers a badline, the VIC-II steals ~40 cycles, leaving the
remaining free cycles for the next iteration.

**The 3-character FLI bug occurs because:**
When the VIC-II detects a badline (cycle 12) and begins stealing cycles, it needs 3
cycles (12, 13, 14) to assert BA and take over the bus. During these 3 cycles, the
VIC-II is already trying to render pixels but has not yet fetched valid screen RAM
data for the new line. It reads indeterminate values from the data bus (effectively
$FF), which decode as color 15 (light grey) for both foreground and background. This
produces the characteristic 24-pixel (3 characters) grey block on the left edge.

### How NUFLI Achieves Near-Photographic Quality

NUFLI combines multiple layers of visual information:

1. **Hires bitmap layer (320x200):** Provides full-resolution pixel data with per-line
   color attributes via FLI (on alternating lines)

2. **Sprite underlay (288 pixels wide):** 6 X-expanded hires sprites beneath the
   bitmap. Where the bitmap shows the background color, the sprite color shows through.
   This adds a third color option per 8-pixel region without consuming bitmap bits.

3. **Sprite crunching:** A VIC-II exploit where toggling sprite Y-expansion at precise
   moments corrupts the sprite line counter. This allows the same sprites to display
   different data pointers repeatedly across the full screen height, effectively
   multiplexing sprite data without the usual 21-line sprite height limit.

4. **Per-line sprite recoloring:** Sprite colors are changed via register writes on
   nearly every raster line, allowing the underlay color to vary continuously.

5. **FLI bug exploitation:** The left 24-pixel area uses a multicolor sprite for up to
   3 additional colors, turning the FLI bug from a liability into a feature.

6. **Optimized scheduling:** The converter software analyzes the image and determines
   the optimal color assignments and register write schedule to minimize visual error
   within the tight CPU timing budget.

The result: approximately 3 freely choosable colors per 8x2 pixel area across most of
the screen, compared to 2 per 8x8 in standard hires. Combined with dithering, this
yields images with an effective perceived color depth far exceeding the 16-color palette.

### Bitmap + ECM: Invalid Mode Effects

Setting ECM=1 together with BMM=1 produces an invalid mode:

**ECM + BMM (mode 6, ECM=1/BMM=1/MCM=0):**
- The screen appears **black** (border color still shows)
- Internally, the VIC-II generates graphics similar to standard bitmap mode, but the
  ECM bit forces g-address bits 9 and 10 to zero
- This means only 1/4 of the bitmap data is used, repeated 4 times across the display
- Sprite-to-background collision detection still works based on this garbled data
- Useful only for niche tricks (e.g., detecting specific pixel patterns for collisions
  without displaying them)

**ECM + BMM + MCM (mode 7, ECM=1/BMM=1/MCM=1):**
- Also displays a **black screen**
- Generates internal graphics similar to multicolor mode but with the same address
  masking as mode 6
- Collision detection remains functional

These "invalid" modes were never documented by Commodore. They exist because the three
control bits produce 8 combinations but only 5 have useful visual output. The VIC-II
does not trap or reject invalid combinations -- it simply applies all the enabled mode
modifications simultaneously, which produces conflicting address calculations that
result in black output.

### Using Bitmap Mode for Smooth Scrolling

Smooth scrolling in bitmap mode is significantly more challenging than in character mode:

**The Problem:**

In character mode, the VIC-II has hardware scroll registers ($D016 bits 0-2 for
horizontal, $D011 bits 0-2 for vertical) that shift the display by 0-7 pixels. After
scrolling 8 pixels, you update the 1000-byte screen RAM to shift character codes and
reset the scroll register. Moving 1000 bytes is fast.

In bitmap mode, scrolling the display by 8 pixels requires moving the entire 8000-byte
bitmap buffer PLUS the 1000-byte screen RAM (color data). That is 9000 bytes that must
be relocated every 8 pixels of movement. At 1 MHz, moving 9000 bytes takes a
substantial fraction of a frame.

**Specific Challenges:**

- **Raw bandwidth:** 9000 bytes per scroll step. Even an optimized unrolled copy loop
  takes ~36,000 cycles (9000 bytes x ~4 cycles per byte). A PAL frame has only ~19,656
  cycles total. Scrolling cannot complete in a single frame.

- **Color RAM ($D800-$DBE7):** Color RAM is I/O-mapped and cannot be double-buffered by
  switching pointers. It must be physically written, and those writes are visible
  immediately. If the VIC-II reads Color RAM while you are updating it, tearing occurs.

- **No pointer tricks:** Unlike screen RAM, the bitmap base can only be at two positions
  within a VIC bank (offset $0000 or $2000). You cannot smoothly alternate between
  multiple bitmap buffers using $D018 alone.

**Solutions:**

- **Double buffering (partial):** Maintain two screen RAM buffers and switch between
  them via $D018. The bitmap can also be double-buffered if you use both halves of a
  VIC bank (0 and $2000), switching bit 3 of $D018. However, this limits you to one
  VIC bank configuration.

- **Distributed updates:** Spread the 9000-byte copy across multiple frames. Shift
  1/4 of the bitmap per frame, completing a full scroll step every 4 frames (still
  looks smooth with the hardware scroll register providing sub-character movement).

- **Raster chasing ("racing the beam"):** Update Color RAM immediately behind the
  raster beam. Once the VIC-II has finished reading a line's color data, you can safely
  update that line's Color RAM entries. This requires precise raster interrupt timing.

- **AGSP (Any Given Screen Position):** A technique that changes the VIC-II display
  mode or pointers at specific raster positions, allowing different parts of the screen
  to show different data.

- **Pseudo-bitmap with custom characters:** Instead of true bitmap mode, define a custom
  character set where each cell has a unique character. This gives pixel-level control
  while keeping the character-mode scrolling advantages. Scrolling only requires updating
  screen RAM (1000 bytes) and potentially redefining character shapes (2048 bytes), but
  the character data can be pointer-switched. Many C64 games use this approach.


## References

### Primary Technical References

- Christian Bauer, "The MOS 6567/6569 video controller (VIC-II) and its application
  in the Commodore 64" -- The definitive VIC-II technical reference
  https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt

- C64 Wiki, "Standard Bitmap Mode"
  https://www.c64-wiki.com/wiki/Standard_Bitmap_Mode

- C64 Wiki, "Multicolor Bitmap Mode"
  https://www.c64-wiki.com/wiki/Multicolor_Bitmap_Mode

- C64 Wiki, "Graphics Modes" -- Overview of all VIC-II modes including invalid modes
  https://www.c64-wiki.com/wiki/Graphics_Modes

- C64 Wiki, "Register 53272 ($D018)"
  https://www.c64-wiki.com/wiki/53272

- C64 Wiki, "VIC bank"
  https://www.c64-wiki.com/wiki/VIC_bank

### Tutorials and Explanations

- Dustlayer, "VIC-II for Beginners Part 4 - Screen Modes"
  https://dustlayer.com/vic-ii/2013/4/26/vic-ii-for-beginners-screen-modes-cheaper-by-the-dozen

- Bumbershoot Software, "Building a Faster C64 Bitmap Library"
  https://bumbershootsoft.wordpress.com/2020/11/09/building-a-faster-c64-bitmap-library/

- Bumbershoot Software, "FLI, Part 1: 16 Color Mode"
  https://bumbershootsoft.wordpress.com/2016/03/12/fli-part-1-16-color-mode/

- C64 OS, "VIC-II and FLI Timing (Parts 1-2)"
  https://c64os.com/post/flitiming1
  https://c64os.com/post/flitiming2

- Codebase64, "Built-in Screen Modes"
  https://codebase64.c64.org/doku.php?id=base:built_in_screen_modes

- Codebase64, "UFLI"
  http://codebase.c64.org/doku.php?id=base:ufli

- STA C64, "Commodore 64 Display Modes"
  https://sta.c64.org/cbm64disp.html

### Advanced Techniques

- C64 Wiki, "NUFLI"
  https://www.c64-wiki.com/wiki/NUFLI

- Cobbpg, "Pushing the Boundaries of C64 Graphics with NUFLIX"
  https://cobbpg.github.io/articles/nuflix.html

- 1AM Studios, "How to Implement Smooth Full-Screen Scrolling on C64"
  http://1amstudios.com/2014/12/07/c64-smooth-scrolling/

### Color Palette

- Pepto, "Calculating the Color Palette of the VIC II"
  https://www.pepto.de/projects/colorvic/

- C64 Wiki, "Color"
  https://www.c64-wiki.com/wiki/Color

- Lospec, "Colodore Palette"
  https://lospec.com/palette-list/colodore

### Tools

- RetroPixels -- https://github.com/micheldebree/retropixels
- Multipaint -- https://multipaint.kameli.net
- Pixcen / SpritePad -- https://sites.google.com/view/side-project-c64-tools/pixcen-and-spritepad
- NUFLIX Studio -- https://cobbpg.github.io/articles/nuflix.html
