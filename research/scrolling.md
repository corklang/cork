# Scrolling Techniques on the Commodore 64

## 1. Overview

The Commodore 64's VIC-II graphics chip (MOS 6567 for NTSC, MOS 6569 for PAL)
provides dedicated hardware support for smooth pixel-level scrolling in both the
horizontal and vertical directions. This hardware capability, combined with the
C64's character-based display architecture, made the platform exceptionally well
suited for scrolling games and demo effects throughout the 1980s and 1990s.

Scrolling is fundamental to C64 software:

- **Games**: Horizontal shooters (Katakis, R-Type), vertical shooters
  (Lightforce, 1943), platformers (Turrican, Creatures, Mayhem in
  Monsterland), and multi-directional explorers (Fort Apocalypse, Metroid)
  all depend on smooth, efficient scrolling.
- **Demos**: The demoscene pushed scrolling far beyond what game developers
  typically needed, producing tech-tech effects, DYCP/DXYCP scrollers,
  full-screen bitmap scrollers, and elaborate parallax compositions.

The VIC-II provides two key mechanisms:

1. **Fine scroll registers** -- pixel-level offsets (0-7 pixels) in both X
   and Y, controlled via registers `$D016` and `$D011`.
2. **Reduced display width/height modes** -- 38-column and 24-row modes that
   mask the screen edges, hiding the visual artifacts that occur when
   character data is shifted.

All practical scrolling on the C64 combines these hardware features with
software techniques for shifting screen data, managing color RAM, and
synchronizing updates to the raster beam.


## 2. Hardware Smooth Scrolling

### 2.1 Horizontal Smooth Scroll: $D016

Register `$D016` (decimal 53270) controls horizontal scrolling via its lowest
three bits (bits 0-2), providing an X-scroll offset of 0 to 7 pixels:

| Bits  | Function                                          |
|-------|---------------------------------------------------|
| 0-2   | XSCROLL: horizontal fine scroll (0-7 pixels)      |
| 3     | CSEL: 0 = 38-column mode, 1 = 40-column mode      |
| 4     | MCM: multicolor mode enable                        |
| 5-7   | Unused on the C64                                  |

Writing a value of 0 to bits 0-2 places the screen at its leftmost position.
Writing 7 shifts the entire display 7 pixels to the right. The VIC-II reloads
character data with a delay of 0-7 pixels based on the XSCROLL value, effectively
shifting the display window relative to the underlying character grid.

### 2.2 Vertical Smooth Scroll: $D011

Register `$D011` (decimal 53265) controls vertical scrolling via its lowest
three bits (bits 0-2), providing a Y-scroll offset of 0 to 7 pixels:

| Bits  | Function                                          |
|-------|---------------------------------------------------|
| 0-2   | YSCROLL: vertical fine scroll (0-7 pixels)         |
| 3     | RSEL: 0 = 24-row mode, 1 = 25-row mode            |
| 4     | DEN: display enable                                |
| 5     | BMM: bitmap mode                                   |
| 6     | ECM: extended color mode                           |
| 7     | RST8: bit 8 of the raster compare register         |

The YSCROLL value determines when "bad lines" occur. The VIC-II triggers a
bad line when `(RASTER & 7) == YSCROLL` for raster lines `$30` through `$F7`
(provided DEN was set during raster line `$30`). Changing YSCROLL effectively
moves the entire character grid vertically by the specified number of pixels.

### 2.3 The 38-Column and 24-Row Modes

When scrolling, intermediate pixel offsets expose partially-shifted character
data at the screen edges. To hide this:

- **38-column mode** (bit 3 of `$D016` = 0): the left and right borders each
  expand inward by 4 pixels, hiding one character column's worth of garbage on
  each side. The visible area shrinks from 320 to 304 pixels wide.
- **24-row mode** (bit 3 of `$D011` = 0): the top and bottom borders each
  expand inward by 4 pixels, hiding one character row's worth of garbage at
  top and bottom. The visible area shrinks from 200 to 192 pixels tall.

Most games accept this tradeoff. Demos and some ambitious games use
techniques to avoid it (see Section 4).

### 2.4 The Scroll-and-Shift Technique

The fundamental scrolling algorithm combines hardware fine scrolling with
software coarse scrolling:

**Horizontal scroll-left example (the most common pattern):**

1. Set XSCROLL to 7 (rightmost offset).
2. Each frame, decrement XSCROLL by the scroll speed (typically 1 pixel/frame).
3. When XSCROLL reaches 0, the screen has moved 7 pixels left via hardware.
4. Now perform a "coarse scroll": shift all screen RAM data one character
   position to the left (i.e., copy byte N+1 to byte N for all 1000 screen
   positions). Fill the rightmost column with new map data.
5. Also shift color RAM one position to the left and fill the rightmost column.
6. Reset XSCROLL to 7 and continue.

For scrolling right, the sequence is reversed: start at XSCROLL=0, increment
to 7, then shift screen data rightward.

**Vertical scrolling** uses the same principle with YSCROLL and shifts rows
instead of columns.

The key insight: the hardware provides 8 pixels of smooth motion (one full
character width), then software repositions the data and resets the register.


## 3. Screen Shifting Methods

The coarse scroll (shifting screen data by one character) is the most
CPU-intensive part of scrolling. Several approaches exist, each with different
tradeoffs.

### 3.1 Direct Screen RAM Copy (Memcpy Approach)

The simplest method copies each byte in screen RAM one position in the scroll
direction:

```
; Shift screen left by one character (simplified)
    ldx #0
loop:
    lda $0401,x     ; load from position + 1
    sta $0400,x     ; store to position
    inx
    cpx #250        ; partial row count
    bne loop
    ; ... repeat for remaining rows
```

A full 40x25 screen requires moving 1000 bytes. At roughly 8 cycles per
byte (LDA abs,X + STA abs,X), this takes approximately 8000 cycles -- a
significant portion of the ~19,656 cycles available per PAL frame (and
only ~17,095 on NTSC).

**Optimization: unrolled loops.** Fully unrolling the copy eliminates the
loop overhead (INX, CPX, BNE) and reduces the per-byte cost to ~6 cycles
(LDA abs + STA abs with hardcoded addresses). An unrolled screen shift
takes around 6000 cycles but consumes roughly 6 KB of code space.

### 3.2 Moving Character Definitions Instead of Screen Data

Instead of moving 1000 bytes of screen RAM, an alternative is to rotate the
character set data. If the screen always displays the same character codes in
the same positions (e.g., characters 0-39 on row 0, 40-79 on row 1, etc.),
scrolling can be achieved by modifying the bitmap data within those character
definitions.

This approach trades screen RAM copies for character ROM manipulation. It
works well for simple repeating patterns (e.g., terrain in a side-scroller)
but becomes complex for varied content, since each screen position is
effectively locked to a fixed character code.

### 3.3 Double Buffering Screen RAM

Double buffering uses two separate 1 KB screen RAM areas. While one is
displayed by the VIC-II, the other is updated off-screen:

1. Display buffer A while preparing buffer B with the shifted screen content.
2. When the shift is complete and XSCROLL wraps, switch the VIC-II to display
   buffer B (by changing the upper 4 bits of `$D018`).
3. Next cycle: display B, prepare A.

This technique eliminates visible tearing during screen shifts, since all
modifications happen to the non-displayed buffer. The VIC-II bank pointer
(`$D018` bits 4-7) selects which 1 KB area is used for screen RAM within
the current 16 KB VIC bank.

**Bank-switched double buffering** places the two screen buffers in different
VIC banks and switches banks via `$DD00` (CIA2 port A, bits 0-1). This
provides more memory layout flexibility.

A practical optimization distributes the copy workload across the 8 frames
of the scroll cycle. For example, with a pattern like 6-7-7-7-7-4 rows per
frame, only 140-170 bytes need copying per frame instead of the full 1000,
reducing per-frame raster consumption from ~37 raster lines to ~9.

### 3.4 Color RAM Challenges

Color RAM presents the single greatest challenge for C64 scrolling:

- **Fixed location**: color RAM occupies `$D800`-`$DBE7` and cannot be
  relocated or banked. There is only one copy, ever.
- **No double buffering**: unlike screen RAM, there is no way to prepare a
  second color buffer and swap to it.
- **Visible during writes**: the VIC-II reads color RAM while the CPU writes
  to it, causing tearing and flicker if colors differ between adjacent
  characters.

Strategies for managing color RAM:

1. **Race the beam**: shift color RAM only for rows the raster has already
   passed. Start copying from the bottom of the screen upward while the beam
   draws the top half, then copy the top portion after the beam has moved
   past. If code executes when the raster is around line `$A0` (160), it
   can safely shift 20 rows of color RAM tear-free.

2. **Distribute across frames**: in a technique called "in-place horizontal
   scrolling," the color RAM shift is spread over the 8 frames of the
   scroll cycle. Only a fraction of rows are updated per frame, with the
   visual transition masked by the XSCROLL hardware offset.

3. **Uniform colors**: if the entire scrolling area uses a single foreground
   color, color RAM never needs shifting. Many games use this approach or
   limit color variation to reduce the shifting burden.

4. **Block-aligned colors**: if colors correspond to map blocks (e.g., 4x4
   character blocks), only 1/4 of the columns require color updates per
   coarse scroll, reducing color RAM writes to 25% of the full amount.

5. **Sprites for color**: overlay sprites on a monochrome scrolling
   background to add color without touching color RAM during scrolls.


## 4. Full-Screen Scrolling

### 4.1 The 40-Column Problem

Standard smooth scrolling uses 38-column mode, sacrificing one column on each
side to hide shifting artifacts. But many games and demos want all 40 columns
visible.

### 4.2 Techniques for Full 40-Column Smooth Scrolling

**Raster-timed mode switching**: switch from 38-column to 40-column mode (and
back) at precise raster positions. Use 38-column mode only on the lines where
the scroll boundary would be visible, and 40-column mode elsewhere. This
requires cycle-exact raster interrupts.

**Row-staggered scrolling**: organize the screen into horizontal groups, each
with slightly different XSCROLL values. Only one group undergoes the character
shift per frame, distributing the visual "jump" so it is nearly imperceptible
at full 40-column width.

**VSP (Variable Screen Position)**: avoid the coarse scroll entirely by
tricking the VIC-II into reading screen data from a different offset
(see Section 7.1). No 38-column mode is needed because no character data
is being shifted.

**AGSP (Any Given Screen Position)**: combines VSP with linecrunch to achieve
full pixel-accurate positioning in both X and Y without coarse scrolling
(see Section 7.1).

### 4.3 Full-Screen Scrolling in Demos

Demo productions regularly achieve full-screen (and even border-to-border)
scrolling effects:

- **Full-screen character scrollers**: scroll the entire 40x25 grid with
  no visible border expansion, using VSP or AGSP.
- **Full-screen bitmap scrollers**: scroll bitmapped graphics across the
  entire display. This requires moving up to 8000 bytes of bitmap data,
  plus screen RAM and color RAM. Double buffering across two VIC banks
  (each containing a full bitmap) is typical, with data streaming done
  over multiple frames.
- **Side-border scrollers**: open the side borders using standard border-
  removal techniques and scroll content into the normally invisible border
  area using sprites.


## 5. Parallax Scrolling

### 5.1 Concept

Parallax scrolling simulates depth by moving multiple visual layers at
different speeds: distant layers scroll slowly, near layers scroll fast.
On the C64, this requires creative use of the limited hardware, since the
VIC-II provides only one set of scroll registers for the entire screen.

### 5.2 Raster-Split Parallax

The most common approach uses raster interrupts to change the scroll register
values (`$D016` / `$D011`) at different vertical positions on the screen:

1. Set XSCROLL to value A for the sky/background region (slow scroll).
2. At a specific raster line, change XSCROLL to value B for the midground
   (medium scroll).
3. At another raster line, change XSCROLL to value C for the foreground
   (fast scroll).

Each region scrolls at its own independent rate. The transitions must be
timed precisely to avoid visible glitches where the scroll value changes.
On a PAL C64, each raster line is 63 cycles, so the register write must
occur within the horizontal blanking interval or the border area.

### 5.3 Sprite-Based Parallax Layers

Sprites scroll independently of the character screen. By placing sprites
behind the background (using the sprite-background priority bit), they
can serve as an additional parallax layer:

- Clouds, mountains, or distant scenery rendered as sprites.
- Sprite X-positions updated at a different rate than the background scroll.
- Sprite multiplexing allows more than 8 sprites per screen, enabling
  richer parallax layers.

### 5.4 Advanced Multi-Layer Parallax

Sophisticated parallax systems combine multiple techniques for different
layers:

| Layer             | Technique                                      |
|-------------------|------------------------------------------------|
| Distant mountains | FLD (Flexible Line Distance) for vertical shift|
| Forested hills    | Character set bank switching                   |
| Mid-ground trees  | Character definition cycling (pre-shifted sets)|
| Foreground        | Hardware scroll registers (`$D011`/`$D016`)    |
| Overlay objects   | Sprites (independent X/Y positioning)          |

The game Parallaxian and the demo Vertical-within-Horizontal Parallax by
Kodiak64 demonstrate some of the most sophisticated parallax implementations,
coordinating up to nine raster interrupt handlers managing sprites, animation,
collision, and scroll layers simultaneously.

### 5.5 Character Definition Cycling for Parallax

Instead of physically moving a layer, pre-draw multiple character sets with
1-pixel vertical (or horizontal) offsets. Cycle through these character sets
by changing the character base pointer in `$D018`:

- 8 character sets provide 8 pixel positions (one full character of motion).
- Switching sets requires only a single register write per raster split.
- Very low CPU cost, but high memory usage (up to 8 x 2 KB = 16 KB for one
  layer's worth of character definitions).


## 6. Direction Variants

### 6.1 Horizontal Scrolling

The most common and simplest direction. Character data is shifted left or
right by one column every 8 frames (at 1 pixel/frame speed). Column-based
updates align naturally with the C64's row-major screen layout, but each
column spans 25 non-contiguous bytes (spaced 40 bytes apart), requiring
indexed addressing.

### 6.2 Vertical Scrolling

Uses YSCROLL in `$D011`. Character data is shifted by one row (40 bytes
copied as a contiguous block from one row address to the previous). Row
shifts are more cache-friendly (contiguous memory) and faster than column
shifts. Vertical scrolling with 24-row mode is the vertical analog of
38-column horizontal scrolling.

### 6.3 Four-Way (Multidirectional) Scrolling

Four-way scrolling -- up, down, left, right -- is essential for exploration
games. The main challenges:

- **Combined register management**: both XSCROLL and YSCROLL must be tracked
  independently. When either wraps, the corresponding coarse shift occurs.
- **Diagonal moves**: when scrolling diagonally, both X and Y shifts may need
  to happen on the same frame, doubling the data movement.
- **Map data structure**: a two-level encoding is typical. The world map
  stores 8-bit block indices; each block (commonly 4x4 or 2x2 characters)
  expands to individual characters and colors. Pre-calculated lookup tables
  store row starting addresses to avoid multiplication.
- **Screen update pattern**: for 8-directional scrolling, the hardware scroll
  register should be "centered when idle" at value 3 or 4. Scrolling right
  uses the sequence 3 -> 5 -> 7 -> (wrap) 1 -> 3. Scrolling left uses
  4 -> 2 -> 0 -> (wrap) 6 -> 4.

Cadaver's game framework (used in the Metal Warrior series and MW ULTRA)
implements multidirectional scrolling with single-frame color RAM and screen
RAM updates without double buffering, organized around compressed zone map
data that is depacked on demand.

### 6.4 Diagonal Scrolling

Games like Zaxxon use diagonal scrolling (isometric perspective). This
combines horizontal and vertical scroll at fixed ratios. Typically
implemented as a special case of 4-way scrolling where both scroll values
advance simultaneously at a predefined ratio (e.g., 2:1 horizontal to
vertical).

### 6.5 Large World Map Scrolling

For worlds larger than available RAM, map data is streamed from disk or
decompressed from a packed format. The world is divided into zones or
sectors; as the player approaches a zone boundary, the next zone's data
is loaded. Cadaver's framework stores zone data compressed and depacks it
for display, with each zone containing objects (doorways) and actors
(items, enemies).

World coordinates use a hierarchical encoding: the high byte represents
the block position, the low byte encodes sub-block position with 3 bits
of sub-pixel precision. This simplifies scrolling math and collision
detection.


## 7. Advanced Techniques

### 7.1 VSP (Variable Screen Position)

VSP (also called DMA Delay) is a VIC-II trick that shifts where the chip
begins reading screen data, effectively scrolling the display horizontally
without any CPU-driven memory copy.

**How it works:**

The VIC-II normally begins fetching character codes at a fixed point in each
bad line. By writing to `$D011` at a precisely timed moment during a raster
line, you can create or suppress a bad line condition, causing the VIC-II's
internal character pointer to start at an offset position. This shifts the
entire display sideways by a variable number of characters.

**Advantages:**
- Eliminates the need to copy screen RAM (saves ~6000-8000 cycles per frame).
- No 38-column mode required -- all 40 columns remain visible.
- Extremely fast scrolling is possible.

**The VSP Crash Bug:**

VSP can corrupt DRAM on some C64 hardware. The root cause is a metastability
condition in the DRAM refresh circuitry:

- The VIC-II generates memory addresses and DRAM refresh commands on two
  independent signal paths. Under VSP conditions, these can desynchronize.
- When refresh occurs with unstable address lines, the RAS (Row Address
  Strobe) latches undefined voltage levels into the row multiplexer.
- This connects multiple memory rows to sense lines simultaneously,
  causing charge to flow between cells and corrupting data.
- Memory locations ending in `$x7` or `$xF` are most vulnerable (every
  eighth byte throughout memory).
- The probability of corruption depends on nanosecond-level timing,
  influenced by VIC-II chip revision, temperature, motherboard trace
  resistance, power supply quality, and the random phase relationship
  between color carrier and dot clock at power-on.

Notable games using VSP include Mayhem in Monsterland, Creatures, and
Phobia.

**Safe VSP** (as demonstrated by Linus Akesson) employs three mitigation
strategies:

1. Ensure all "fragile bytes" (addresses ending in 7 or F) within each
   page contain identical values (e.g., all `$EA` for NOP).
2. Use the undocumented opcode `$80` (2-byte NOP) to skip fragile code
   locations entirely.
3. Continuously restore graphics data from safe backup copies in
   non-fragile memory.

### 7.2 AGSP (Any Given Screen Position)

AGSP combines VSP (for horizontal positioning) with linecrunch (for
vertical positioning) to achieve arbitrary pixel-level screen placement
in both axes with minimal CPU cost.

**Linecrunch** compresses a full character line (8 pixels tall) into a
single raster line. This is done by changing YSCROLL in `$D011` on a bad
line but before cycle 14 (when the VIC-II would begin stealing CPU cycles),
making the bad line condition no longer true. The VIC-II skips ahead to the
next character row. Repeating this for N bad lines effectively scrolls the
display up by N character rows in just N raster lines.

**AGSP implementation:**
1. Establish a stable raster interrupt at the top of the display (e.g.,
   line 45).
2. Use linecrunch to skip ahead to the desired vertical position (costing
   the top ~12% of screen real estate).
3. Use VSP (DMA delay) to set the desired horizontal offset.
4. The main display then renders from the calculated position.

**Tradeoff**: the top 5-6 character rows of the screen are consumed by the
linecrunch sequence. Demos using AGSP typically blank this area or display
a static panel. Games using AGSP include the Fred's Back series and Jim
Slim.

### 7.3 Tech-Tech Scrollers

Tech-tech is a demo effect where text or graphics wave horizontally in a
sinusoidal pattern, with each raster line having its own X-scroll offset.

**Basic technique:**
- Set up a raster interrupt that fires on every visible raster line (or
  use a timed loop).
- On each line, write a different value to XSCROLL (bits 0-2 of `$D016`).
- The values are read from a sine table, creating a wave pattern.

**Limitation**: XSCROLL only provides 0-7 pixels of offset, giving a
maximum wave amplitude of 7 pixels -- too subtle for a dramatic effect.

**Extended tech-tech:**
- Prepare multiple character sets, each shifted by one additional pixel
  (8 pixels per character means 7 extra sets needed).
- On each raster line, change both XSCROLL (`$D016` bits 0-2) and the
  character set pointer (`$D018` bits 1-3) to address a different
  pre-shifted character set.
- With 7 character sets fitting in one VIC bank, the effective wave
  amplitude reaches 56 pixels.
- For even wider effects, switch VIC banks via `$DD00` mid-screen.

Each raster line requires a precise timed write sequence: load the sine
value, split it into an XSCROLL component (low 3 bits) and a charset
selector (remaining bits), write both registers, and wait for the next
line. This consumes a large portion of available CPU cycles during the
visible frame.

### 7.4 DYCP and DXYCP Scrollers

**DYCP (Different Y Character Position)** is a demo effect where each
letter in a scroll text moves independently in the vertical direction,
typically following a sine wave. The letters appear to bounce up and down
while scrolling horizontally.

Implementation: the character set bitmap data is rewritten each frame.
For each letter, its vertical position (Y-offset within the character
grid) is calculated from a sine table, and the letter's pixel data is
placed at the corresponding row within a dedicated character definition.
This requires real-time manipulation of character set RAM.

**DXYCP (Different X and Y Character Position)** extends DYCP to allow
each letter to move independently in both X and Y, resembling Amiga BOBs
(Blitter Objects).

DXYCP implementation approaches:

1. **Pre-shifted font data**: generate all pixel-shifted variants of each
   character at startup. A 5x5 font with 32 characters and 8 shift values
   produces ~1920 bytes of lookup data.
2. **Bitmap rendering**: iterate through each character, calculate its
   screen byte position and required pixel shift, then use LDA/ORA/STA
   sequences to composite overlapping characters.
3. **Sprite-based**: render characters into sprite data and use a sprite
   multiplexer (up to 32 sprites) for display. Each sprite holds 3-4
   characters. Optimization involves testing multiple X-position
   arrangements to minimize byte overlaps.

A typical DXYCP frame requires ~15,000 cycles and ~11,000 bytes of
generated (self-modifying) plotting code.

### 7.5 Full-Screen Bitmap Scrolling

Bitmap scrolling is far more expensive than character scrolling because the
bitmap data alone is 8000 bytes (versus 1000 for screen RAM).

**Typical approach:**
1. Double buffer using two VIC banks, each containing a full 8 KB bitmap
   plus 1 KB screen RAM.
2. Display one bitmap while the CPU updates the other.
3. For each new line of scrolled content, 320 bytes of bitmap data, 40 bytes
   of screen data, and 20 bytes of color data must be written.
4. Scroll at reduced speed (e.g., 1 pixel per 2 frames) to allow enough
   time for data transfer.
5. Color RAM is updated by "chasing the beam" -- writing color values only
   after the VIC-II has finished reading that raster line.

The demo "Memento Mori" by Genesis Project achieved a full-screen bitmap
scroller streaming extra data from disk at 25 fps -- a landmark achievement.

### 7.6 "Big Scrollers"

Big scrollers are demo effects where enormous text characters (often 8x8
or larger character cells per letter) scroll across the full screen. They
combine:

- Large pre-rendered font data (each letter spans many characters).
- Efficient screen shifting (typically unrolled or self-modifying code).
- Hardware smooth scroll for sub-character movement.
- Sometimes VSP or AGSP to avoid the coarse shift entirely.

Side-border scrollers extend the display into the normally hidden border
area using sprite overlays or border-removal techniques, creating a
visually wider scrolling field.


## 8. Hardcore Details

### 8.1 Exact Cycle Timing for Scroll Register Changes

On a PAL C64, each raster line takes exactly 63 CPU cycles (504 pixels
including borders). On NTSC, this is 64 or 65 cycles depending on the
VIC-II revision (6567R56A vs. 6567R8).

The XSCROLL and YSCROLL registers can be written at any time, but the
VIC-II's internal behavior determines when the new value takes effect:

- **XSCROLL** (`$D016`): controls the delay of character data reload within
  each raster line. The VIC-II applies the XSCROLL value during the
  character fetch phase, so changes take effect on the next raster line
  if written during horizontal blanking, or may partially affect the
  current line if written during the active display.

- **YSCROLL** (`$D011`): checked at the beginning of each raster line to
  determine whether a bad line occurs. The formal bad line condition is:

  ```
  RASTER >= $30 AND RASTER <= $F7
  AND (RASTER & 7) == YSCROLL
  AND DEN was set during any cycle of raster line $30
  ```

  Modifying YSCROLL before cycle 14 of a potential bad line can suppress
  or trigger the bad line, since the VIC-II checks the condition at the
  start of each line.

### 8.2 When the VIC-II Latches Scroll Values

The VIC-II does not simply "read" the scroll register at one fixed instant.
The behavior is nuanced:

- **Bad line detection** happens at every cycle's negative edge of phi-0.
  YSCROLL is compared to `RASTER & 7` continuously. If the condition
  becomes true at any cycle before the VIC-II begins character fetches
  (cycle 14), a bad line is initiated.
- **XSCROLL** determines how many pixels to delay before beginning to output
  character data. This is latched during the graphics sequencer's setup
  phase at the start of each character row.
- For techniques like tech-tech (changing XSCROLL every raster line), the
  write must occur during the previous line's right border or the current
  line's left border -- typically between cycles 55 and 10 of the next
  line.

### 8.3 Interaction Between Scrolling and Bad Lines

Bad lines are the heartbeat of the VIC-II's character display. On a bad
line, the VIC-II steals approximately 40-43 cycles from the CPU to fetch
40 bytes of character codes from screen RAM.

**Impact on scrolling code:**
- On a normal raster line: 63 cycles available (PAL).
- On a bad line: only 20-23 cycles available.
- With sprites enabled: each sprite on the current line costs an additional
  2 cycles (for pointer fetch) plus 3 cycles per sprite for data fetch.
  With 4 sprites: approximately 10-12 cycles remain on a bad line.

**YSCROLL and bad line position**: changing YSCROLL moves where bad lines
occur. If YSCROLL=3, bad lines happen on raster lines `$33`, `$3B`, `$43`,
etc. Scrolling by changing YSCROLL each frame means bad lines shift position
each frame, which must be accounted for in raster interrupt timing.

**Bad line suppression for scrolling**: FLD (Flexible Line Distance)
suppresses bad lines by continuously changing YSCROLL so the bad line
condition is never met. This delays character row rendering and can be
used to scroll the display downward. Linecrunch does the opposite:
triggers and immediately cancels bad lines to skip character rows,
scrolling upward.

### 8.4 Optimized Screen Shift Routines

**Unrolled LDA/STA pairs:**

The fastest screen shift eliminates all loop overhead by hardcoding every
load and store address:

```
    lda $0401          ; 4 cycles
    sta $0400          ; 4 cycles
    lda $0402          ; 4 cycles
    sta $0401          ; 4 cycles
    ; ... repeat for all 1000 positions
```

Cost: ~8 cycles per byte, ~8000 cycles total. Code size: ~6000 bytes.

**Self-modifying code:**

Store the source and destination addresses directly into the LDA/STA
instructions, then advance them:

```
    lda source+1       ; load high byte of source
    clc
    adc #40            ; advance by one row
    sta source+1
    sta dest+1
source: lda $0400,x    ; self-modified address
dest:   sta $03FF,x    ; self-modified address
```

This allows a single compact loop to handle row-by-row shifting with
different source/destination offsets.

**Page-aligned buffers:**

Aligning screen buffers to page boundaries (e.g., `$0400`, `$0800`)
enables the use of absolute indexed addressing without crossing page
boundaries, saving 1 cycle per access on page-crossing penalties.

**Split-frame copies:**

Rather than copying all 1000 bytes in one burst, split the work across
multiple frames. During the 8-frame scroll cycle, copy ~125 bytes per
frame. This reduces per-frame CPU load and leaves time for game logic,
music, and sprite updates.

### 8.5 Memory Layouts for Efficient Scrolling

**VIC-II bank organization:**

The VIC-II sees a 16 KB window selected by `$DD00` bits 0-1:

| Bank | Address Range     | $DD00 bits 0-1 |
|------|-------------------|-----------------|
| 0    | $0000-$3FFF       | %11             |
| 1    | $4000-$7FFF       | %10             |
| 2    | $8000-$BFFF       | %01             |
| 3    | $C000-$FFFF       | %00             |

Banks 0 and 2 have the character ROM mapped at offsets `$1000`-`$1FFF`,
making the default character set available without RAM copies. Banks 1
and 3 require character data to be copied into RAM.

**Typical scrolling memory layout (Bank 0):**

```
$0000-$03FF   Zero page, stack, BASIC pointers
$0400-$07FF   Screen buffer A (1 KB)
$0800-$0BFF   Screen buffer B (1 KB)
$0C00-$0FFF   Free / sprite pointers
$1000-$1FFF   Character ROM (seen by VIC-II) or custom charset
$2000-$3FFF   Custom character sets / bitmap / sprite data
```

**Double-buffer bank layout (for bank switching):**

```
Bank 0 ($0000-$3FFF): Screen A + charset A + sprites
Bank 1 ($4000-$7FFF): Screen B + charset B + sprites
```

Switch between banks via `$DD00` on the coarse scroll frame.

### 8.6 Color RAM Scrolling Optimization Tricks

Since color RAM at `$D800`-`$DBE7` is the bottleneck, every cycle saved
matters:

1. **Raster-chased updates**: begin color RAM writes immediately after the
   raster passes each section. A well-timed routine starting at raster line
   `$A0` can shift 20 rows of color RAM before the beam wraps back to the
   top of the screen.

2. **Half-screen splits**: copy the bottom half of color RAM during the
   top half's display, and the top half during the bottom's display
   (or during vertical blank). This distributes the work and avoids
   any single large copy.

3. **Staggered color updates**: in free-directional scrolling, update only
   the newly exposed column (25 bytes for horizontal, 40 bytes for
   vertical). Only the edge column/row changes; the rest remains valid.
   This is vastly cheaper than shifting all 1000 bytes.

4. **Block-quantized colors**: if colors are assigned per 2x2 or 4x4 block
   rather than per character, only 1/2 or 1/4 of color RAM positions need
   updating per coarse scroll. With 4x4 blocks, a horizontal scroll only
   updates 6-7 color bytes per row instead of 40.

5. **Uniform color regions**: design levels so that large horizontal bands
   share a single color. Only update color RAM when crossing a color
   boundary, which may happen infrequently.

6. **Interleaved screen/color copies**: alternate between copying one row
   of screen RAM and one row of color RAM. This keeps both updates
   progressing in lockstep and avoids scenarios where screen data is
   shifted but colors lag behind.

7. **NTSC considerations**: NTSC machines have fewer cycles per frame
   (~17,095 vs ~19,656 for PAL). Cadaver recommends splitting color
   screen updates in two halves for NTSC compatibility.


## References

### Primary Technical Documents

- Christian Bauer, "The MOS 6567/6569 Video Controller (VIC-II) and its
  Application in the Commodore 64" (vic-ii.txt)
  <https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt>

- C64-Wiki: Scrolling
  <https://www.c64-wiki.com/wiki/Scrolling>

- C64-Wiki: Register 53265 ($D011)
  <https://www.c64-wiki.com/wiki/53265>

- C64-Wiki: VIC-II
  <https://www.c64-wiki.com/wiki/VIC>

- C64-Wiki: Parallax Scrolling
  <https://www.c64-wiki.com/wiki/Parallax_Scrolling>

- C64-Wiki: Color RAM
  <https://www.c64-wiki.com/wiki/Color_RAM>

### Tutorials and Implementation Guides

- 1am Studios, "How to Implement Smooth Full-Screen Scrolling on C64"
  <http://1amstudios.com/2014/12/07/c64-smooth-scrolling/>

- jeff-1amstudios, c64-smooth-scrolling (GitHub, source code)
  <https://github.com/jeff-1amstudios/c64-smooth-scrolling>

- Cadaver (Lasse Oorni), "Multidirectional Scrolling and the Game World"
  <https://cadaver.github.io/rants/scroll.html>

- Cadaver, c64gameframework (GitHub)
  <https://github.com/cadaver/c64gameframework>

- Kodiak64, "Bank-Switched Double-Buffer Scrolling on the Commodore 64"
  <https://kodiak64.com/blog/bank-switched-double-buffer-scrolling>

- Kodiak64, "Vertical-within-Horizontal Parallax on C64"
  <https://kodiak64.com/blog/vertical-parallax-on-commodore-64>

- LemonSpawn, "Tutorial 8: Full-Screen Smooth Scrolling"
  <https://lemonspawn.com/turbo-rascal-syntax-error-expected-but-begin/turbo-rascal-se-tutorials/tutorial-8-full-screen-smooth-scrolling-know-your-timing-banking/>

- 0xc64, "1x1 Smooth Text Scroller"
  <http://www.0xc64.com/2013/11/24/1x1-smooth-text-scroller/>

- C64 Programmer's Reference Guide: Smooth Scrolling
  <https://www.devili.iki.fi/Computers/Commodore/C64/Programmers_Reference/Chapter_3/page_128.html>

- Introduction to Programming C-64 Demos (tech-tech, DYCP)
  <https://odd.blog/wp-content/blogs.dir/2/files/2008/01/intro-to-programming-c64-demos.html>

### VSP and AGSP

- Bumbershoot Software, "Variable Screen Placement: The VIC-II's Forbidden
  Technique"
  <https://bumbershootsoft.wordpress.com/2015/04/19/variable-screen-placement-the-vic-iis-forbidden-technique/>

- Linus Akesson, "Safe VSP"
  <https://linusakesson.net/scene/safevsp/index.php>

- Set Side B, "Variable Screen Position on the Commodore 64"
  <https://setsideb.com/variable-screen-position-on-the-commodore-64/>

- Kodiak64, "The Future of VSP Scrolling on the C64"
  <https://kodiak64.co.uk/blog/future-of-VSP-scrolling>

- Codebase64, "AGSP: Any Given Screen Position"
  <https://codebase64.org/doku.php?id=base:agsp_any_given_screen_position>

- Codebase64, "Smooth Linecrunch"
  <https://codebase64.org/doku.php?id=base:smooth_linecrunch>

### Bad Lines, FLD, and Raster Timing

- Bumbershoot Software, "Flickering Scanlines: The VIC-II and Bad Lines"
  <https://bumbershootsoft.wordpress.com/2014/12/06/flickering-scanlines-the-vic-ii-and-bad-lines/>

- Bumbershoot Software, "Flexible Line Distance (FLD)"
  <https://bumbershootsoft.wordpress.com/2015/09/17/flexible-line-distance-fld/>

- Bumbershoot Software, "VIC-II Interrupt Timing"
  <https://bumbershootsoft.wordpress.com/2015/07/26/vic-ii-interrupt-timing-or-how-i-learned-to-stop-worrying-and-love-unstable-rasters/>

- nurpax, "BINTRIS C64: Bad Lines and Flexible Line Distance"
  <https://nurpax.github.io/posts/2018-06-19-bintris-on-c64-part-5.html>

- Dustlayer, "VIC-II for Beginners Part 3: Beyond the Screen"
  <https://dustlayer.com/vic-ii/2013/4/25/vic-ii-for-beginners-beyond-the-screen-rasters-cycle>

- Antimon, "FLD - Scrolling the Screen" by Marek Klampar
  <http://www.antimon.org/dl/c64/code/fld.txt>

### Demo Scroller Techniques

- The Raistlin Papers, "DXYCP Scrollers"
  <https://c64demo.com/dxycp-scrollers/>

- The Raistlin Papers, "Side Border Bitmap Scroller"
  <https://c64demo.com/side-border-bitmap-scroller/>

- The Raistlin Papers, "Star Wars Scrollers"
  <https://c64demo.com/star-wars-scrollers/>

- Antimon, "DYCP - Horizontal Scrolling" by Pasi 'Albert' Ojala
  <http://www.antimon.org/dl/c64/code/dycp.txt>

### Community Discussions

- Lemon64 Forum, various scrolling discussions:
  - $D016 scrolling: <https://www.lemon64.com/forum/viewtopic.php?t=35782>
  - Super-fast scroll routine: <https://www.lemon64.com/forum/viewtopic.php?t=50260>
  - In-place horizontal scrolling: <https://www.lemon64.com/forum/viewtopic.php?t=70401>
  - Color RAM scrolling: <https://www.lemon64.com/forum/viewtopic.php?t=69703>
  - AGSP and linecrunch: <https://www.lemon64.com/forum/viewtopic.php?t=40019>
  - VSP tutorial: <https://www.lemon64.com/forum/viewtopic.php?t=70539>
  - Horizontal bitmap scrolling: <https://www.lemon64.com/forum/viewtopic.php?t=51678>
