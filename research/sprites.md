# Commodore 64 Sprite Graphics Reference

## 1. Overview

The VIC-II (MOS 6567/6569) provides eight hardware sprites, officially called
MOBs (Movable Object Blocks). Sprites are graphical objects that can be
positioned independently of the character/bitmap display, with hardware support
for collision detection, priority layering, expansion, and two color modes.

Key characteristics:

- 8 sprites available simultaneously per scanline (numbered 0-7)
- Each sprite is 24 pixels wide x 21 pixels tall
- Two modes: standard (hires) and multicolor
- Independent X/Y positioning across the full screen
- Hardware collision detection (sprite-sprite and sprite-background)
- Hardware priority control (sprite-to-sprite and sprite-to-background)
- Independent double-width and double-height expansion per sprite
- Sprites display in the border area (they are not clipped to the main screen)
- Sprite data is fetched via DMA, stealing CPU cycles

What makes C64 sprites special compared to contemporaries:

- Large sprite size (24x21) relative to the era
- True hardware transparency (bit 0 = transparent, not a "color key")
- Multicolor mode with shared palette for color-rich objects
- Full 320-pixel horizontal range via 9-bit X coordinate
- Sprites remain visible in the border area, enabling border tricks
- Rich set of exploitable hardware quirks discovered by the demo scene

---

## 2. Sprite Basics

### 2.1 Sprite Data Format

Each sprite occupies exactly **64 bytes** in memory, of which 63 bytes contain
pixel data and 1 byte is unused padding (to align on a 64-byte boundary).

The 63 data bytes encode 21 rows of 3 bytes each:

```
Byte 0:  Row 0, pixels  0-7   (MSB = leftmost pixel)
Byte 1:  Row 0, pixels  8-15
Byte 2:  Row 0, pixels 16-23
Byte 3:  Row 1, pixels  0-7
Byte 4:  Row 1, pixels  8-15
Byte 5:  Row 1, pixels 16-23
...
Byte 60: Row 20, pixels  0-7
Byte 61: Row 20, pixels  8-15
Byte 62: Row 20, pixels 16-23
Byte 63: (unused padding byte)
```

Total: 21 rows x 3 bytes = 63 bytes of pixel data + 1 pad = 64 bytes.

Sprite data must be aligned to a 64-byte boundary within the current VIC-II
bank. The 16 KB VIC bank is divided into 256 blocks of 64 bytes each; sprite
data can reside in any of these blocks (block numbers 0-255).

**Important**: The VIC-II addresses memory independently of the CPU. It sees
only 16 KB at a time (selected via CIA2 register $DD00, bits 0-1). ROM
character data at $1000-$1FFF and $9000-$9FFF is visible to the VIC-II but not
the CPU in the same way, which can cause confusion when placing sprite data.

### 2.2 Sprite Pointers

Sprite pointers are located at the **last 8 bytes of screen RAM** (the 1024-byte
video matrix). With the default screen at $0400, the pointers are:

| Sprite | Pointer Address | Default |
|--------|----------------|---------|
| 0      | Screen + $03F8 | $07F8   |
| 1      | Screen + $03F9 | $07F9   |
| 2      | Screen + $03FA | $07FA   |
| 3      | Screen + $03FB | $07FB   |
| 4      | Screen + $03FC | $07FC   |
| 5      | Screen + $03FD | $07FD   |
| 6      | Screen + $03FE | $07FE   |
| 7      | Screen + $03FF | $07FF   |

Each pointer is a **block number** (0-255). The actual address of sprite data
within the VIC bank is:

    sprite_address = pointer_value * 64

For example, pointer value $80 (128) points to address $2000 within the current
VIC bank.

**Pointer fetch timing**: The VIC-II reads sprite pointers once per rasterline
from the video matrix, during the border area of the line. This means changing
a sprite pointer takes effect on the next rasterline where that sprite would
be displayed.

### 2.3 Enabling Sprites

**Register $D015** (53269) -- Sprite Enable Register (MxE)

Each bit enables or disables the corresponding sprite:

| Bit | Sprite |
|-----|--------|
| 0   | Sprite 0 |
| 1   | Sprite 1 |
| 2   | Sprite 2 |
| 3   | Sprite 3 |
| 4   | Sprite 4 |
| 5   | Sprite 5 |
| 6   | Sprite 6 |
| 7   | Sprite 7 |

Set bit to 1 = sprite enabled. Set bit to 0 = sprite disabled.

A disabled sprite consumes no DMA cycles (the pointer is still fetched, but
the 3 data bytes are not).

### 2.4 Positioning

Each sprite has independent X and Y position registers:

| Sprite | X Register   | Y Register   |
|--------|-------------|-------------|
| 0      | $D000 (53248) | $D001 (53249) |
| 1      | $D002 (53250) | $D003 (53251) |
| 2      | $D004 (53252) | $D005 (53253) |
| 3      | $D006 (53254) | $D007 (53255) |
| 4      | $D008 (53256) | $D009 (53257) |
| 5      | $D00A (53258) | $D00B (53259) |
| 6      | $D00C (53260) | $D00D (53261) |
| 7      | $D00E (53262) | $D00F (53263) |

**Y coordinate** (8-bit, range 0-255):
- The visible screen area spans Y=50 to Y=249 (PAL) or Y=50 to Y=229 (NTSC)
- Y=0 corresponds to the top of the border area
- Sprites wrap around vertically; Y=255 is one line above Y=0

**X coordinate** (9-bit, range 0-511):
- The low 8 bits are in the sprite's X register ($D000+2*n)
- The 9th (most significant) bit is in **$D010** (53264), one bit per sprite
- The visible screen area starts at approximately X=24 and ends at X=343
- **$D010 bit layout**: Bit n = MSB of sprite n's X coordinate

To set a sprite's X position to a value > 255, you must set the corresponding
bit in $D010. For example, to place sprite 0 at X=300:

```
    LDA #44         ; 300 - 256 = 44
    STA $D000       ; Low 8 bits of X
    LDA $D010
    ORA #$01        ; Set bit 0 (sprite 0 MSB)
    STA $D010
```

**PAL invisible range**: On the PAL 6569, X positions $1F8-$1FF (504-511) are
never reached by the X raster counter, making sprites at those positions
invisible.

### 2.5 Priority

**Sprite-to-sprite priority** is fixed by hardware:
- Sprite 0 has the highest priority
- Sprite 7 has the lowest priority
- When two sprites overlap, the higher-priority (lower-numbered) sprite's
  non-transparent pixels appear on top

**Sprite-to-background priority** is controllable per sprite:

**Register $D01B** (53275) -- Sprite-to-Background Priority (MxDP)

| Bit Value | Effect |
|-----------|--------|
| 0         | Sprite appears in front of background (foreground) pixels |
| 1         | Sprite appears behind background foreground pixels |

When a sprite is set behind the background, the background's **foreground pixels**
(non-background-color pixels) will occlude the sprite. The sprite remains
visible through background-color pixels.

Definition of "foreground" vs "background" pixels depends on the graphics mode:
- Standard character mode (MCM=0): "0" bits are background, "1" bits are foreground
- Multicolor character mode (MCM=1): "00" and "01" bit-pairs are background;
  "10" and "11" bit-pairs are foreground

---

## 3. Sprite Modes

### 3.1 Standard (Hires) Sprites

In standard mode, each pixel is represented by a single bit:

| Bit | Display |
|-----|---------|
| 0   | Transparent (whatever is behind shows through) |
| 1   | Sprite's individual color |

Resolution: 24 x 21 pixels (full resolution).

Each row is 3 bytes = 24 bits = 24 pixels. The MSB of each byte is the
leftmost pixel.

### 3.2 Multicolor Sprites

**Register $D01C** (53276) -- Sprite Multicolor Mode (MxMC)

Set a bit to 1 to enable multicolor mode for that sprite. Set to 0 for
standard (hires) mode.

In multicolor mode, bits are read in pairs, and each pixel is **double-width**
(2 physical pixels wide):

| Bit Pair | Display |
|----------|---------|
| 00       | Transparent |
| 01       | Sprite multicolor 0 (register $D025) |
| 10       | Sprite's individual color (register $D027+n) |
| 11       | Sprite multicolor 1 (register $D026) |

Resolution: 12 x 21 pixels (each pixel is 2 screen pixels wide). Vertical
resolution remains 21 pixels (unchanged).

Multicolor sprites can be mixed freely with standard sprites on the same
screen. Standard and multicolor sprites can even overlap -- the standard sprite
retains its full 24-pixel horizontal resolution while the multicolor sprite
beneath has 12-pixel resolution.

### 3.3 Color Registers

**Individual sprite colors** (one per sprite):

| Register | Sprite |
|----------|--------|
| $D027 (53287) | Sprite 0 color |
| $D028 (53288) | Sprite 1 color |
| $D029 (53289) | Sprite 2 color |
| $D02A (53290) | Sprite 3 color |
| $D02B (53291) | Sprite 4 color |
| $D02C (53292) | Sprite 5 color |
| $D02D (53293) | Sprite 6 color |
| $D02E (53294) | Sprite 7 color |

**Shared multicolor registers** (used by all multicolor sprites):

| Register | Purpose | Bit Pair |
|----------|---------|----------|
| $D025 (53285) | Sprite multicolor 0 | 01 |
| $D026 (53286) | Sprite multicolor 1 | 11 |

Only the lower 4 bits of each color register are significant (16 colors).

The two shared multicolor registers ($D025, $D026) apply to ALL sprites that
have multicolor mode enabled. This means all multicolor sprites share two of
their three colors; only the individual color ($D027-$D02E) is unique per
sprite.

---

## 4. Sprite Expansion

### 4.1 Double Width

**Register $D01D** (53277) -- Sprite X-Expansion (MxXE)

Set a bit to 1 to double the width of the corresponding sprite. Each pixel
becomes 2 screen pixels wide.

- Standard sprite: 24 pixels -> 48 screen pixels wide
- Multicolor sprite: 12 pixels -> 24 screen pixels wide (each MC pixel = 4
  screen pixels)

The data format does not change. The resolution remains 24x21 (or 12x21 for
multicolor); pixels are simply stretched horizontally.

### 4.2 Double Height

**Register $D017** (53271) -- Sprite Y-Expansion (MxYE)

Set a bit to 1 to double the height of the corresponding sprite. Each row is
displayed on two consecutive rasterlines instead of one.

- Normal sprite: 21 rasterlines tall
- Y-expanded sprite: 42 rasterlines tall

The data format does not change. The resolution remains 24x21 (or 12x21 for
multicolor); rows are simply doubled vertically.

### 4.3 Internal Y-Expansion Mechanism

The VIC-II maintains an internal **expansion flip-flop** per sprite. When
Y-expansion is enabled:

- The flip-flop toggles on each rasterline
- When the flip-flop is in the "repeat" state, the same row of sprite data is
  displayed again (MCBASE does not advance)
- When the flip-flop is in the "advance" state, MCBASE updates and the next
  row is fetched

This mechanism is key to advanced tricks like sprite stretching and sprite
crunching (see Section 7).

### 4.4 Combined Expansion

X and Y expansion can be enabled independently and combined. A sprite with
both expansions enabled is 48 screen pixels wide and 42 rasterlines tall, but
the resolution is still 24x21 (or 12x21 multicolor).

---

## 5. Collision Detection

### 5.1 Sprite-Sprite Collision

**Register $D01E** (53278) -- Sprite-Sprite Collision (M2M) [READ ONLY]

When two or more sprites have overlapping **non-transparent** pixels on the
same rasterline, the bits corresponding to ALL participating sprites are set
in this register.

For example, if sprite 0 and sprite 3 overlap in non-transparent areas:
$D01E reads as %00001001 (bits 0 and 3 set).

Key behaviors:
- **Reading clears the register**. You get only one chance to read the
  collision data; the second read will return 0 (until new collisions occur).
- Collision bits accumulate across scanlines within a frame until the register
  is read.
- Transparent pixels (bit value 0 in hires, bit pair 00 in multicolor) do NOT
  participate in collisions.
- In multicolor mode, bit pair "01" (multicolor 0 color) also does NOT
  participate in collisions.
- At least two sprites must have overlapping non-transparent pixels for any
  bits to be set.

**Collision interrupt**: Bit 2 of $D01A (IRQ enable) can enable an interrupt
on sprite-sprite collision. The corresponding flag is bit 2 of $D019
(interrupt status).

### 5.2 Sprite-Background Collision

**Register $D01F** (53279) -- Sprite-Background Collision (M2D) [READ ONLY]

When a sprite's non-transparent pixels overlap with foreground pixels of the
character/bitmap display, the bit for that sprite is set.

Definition of "foreground" pixels:
- Standard character/bitmap mode (MCM=0): Pixels with value "1"
- Multicolor character/bitmap mode (MCM=1): Pixels with bit-pair "10" or "11"
  (the foreground colors)

Key behaviors:
- **Reading clears the register**, same as sprite-sprite collision.
- The collision register tells you WHICH sprites collided with the background,
  but not WHERE on the sprite the collision occurred.
- In multicolor sprite mode, bit pair "01" does NOT participate in
  sprite-background collisions.

**Collision interrupt**: Bit 1 of $D01A (IRQ enable) can enable an interrupt
on sprite-background collision. The corresponding flag is bit 1 of $D019.

### 5.3 Practical Usage Notes

- Always read the collision register promptly after the frame is drawn (e.g.,
  in a raster interrupt at the bottom of the screen), because reading clears
  it.
- For precise collision detection in games, the hardware registers provide a
  coarse "did they touch?" signal. Most games supplement this with bounding-box
  or distance-based checks in software.
- Collision between specific pairs of sprites cannot be determined directly
  from $D01E alone -- it only tells you which sprites are involved, not which
  pairs collided. For example, if sprites 0, 2, and 5 all have bits set, you
  know at least two of them collided, but not whether all three overlap or just
  two of the three.
- Mid-frame reading of collision registers can be used to determine which part
  of a sprite collided (upper half vs lower half) by reading $D01E/$D01F at a
  rasterline midway through the sprite.
- On expanded sprites, collision detection uses the expanded pixel positions
  (the collision area is the full displayed area).

---

## 6. Sprite Multiplexing

### 6.1 The Fundamental Principle

The VIC-II can display at most 8 sprites per rasterline -- this is a hard
hardware limit. However, there is no limit on how many sprites can appear on
the screen as a whole. By changing sprite positions, pointers, and colors
mid-frame using raster interrupts, the same 8 physical sprites can be reused
at multiple vertical positions, creating the illusion of many more sprites.

This technique is called **sprite multiplexing** and is used in virtually every
C64 game and demo that needs more than 8 on-screen objects.

### 6.2 Requirements

For multiplexing to work:

1. **Vertical separation**: A physical sprite can only be reused after it has
   finished displaying at its first position. A normal sprite is 21 rasterlines
   tall; a Y-expanded sprite is 42 lines tall. There must be at least 1-2 lines
   of gap between usages for register writes.
2. **Raster interrupt timing**: The CPU must be notified (via raster interrupt)
   when it is safe to rewrite sprite registers for the next set of virtual
   sprites.
3. **Sorted sprite list**: Virtual sprites are typically sorted by Y coordinate
   so they can be assigned to physical sprites in top-to-bottom order.

### 6.3 Basic Algorithm

1. **Maintain a list of virtual sprites** with their X, Y, pointer, color, etc.
2. **Sort by Y coordinate** (ascending -- topmost sprites first).
3. **Map virtual sprites to physical sprites** cyclically:
   - Virtual sprites 0-7 -> Physical sprites 0-7
   - Virtual sprite 8 -> reuse physical sprite 0
   - Virtual sprite 9 -> reuse physical sprite 1
   - ...and so on
4. **Set initial sprite registers** before the frame begins (for the first set
   of virtual sprites).
5. **Program raster interrupts** to fire a few rasterlines before each
   subsequent group of virtual sprites needs to appear. In the interrupt handler,
   update the physical sprite's Y position, X position, pointer, and color to
   the next virtual sprite's values.
6. **Reject sprites** that would violate the 8-per-scanline limit:
   ```
   if (virtual_y[next] - virtual_y[next - 8] < 21) then reject
   ```

### 6.4 Sorting Algorithms

Several sorting approaches are used in practice:

**Bubble Sort** (O(n^2)):
Simple to implement. Maintains an index array. Suitable for small sprite
counts (< 16). Swaps Y coordinates and index entries.

**Linear Search / Selection Sort** (O(n^2)):
Repeatedly finds the minimum Y from the unsorted portion. No swapping needed;
clear insertion point for rejection logic.

**Continuous Insertion Sort** ("Ocean algorithm", recommended):
Maintains a persistent sorted order array that is incrementally updated each
frame. Extremely fast when sprite positions change minimally between frames
(which is the common case in games). Used in many Ocean/Imagine games
(Green Beret, Midnight Resistance). This is the recommended approach for game
development.

**Bucket Sort / Y/8 Bucketing** (O(n)):
Divides the screen into buckets by Y coordinate. Fast but does not guarantee
correct ordering within buckets. Suitable when approximate ordering is
acceptable.

**Radix Sort** (O(n)):
Two-pass bucket sort ensuring correct order. Fast but requires more memory and
complex implementation.

### 6.5 Raster Interrupt Strategies

**Pre-sprite interrupt (writing before display)**:
Fire the interrupt a few rasterlines before the next virtual sprite needs to
appear. Write Y coordinate first (most time-critical), then X, pointer, and
color. Number of lead lines depends on how many sprites must be rewritten:
- 1 sprite: ~2 lines lead time
- 8 sprites: ~10-12 lines lead time

**Post-sprite interrupt (writing after previous display completes)**:
Fire the interrupt just after the previous set of virtual sprites finishes
displaying. Write new values for the next reuse. Write Y coordinate last
(to prevent partial display if timing slips). Safer against glitches but may
miss sprites in dense formations.

### 6.6 Implementation Details

**$D010 handling**: The MSB register for X coordinates must be carefully
managed when rewriting sprite positions. Precalculate OR and AND masks for
each physical sprite to avoid slow bit manipulation in the interrupt handler.

**Double-buffering**: Allocate two sorted sprite arrays. Sort the next frame's
data in one buffer while the current frame displays from the other. Toggle
buffers each frame. This prevents sorting from interfering with display.

**Separate code paths**: For maximum speed, use separate (unrolled) code for
each physical sprite rather than a loop with sprite index calculations.

**Timing safety check**: Prevent missed interrupts:
```
    STA $D012       ; Set next raster interrupt
    SEC
    SBC #$03
    CMP $D012       ; Are we already past it?
    BCC fire_now    ; Yes, handle immediately
```

### 6.7 Typical Limits

- **Simple multiplexer**: 16 sprites (two zones of 8)
- **General multiplexer**: 20-24 sprites comfortably
- **Aggressive multiplexer**: 30+ sprites possible with careful timing
- **Theoretical maximum**: Limited by CPU time for register writes and sorting;
  practical game use rarely exceeds 24-32 virtual sprites
- **Hard limit**: Still max 8 sprites on any single rasterline

### 6.8 Common Artifacts

- **Flicker**: When interrupts fire late and register writes overlap with
  sprite display, causing partial/corrupt sprite rendering
- **Missing sprites**: When there is insufficient time to write registers
  before the raster reaches the sprite's Y position
- **Slowdown**: When excessive virtual sprites consume too much CPU time for
  sorting and interrupt handling
- **Y-gap**: Multiplexed sprites must have vertical separation; sprites that
  need to be at the same Y position (or within 21 lines of each other) must
  use different physical sprite slots

---

## 7. Advanced Techniques

### 7.1 Sprite Stretching (Y-Expansion Toggling)

By toggling the Y-expansion bit ($D017) on and off during sprite display, you
can stretch a sprite to arbitrary heights.

**How it works**: The VIC-II's internal expansion flip-flop controls whether
MCBASE advances. If you enable Y-expansion while a sprite row is being
displayed, the flip-flop resets, causing the same row to be repeated. By
continuously toggling Y-expansion on and off each rasterline, you can repeat
any sprite row indefinitely.

**Practical use**: Stretching creates a single row repeated many times,
producing a horizontally-striped pattern. Combined with sprite pointer changes
on each rasterline, this can create tall sprite columns. This technique is
simpler than sprite crunching and requires less precise timing.

### 7.2 Sprite Crunching

Sprite crunching is a VIC-II hardware glitch that allows non-sequential access
to sprite data, effectively shrinking or distorting sprites in the vertical
dimension.

**The mechanism**: On each rasterline, the VIC-II checks whether to advance
the sprite's internal data counter (MCBASE). The Y-expansion flip-flop
controls this. If Y-expansion is disabled at precisely **cycle 15** of a
rasterline (4-cycle write completing at cycle 15), the internal MCBASE register
gets corrupted according to:

    MCBASE_new = ((MC | MCBASE) & $15) | ((MC & MCBASE) & $2A)

This formula produces a bitwise combination of the MC and MCBASE counters,
causing the sprite to skip rows, repeat rows, or read data from unexpected
offsets within the 63-byte sprite data.

**Requirements**:
- Cycle-exact timing (the $D017 write must complete at exactly cycle 15)
- Knowledge of the MCBASE/MC state to predict the resulting pattern
- Bad lines must be suppressed (via $D011 manipulation) to maintain timing

**Effects achievable**:
- Shrinking sprites vertically (fewer than 21 rows displayed)
- Variable-height sprites from the same data
- Non-linear access patterns through sprite memory
- Combined with pointer changes: complex vertical effects

**MISC (Massively Interleaved Sprite Crunch)**: An advanced technique by Linus
Akesson that combines sprite crunching with collision-register-based
computation. Starting from MCBASE offset $35, eight distinct crunch loops of
different lengths (1, 13, 14, 17, 18, 19, 20, 21 rasterlines) are possible,
enabling variable sprite heights from a single pixel to full 21-line sprites.
MISC encodes crunch schedules into the rightmost sprite pixels and uses
collision detection ($D01F) to automatically output Y-expand bit patterns to
the hardware register.

### 7.3 Sprites in the Border Area

Sprites are not clipped to the main screen area -- they can appear in the
border region. However, the border color normally covers them. Opening the
borders allows sprites to become visible there.

**Top/bottom border**: Manipulate bit 3 of $D011 (RSEL -- row select):
1. During the last visible character row (rasterlines $F2-$FA), clear RSEL
   (switch from 25-row to 24-row mode). This causes the VIC-II to think the
   bottom border should already be active, but it misses the trigger.
2. Before the top of the next frame, set RSEL again (back to 25-row mode).
3. The result: the top/bottom borders are "open" and sprites are visible there.
4. This must be done every frame. Timing is relatively loose (anywhere within
   the specified rasterline range).

**Side borders**: Similar technique using bit 3 of $D016 (CSEL -- column select):
1. Switch from 40-column to 38-column mode when the VIC-II checks for the
   right border edge.
2. Switch back to 40-column mode immediately after.
3. This must be done on **every single rasterline**, requiring stable raster
   timing.
4. Much more demanding than top/bottom border opening due to per-line
   requirements and bad line interference.

**$3FFF ghost byte**: When the borders are open, the VIC-II fetches data from
the last byte of its 16 KB address space ($3FFF) and displays it as background
garbage. Set this byte to $00 to prevent visible artifacts.

### 7.4 AGSP (Any Given Screen Position)

AGSP combines VSP (Variable Screen Position) and line crunching techniques to
achieve scrolling effects with more colors and faster speeds than normally
possible. It is primarily a character/bitmap technique rather than a pure sprite
trick, but sprites are often used in conjunction with AGSP for overlay layers
in demos and games (e.g., Fred's Back, Jim Slim).

### 7.5 Sprite Overlay Technique

By overlaying a hires sprite on top of a multicolor sprite (using sprite
priority -- lower-numbered sprite on top), you can achieve the appearance of
more colors with higher detail:

- The multicolor sprite provides 3 colors + transparency at 12x21 resolution
- The hires sprite provides a single-color high-resolution outline at 24x21
- The combined effect gives the impression of 4+ colors at full resolution

This technique was standard practice from the mid-1980s onward. Notable
examples:
- **Summer Games** (1984): Hires overlay for athlete sprites
- **Daley Thompson's Decathlon** (1984): Hires billboards + overlay athletes
- **Last Ninja**, **Robocop**, **Batman the Movie**: Multi-sprite overlays

More elaborate setups use 1 multicolor + 3 hires sprites to achieve 6 visible
colors from 4 physical sprites, leaving 4 sprites for a multiplexer.

### 7.6 Sprite Underlay Technique

Sprites set behind the background ($D01B bit = 1) can serve as "underlays":
- The sprite is visible only where the background has background-colored pixels
- Background foreground pixels occlude the sprite
- Used in advanced graphics modes like NUFLI to add extra colors beneath the
  bitmap display

### 7.7 Sprite-Based Scrolling Overlays

Sprites can be used as a parallax scrolling layer:
- Position sprites over a scrolling background to create depth
- Since sprites move independently of the character/bitmap display, they
  provide a free additional scrolling layer
- Commonly used for status bars, score displays, and floating UI elements that
  must not scroll with the background

### 7.8 Sprite Animation

Animation is achieved by cycling through multiple sprite data blocks:

1. Store animation frames as consecutive 64-byte blocks in memory
2. Each frame, increment (or update) the sprite pointer to point to the next
   frame's data block
3. Use a frame counter or timer to control animation speed

For example, with 4 animation frames at $2000, $2040, $2080, $20C0:
- Pointer values cycle through $80, $81, $82, $83
- The sprite pointer is simply incremented each animation step

Best practice: update sprite pointers and positions during the vertical blank
(via a raster interrupt when the raster is in the border area) to avoid
visual tearing.

---

## 8. Hardcore Details

### 8.1 Sprite DMA Timing

The VIC-II fetches sprite data by stealing bus cycles from the CPU. This
happens on every rasterline where a sprite is active (its Y coordinate falls
within the sprite's display range).

#### 8.1.1 Memory Access Types

The VIC-II performs two types of sprite memory access per rasterline:

- **p-access** (pointer access): 1 half-cycle to read the sprite data pointer
  from the last 8 bytes of the video matrix. This happens for ALL 8 sprites on
  EVERY rasterline, regardless of whether the sprite is enabled or active.
- **s-access** (sprite data access): 3 half-cycles to read the 3 bytes of
  sprite data for the current row. This happens only for sprites with active
  DMA.

Each sprite thus requires 1 p-access + 3 s-accesses = 4 memory accesses when
active, or just 1 p-access when inactive.

#### 8.1.2 Access Cycle Positions (PAL 6569)

On the PAL 6569, each rasterline has 63 clock cycles (numbered 1-63). Sprite
accesses occur during the horizontal blanking interval (border area), split
across the end of one line and the beginning of the next:

**Sprites 0-2** are fetched at the end of the rasterline (right border area):

| Sprite | p-access | s-accesses (3 bytes) |
|--------|----------|---------------------|
| 0      | Cycle 58 | Cycles 59-60 (phi1 of 59, phi2 of 59, phi1 of 60) |
| 1      | Cycle 60 | Cycles 61-62 |
| 2      | Cycle 62 | Cycles 63, 1 (wraps to next line) |

**Sprites 3-7** are fetched at the beginning of the next rasterline (left
border area):

| Sprite | p-access | s-accesses (3 bytes) |
|--------|----------|---------------------|
| 3      | Cycle 1  | Cycles 2-3 |
| 4      | Cycle 3  | Cycles 4-5 |
| 5      | Cycle 5  | Cycles 6-7 |
| 6      | Cycle 7  | Cycles 8-9 |
| 7      | Cycle 9  | Cycles 10-11 |

Note: The s-accesses occupy 3 consecutive half-cycles directly following the
p-access. Since the VIC-II clock runs at twice the system clock frequency,
2 half-cycles fit in each full cycle.

#### 8.1.3 BA Signal and Cycle Stealing

Before stealing cycles, the VIC-II must warn the CPU by pulling the BA (Bus
Available) signal low **3 cycles in advance**. During these 3 warning cycles,
the CPU can still perform write operations but will halt as soon as it
encounters a read instruction.

**Cost per sprite depends on context**:

- **Lone sprite (no adjacent sprites active)**: 5 CPU cycles stolen
  (3 warning + 2 data fetch)
- **Consecutive sprite following an active sprite**: 2 CPU cycles stolen
  (the VIC-II already holds the bus)
- **Sprite following one inactive slot after an active sprite**: 4 CPU cycles
  stolen (VIC idles for 2 cycles rather than releasing and reclaiming the bus)

**Practical cycle cost formula** (from sprite enable bit pattern, MSB=sprite 7):
Replace bit patterns in the enable register:
- `100` -> 5 cycles
- `10` -> 4 cycles  
- `1` (leading or isolated) -> 5 cycles (first transition)
- `1` (following another 1) -> 2 cycles

**Examples**:
| Active Sprites     | Pattern    | CPU Cycles Stolen |
|-------------------|------------|-------------------|
| None              | 00000000   | 0                 |
| Sprite 0 only     | xxxxxxx1   | 5                 |
| Sprites 0,1       | xxxxxx11   | 5+2 = 7           |
| Sprites 0,1,2     | xxxxx111   | 5+2+2 = 9         |
| Sprites 0,2       | xxxxx101   | 5+4 = 9           |
| All 8 sprites     | 11111111   | 5+2+2+2+2+2+2+2 = 19 |
| Sprites 0,2,4,6   | x1x1x1x1  | 5+4+4+4 = 17     |

**Key insight**: Sprites 0 and 1 together cost the same (9 cycles) as sprites
0 and 2 alone (5+4=9). The VIC-II holds the bus during inactive sprite slots
between active ones rather than releasing and reclaiming it.

#### 8.1.4 Available CPU Cycles per Line

| Condition                        | CPU Cycles Available |
|----------------------------------|---------------------|
| Normal line, 0 sprites           | 63                  |
| Normal line, 8 sprites           | 44 (63 - 19)        |
| Bad line, 0 sprites              | 23                  |
| Bad line, 8 sprites              | 4-7                 |

On a bad line with all 8 sprites active, the CPU may have as few as 4 usable
cycles. This severely limits what can be done on such lines.

### 8.2 Sprite Display Pipeline

The sprite display follows a precise per-rasterline sequence:

1. **Cycle 55-56** (first phases): The VIC-II checks all 8 sprites:
   - Is the sprite enabled ($D015 bit set)?
   - Does the sprite's Y coordinate match the current rasterline?
   - If both true: DMA is activated for this sprite (if not already active),
     and MCBASE is reset to 0.
   - The Y-expansion flip-flop is also examined here.

2. **Cycle 58 - cycle 11** (spanning the line boundary): Sprite pointer and
   data fetches occur for all 8 sprites (as detailed in 8.1.2).

3. **Cycle 58** (first phase): MC (MOB Data Counter) is loaded from MCBASE for
   all sprites, preparing for the next line's display.

4. **Cycle 15** (first phase): If the expansion flip-flop is set, MCBASE is
   incremented by 2.

5. **Cycle 16** (first phase): MCBASE is incremented by 1. If MCBASE equals
   63, sprite DMA is turned off and the sprite display is disabled.

6. **During visible area**: The 24 pixels of sprite data are shifted out from
   the sprite's internal shift register, aligned to the sprite's X position.

**Important timing note**: The sprite Y coordinate stored in the register must
be **1 less** than the desired Y position of the first sprite line, because the
Y-coordinate comparison at cycle 55-56 enables display starting from the
following line.

### 8.3 Y-Expansion and the Flip-Flop

The Y-expansion mechanism uses a per-sprite flip-flop:

- If MxYE (Y-expansion enable bit) is set in cycle 56, and DMA is active for
  that sprite, the flip-flop is **inverted**.
- The flip-flop determines whether MCBASE advances:
  - Flip-flop clear: MCBASE does not advance -> same row displayed again
  - Flip-flop set: MCBASE advances -> next row displayed
- This naturally produces the double-height effect (each row displayed twice).

**Exploitation**: By toggling MxYE at specific times:
- Setting MxYE resets the flip-flop -> repeat current row (stretching)
- Clearing MxYE at cycle 15 -> corrupt MCBASE (crunching)

### 8.4 Sprite Crunch: Exact Mechanism

The sprite crunch occurs when Y-expansion is disabled at a specific cycle:

1. The sprite is currently displaying (DMA active).
2. Y-expansion was enabled on the previous line (flip-flop is in a known state).
3. A write to $D017 clearing the MxYE bit completes at **cycle 15**.
4. The VIC-II attempts to increment MCBASE, but the expansion flip-flop state
   creates an inconsistent update. Instead of the normal MCBASE = MC transfer,
   the result is:

   ```
   MCBASE = ((MC | MCBASE) & $15) | ((MC & MCBASE) & $2A)
   ```

5. This corrupted MCBASE causes the sprite to jump to an unexpected row on the
   next scanline.

The crunch values depend on the current MCBASE, allowing predictable (if
complex) patterns to be generated. Starting from specific initial offsets,
different crunch sequences produce loops of varying lengths.

### 8.5 Sprite Pointer Fetch Timing Details

The pointer fetch (p-access) occurs on **every** rasterline for all 8 sprites,
regardless of whether the sprite is enabled or has active DMA. The VIC-II
always reads the pointer; it simply discards the result for inactive sprites.

This has practical implications:
- Changing a sprite pointer takes effect on the next rasterline (after the
  p-access for that sprite occurs)
- For sprites 0-2 (fetched at cycles 58-62 at the end of the line), the
  pointer must be updated before cycle 58 of the current line to affect the
  next line's display
- For sprites 3-7 (fetched at cycles 1-11 at the beginning of the next line),
  the pointer change window extends through the end of the current line

This timing asymmetry is important for multiplexing and per-line sprite pointer
changes.

### 8.6 Interactions Between Sprite DMA and Bad Lines

Bad lines and sprite DMA both steal CPU cycles, and their effects combine:

- A bad line steals approximately 40 CPU cycles (for character pointer and
  color RAM fetches)
- Sprite DMA can steal up to 19 additional cycles
- On a bad line with 8 active sprites, the CPU may be left with only **4-7
  cycles** for the entire rasterline
- The VIC-II does not release the bus between bad line accesses and sprite
  accesses if they are adjacent in timing

**Practical impact for multiplexing**: If a multiplexer interrupt needs to
rewrite sprite registers, and the target rasterline coincides with a bad line,
there may be insufficient CPU time to complete the writes. Solutions include:
- Avoiding sprite reuse zones that overlap with bad lines
- Suppressing bad lines (via $D011 scroll manipulation) in critical areas
- Using FLD (Flexible Line Distance) to shift bad lines away from multiplexer
  zones

### 8.7 Maximum Sprites with Multiplexing

Theoretical and practical limits:

**Per-scanline**: 8 (absolute hardware limit, cannot be exceeded)

**Per-frame, with multiplexing**:
- The screen has approximately 200 visible rasterlines (PAL)
- A non-expanded sprite occupies 21 rasterlines
- With 1 line gap for register writes: 200 / 22 = ~9 reuse zones
- 9 zones x 8 sprites = 72 theoretical sprites per frame
- In practice, sorting overhead, interrupt latency, and irregular sprite
  placement reduce this to 20-30 sprites for most game engines
- Demo-optimized multiplexers with static or semi-static sprite layouts can
  push to 50+ sprites

**CPU time constraint**: Each reuse zone requires approximately 40-60 cycles
of interrupt handling per sprite being rewritten. With all 8 sprites rewritten
per zone plus sorting overhead, the practical limit is governed by available
CPU time rather than rasterline count.

---

## 9. VIC-II Sprite Register Summary

| Address | Name  | Description                              | R/W |
|---------|-------|------------------------------------------|-----|
| $D000   | M0X   | Sprite 0 X position (low 8 bits)         | R/W |
| $D001   | M0Y   | Sprite 0 Y position                      | R/W |
| $D002   | M1X   | Sprite 1 X position (low 8 bits)         | R/W |
| $D003   | M1Y   | Sprite 1 Y position                      | R/W |
| $D004   | M2X   | Sprite 2 X position (low 8 bits)         | R/W |
| $D005   | M2Y   | Sprite 2 Y position                      | R/W |
| $D006   | M3X   | Sprite 3 X position (low 8 bits)         | R/W |
| $D007   | M3Y   | Sprite 3 Y position                      | R/W |
| $D008   | M4X   | Sprite 4 X position (low 8 bits)         | R/W |
| $D009   | M4Y   | Sprite 4 Y position                      | R/W |
| $D00A   | M5X   | Sprite 5 X position (low 8 bits)         | R/W |
| $D00B   | M5Y   | Sprite 5 Y position                      | R/W |
| $D00C   | M6X   | Sprite 6 X position (low 8 bits)         | R/W |
| $D00D   | M6Y   | Sprite 6 Y position                      | R/W |
| $D00E   | M7X   | Sprite 7 X position (low 8 bits)         | R/W |
| $D00F   | M7Y   | Sprite 7 Y position                      | R/W |
| $D010   | MSIGX | Sprite X position MSBs (bit n = sprite n)| R/W |
| $D015   | MxE   | Sprite enable (bit n = sprite n)         | R/W |
| $D017   | MxYE  | Sprite Y expansion (bit n = sprite n)    | R/W |
| $D01B   | MxDP  | Sprite-bg priority (0=front, 1=behind)   | R/W |
| $D01C   | MxMC  | Sprite multicolor mode (bit n = sprite n)| R/W |
| $D01D   | MxXE  | Sprite X expansion (bit n = sprite n)    | R/W |
| $D01E   | M2M   | Sprite-sprite collision (read clears)    | R   |
| $D01F   | M2D   | Sprite-background collision (read clears)| R   |
| $D025   | MM0   | Sprite multicolor 0 (shared, bit pair 01)| R/W |
| $D026   | MM1   | Sprite multicolor 1 (shared, bit pair 11)| R/W |
| $D027   | M0C   | Sprite 0 individual color                | R/W |
| $D028   | M1C   | Sprite 1 individual color                | R/W |
| $D029   | M2C   | Sprite 2 individual color                | R/W |
| $D02A   | M3C   | Sprite 3 individual color                | R/W |
| $D02B   | M4C   | Sprite 4 individual color                | R/W |
| $D02C   | M5C   | Sprite 5 individual color                | R/W |
| $D02D   | M6C   | Sprite 6 individual color                | R/W |
| $D02E   | M7C   | Sprite 7 individual color                | R/W |

Related registers (not sprite-specific but used with sprites):

| Address | Name  | Description                              | R/W |
|---------|-------|------------------------------------------|-----|
| $D011   | CR1   | VIC control register 1 (RSEL in bit 3)   | R/W |
| $D012   | RASTER| Raster line counter (read/write)         | R/W |
| $D016   | CR2   | VIC control register 2 (CSEL in bit 3)   | R/W |
| $D019   | IRQST | Interrupt status register                 | R/W |
| $D01A   | IRQEN | Interrupt enable register                 | R/W |

---

## 10. References

### Primary Technical Documentation

- Christian Bauer, "The MOS 6567/6569 video controller (VIC-II) and its
  application in the Commodore 64" (1996) -- the definitive VIC-II reference
  https://www.cebix.net/VIC-Article.txt

- Marko Makela, "The memory accesses of the 6569 / 8566" -- detailed PAL
  timing diagrams
  https://ist.uwaterloo.ca/~schepers/MJK/ascii/vic2-pal.txt

- Pasi 'Albert' Ojala, "Missing Cycles" -- sprite DMA cycle stealing analysis
  http://www.antimon.org/dl/c64/code/missing.txt

### Sprite Multiplexing

- Cadaver (Lasse Oorni), "Sprite multiplexing" -- comprehensive multiplexer
  guide with algorithms and code
  https://cadaver.github.io/rants/sprite.html

- Codebase64 Wiki, "Sprite Multiplexing" -- algorithms and implementation
  https://codebase.c64.org/doku.php?id=base:sprite_multiplexing

- Kodiak64, "Toggle-plexing Sprites for Games on the Commodore 64"
  https://kodiak64.com/blog/toggleplexing-sprites-c64

### Advanced Techniques

- Linus Akesson, "Massively Interleaved Sprite Crunch (MISC)"
  https://www.linusakesson.net/scene/lunatico/misc.php

- Codebase64 Wiki, "Sprite Crunching"
  https://codebase64.org/doku.php?id=base:sprite-crunching

- Pasi 'Albert' Ojala, "Opening the borders"
  https://www.antimon.org/dl/c64/code/opening.txt

### General References

- C64-Wiki, "Sprite" article
  https://www.c64-wiki.com/wiki/Sprite

- Dustlayer, "VIC-II for Beginners Part 5: Bringing Sprites in Shape"
  https://dustlayer.com/vic-ii/2013/4/28/vic-ii-for-beginners-part-5-bringing-sprites-in-shape

- Bumbershoot Software, "Spritework on the Commodore 64"
  https://bumbershootsoft.wordpress.com/2026/02/21/spritework-on-the-commodore-64/

- Bumbershoot Software, "Sprites and Raster Timing on the C64"
  https://bumbershootsoft.wordpress.com/2016/02/05/sprites-and-raster-timing-on-the-c64/

- emudev.de, "Sprites, more CPU timings and IRQ quirks"
  https://emudev.de/q00-c64/sprites-more-cpu-timings-and-irq-quirks/

- Retro Game Coders, "How C64 Sprites Work"
  https://retrogamecoders.com/how-c64-sprites-work/

- C64 OS, "VIC-II and FLI Timing (2/3)"
  https://c64os.com/post/flitiming2
