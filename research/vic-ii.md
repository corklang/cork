# MOS 6567/6569 VIC-II Graphics Chip Reference

Comprehensive technical reference for the Video Interface Controller II used in the
Commodore 64.


---

## 1. Overview

The VIC-II (Video Interface Controller II) is the graphics and video chip at the heart of
the Commodore 64. Designed by MOS Technology, it generates all video output, manages
dynamic RAM refresh, and provides hardware sprite support. The chip shares the system bus
with the 6510 CPU using an alternating half-cycle access scheme (phi1 for VIC-II, phi2 for
CPU), and can halt the CPU when it needs extra bus bandwidth.

### Chip Variants

| Part Number | Standard | Process | Notes |
|-------------|----------|---------|-------|
| MOS 6567    | NTSC     | 5um NMOS | Original NTSC (R56A: 64 cyc/line, R8: 65 cyc/line) |
| MOS 6569    | PAL-B    | 5um NMOS | Most common PAL variant (revisions R1, R3, R4, R5) |
| MOS 6572    | PAL-N    | NMOS    | Argentina, Paraguay, Uruguay |
| MOS 6573    | PAL-M    | NMOS    | Brazil |
| MOS 8562    | NTSC     | 3.5um HMOS-II | C64C / C64E, 5V only |
| MOS 8565    | PAL-B    | 3.5um HMOS-II | C64C / C64E, 5V only |
| MOS 8564    | NTSC     | HMOS-II | C128 VIC-IIe (48-pin, Z80 clock output) |
| MOS 8566    | PAL-B    | HMOS-II | C128 VIC-IIe |
| MOS 8569    | PAL-N    | HMOS-II | C128 VIC-IIe |

### PAL vs NTSC Timing

| Parameter              | 6569 (PAL)   | 6567R56A (NTSC) | 6567R8 (NTSC) |
|------------------------|--------------|-----------------|----------------|
| System clock           | 985.248 kHz  | 1022.727 kHz    | 1022.727 kHz   |
| Dot clock              | 7.8819 MHz   | 8.1818 MHz      | 8.1818 MHz     |
| Color clock            | 17.734472 MHz| 14.31818 MHz    | 14.31818 MHz   |
| Cycles per line        | 63           | 64              | 65             |
| Total raster lines     | 312          | 262             | 263            |
| Visible raster lines   | 284          | 234             | 235            |
| Pixels per line        | 504          | 512             | 520            |
| Frame rate             | ~50.125 Hz   | ~59.826 Hz      | ~59.826 Hz     |

### Physical Characteristics

- **Package:** 40-pin DIP (VIC-II), 48-pin PLCC (VIC-IIe for C128)
- **Address bus:** 14 bits (addresses 16 KB directly)
- **Data bus:** 12 bits (8-bit main data + 4-bit color nybble)
- **Power:** 6567/6569 require +5V and +12V; 8562/8565 require +5V only


---

## 2. Register Map ($D000-$D03F)

The VIC-II exposes 47 registers mapped at $D000-$D02E. These registers repeat every 64
bytes throughout the range $D000-$D3FF.

### 2.1 Sprite Position Registers ($D000-$D010)

| Address | Name | Bits   | R/W | Description |
|---------|------|--------|-----|-------------|
| $D000   | M0X  | 7-0    | R/W | Sprite 0 X position (bits 0-7) |
| $D001   | M0Y  | 7-0    | R/W | Sprite 0 Y position |
| $D002   | M1X  | 7-0    | R/W | Sprite 1 X position (bits 0-7) |
| $D003   | M1Y  | 7-0    | R/W | Sprite 1 Y position |
| $D004   | M2X  | 7-0    | R/W | Sprite 2 X position (bits 0-7) |
| $D005   | M2Y  | 7-0    | R/W | Sprite 2 Y position |
| $D006   | M3X  | 7-0    | R/W | Sprite 3 X position (bits 0-7) |
| $D007   | M3Y  | 7-0    | R/W | Sprite 3 Y position |
| $D008   | M4X  | 7-0    | R/W | Sprite 4 X position (bits 0-7) |
| $D009   | M4Y  | 7-0    | R/W | Sprite 4 Y position |
| $D00A   | M5X  | 7-0    | R/W | Sprite 5 X position (bits 0-7) |
| $D00B   | M5Y  | 7-0    | R/W | Sprite 5 Y position |
| $D00C   | M6X  | 7-0    | R/W | Sprite 6 X position (bits 0-7) |
| $D00D   | M6Y  | 7-0    | R/W | Sprite 6 Y position |
| $D00E   | M7X  | 7-0    | R/W | Sprite 7 X position (bits 0-7) |
| $D00F   | M7Y  | 7-0    | R/W | Sprite 7 Y position |
| $D010   | MxX8 | 7-0    | R/W | MSB of X position for sprites 0-7 (bit N = sprite N) |

Sprite coordinates use a 9-bit X range (0-511) and 8-bit Y range (0-255). The 9th bit
of each sprite's X coordinate is stored in the corresponding bit of $D010.

### 2.2 Control Register 1 ($D011)

**Address:** $D011 (53265)  
**Read/Write:** R/W (bit 7 is read-only when reading current raster)

| Bit | Name    | Description |
|-----|---------|-------------|
| 7   | RST8    | Bit 8 of raster compare register (write) / current raster (read) |
| 6   | ECM     | Extended Color Mode (1 = enabled) |
| 5   | BMM     | Bitmap Mode (1 = enabled) |
| 4   | DEN     | Display Enable (0 = blank screen, border color only) |
| 3   | RSEL    | Row Select: 0 = 24 rows (192 px), 1 = 25 rows (200 px) |
| 2-0 | YSCROLL | Vertical fine scroll (0-7 pixels) |

Default value on power-up: $1B (%00011011) = DEN on, RSEL=1, YSCROLL=3.

### 2.3 Raster Counter ($D012)

**Address:** $D012 (53266)  
**Read:** Returns low 8 bits of current raster line (bit 8 in $D011 bit 7)  
**Write:** Sets low 8 bits of raster compare value (bit 8 via $D011 bit 7)

The raster counter counts from 0 to 311 (PAL) or 262/263 (NTSC).

When the raster counter matches the compare value, the IRST bit in $D019 is set. If the
corresponding enable bit in $D01A is also set, an IRQ is generated.

### 2.4 Light Pen Registers ($D013-$D014)

| Address | Name | R/W | Description |
|---------|------|-----|-------------|
| $D013   | LPX  | R   | Light pen X position (latched, divided by 2: range 0-255) |
| $D014   | LPY  | R   | Light pen Y position (latched: current raster line) |

The light pen position is latched on a negative transition of the LP pin. Only one latch
per frame; subsequent transitions within the same frame are ignored.

### 2.5 Sprite Enable ($D015)

**Address:** $D015 (53269)  
**R/W:** R/W

Each bit enables the corresponding sprite (bit 0 = sprite 0, bit 7 = sprite 7).
Set bit to 1 to enable, 0 to disable.

### 2.6 Control Register 2 ($D016)

**Address:** $D016 (53270)  
**R/W:** R/W (bits 7-6 always read as 1 on some revisions, or unused)

| Bit | Name    | Description |
|-----|---------|-------------|
| 7-6 | --      | Unused |
| 5   | RES     | Reset bit (normally 0; no practical effect in C64) |
| 4   | MCM     | Multicolor Mode (1 = enabled) |
| 3   | CSEL    | Column Select: 0 = 38 columns (304 px), 1 = 40 columns (320 px) |
| 2-0 | XSCROLL | Horizontal fine scroll (0-7 pixels) |

Default value on power-up: $C8 or $08 (CSEL=1, XSCROLL=0).

### 2.7 Sprite Y Expansion ($D017)

**Address:** $D017 (53271)  
**R/W:** R/W

Each bit doubles the height of the corresponding sprite (24x42 instead of 24x21).

### 2.8 Memory Pointers ($D018)

**Address:** $D018 (53272)  
**R/W:** R/W

| Bit | Name     | Description |
|-----|----------|-------------|
| 7-4 | VM13-VM10 | Video matrix (screen RAM) base address: value * $0400 |
| 3-1 | CB13-CB11 | Character generator / bitmap base: value * $0800 |
| 0   | --       | Unused (ignored) |

**Screen RAM location** (relative to VIC bank start): upper nybble * $0400.  
16 possible positions from $0000 to $3C00 in $0400 (1 KB) increments.

**Character set location** (relative to VIC bank start): bits 3-1 * $0800.  
8 possible positions from $0000 to $3800 in $0800 (2 KB) increments.

**Bitmap location** (bitmap mode): only bit 3 matters.  
Bit 3 = 0: bitmap at $0000. Bit 3 = 1: bitmap at $2000.

Default value: $15 = screen at $0400, character ROM at $1000.

### 2.9 Interrupt Registers ($D019-$D01A)

**$D019 - Interrupt Register (R/W):**

| Bit | Name | Description |
|-----|------|-------------|
| 7   | IRQ  | Read: 1 if any enabled interrupt source is active. Write: no effect |
| 3   | ILP  | Light pen interrupt (1 = triggered) |
| 2   | IMMC | Sprite-sprite collision interrupt (1 = triggered) |
| 1   | IMBC | Sprite-background collision interrupt (1 = triggered) |
| 0   | IRST | Raster compare interrupt (1 = triggered) |

Write 1 to a bit to acknowledge/clear that interrupt source. Common method: `ASL $D019`
(shifts bit 7 into carry and clears it) or `LDA #$FF / STA $D019`.

**$D01A - Interrupt Enable Register (R/W):**

| Bit | Name | Description |
|-----|------|-------------|
| 3   | ELP  | Enable light pen interrupt |
| 2   | EMMC | Enable sprite-sprite collision interrupt |
| 1   | EMBC | Enable sprite-background collision interrupt |
| 0   | ERST | Enable raster compare interrupt |

### 2.10 Sprite Priority ($D01B)

**Address:** $D01B (53275)  
**R/W:** R/W

Each bit controls whether the corresponding sprite appears in front of (0) or behind (1)
background graphics.

### 2.11 Sprite Multicolor Enable ($D01C)

**Address:** $D01C (53276)  
**R/W:** R/W

When a sprite's bit is set to 1, that sprite uses multicolor mode (12x21 resolution,
3 colors + transparent) instead of standard mode (24x21, 1 color + transparent).

### 2.12 Sprite X Expansion ($D01D)

**Address:** $D01D (53277)  
**R/W:** R/W

Each bit doubles the width of the corresponding sprite (48x21 instead of 24x21).

### 2.13 Collision Registers ($D01E-$D01F)

| Address | Name | R/W | Description |
|---------|------|-----|-------------|
| $D01E   | MxM  | R   | Sprite-sprite collision (bit set for each sprite involved) |
| $D01F   | MxD  | R   | Sprite-background collision (bit set for each sprite involved) |

Both registers are cleared after being read. Collision detection works even on invalid
graphics modes (the data is still processed internally). Only non-transparent sprite pixels
and non-background-color graphics pixels count as collisions.

### 2.14 Color Registers ($D020-$D02E)

All color registers use only the lower 4 bits (values 0-15).

| Address | Name | Description |
|---------|------|-------------|
| $D020   | EC   | Border (exterior) color |
| $D021   | B0C  | Background color 0 |
| $D022   | B1C  | Background color 1 (multicolor/ECM) |
| $D023   | B2C  | Background color 2 (multicolor/ECM) |
| $D024   | B3C  | Background color 3 (ECM only) |
| $D025   | MM0  | Sprite multicolor 0 (shared by all multicolor sprites) |
| $D026   | MM1  | Sprite multicolor 1 (shared by all multicolor sprites) |
| $D027   | M0C  | Sprite 0 individual color |
| $D028   | M1C  | Sprite 1 individual color |
| $D029   | M2C  | Sprite 2 individual color |
| $D02A   | M3C  | Sprite 3 individual color |
| $D02B   | M4C  | Sprite 4 individual color |
| $D02C   | M5C  | Sprite 5 individual color |
| $D02D   | M6C  | Sprite 6 individual color |
| $D02E   | M7C  | Sprite 7 individual color |

### 2.15 Unused/Read-Only Registers ($D02F-$D03F)

Addresses $D02F-$D03F are unused and always return $FF on read.

### 2.16 Color RAM ($D800-$DBE7)

Not part of the VIC-II chip itself, but essential: 1000 nybbles of static color RAM at
$D800-$DBE7 provide per-character color data. Only the lower 4 bits are valid; the upper
4 bits read as random/unstable values (they are directly on the VIC-II's 4-bit color data
bus, separate from the CPU data bus).

### 2.17 Color Palette

The VIC-II has a fixed 16-color palette. Approximate RGB values (Pepto's colodore model):

| Index | Name         | Hex RGB  |
|-------|-------------|----------|
| 0     | Black        | #000000  |
| 1     | White        | #FFFFFF  |
| 2     | Red          | #68372B  |
| 3     | Cyan         | #70A4B2  |
| 4     | Purple       | #6F3D86  |
| 5     | Green        | #588D43  |
| 6     | Blue         | #352879  |
| 7     | Yellow       | #B8C76F  |
| 8     | Orange       | #6F4F25  |
| 9     | Brown        | #433900  |
| 10    | Light Red    | #9A6759  |
| 11    | Dark Grey    | #444444  |
| 12    | Medium Grey  | #6C6C6C  |
| 13    | Light Green  | #9AD284  |
| 14    | Light Blue   | #6C5EB5  |
| 15    | Light Grey   | #959595  |

Note: There is no single canonical RGB mapping. Different VIC-II revisions, RF modulators,
and display devices produce variations. The values above are from Pepto's analysis of the
PAL color generation circuit.


---

## 3. Graphics Modes

The VIC-II supports five official graphics modes controlled by three bits:

- **ECM** (bit 6 of $D011) -- Extended Color Mode
- **BMM** (bit 5 of $D011) -- Bitmap Mode
- **MCM** (bit 4 of $D016) -- Multicolor Mode

| ECM | BMM | MCM | Mode |
|-----|-----|-----|------|
| 0   | 0   | 0   | Standard Character Mode |
| 0   | 0   | 1   | Multicolor Character Mode |
| 0   | 1   | 0   | Standard Bitmap Mode |
| 0   | 1   | 1   | Multicolor Bitmap Mode |
| 1   | 0   | 0   | Extended Background Color Mode |
| 1   | 0   | 1   | Invalid (black display) |
| 1   | 1   | 0   | Invalid (black display) |
| 1   | 1   | 1   | Invalid (black display) |

### 3.1 Standard Character Mode (ECM=0, BMM=0, MCM=0)

This is the default mode at power-on. The screen displays a 40x25 grid of 8x8 pixel
characters.

**Resolution:** 320 x 200 pixels  
**Colors per cell:** 2 (1 foreground + 1 background)  
**Total unique colors:** Up to 16 foreground colors + 1 global background

**How colors are determined:**
- Bit = 0: Background color from $D021
- Bit = 1: Foreground color from Color RAM ($D800+) lower nybble

**Memory layout:**
- Screen RAM (1000 bytes): character code (0-255) for each cell
- Character generator (2048 bytes): 8 bytes per character, 256 characters
- Color RAM (1000 nybbles): foreground color per cell

**VIC-II address generation:**
- c-access (character pointer): `VM13-VM10 + VC9-VC0`
- g-access (pixel data): `CB13-CB11 + D7-D0 (char code) + RC2-RC0`

### 3.2 Multicolor Character Mode (ECM=0, BMM=0, MCM=1)

Each character cell can individually be standard or multicolor, controlled by the Color
RAM value.

**Resolution:** 160 x 200 pixels (multicolor cells) or 320 x 200 (standard cells)  
**Colors per cell:** 4 (multicolor) or 2 (standard)

**Per-cell mode selection:**
- If Color RAM bit 3 = 0: standard mode (uses colors 0-7 only for foreground)
- If Color RAM bit 3 = 1: multicolor mode

**Multicolor pixel mapping (2-bit pixel pairs):**
- `00`: Background color 0 ($D021)
- `01`: Background color 1 ($D022)
- `10`: Background color 2 ($D023)
- `11`: Color RAM lower 3 bits (colors 0-7 only when in multicolor mode; the MSB
  is used as the mode flag)

**Standard cell pixel mapping** (when Color RAM bit 3 = 0):
- `0`: Background color ($D021)
- `1`: Color RAM lower 4 bits (full 0-15 range)

The multicolor pixels are double-wide (4x8 pixels per cell at the subpixel level).

### 3.3 Standard Bitmap Mode (ECM=0, BMM=1, MCM=0)

High-resolution bitmapped graphics.

**Resolution:** 320 x 200 pixels  
**Colors per 8x8 cell:** 2  
**Bitmap size:** 8000 bytes  

**How colors are determined:**
- Bit = 0: Screen RAM low nybble (bits 0-3)
- Bit = 1: Screen RAM high nybble (bits 4-7)

Each byte of screen RAM provides two colors for the corresponding 8x8 pixel cell.

**Memory layout:**
- Bitmap (8000 bytes): pixel data arranged in 8x8 cell order. Each cell occupies
  8 consecutive bytes (one byte per pixel row within the cell). Cells are stored
  left-to-right, top-to-bottom.
- Screen RAM (1000 bytes): color information (2 colors per cell)

**Bitmap byte order:** For cell at column C, row R:
`bitmap_base + (R/8)*320 + (C*8) + (R mod 8)`

Or equivalently: `bitmap_base + (R/8 * 40 + C) * 8 + (R mod 8)`

**VIC-II address generation:**
- c-access (color data): `VM13-VM10 + VC9-VC0`
- g-access (pixel data): `CB13 + VC9-VC0 + RC2-RC0`

### 3.4 Multicolor Bitmap Mode (ECM=0, BMM=1, MCM=1)

Medium-resolution bitmapped graphics with more colors per cell.

**Resolution:** 160 x 200 pixels (double-wide pixels)  
**Colors per 8x8 cell:** 4  

**Pixel mapping (2-bit pairs):**
- `00`: Background color 0 ($D021) -- counts as background for collision detection
- `01`: Screen RAM high nybble (bits 4-7)
- `10`: Screen RAM low nybble (bits 0-3)
- `11`: Color RAM nybble

**Memory layout:** Same as standard bitmap mode, but pixels are interpreted in pairs.

### 3.5 Extended Background Color Mode (ECM=1, BMM=0, MCM=0)

Text mode with 4 selectable background colors per character.

**Resolution:** 320 x 200 pixels  
**Character set:** 64 characters only (bits 6-7 of character code select background)  
**Colors per cell:** 1 foreground + 1 of 4 backgrounds

**How it works:**
- Character code bits 7-6 select one of four background colors:
  - `00`: $D021 (background color 0)
  - `01`: $D022 (background color 1)
  - `10`: $D023 (background color 2)
  - `11`: $D024 (background color 3)
- Character code bits 5-0 select the character shape (0-63 only)
- Foreground color from Color RAM

**Limitation:** Only 64 unique characters are available (instead of 256), since the top
two bits are repurposed for background selection.

### 3.6 Invalid / Mixed Modes

Setting ECM=1 together with BMM=1 and/or MCM=1 produces invalid modes:

- **ECM+MCM (1/0/1):** Displays black pixels in the display window. The VIC-II still
  performs its normal data fetches and collision detection works, but the graphics
  sequencer outputs black for all foreground pixels.
- **ECM+BMM (1/1/0):** Same as above: black display, data still fetched.
- **ECM+BMM+MCM (1/1/1):** Same: black display.

The border color still shows normally. Sprite display is unaffected. These modes can be
exploited for collision detection without visible graphics.


---

## 4. Screen Layout

### 4.1 Display Areas

The VIC-II output consists of several nested regions:

```
+--------------------------------------------------+
|              Vertical Blanking                     |
|  +--------------------------------------------+  |
|  |              Top Border                     |  |
|  |  +--------------------------------------+  |  |
|  |  |  Left  |   Display Window  |  Right  |  |  |
|  |  | Border |   (320 x 200)    | Border  |  |  |
|  |  |        |   or 304 x 192   |         |  |  |
|  |  +--------------------------------------+  |  |
|  |             Bottom Border                   |  |
|  +--------------------------------------------+  |
|              Vertical Blanking                     |
+--------------------------------------------------+
```

### 4.2 RSEL: 24/25 Row Mode

Controlled by bit 3 of $D011:

| RSEL | Rows | Display window (raster lines) | Height |
|------|------|-------------------------------|--------|
| 0    | 24   | Lines 55-246                  | 192 px |
| 1    | 25   | Lines 51-250                  | 200 px |

### 4.3 CSEL: 38/40 Column Mode

Controlled by bit 3 of $D016:

| CSEL | Cols | Display window (X coordinates) | Width  |
|------|------|--------------------------------|--------|
| 0    | 38   | X = 31-334                     | 304 px |
| 1    | 40   | X = 24-343                     | 320 px |

The 38-column / 24-row modes are primarily used for smooth scrolling: they hide the
edge column/row behind the border so that scrolled-in content is not visible at the
margins.

### 4.4 First/Last Visible Coordinates (6569 PAL)

| Region                | First line | Last line | X start | X end |
|-----------------------|-----------|-----------|---------|-------|
| Vertical blanking     | 300       | 311/0-15  | --      | --    |
| Top border            | 16        | 50        | --      | --    |
| Display (RSEL=1)      | 51        | 250       | 24      | 343   |
| Display (RSEL=0)      | 55        | 246       | 31      | 334   |
| Bottom border         | 251       | 299       | --      | --    |
| Full visible area     | 16        | 299       | 0       | 403   |

### 4.5 Smooth Scrolling

**Vertical scroll (YSCROLL, bits 0-2 of $D011):** Shifts the entire display window down
by 0-7 pixels. Also determines which raster lines become badlines (see section 7.1).

**Horizontal scroll (XSCROLL, bits 0-2 of $D016):** Shifts the entire display window
right by 0-7 pixels.


---

## 5. Memory Banking

### 5.1 VIC-II Address Space

The VIC-II has a 14-bit address bus, allowing it to directly address 16 KB of memory.
The CPU's full 64 KB address space is divided into four 16 KB banks. The active bank is
selected via the CIA-2 chip.

### 5.2 Bank Selection via CIA-2 ($DD00)

Bits 0-1 of CIA-2 Port A ($DD00) select the VIC bank. Note: these bits are active-low
(inverted by external hardware).

| $DD00 bits 1-0 | Bank | Address Range   | Character ROM visible? |
|-----------------|------|-----------------|------------------------|
| %xxxxxx11       | 0    | $0000-$3FFF     | Yes, at $1000-$1FFF    |
| %xxxxxx10       | 1    | $4000-$7FFF     | No                     |
| %xxxxxx01       | 2    | $8000-$BFFF     | Yes, at $9000-$9FFF    |
| %xxxxxx00       | 3    | $C000-$FFFF     | No                     |

**Important:** The bit pattern is inverted. Writing `%xxxxxx00` to $DD00 selects bank 3
($C000-$FFFF), not bank 0.

To select a bank, mask the other bits and set bits 0-1:
```
LDA $DD00
AND #%11111100
ORA #%00000010   ; Select bank 1 ($4000-$7FFF)
STA $DD00
```

### 5.3 Character ROM

In banks 0 and 2, the VIC-II sees the character generator ROM at offsets $1000-$1FFF
(physical addresses $1000-$1FFF in bank 0, $9000-$9FFF in bank 2). This ROM is only
visible to the VIC-II, not to the CPU (which sees RAM at those addresses unless ROM is
banked in via $0001).

Banks 1 and 3 have no character ROM overlay, so you must provide your own character set
in RAM.

### 5.4 Screen RAM, Character Set, and Bitmap Locations

All locations controlled by $D018 are relative to the start of the active VIC bank.

**Screen RAM** (bits 7-4 of $D018):

| Bits 7-4 | Offset | Default bank 0 address |
|----------|--------|------------------------|
| %0000    | $0000  | $0000                  |
| %0001    | $0400  | $0400 (default)        |
| %0010    | $0800  | $0800                  |
| ...      | ...    | ...                    |
| %1111    | $3C00  | $3C00                  |

**Character set** (bits 3-1 of $D018, character modes):

| Bits 3-1 | Offset | In bank 0         |
|----------|--------|--------------------|
| %000     | $0000  | $0000 (RAM)        |
| %001     | $0800  | $0800 (RAM)        |
| %010     | $1000  | $1000 (Char ROM!)  |
| %011     | $1800  | $1800 (Char ROM!)  |
| %100     | $2000  | $2000 (RAM)        |
| %101     | $2800  | $2800 (RAM)        |
| %110     | $3000  | $3000 (RAM)        |
| %111     | $3800  | $3800 (RAM)        |

**Bitmap** (bit 3 of $D018, bitmap modes):

| Bit 3 | Offset |
|-------|--------|
| 0     | $0000  |
| 1     | $2000  |

### 5.5 Sprite Pointers

Sprite data pointers are stored in the last 8 bytes of the 1 KB screen RAM block:

```
Sprite pointer address = Screen RAM base + $03F8 + sprite number
```

For the default screen at $0400, sprite pointers are at $07F8-$07FF.

Each pointer value (0-255) is multiplied by 64 to give the sprite data address (relative
to VIC bank start). So pointer value N means sprite data at `VIC_bank_start + N * 64`.

### 5.6 Capacity Per Bank

| Resource        | Banks 0,2 (with char ROM) | Banks 1,3 (RAM only) |
|-----------------|---------------------------|----------------------|
| Sprite shapes   | 192                       | 256                  |
| Screen locations| 12                        | 16                   |
| Character sets  | 6                         | 8                    |


---

## 6. Raster Interrupt

### 6.1 How It Works

The VIC-II compares the current raster line counter against a programmable compare value.
When they match, the IRST flag (bit 0) in $D019 is set. If ERST (bit 0 of $D01A) is also
set, the VIC-II asserts the IRQ line, which triggers the CPU's IRQ handler.

The 9-bit raster compare value is split across two registers:
- Bits 0-7: $D012
- Bit 8: bit 7 of $D011

### 6.2 Basic Setup

```asm
        SEI                 ; Disable interrupts

        LDA #$7F
        STA $DC0D           ; Disable all CIA-1 interrupts
        LDA $DC0D           ; Acknowledge pending CIA-1 IRQ

        LDA #<irq_handler   ; Set IRQ vector
        STA $0314
        LDA #>irq_handler
        STA $0315

        LDA #$00            ; Clear bit 7 of $D011 (raster bit 8 = 0)
        AND $D011
        ORA #$00
        STA $D011

        LDA #$80            ; Trigger on raster line $80
        STA $D012

        LDA #$01
        STA $D01A           ; Enable raster interrupt

        ASL $D019           ; Acknowledge any pending VIC IRQ

        CLI                 ; Enable interrupts
```

### 6.3 Interrupt Handler

```asm
irq_handler:
        ; Do raster work here (change colors, swap modes, etc.)

        ASL $D019           ; Acknowledge raster IRQ (or: LDA #$FF / STA $D019)

        ; If using KERNAL: jump to $EA31 (full handler) or $EA81 (abbreviated)
        JMP $EA31           ; Restore registers and RTI via KERNAL

        ; If NOT using KERNAL:
        ; PLA / TAY / PLA / TAX / PLA / RTI
```

### 6.4 Chaining Multiple Raster Interrupts

To create split-screen effects, each handler reconfigures the next interrupt:

```asm
irq_top:
        ; Set up graphics for top of screen
        LDA #<irq_bottom
        STA $0314
        LDA #>irq_bottom
        STA $0315
        LDA #$80            ; Next IRQ at line $80
        STA $D012
        ASL $D019
        JMP $EA81           ; Abbreviated exit (no KERNAL keyboard scan)

irq_bottom:
        ; Set up graphics for bottom of screen
        LDA #<irq_top
        STA $0314
        LDA #>irq_top
        STA $0315
        LDA #$00            ; Next IRQ at line $00
        STA $D012
        ASL $D019
        JMP $EA31           ; Full exit (KERNAL keyboard scan once per frame)
```

### 6.5 Timing Uncertainty

When the raster IRQ fires, the CPU may be in the middle of any instruction. Since 6510
instructions take 2-7 cycles, and the CPU can only check IRQ between instructions, there
is a jitter of up to 7 cycles in when the handler actually starts executing. Additionally,
the interrupt response itself takes 7 cycles (push PC and status, fetch vector). Total
uncertainty from IRQ assertion to first handler instruction: approximately 7-15 cycles
(depending on the current instruction).

### 6.6 Stable Raster Technique

To eliminate jitter for cycle-exact raster effects, a common approach uses a double
interrupt:

1. Set up first IRQ a couple of lines before the target line.
2. In the first handler, set up a second IRQ for the very next raster line.
3. Re-enable interrupts (CLI) within the first handler and execute NOPs.
4. The second IRQ fires during the NOP sled, reducing jitter to at most 1 cycle
   (since NOP is exactly 2 cycles).
5. Use a final cycle-counting trick (e.g., a BIT $EA instruction, or reading the
   cycle counter) to eliminate the last cycle of jitter.

After stabilization, the programmer knows the exact cycle position within the raster line
and can write to VIC-II registers at precisely the right moment.


---

## 7. Hardcore Details

### 7.1 Badlines

A **Bad Line Condition** exists at any clock cycle when ALL of the following are true:

1. The current raster line (RASTER) is in the range $30-$F7 (decimal 48-247)
2. The lower 3 bits of RASTER equal YSCROLL (bits 0-2 of $D011)
3. The DEN bit (bit 4 of $D011) was set at some point during raster line $30

When a Bad Line Condition occurs, the VIC-II needs to fetch 40 bytes of character pointer
data (c-accesses) from the video matrix plus 40 nybbles from Color RAM. This requires
exclusive bus access during cycles 15-54 of the line.

**Cycle stealing mechanism:**
- The VIC-II pulls the BA (Bus Available) signal low 3 cycles before it needs the bus
  (at cycle 12).
- During cycles 12-14, the CPU can only complete write cycles (reads are delayed).
- From cycle 15 to cycle 54, the VIC-II performs 40 c-accesses (one per phi2 phase),
  completely locking out the CPU.
- The CPU loses 40-43 cycles, leaving only 20-23 cycles for CPU execution on a badline.

**On non-badlines**, the VIC-II only performs g-accesses (reading character generator or
bitmap data) during phi1 phases, allowing the CPU full use of all 63 cycles (PAL).

**Badline frequency:** In the default configuration (YSCROLL = 3), badlines occur on
raster lines $33, $3B, $43, $4B, ..., $F3, $FB -- every 8th line, corresponding to the
first pixel row of each character row.

### 7.2 Line Timing (6569 PAL)

Each raster line consists of exactly **63 clock cycles** (numbered 1-63). The VIC-II
performs specific memory accesses during each cycle's phi1 and phi2 phases.

**Access types:**

| Symbol | Type     | Description |
|--------|----------|-------------|
| p      | p-access | Sprite pointer read (1 byte) |
| s      | s-access | Sprite data read (1 byte, 3 per sprite per line) |
| c      | c-access | Video matrix + Color RAM read (8+4 = 12 bits) |
| g      | g-access | Character generator or bitmap read (8 bits) |
| r      | r-access | DRAM refresh |
| i      | idle     | Idle access (reads $3FFF) |

**Cycle-by-cycle layout (6569 PAL, simplified):**

```
Cycle:  1    2    3    4    5    6    7    8    9   10   11
phi1:  p3   s3   s3   s3   p4   s4   s4   s4   p5   s5   s5
phi2:  irq  --   --   --   --   --   --   --   --   --   --

Cycle: 12   13   14   15   16   17   18   19   ...  54   55
phi1:  s5   p6   s6   s6   s6   p7   s7   s7   ...  i/g  i/g
phi2:  --   --   VC   c    c    c    c    c    ...  c    --

Cycle: 55   56   57   58   59   60   61   62   63
phi1:  i    i    r    r    r    r    r    p0   p1
phi2:  --   --   --   --   --   --   --   s0   s0
```

(Exact assignments vary depending on which sprites are active. The above is a general
outline.)

**Key cycle assignments (6569 PAL):**
- Cycle 1: p-access for sprite 3 (phi1)
- Cycles 1-10: Sprite 3, 4, 5 data fetches (if enabled)
- Cycle 11-12: Sprite 5/6 boundary
- Cycles 12-14: BA goes low if badline (3-cycle warning)
- Cycle 14 phi1: VC loaded from VCBASE; if badline, RC reset to 0
- Cycles 15-54: c-accesses (badline) + g-accesses (phi1)
- Cycles 55-56: Sprite Y-coordinate comparison
- Cycles 57-61: Five DRAM refresh accesses
- Cycles 58-63 and 1-10 (next line): Sprite p/s-accesses for sprites 0-7

### 7.3 Phi1 / Phi2 Access Patterns

The system clock alternates between two phases:

- **Phi1 (first half):** VIC-II has the bus. It performs g-accesses (pixel data reads),
  sprite reads, refresh, or idle accesses.
- **Phi2 (second half):** Normally the CPU has the bus. On badlines, the VIC-II takes
  phi2 as well for c-accesses.

This interleaving is the core reason the VIC-II can generate video without normally
stalling the CPU. The penalty comes only on badlines (c-accesses) and during sprite
DMA (s-accesses for active sprites at end/start of line).

### 7.4 Sprite DMA Timing

Each active sprite requires bus cycles for DMA:

**Per sprite per raster line:**
- 1 p-access (pointer read): reads the sprite data pointer from screen RAM
- 3 s-accesses (data read): reads 3 bytes of sprite data per line

p-accesses happen on every line for all sprites (even disabled ones -- they just don't
trigger s-accesses). s-accesses happen only when a sprite is displaying (Y coordinate
matches and sprite is enabled).

**BA signal for sprites:**
The VIC-II lowers BA 3 cycles before the first s-access of each sprite. If multiple
sprites have adjacent DMA windows, the CPU stall periods merge.

**Cycle cost per sprite:** 2 cycles of CPU time stolen per active sprite (the p-access
occurs during phi1 and does not cost the CPU anything; the 3 s-accesses span 2 full
cycles where the VIC-II uses both phi1 and phi2).

**Maximum sprite overhead:** With all 8 sprites active, the CPU loses 16 cycles per
line to sprite DMA, plus 3 additional "warning" cycles where BA is low. On a badline
with 8 sprites, the CPU may have as few as 2-4 usable cycles.

**Sprite activation check:** During the first phases of cycles 55 and 56, the VIC-II
checks each sprite to see if MxE (enable bit in $D015) is set and the sprite's Y
coordinate matches the lower 8 bits of the raster counter. If both conditions are met,
DMA is activated for that sprite.

**Sprite counter (MC):** A 6-bit counter (0-63) per sprite tracks which of the 63
bytes of sprite data have been fetched. MC is loaded from MCBASE at cycle 58. MCBASE
increments as MC is advanced through the 3 s-accesses per line. When MC reaches 63,
sprite DMA terminates.

**Y expansion:** Controlled by a flip-flop per sprite. When MxYE is set in $D017, the
flip-flop toggles each line (at cycle 55). When the flip-flop is cleared, MCBASE does
not advance, causing each line of sprite data to be displayed twice.

### 7.5 VC, VCBASE, and RC Counters

These internal counters control video data addressing:

- **VC (Video Counter):** 10-bit counter (0-999). Indexes into the video matrix
  (screen RAM). Incremented after each c/g-access pair in display state.
- **VCBASE (Video Counter Base):** 10-bit register. VC is loaded from VCBASE at the
  start of each line (cycle 14 phi1).
- **RC (Row Counter):** 3-bit counter (0-7). Counts the pixel row within the current
  character line. Used as the 3 low bits of the g-access address.

**State machine rules:**

1. At the beginning of each frame (line $30 area), VCBASE is reset to 0.
2. At cycle 14 phi1 of each line, VC is loaded from VCBASE (and VMLI is cleared).
   If a Bad Line Condition exists, RC is also reset to 0.
3. During cycles 15-54 on a badline, c-accesses fetch character pointers into an
   internal 40x12-bit line buffer.
4. During g-accesses in display state, VC and VMLI are incremented.
5. At cycle 58 phi1: if RC=7, the sequencer transitions to idle state and VCBASE is
   loaded from VC. Otherwise, RC is incremented.
6. If a Bad Line Condition exists at cycle 58, the sequencer remains in (or enters)
   display state regardless of RC.

### 7.6 Display State vs Idle State

The VIC-II's graphics sequencer has two states:

- **Display state:** Active rendering. c-accesses and g-accesses fetch real data from
  the video matrix and character generator/bitmap.
- **Idle state:** g-accesses read from address $3FFF (or $39FF if ECM=1). The fetched
  data is displayed as if the character code were 0 with the current background color.
  No c-accesses occur.

Transition idle-to-display: occurs when a Bad Line Condition is detected.  
Transition display-to-idle: occurs at cycle 58 when RC=7 and no Bad Line Condition.

### 7.7 FLI (Flexible Line Interpretation)

FLI is an advanced technique that forces a badline on every single raster line, allowing
the VIC-II to re-read the video matrix every line instead of every 8th line. This
effectively gives each pixel row its own set of color attributes.

**How it works:**

1. A stable raster interrupt runs cycle-exact code on every raster line.
2. On each line, the code writes to $D011 to change YSCROLL so that
   `(YSCROLL & 7) == (RASTER & 7)`, forcing a Bad Line Condition.
3. The code also changes bits 4-7 of $D018 on each line to point the VIC-II at
   a different 1 KB block of screen RAM, so that each line reads different color data.
4. Since badlines steal 40-43 cycles, only 20-23 cycles remain for the CPU on each
   line, which is just enough to perform the two register writes and loop.

**FLI in bitmap mode:** Typically used with multicolor bitmap mode. The bitmap provides
per-pixel data, while FLI provides per-line color information from 8 different screen
RAM blocks (cycled through via $D018). Combined with Color RAM (which cannot change per
line), this yields 3 freely choosable colors per 4x1 pixel cell instead of per 4x8 cell.

**The FLI bug (gray dots):** When a badline is forced by writing to $D011, the VIC-II
takes 3 cycles to halt the CPU. During those 3 cycles (cycles 12-14), the VIC-II has
not yet started c-accesses, so it reads garbage instead of valid character/color data
for the first 3 characters of the line. This produces a 24-pixel-wide column of
corrupted pixels on the left side of the screen, commonly appearing as gray or random-
colored blocks. This artifact is inherent and cannot be eliminated; it is typically
hidden by positioning a black border or sprites over the left edge.

**Why normal badlines don't have the bug:** On regular badlines, the VIC-II anticipates
the badline naturally (it knows YSCROLL matches RASTER in advance), so the 3-cycle
BA warning happens smoothly. With FLI, the badline is forced unexpectedly by a mid-line
$D011 write, causing the glitch.

**Variants:**
- **AFLI (Advanced FLI):** FLI in standard (hires) bitmap mode. Higher resolution
  (320x200) but only 2 colors per 8x1 cell.
- **IFLI (Interlace FLI):** Alternates between two FLI screens on even/odd frames,
  using interlace to simulate higher color resolution. Requires 50 Hz flicker.
- **BFLI (Big FLI):** Uses the full visible area including borders.

### 7.8 FLD (Flexible Line Distance)

FLD increases the vertical distance between character rows by preventing badlines.

**How it works:** On each raster line where a badline would occur, the code changes
YSCROLL so that it does NOT match `RASTER & 7`. This prevents the Bad Line Condition,
so the VIC-II stays in idle state and displays the border/background color instead of
character data. RC continues incrementing without being reset.

**Effect:** The display "gap" between text lines can be increased by any number of
raster lines. This enables smooth vertical scrolling, wipe effects, and pushing content
down the screen without moving data in memory.

**CPU cost:** Minimal. Only one write to $D011 per line is needed, and since no
badlines occur, the CPU has all 63 cycles available.

### 7.9 Linecrunch

The inverse of FLD: triggering extra badlines to skip character rows.

**How it works:** By forcing a badline when RC != 0 (via YSCROLL manipulation), the
VIC-II resets RC to 0, effectively "crunching" the current character row. VCBASE
advances but the display height shrinks.

**Effect:** Character rows are removed from the display, allowing content to scroll up
without moving memory. Combined with FLD, enables arbitrary vertical scrolling.

### 7.10 DMA Delay / VSP (Variable Screen Position)

VSP manipulates the timing of the VIC-II's transition from idle state to display state
to offset the VCBASE counter.

**How it works:** By writing to $D011 to trigger a badline at a specific cycle within
the c-access window (cycles 15-54), the VIC-II begins fetching character pointers at
a column offset. This shifts the entire screen content horizontally by a number of
character columns.

**The hardware glitch:** The VIC-II changes its address bus output at a moment not
anticipated by the DRAM timing. The address lines may be in an undefined voltage state
when the RAS signal fires, potentially causing RAM corruption. This makes VSP
unreliable on some hardware and can cause crashes.

**"Safe VSP":** Techniques exist to minimize the corruption risk by careful timing and
avoiding accesses to vulnerable memory regions.

### 7.11 Border Removal

**Top/bottom border removal:** At the raster line where the border would turn on
(line $F7/$FA area), switch $D011 from 25-row mode to 24-row mode. The VIC-II thinks
it already started drawing the border (because the 24-row border boundary was passed),
so the border flip-flop is never set. Must be done once per frame at the right line.

**Side border removal:** Same principle with $D016, switching between 40 and 38 column
mode at the right cycle within each raster line. Must be done on every single line, with
cycle-exact timing. Far more CPU-intensive than vertical border removal.

**Content in the border:** Only sprites can be displayed in the border area. The VIC-II
also reads from the last byte of the VIC bank ($3FFF) during border/idle areas, and this
data can be manipulated for simple effects.

### 7.12 Exact Visible Area Coordinates

**6569 PAL:**

| Item                    | Value |
|-------------------------|-------|
| First visible line      | 16    |
| Last visible line       | 299   |
| First X coordinate      | 404 (wraps; left edge of display) |
| Last X coordinate       | 403   |
| Display window Y start  | 51 (RSEL=1) / 55 (RSEL=0) |
| Display window Y end    | 250 (RSEL=1) / 246 (RSEL=0) |
| Display window X start  | 24 (CSEL=1) / 31 (CSEL=0) |
| Display window X end    | 343 (CSEL=1) / 334 (CSEL=0) |
| Cycles per line         | 63    |
| Total lines per frame   | 312   |

**6567R8 NTSC:**

| Item                    | Value |
|-------------------------|-------|
| First visible line      | 16    |
| Last visible line       | 250   |
| Display window Y start  | 51 (RSEL=1) / 55 (RSEL=0) |
| Display window Y end    | 250 (RSEL=1) / 246 (RSEL=0) |
| Cycles per line         | 65    |
| Total lines per frame   | 263   |


---

## 8. References

### Primary Technical Documents

- Christian Bauer, "The MOS 6567/6569 video controller (VIC-II) and its application in
  the Commodore 64" (1996) -- The definitive reverse-engineered reference.
  https://www.cebix.net/VIC-Article.txt
  Also mirrored at: https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt

- MOS Technology, "6567 Video Interface Chip (VIC-II) Preliminary Data Sheet"
  http://archive.6502.org/datasheets/mos_6567_vic_ii_preliminary.pdf

### Register References

- Oxyron, "VIC-II Register Reference"
  https://www.oxyron.de/html/registers_vic2.html

- C64-Wiki, "VIC" (comprehensive wiki article)
  https://www.c64-wiki.com/wiki/VIC

- C64-Wiki, "53272" ($D018 register details)
  https://www.c64-wiki.com/wiki/53272

### Graphics Modes

- C64-Wiki, "Graphics Modes"
  https://www.c64-wiki.com/wiki/Graphics_Modes

- Codebase64, "Built-in Screen Modes"
  https://codebase.c64.org/doku.php?id=base:built_in_screen_modes

### Memory Banking

- C64-Wiki, "VIC bank"
  https://www.c64-wiki.com/wiki/VIC_bank

### Timing and Advanced Techniques

- Marko Makela, "The memory accesses of the 6569/8566"
  https://ist.uwaterloo.ca/~schepers/MJK/ascii/vic2-pal.txt

- Linus Akesson, "VIC 6569/8565 Timing Chart"
  https://www.linusakesson.net/programming/vic-timing/victiming.pdf

- Bumbershoot Software, "Flickering scanlines: The VIC-II and Bad Lines"
  https://bumbershootsoft.wordpress.com/2014/12/06/flickering-scanlines-the-vic-ii-and-bad-lines/

- Bumbershoot Software, "Variable Screen Placement: The VIC-II's Forbidden Technique"
  https://bumbershootsoft.wordpress.com/2015/04/19/variable-screen-placement-the-vic-iis-forbidden-technique/

- Bumbershoot Software, "Flexible Line Distance"
  https://bumbershootsoft.wordpress.com/2015/09/17/flexible-line-distance-fld/

- Bumbershoot Software, "VIC-II Interrupt Timing"
  https://bumbershootsoft.wordpress.com/2015/07/26/vic-ii-interrupt-timing-or-how-i-learned-to-stop-worrying-and-love-unstable-rasters/

- Antimon, "Making stable raster routines"
  https://www.antimon.org/dl/c64/code/stable.txt

- Antimon, "Opening the borders"
  https://www.antimon.org/dl/c64/code/opening.txt

### Raster Interrupts

- C64-Wiki, "Raster interrupt"
  https://www.c64-wiki.com/wiki/Raster_interrupt

- Codebase64, "Introduction to Raster IRQs"
  http://codebase.c64.org/doku.php?id=base:introduction_to_raster_irqs

### Color Palette

- Pepto, "Calculating the color palette of the VIC-II"
  https://www.pepto.de/projects/colorvic/

### General

- Wikipedia, "MOS Technology VIC-II"
  https://en.wikipedia.org/wiki/MOS_Technology_VIC-II

- Dustlayer, "VIC-II for Beginners" (multi-part series)
  https://dustlayer.com/vic-ii/2013/4/22/when-visibility-matters

- C64 OS, "VIC-II and FLI Timing" (3-part series)
  https://c64os.com/post/flitiming1

- Linus Akesson, "Safe VSP"
  https://linusakesson.net/scene/safevsp/index.php
