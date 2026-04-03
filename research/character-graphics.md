# Character-Based Graphics on the Commodore 64

Comprehensive technical reference for character-mode display, custom character sets,
PETSCII encoding, and advanced techniques on the C64.


---

## 1. Overview

The Commodore 64's primary display mode is character-based. The VIC-II chip renders a
40x25 grid of characters, where each character occupies an 8x8 pixel block, producing a
full-screen resolution of 320x200 pixels. Character-based graphics are the foundation of
nearly all C64 software -- games, demos, applications, and the BASIC environment.

### Key Characteristics

- **Screen grid:** 40 columns x 25 rows = 1,000 character cells
- **Character size:** 8 pixels wide x 8 pixels tall
- **Pixel resolution:** 320 x 200 (standard) or 160 x 200 (multicolor)
- **Characters per set:** 256 (each defined by 8 bytes = 2,048 bytes per full set)
- **Color RAM:** 1,000 nybbles, one per character cell
- **ROM character sets:** Two built-in sets in Character ROM at $D000-$DFFF

### Why Character Mode Matters

Character mode is extremely memory-efficient. A full screen of graphics requires only
1,000 bytes of screen RAM plus 2,048 bytes of character definitions -- a total of roughly
3 KB, compared to 8 KB for a bitmap screen. This leaves far more RAM for game logic,
level data, and music. Character mode also allows faster screen updates since changing a
single byte in screen RAM replaces an entire 8x8 tile instantly.

Most classic C64 games use redefined character sets to create detailed game worlds.
Titles like Ultima, Boulder Dash, Lode Runner, Paradroid, and many others rely entirely
on character-based graphics combined with hardware scrolling and sprites.


---

## 2. Standard Character Mode

Standard Character Mode (also called "hires text mode") is the default display mode of
the C64. It is active when the ECM, BMM, and MCM bits are all cleared.

### 2.1 Mode Activation

| Register | Bit   | Name | Value | Purpose                        |
|----------|-------|------|-------|--------------------------------|
| $D011    | Bit 6 | ECM  | 0     | Extended Color Mode off        |
| $D011    | Bit 5 | BMM  | 0     | Bitmap Mode off                |
| $D016    | Bit 4 | MCM  | 0     | Multicolor Mode off            |

Default register values: `$D011 = $1B`, `$D016 = $08`.

### 2.2 How It Works

The VIC-II renders each character cell by combining three data sources:

1. **Screen RAM** -- Contains a character code (0-255) for each of the 1,000 cells.
   This code is an index into the current character set. Default location: $0400-$07E7.

2. **Character Generator (ROM or RAM)** -- Contains the pixel data for all 256
   characters. Each character is 8 bytes: one byte per row, MSB is leftmost pixel.
   Default location: Character ROM at $D000 (uppercase/graphics set).

3. **Color RAM** -- Contains a 4-bit color value for each cell, determining the
   foreground color of that character. Fixed at $D800-$DBE7.

For each pixel in a character cell:

- **Bit = 1:** Pixel is drawn in the foreground color (from Color RAM)
- **Bit = 0:** Pixel is drawn in the background color (from register $D021)

### 2.3 Screen RAM

Screen RAM holds 1,000 bytes, one per character cell, in row-major order (left to right,
top to bottom). Each byte is a **screen code** (not a PETSCII code -- see Section 6)
that indexes into the character set.

| Cell Position | Address (default) | Offset |
|---------------|-------------------|--------|
| Row 0, Col 0  | $0400             | +0     |
| Row 0, Col 39 | $0427             | +39    |
| Row 1, Col 0  | $0428             | +40    |
| Row 24, Col 39| $07E7             | +999   |

Screen RAM can be relocated via bits 4-7 of $D018 (see Section 2.5).

### 2.4 Color RAM ($D800-$DBE7)

Color RAM is a dedicated 1Kx4-bit static RAM chip (2114 SRAM) on the C64 motherboard.
It is permanently mapped at $D800-$DBE7 and cannot be relocated.

Key properties:

- **Width:** 4 bits per location (only the lower nybble is meaningful)
- **Upper nybble:** Undefined/random when read; writes to upper bits are ignored
- **Size:** 1,000 nybbles (one per character cell), plus 24 unused bytes at $DBE8-$DBFF
- **Direct wiring:** The VIC-II reads Color RAM via a dedicated 4-bit data bus, separate
  from the main 8-bit data bus. This means Color RAM is always accessible regardless of
  the selected VIC bank.
- **No bank switching:** Color RAM is always at $D800, regardless of which 16KB VIC bank
  is selected via $DD00.

The 4-bit value selects one of the 16 C64 colors as the foreground color for that cell:

| Value | Color        | Value | Color        |
|-------|--------------|-------|--------------|
| 0     | Black        | 8     | Orange       |
| 1     | White        | 9     | Brown        |
| 2     | Red          | 10    | Light Red    |
| 3     | Cyan         | 11    | Dark Grey    |
| 4     | Purple       | 12    | Medium Grey  |
| 5     | Green        | 13    | Light Green  |
| 6     | Blue         | 14    | Light Blue   |
| 7     | Yellow       | 15    | Light Grey   |

### 2.5 The Default Character Sets

The C64 includes two character sets burned into the 4KB Character Generator ROM
at $D000-$DFFF:

| Set | ROM Address     | Contents                                          |
|-----|-----------------|---------------------------------------------------|
| 1   | $D000-$D7FF     | Uppercase letters + PETSCII graphics symbols       |
| 2   | $D800-$DFFF     | Uppercase + lowercase letters                      |

Each set contains 256 characters x 8 bytes = 2,048 bytes. In both sets, characters
128-255 are the reverse (inverted) versions of characters 0-127.

**Switching between sets:**

- **Keyboard:** Press SHIFT + Commodore key to toggle
- **BASIC:** `POKE 53272,21` (set 1) or `POKE 53272,23` (set 2)
- **PRINT:** `PRINT CHR$(142)` (set 1, uppercase/graphics) or
  `PRINT CHR$(14)` (set 2, uppercase/lowercase)

### 2.6 How $D018 Selects Character Set Location

Register $D018 (53272) controls where the VIC-II looks for both screen RAM and character
data. All addresses are relative to the start of the currently selected VIC bank.

**Bit layout:**

```
Bit 7  6  5  4  3  2  1  0
    V  V  V  V  C  C  C  -
```

- **Bits 7-4 (VVVV):** Screen memory offset. Multiply by 1024 to get the offset within
  the VIC bank. Range: 0-15 = offsets $0000-$3C00.
- **Bits 3-1 (CCC):** Character memory offset. Multiply by 2048 to get the offset within
  the VIC bank. Range: 0-7 = offsets $0000-$3800.
- **Bit 0:** Unused.

**Character set locations (within VIC bank):**

| Bits 3-1 | Value in $D018 | Char Set Offset | Example (Bank 0) |
|----------|----------------|-----------------|-------------------|
| %000     | $x0            | $0000           | $0000             |
| %001     | $x2            | $0800           | $0800             |
| %010     | $x4            | $1000           | $1000 (ROM image) |
| %011     | $x6            | $1800           | $1800 (ROM image) |
| %100     | $x8            | $2000           | $2000             |
| %101     | $xA            | $2800           | $2800             |
| %110     | $xC            | $3000           | $3000             |
| %111     | $xE            | $3800           | $3800             |

### 2.7 VIC Bank Selection

The VIC-II can only address 16 KB at a time. Bits 0-1 of CIA-2 Port A ($DD00) select
which 16 KB bank the VIC-II sees. The bit pattern is **inverted** (due to hardware
inverters in the address path):

| $DD00 bits 0-1 | Bank | Address Range   | ROM Charset Visible?           |
|----------------|------|-----------------|--------------------------------|
| %11            | 0    | $0000-$3FFF     | Yes (at offsets $1000, $1800)  |
| %10            | 1    | $4000-$7FFF     | No                             |
| %01            | 2    | $8000-$BFFF     | Yes (at offsets $1000, $1800)  |
| %00            | 3    | $C000-$FFFF     | No                             |

In banks 0 and 2, the VIC-II sees the Character ROM "shadowed" at offsets $1000-$1FFF
(i.e., $1000-$17FF = set 1, $1800-$1FFF = set 2). This means pointing $D018 to those
offsets will use the ROM character sets even though RAM exists at those CPU-visible
addresses.

In banks 1 and 3, there is no ROM shadow. If you point the character generator to offset
$1000 or $1800 in these banks, the VIC-II reads RAM at those addresses, which is useful
for placing custom charsets but means you must supply your own character data.


---

## 3. Custom Character Sets

### 3.1 Creating Custom Character Data

Each character is defined by 8 consecutive bytes. Each byte represents one horizontal row
of 8 pixels, with the MSB (bit 7) being the leftmost pixel and the LSB (bit 0) the
rightmost.

**Example: The letter "A" (screen code 1)**

```
Byte 0:  %00011000  = $18   row 0:    ..##....
Byte 1:  %00111100  = $3C   row 1:    .####...
Byte 2:  %01100110  = $66   row 2:    .##..##.
Byte 3:  %01111110  = $7E   row 3:    .######.
Byte 4:  %01100110  = $66   row 4:    .##..##.
Byte 5:  %01100110  = $66   row 5:    .##..##.
Byte 6:  %01100110  = $66   row 6:    .##..##.
Byte 7:  %00000000  = $00   row 7:    ........
```

A full character set of 256 characters requires 256 x 8 = 2,048 bytes (2 KB).

### 3.2 Memory Alignment Requirements

Character sets must be aligned to **2 KB ($0800) boundaries** within the current VIC
bank. Valid offsets within a VIC bank are:

```
$0000, $0800, $1000, $1800, $2000, $2800, $3000, $3800
```

This gives 8 possible locations per bank (though $1000/$1800 in banks 0 and 2 are
occupied by the ROM character image).

### 3.3 Copying ROM Character Set to RAM

The Character ROM is not directly visible to the CPU at $D000 during normal operation
because the I/O area ($D000-$DFFF) overlays it. To copy it, you must temporarily switch
out the I/O region by modifying the processor port at address $01.

**Assembly language routine:**

```asm
        sei                 ; Disable interrupts (critical!)
        lda #$33            ; %00110011 - HIRAM off, LORAM off
        sta $01             ; CPU now sees Char ROM at $D000-$DFFF
                            ; (I/O area is switched out)

        ldx #$00            ; Copy 2048 bytes ($D000 -> $3000)
loop:   lda $D000,x         ; Read from Character ROM
        sta $3000,x         ; Write to RAM destination
        lda $D100,x
        sta $3100,x
        lda $D200,x
        sta $3200,x
        lda $D300,x
        sta $3300,x
        lda $D400,x
        sta $3400,x
        lda $D500,x
        sta $3500,x
        lda $D600,x
        sta $3600,x
        lda $D700,x
        sta $3700,x
        inx
        bne loop

        lda #$37            ; %00110111 - Restore normal config
        sta $01             ; I/O area visible again
        cli                 ; Re-enable interrupts
```

**BASIC routine (slow but functional):**

```basic
10 POKE 56334,PEEK(56334) AND 254  : REM DISABLE CIA1 TIMER IRQ
20 POKE 1,PEEK(1) AND 251          : REM SWITCH OUT I/O, SHOW CHAR ROM
30 FOR A=0 TO 2047
40 POKE 12288+A,PEEK(53248+A)      : REM COPY $D000 -> $3000
50 NEXT A
60 POKE 1,PEEK(1) OR 4             : REM RESTORE I/O AREA
70 POKE 56334,PEEK(56334) OR 1     : REM RE-ENABLE CIA1 TIMER IRQ
80 POKE 53272,(PEEK(53272) AND 240) OR 12 : REM POINT VIC TO $3000
```

**Important:** Interrupts must be disabled while the I/O area is switched out, because
the CIA interrupt hardware lives at $DC00-$DDFF (within the I/O region). If an interrupt
fires while I/O is switched out, the system will crash.

### 3.4 Partial Character Set Replacement

You do not need to redefine all 256 characters. A common approach is:

1. Copy the ROM character set to RAM (preserving letters and numbers)
2. Overwrite only the characters you want to change (e.g., characters 64-127 for game
   tiles, leaving 0-63 for text)
3. Point the VIC-II to the RAM copy

This gives you custom game tiles while retaining readable text characters.

**Address calculation for a specific character:**

```
character_address = charset_base + (screen_code * 8)
```

For example, to modify screen code 65 (letter "A" in set 1) in a charset at $3000:

```
$3000 + (65 * 8) = $3000 + $0208 = $3208
```

### 3.5 Using Characters as Pseudo-Bitmap Tiles

A powerful technique is to treat the 256 characters not as letters but as 8x8 pixel
tiles. By arranging specific tiles on screen, you build up a detailed graphical image
using only 1 KB of screen data and 2 KB of tile definitions.

**Multi-character tiles (meta-tiles):**

Games commonly group characters into larger logical tiles:

- **2x2 tiles** (16x16 pixels): 4 characters per tile = up to 64 unique tiles
- **4x4 tiles** (32x32 pixels): 16 characters per tile = up to 16 unique tiles

A common memory layout for 2x2 tiles:

```
Characters 0-63:    Top-left corner of tiles 0-63
Characters 64-127:  Top-right corner of tiles 0-63
Characters 128-191: Bottom-left corner of tiles 0-63
Characters 192-255: Bottom-right corner of tiles 0-63
```

This layout allows easy calculation: for tile N, the four screen codes are
`N`, `N+64`, `N+128`, `N+192`.

### 3.6 Character Animation Techniques

Characters in a set can be animated by modifying their pixel data in real-time. Since
all instances of a character on screen share the same definition, changing the 8 bytes
of a character instantly updates every occurrence on screen.

**Waterfall/water animation:**

Define 3-4 frames of a "water" character and cycle through them by overwriting the
character data in the charset:

```asm
; Animate character 64 (water tile) with 4 frames
; frame_ptr points to current animation frame data
animate:
        ldx frame_index
        lda frame_table_lo,x
        sta $fb
        lda frame_table_hi,x
        sta $fc

        ldy #7              ; Copy 8 bytes
aloop:  lda ($fb),y         ; Read frame data
        sta $3200,y         ; Write to char 64 in charset at $3000
        dey                 ; ($3000 + 64*8 = $3200)
        bpl aloop

        inx                 ; Advance frame
        cpx #4              ; 4 frames total
        bcc nowrap
        ldx #0
nowrap: stx frame_index
        rts
```

**Optimization:** Only update the bytes that actually change between frames. If only
3 of 8 rows differ, update only those 3 bytes for faster animation.

**Double-buffered charset animation:**

Prepare two complete character sets in RAM. Animate into the "back" set while the
"front" set is displayed, then swap by changing $D018. This prevents visual tearing
during multi-character updates.


---

## 4. Multicolor Character Mode

### 4.1 How It Works

Multicolor Character Mode trades horizontal resolution for additional colors. Instead
of each bit representing one pixel, **pairs of bits** encode one of four colors per
"double-wide" pixel.

| Effective Resolution | Pixel Width | Colors per Cell |
|----------------------|-------------|-----------------|
| 4 wide x 8 tall     | 2 pixels    | Up to 4         |

The mode is enabled by setting bit 4 (MCM) of $D016:

```
$D011 = $1B   ; ECM=0, BMM=0 (character mode)
$D016 = $18   ; MCM=1, 40 columns
```

### 4.2 Bit-Pair Color Sources

Each pair of bits in the character data selects a color source:

| Bit Pair | Color Source                              | Scope         |
|----------|-------------------------------------------|---------------|
| %00      | Background color ($D021)                  | Global        |
| %01      | Extra background color 1 ($D022)          | Global        |
| %10      | Extra background color 2 ($D023)          | Global        |
| %11      | Color RAM (bits 0-2 only)                 | Per character  |

The bit pairs are read left to right within each byte:

```
Byte: B7 B6 B5 B4 B3 B2 B1 B0
      \--/  \--/  \--/  \--/
      Px 0  Px 1  Px 2  Px 3
```

Each multicolor pixel is 2 screen pixels wide, so 4 multicolor pixels span 8 screen
pixels (one character width).

### 4.3 The Color RAM Bit 3 Selector

This is one of the most important details of Multicolor Character Mode: **bit 3 of the
Color RAM value determines whether each individual character is rendered in multicolor
or standard (hires) mode.**

| Color RAM Value | Bit 3 | Rendering Mode  | Available Colors               |
|-----------------|-------|-----------------|--------------------------------|
| 0-7             | 0     | Standard (hires)| Background ($D021) + Color RAM |
| 8-15            | 1     | Multicolor      | $D021, $D022, $D023, Color RAM bits 0-2 |

When a character has a Color RAM value of 0-7 (bit 3 = 0), it renders exactly as in
Standard Character Mode -- full 8-pixel horizontal resolution with 2 colors.

When a character has a Color RAM value of 8-15 (bit 3 = 1), it renders in multicolor
mode with 4-pixel horizontal resolution and up to 4 colors. The foreground color for
bit-pair %11 comes from **only bits 0-2** of the Color RAM value (the lower 3 bits),
giving 8 possible colors (0-7) for the multicolor foreground.

### 4.4 Mixing Hires and Multicolor Characters

Because the hires/multicolor decision is per-character, you can freely mix both types
on the same screen. This is extremely useful:

- **Text** can use hires characters (Color RAM 0-7) for full-resolution readability
- **Game tiles** can use multicolor characters (Color RAM 8-15) for richer graphics
- The three global colors ($D021, $D022, $D023) are shared across all multicolor
  characters on screen

**Limitation:** Multicolor foreground is restricted to colors 0-7 (the lower 8 of the
C64's 16 colors). The full set of 16 colors is only available in the shared registers
$D021, $D022, and $D023.

### 4.5 Multicolor Character Data Example

```
Standard (hires) byte:  %10110100
  8 pixels: 1 0 1 1 0 1 0 0

Multicolor byte:        %10110100
  4 double-wide pixels: 10  11  01  00
  Colors:               $D023 ColorRAM $D022 $D021
```

The same byte value produces very different output depending on whether the character is
in hires or multicolor mode.


---

## 5. Extended Background Color (ECM) Mode

### 5.1 How It Works

Extended Background Color Mode allows each character cell to have one of four different
background colors, selected per character. It is activated by setting bit 6 (ECM) in
$D011:

```
$D011 = $5B   ; ECM=1, BMM=0 (character mode)
$D016 = $08   ; MCM=0
```

### 5.2 Background Color Selection via Character Code

The **top two bits** (bits 7-6) of the screen code are repurposed to select which of
four background color registers applies to that character:

| Bits 7-6 | Background Color Register | Character Range |
|----------|---------------------------|-----------------|
| %00      | $D021 (Background 0)      | Characters 0-63 |
| %01      | $D022 (Background 1)      | Characters 64-127 (display as 0-63) |
| %10      | $D023 (Background 2)      | Characters 128-191 (display as 0-63) |
| %11      | $D024 (Background 3)      | Characters 192-255 (display as 0-63) |

The actual character shape is determined by only bits 5-0 of the screen code (the lower
6 bits). This means **only 64 unique character shapes are available** (characters 0-63).

The foreground color still comes from Color RAM, as in Standard Character Mode.

### 5.3 Practical Usage

To display character shape 5 with background color from $D023:

```
Screen code = %10_000101 = 128 + 5 = 133
```

To display character shape 30 with background color from $D022:

```
Screen code = %01_011110 = 64 + 30 = 94
```

**From BASIC:**

```basic
POKE 53265,PEEK(53265) OR 64    : REM ENABLE ECM
POKE 53281,0  : REM BG0 = BLACK
POKE 53282,2  : REM BG1 = RED
POKE 53283,5  : REM BG2 = GREEN
POKE 53284,6  : REM BG3 = BLUE
POKE 1024,65  : REM CHAR 1 WITH BG1 (RED) AT TOP-LEFT
```

### 5.4 Trade-offs and Use Cases

**Advantages:**
- Four selectable background colors per character cell
- Full 8x8 pixel resolution (unlike multicolor mode)
- Foreground color still selectable per cell from Color RAM

**Disadvantages:**
- Only 64 character shapes available (versus 256 in normal mode)
- Cannot be combined with multicolor mode (produces invalid mode)
- Loses access to characters 64-255 for their original shapes
- The upper/lowercase letter set (set 2) becomes largely unusable since many common
  characters have screen codes above 63

**Use cases:**
- Status bars or UI elements needing colored backgrounds behind text
- Board games or puzzle games where cell background color matters
- Dialogue boxes with distinct background tinting
- Relatively uncommon in commercial software due to the severe character limitation


---

## 6. PETSCII and Screen Codes

### 6.1 Two Different Encodings

The C64 uses two distinct character encodings that are frequently confused:

- **PETSCII (PET Standard Code of Information Interchange):** Used by the KERNAL, BASIC,
  and I/O routines. This is what `CHR$()` and `PRINT` use. Based on (but different from)
  ASCII.
- **Screen codes (video matrix codes):** Used in screen RAM. These are indices directly
  into the character generator ROM/RAM. This is what you `POKE` into screen memory.

**They are not the same.** For example:

| Character | PETSCII Code | Screen Code |
|-----------|-------------|-------------|
| @         | 64 ($40)    | 0 ($00)     |
| A         | 65 ($41)    | 1 ($01)     |
| B         | 66 ($42)    | 2 ($02)     |
| Space     | 32 ($20)    | 32 ($20)    |
| 0         | 48 ($30)    | 48 ($30)    |

### 6.2 Conversion Between PETSCII and Screen Codes

**PETSCII to Screen Code:**

| PETSCII Range      | Operation   | Screen Code Range |
|--------------------|-------------|-------------------|
| $00-$1F (0-31)     | Add 128     | $80-$9F (128-159) |
| $20-$3F (32-63)    | Unchanged   | $20-$3F (32-63)   |
| $40-$5F (64-95)    | Subtract 64 | $00-$1F (0-31)    |
| $60-$7F (96-127)   | Subtract 32 | $40-$5F (64-95)   |
| $80-$9F (128-159)  | Add 64      | $C0-$DF (192-223) |
| $A0-$BF (160-191)  | Subtract 64 | $60-$7F (96-127)  |
| $C0-$DF (192-223)  | Subtract 128| $40-$5F (64-95)   |
| $E0-$FE (224-254)  | Subtract 128| $60-$7E (96-126)  |
| $FF (255, pi)      | Special     | $5E (94)           |

**Screen Code to PETSCII:**

| Screen Code Range   | Operation   | PETSCII Range      |
|---------------------|-------------|--------------------|
| $00-$1F (0-31)      | Add 64      | $40-$5F (64-95)    |
| $20-$3F (32-63)     | Unchanged   | $20-$3F (32-63)    |
| $40-$5F (64-95)     | Add 32      | $60-$7F (96-127)   |
| $60-$7F (96-127)    | Add 64      | $A0-$BF (160-191)  |
| $80-$9F (128-159)   | Subtract 128| $00-$1F (0-31)     |
| $C0-$DF (192-223)   | Subtract 64 | $80-$9F (128-159)  |

Note: Screen codes $A0-$BF are the reverse (inverted) versions of $20-$3F, and codes
$E0-$FF are the reverse versions of $60-$7F.

### 6.3 The Character Set Table

**Set 1: Uppercase/Graphics (ROM at $D000-$D7FF)**

| Screen Code | Character     | Screen Code | Character     |
|-------------|---------------|-------------|---------------|
| 0           | @             | 32          | (space)       |
| 1-26        | A-Z           | 33-47       | ! " # $ % & ' ( ) * + , - . / |
| 27          | [             | 48-57       | 0-9           |
| 28          | Pound sign    | 58-63       | : ; < = > ?   |
| 29          | ]             | 64-95       | Graphics symbols (lines, blocks) |
| 30          | Up arrow      | 96-127      | More graphics symbols |
| 31          | Left arrow    | 128-255     | Reversed versions of 0-127 |

**Set 2: Uppercase/Lowercase (ROM at $D800-$DFFF)**

| Screen Code | Character     | Screen Code | Character     |
|-------------|---------------|-------------|---------------|
| 0           | @             | 32          | (space)       |
| 1-26        | a-z           | 33-57       | Same as set 1 |
| 27-31       | Same as set 1 | 58-63       | Same as set 1 |
| 64          | (none/space)  | 65-90       | A-Z           |
| 91-127      | Graphics      | 128-255     | Reversed 0-127|

### 6.4 Control Codes

PETSCII codes $00-$1F and $80-$9F are control codes. They are not printable characters
but cause actions when sent via PRINT or the KERNAL output routine.

**Range $00-$1F:**

| PETSCII | Hex   | Function                   |
|---------|-------|----------------------------|
| 3       | $03   | STOP (RUN/STOP key)        |
| 5       | $05   | Color: White               |
| 8       | $08   | Disable SHIFT+C= switching |
| 9       | $09   | Enable SHIFT+C= switching  |
| 13      | $0D   | RETURN (carriage return)   |
| 14      | $0E   | Switch to lowercase charset|
| 17      | $11   | Cursor Down                |
| 18      | $12   | Reverse Video On           |
| 19      | $13   | HOME (cursor to top-left)  |
| 20      | $14   | DELETE                     |
| 28      | $1C   | Color: Red                 |
| 29      | $1D   | Cursor Right               |
| 30      | $1E   | Color: Green               |
| 31      | $1F   | Color: Blue                |

**Range $80-$9F:**

| PETSCII | Hex   | Function                   |
|---------|-------|----------------------------|
| 129     | $81   | Color: Orange              |
| 131     | $83   | RUN                        |
| 133-136 | $85-$88 | F1, F3, F5, F7           |
| 137-140 | $89-$8C | F2, F4, F6, F8           |
| 141     | $8D   | SHIFT+RETURN               |
| 142     | $8E   | Switch to uppercase charset|
| 144     | $90   | Color: Black               |
| 145     | $91   | Cursor Up                  |
| 146     | $92   | Reverse Video Off          |
| 147     | $93   | CLEAR (clear screen + home)|
| 148     | $94   | INSERT                     |
| 149     | $95   | Color: Brown               |
| 150     | $96   | Color: Pink (Light Red)    |
| 151     | $97   | Color: Dark Grey           |
| 152     | $98   | Color: Grey (Medium)       |
| 153     | $99   | Color: Light Green         |
| 154     | $9A   | Color: Light Blue          |
| 155     | $9B   | Color: Light Grey          |
| 156     | $9C   | Color: Purple              |
| 157     | $9D   | Cursor Left                |
| 158     | $9E   | Color: Yellow              |
| 159     | $9F   | Color: Cyan                |


---

## 7. Techniques and Tricks

### 7.1 Fake High-Resolution Using Redefined Characters

The most common game graphics technique on the C64: redefine the character set to contain
game-specific tiles, then "paint" the screen by placing tile codes in screen RAM.

**How it works:**

1. Design 8x8 pixel tiles for terrain, objects, UI elements, etc.
2. Store them as a custom character set (2 KB)
3. Build game screens by filling screen RAM with tile codes
4. Use Color RAM for per-tile foreground color

**Advantages over bitmap mode:**

- 10 KB less memory (3 KB vs 9 KB for screen + colors)
- Fast screen updates (1 byte = 1 tile vs 8 bytes = 1 tile column)
- Hardware scrolling integrates naturally
- Still allows 8 hardware sprites on top

**Many games use this to produce pseudo-bitmap imagery** where each screen code is unique,
essentially building up a bitmap image from 1,000 distinct characters. This uses a full
charset (no character reuse) but gives bitmap-quality graphics with character-mode speed.

### 7.2 Charset Animation

Since all instances of a character share the same definition, modifying the 8 bytes of a
character definition instantly updates every occurrence on screen.

**Common animated effects:**

- **Water/waves:** 3-4 frames of a water tile, cycled every few frames
- **Lava/fire:** Similar cycling with different patterns
- **Conveyor belts:** Shift pixel rows left or right each frame
- **Blinking lights:** Toggle specific bits on/off
- **Growing vegetation:** Gradually modify character data over time

**Implementation approach:**

```asm
; Animation table: N frames x 8 bytes each
water_frames:
        .byte $00,$36,$49,$49,$36,$00,$00,$00  ; frame 0
        .byte $00,$1B,$24,$24,$1B,$00,$00,$00  ; frame 1
        .byte $00,$6C,$92,$92,$6C,$00,$00,$00  ; frame 2

; Copy current frame to charset
        lda frame_counter
        asl
        asl
        asl                 ; Multiply by 8
        tax
        ldy #7
@loop:  lda water_frames,x
        sta charset+512,y   ; Character 64 = charset_base + 64*8
        inx
        dey
        bpl @loop
```

**Optimization:** Only overwrite the rows that actually change between frames. For
symmetrical patterns, one source byte can be written to two rows.

### 7.3 Color-Per-Character Tricks

Each character cell has its own Color RAM entry, so a 40x25 screen can have up to 1,000
different foreground colors -- one per cell. Combined with custom characters, this enables
richly colored screens.

**Color wash effect:** Rapidly update Color RAM values across a row or column to create
rainbow or shimmer effects, often seen in demo scrollers.

**Attribute clash management:** Unlike the ZX Spectrum's 8x8 attribute system (which the
C64 shares in principle), the C64's background color is global in standard mode, which
means careful tile design can minimize the perception of "color clash."

### 7.4 Combining Character Mode with Raster Tricks

Using raster interrupts, you can change VIC-II registers at specific scanlines, enabling:

**Split-screen effects:**
- Different character sets in different screen regions (swap $D018 at a raster line)
- Character mode in one area, bitmap mode in another
- Different background colors ($D021) for different screen sections

**More effective colors:**
- Change $D021 (background) per raster line for gradient backgrounds
- Change $D022/$D023 at specific lines for different multicolor palettes in different
  screen regions
- Swap charset pointer ($D018) mid-screen to display more than 256 unique tiles

**Charset switching mid-screen:**

```asm
irq_handler:
        lda #$1C            ; Point to charset at $3000
        sta $D018
        lda #<next_irq
        sta $FFFE
        lda #$C8            ; Next IRQ at raster line 200
        sta $D012
        asl $D019           ; Acknowledge interrupt
        rti

next_irq:
        lda #$18            ; Point to charset at $2000
        sta $D018
        lda #<irq_handler
        sta $FFFE
        lda #$32            ; Next IRQ at raster line 50
        sta $D012
        asl $D019
        rti
```

### 7.5 Soft Scrolling with Character Mode

The VIC-II provides hardware-assisted smooth scrolling via scroll registers:

**Horizontal scrolling:** Bits 0-2 of $D016 (values 0-7) offset the display 0-7 pixels
to the right.

**Vertical scrolling:** Bits 0-2 of $D011 (values 0-7) offset the display 0-7 pixels
downward.

**The scrolling process (horizontal example):**

1. Start with horizontal scroll = 7 (or 0, depending on direction)
2. Each frame, decrement the scroll value by 1 (for leftward scrolling)
3. When the scroll value wraps past 0, reset to 7 and shift all screen RAM contents
   one column to the left, drawing new data into the rightmost column
4. Repeat

**38-column / 24-row mode:** When scrolling, set bit 3 of $D016 to 0 (38 columns) or
bit 3 of $D011 to 0 (24 rows). This hides the outermost column/row behind the border,
masking the visual discontinuity when screen data is shifted. The hidden column/row is
where new data appears before scrolling into view.

```asm
; Horizontal scroll left example
        dec scroll_x
        bpl no_shift
        lda #7
        sta scroll_x

        ; Shift screen RAM left by 1 column
        ; (move bytes at offset 1-39 to 0-38 for each row)
        ; Draw new column at position 39
        jsr shift_screen_left
        jsr draw_new_column

no_shift:
        lda scroll_x
        ora #$C0             ; Keep upper bits, 38-column mode (bit 3 = 0)
        sta $D016
```

**Color RAM scrolling:** Color RAM at $D800 must also be shifted when the screen data
shifts. This is a significant CPU cost since Color RAM cannot be relocated or
double-buffered.

### 7.6 Using Character Graphics for Fast Game Maps

Character-based maps are far more memory-efficient than bitmaps:

**Tile map structure:**

```
Level map: 100 columns x 25 rows = 2,500 bytes
Character set: 2,048 bytes
Color data: 2,500 bytes (or use block-level color)
Total: ~7 KB for a level 2.5 screens wide
```

Compare with bitmap: 100 columns x 200 pixels = 8 KB bitmap + 1 KB color = 9 KB per
screen, so 2.5 screens would need ~22 KB.

**Block-based compression:** Instead of storing individual character codes, store a
"block map" where each byte references a meta-tile (e.g., 2x2 characters). A map of
50x12 blocks = 600 bytes can represent the same level, with a block definition table
of 64 blocks x 4 bytes = 256 bytes.

**Screen rendering:** To draw a visible screen from a map:

```asm
; For each visible row
;   For each visible column
;     Look up block ID from map
;     Look up 2x2 character codes from block table
;     Write 4 screen codes to screen RAM
;     Write 4 color values to Color RAM
```

**Tools:** CharPad (by Subchrist Software) is the standard tool for designing C64
character sets, tiles, and maps. It exports data in formats suitable for direct use in
assembly programs.


---

## 8. Hardcore Details

### 8.1 Character Fetch Timing (Bad Lines)

The VIC-II must read character codes from screen RAM (called a "c-access") and then
pixel data from the character generator (called a "g-access"). On most raster lines,
only g-accesses are needed because the character codes are cached internally. However,
on the first raster line of each character row, the VIC-II must fetch all 40 character
codes -- these are the infamous **bad lines**.

**Bad Line Condition:**

A bad line occurs when all of the following are true:

1. `RASTER >= $30` and `RASTER <= $F7` (within the display window)
2. The lower 3 bits of RASTER equal YSCROLL (bits 0-2 of $D011)
3. The DEN bit (bit 4 of $D011) was set at some point during raster line $30

With default YSCROLL = 3, bad lines occur at raster lines $33, $3B, $43, ..., $F3
(every 8th line, 25 total).

### 8.2 How the VIC-II Reads Character Pointers and Pixel Data

**C-access (character pointer fetch):**

- Occurs during cycles 15-54 on bad lines (40 accesses, one per character)
- Reads 12 bits simultaneously:
  - 8 bits from the data bus: the screen code (character number)
  - 4 bits from the dedicated color bus: the Color RAM nybble
- The 12-bit values are stored in an internal 40x12-bit line buffer
- Address formula: `VM13-VM10 | VC9-VC0` (video matrix base + video counter)

**G-access (graphics/pixel data fetch):**

- Occurs during cycles 15-54 on every visible raster line (bad and non-bad)
- Reads 8 bits of character pixel data
- Address formula for character mode:
  `CB13-CB11 | D7-D0 | RC2-RC0`
  (character base + character code from c-access + row counter)
- The row counter (RC) cycles 0-7 across the 8 raster lines of each character row

On bad lines, both c-accesses and g-accesses happen in the same cycle range (15-54).
The VIC-II reads the c-access during the first half-cycle (phi1) and the g-access during
the same cycle's phi1 as well -- it effectively performs both by using the c-access data
from the previous cycle for the current g-access.

### 8.3 Timing of Color RAM Reads

Color RAM is read simultaneously with screen RAM during c-accesses. The VIC-II's 4-bit
color data bus is directly wired to the Color RAM chip, which shares the lower 10 address
lines with the main address bus. This means:

- Color data arrives at the same time as character codes, during cycles 15-54 of bad lines
- No additional cycles are consumed for color reads
- The Color RAM is accessible in all VIC banks (it is not affected by bank switching)
- The 4-bit values are latched into the internal 40x12-bit line buffer alongside the
  8-bit character codes

### 8.4 CPU Cycle Stealing on Bad Lines

During bad lines, the VIC-II takes over the bus for 40 cycles (cycles 15-54), but the
actual CPU stall may be 40-43 cycles depending on when the CPU can release the bus:

- The VIC-II asserts BA (Bus Available) low 3 cycles before it needs the bus (at cycle 12)
- The CPU can complete up to 3 pending write cycles before halting on the RDY line
- After the VIC-II releases the bus at cycle 54, the CPU resumes

This leaves the CPU with only 20-23 cycles per bad line (out of 63 on PAL). On
non-bad lines, the CPU gets all 63 cycles (minus any sprite DMA stealing).

### 8.5 The Video Counter (VC) and Row Counter (RC)

**VC (Video Counter):** A 10-bit counter (0-999) that tracks which of the 1,000 screen
positions is being accessed. At cycle 14 of each line, VC is loaded from VCBASE. After
each c-access, VC increments. When RC reaches 7 at cycle 58, VCBASE is loaded from VC,
advancing to the next character row.

**RC (Row Counter):** A 3-bit counter (0-7) tracking which pixel row within the current
character row is being rendered. RC is reset to 0 on bad lines (at cycle 14) and
increments at cycle 58 of each line. When RC = 7, the current character row is complete.

**VCBASE:** Holds the VC value at the start of each character row. It only updates when
RC reaches 7, ensuring the same 40 character codes are used for all 8 raster lines of a
character row.

### 8.6 FLI Interaction with Character Mode

FLI (Flexible Line Interpretation) is a raster trick that forces the VIC-II to re-read
character pointers on every raster line, not just every 8th line. This overcomes the
limitation that color information (from screen RAM and Color RAM) is only fetched once
per character row.

**How FLI works:**

1. On each raster line, change YSCROLL (bits 0-2 of $D011) so that the lower 3 bits of
   YSCROLL match the current raster line's lower 3 bits
2. This creates a bad line condition on every raster line
3. Simultaneously change $D018 to point to a different screen RAM area for each line
4. The VIC-II re-reads screen RAM (and Color RAM) every line, allowing unique color
   information per pixel row

**The 3-character grey block:** When FLI forces a bad line, the VIC-II starts fetching
character pointers at cycle 15, but the $D018 change may not take effect until after
the first few characters are fetched. This results in the leftmost 3 characters (24
pixels) reading undefined data, appearing as a grey block. This artifact is unavoidable
and is present in all FLI images.

**FLI in character mode:** FLI is most commonly used with bitmap modes, but the same
principle can be applied to character mode to allow different Color RAM values per
raster line, enabling more than the usual 2 colors per 8x8 cell.

### 8.7 ECM + Multicolor (Invalid Mode) Behavior

Setting both ECM (bit 6 of $D011) and MCM (bit 4 of $D016) simultaneously produces an
**invalid graphics mode**. The VIC-II does not crash or lock up, but the behavior is
well-defined:

**Mode 5 (ECM + MCM text):**
- All pixels are rendered as the background color (effectively black/invisible)
- Internally, the VIC-II generates graphics data as if in multicolor text mode, but
  with the ECM 64-character limitation applied
- **Sprite collisions still work:** The internal graphics data is used for
  sprite-foreground and sprite-background collision detection, even though no visible
  pixels are produced
- This allows a form of "invisible collision map" technique

**Mode 6 (ECM + BMM standard bitmap):**
- All visible pixels are black
- Address generation is modified: bits 9-10 of the g-access address are forced to 0
- This causes the bitmap data to repeat in 4 sections

**Mode 7 (ECM + BMM + MCM multicolor bitmap):**
- All visible pixels are black
- Same address modification as mode 6
- Same invisible collision detection as mode 5

### 8.8 Exact Memory Alignment Requirements

| Data Type         | Alignment | Size   | Set via        |
|-------------------|-----------|--------|----------------|
| Character set     | 2 KB      | 2 KB   | $D018 bits 1-3 |
| Screen RAM        | 1 KB      | 1 KB   | $D018 bits 4-7 |
| Bitmap            | 8 KB      | 8 KB   | $D018 bit 3    |
| Color RAM         | Fixed     | 1 KB   | Always $D800   |
| VIC bank          | 16 KB     | 16 KB  | $DD00 bits 0-1 |

All addresses specified in $D018 are **offsets within the currently selected VIC bank**.
The absolute address is: `VIC_bank_base + offset`.

**Sprite pointers** are stored at the last 8 bytes of screen RAM (e.g., $07F8-$07FF
for default screen at $0400). Each byte is a sprite data pointer: multiply by 64 to
get the sprite data address within the VIC bank.

### 8.9 Idle State Access

When the VIC-II is not in the display state (outside the 25 character rows, or when the
display is disabled), it enters idle state. In idle state:

- Only g-accesses occur (no c-accesses)
- The access address is always $3FFF (last byte of the VIC bank)
- With ECM set, the address becomes $39FF
- The fetched data is displayed using the current graphics mode, but with all video
  matrix data treated as zeros
- This can produce visible artifacts at the top/bottom of the screen if YSCROLL causes
  partial character rows to appear in the border region


---

## 9. Quick Reference: Mode Comparison

| Feature                | Standard | Multicolor | ECM     |
|------------------------|----------|------------|---------|
| Pixel resolution       | 8x8      | 4x8        | 8x8     |
| Colors per cell        | 2        | 4          | 2       |
| Background colors      | 1        | 1          | 4       |
| Character set size     | 256      | 256        | 64      |
| Foreground colors      | 16       | 8 (0-7)   | 16      |
| $D011 ECM bit          | 0        | 0          | 1       |
| $D016 MCM bit          | 0        | 1          | 0       |
| Can mix with hires?    | N/A      | Yes        | No      |

### Register Quick Reference

| Register | Address | Relevant Bits                             |
|----------|---------|-------------------------------------------|
| $D011    | 53265   | Bit 6: ECM, Bit 5: BMM, Bit 4: DEN, Bits 0-2: YSCROLL |
| $D016    | 53270   | Bit 4: MCM, Bit 3: CSEL (38/40 col), Bits 0-2: XSCROLL |
| $D018    | 53272   | Bits 7-4: Screen RAM, Bits 3-1: Char set  |
| $D021    | 53281   | Background color 0                         |
| $D022    | 53282   | Background color 1 / MC color 1            |
| $D023    | 53283   | Background color 2 / MC color 2            |
| $D024    | 53284   | Background color 3 (ECM only)              |
| $DD00    | 56576   | Bits 0-1: VIC bank selection (inverted)    |


---

## 10. References

### Primary Technical Sources

- Christian Bauer, "The MOS 6567/6569 video controller (VIC-II) and its application in
  the Commodore 64" -- the definitive VIC-II technical document
  - https://www.cebix.net/VIC-Article.txt
  - https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt

- Commodore 64 Programmer's Reference Guide, Chapter 3: Programming Graphics
  - https://www.devili.iki.fi/Computers/Commodore/C64/Programmers_Reference/Chapter_3/page_104.html

### C64-Wiki Articles

- Character Set: https://www.c64-wiki.com/wiki/Character_set
- Standard Character Mode: https://www.c64-wiki.com/wiki/Standard_Character_Mode
- Extended Color Mode: https://www.c64-wiki.com/wiki/Extended_color_mode
- Color RAM: https://www.c64-wiki.com/wiki/Color_RAM
- Graphics Modes: https://www.c64-wiki.com/wiki/Graphics_Modes
- VIC Bank: https://www.c64-wiki.com/wiki/VIC_bank
- Register 53272 ($D018): https://www.c64-wiki.com/wiki/53272
- Raster Interrupt: https://www.c64-wiki.com/wiki/Raster_interrupt
- PETSCII: https://www.c64-wiki.com/wiki/PETSCII
- PETSCII Codes in Listings: https://www.c64-wiki.com/wiki/PETSCII_Codes_in_Listings

### PETSCII and Screen Code References

- Commodore 64 PETSCII codes: https://sta.c64.org/cbm64pet.html
- PETSCII to screen code conversion: https://sta.c64.org/cbm64pettoscr.html
- Screen code to PETSCII conversion: https://sta.c64.org/cbm64scrtopet.html
- C64 display modes: https://sta.c64.org/cbm64disp.html
- Ultimate Commodore Charset/PETSCII/Keyboard Reference: https://www.pagetable.com/c64ref/charset/
- PETSCII (Wikipedia): https://en.wikipedia.org/wiki/PETSCII

### Codebase64 Wiki

- Built-in Screen Modes: https://codebase.c64.org/doku.php?id=base:built_in_screen_modes
- VIC-II Memory Organizing: https://codebase.c64.org/doku.php?id=base:vicii_memory_organizing

### Tutorials and Articles

- Dustlayer VIC-II tutorials:
  - Screen Modes: https://dustlayer.com/vic-ii/2013/4/26/vic-ii-for-beginners-screen-modes-cheaper-by-the-dozen
  - Custom Character Sets: https://dustlayer.com/c64-coding-tutorials/2013/5/24/episode-3-6-custom-character-sets-hello-charpad
  - Visibility and Memory: https://dustlayer.com/vic-ii/2013/4/22/when-visibility-matters
  - Rasters and Cycles: https://dustlayer.com/vic-ii/2013/4/25/vic-ii-for-beginners-beyond-the-screen-rasters-cycle

- C64 OS articles:
  - Character Animation: https://c64os.com/post/characteranimation
  - FLI Timing: https://c64os.com/post/flitiming1
  - PETSCII/ASCII Conversion: https://c64os.com/post/petsciiasciiconversion

- FLI technique explained: https://bumbershootsoft.wordpress.com/2016/03/12/fli-part-1-16-color-mode/
- FLI by Pasi Ojala: http://www.antimon.org/code/fli.txt
- Multidirectional scrolling by Cadaver: https://cadaver.github.io/rants/scroll.html
- MOS Technology VIC-II (Wikipedia): https://en.wikipedia.org/wiki/MOS_Technology_VIC-II

### Tools

- CharPad C64 (character set and tile map editor): https://subchristsoftware.itch.io/charpad-c64-free
