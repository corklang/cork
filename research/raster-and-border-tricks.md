# Raster Interrupt Techniques and Border Tricks on the Commodore 64

Comprehensive technical reference covering raster interrupts, stable raster techniques,
border opening tricks, and advanced VIC-II exploits used in C64 demos and games.


---

## 1. Overview

The Commodore 64's VIC-II graphics chip generates its video output one rasterline at a
time, painting pixels from left to right and top to bottom in lockstep with the CRT
electron beam. The CPU and VIC-II share the same clock and the same bus, which means a
programmer can predict exactly which pixel the beam is painting at any given CPU cycle.
This deterministic relationship is the foundation of every raster trick on the C64.

### 1.1 Racing the Beam

The phrase "racing the beam" describes the fundamental technique: the CPU modifies
VIC-II registers (colors, scroll offsets, graphics mode bits, bank pointers) while the
beam is still drawing, causing the change to take effect partway through the frame. The
result is that different parts of the screen can display different colors, modes, or
configurations -- all from a chip that officially supports only one set of settings per
frame.

Because the PAL VIC-II draws 312 lines of 63 cycles each, a single frame takes exactly
19,656 CPU cycles. The programmer has roughly 1/50th of a second per frame and must
perform all register changes at precise cycle positions within that budget.

### 1.2 Why Raster Tricks Matter

Raster tricks are central to C64 programming because:

- The VIC-II has only one background color register, one border color register, and one
  set of mode/scroll bits for the entire screen. Without raster tricks, the whole screen
  must use the same settings.
- Sprites are limited to 8 per scanline. Multiplexing sprites across lines (re-using
  them lower on screen) requires precise raster timing.
- Advanced graphics modes like FLI, FLD, and VSP are entirely based on manipulating
  VIC-II registers at exact cycle positions within each rasterline.
- Games routinely use split-screen techniques (e.g. a bitmap playfield with a text
  status bar) that require mid-frame mode switches.
- The C64 demo scene has pushed these techniques to extraordinary extremes, achieving
  visual results far beyond the chip's intended capabilities.


---

## 2. Raster Interrupts Basics

### 2.1 Setting Up a Raster IRQ

The VIC-II can generate a hardware interrupt when the raster beam reaches a specific
scanline. Three registers are involved:

| Register | Address | Purpose |
|----------|---------|---------|
| RASTER   | $D012   | Low 8 bits of the comparison rasterline |
| CR1      | $D011   | Bit 7 = bit 8 (MSB) of the comparison rasterline |
| IRQEN    | $D01A   | Bit 0: enable raster compare interrupt |
| IRQFLAG  | $D019   | Bit 0: raster interrupt occurred (write 1 to acknowledge) |

The rasterline comparison value is 9 bits wide (lines 0-311 on PAL), split across
$D012 (bits 0-7) and $D011 bit 7 (bit 8).

#### Standard Initialization Sequence

```asm
        SEI                     ; disable interrupts during setup

        ; Disable CIA-1 timer interrupts (they share the IRQ line)
        LDA #$7F
        STA $DC0D               ; clear all CIA-1 interrupt enable bits
        LDA $DC0D               ; acknowledge any pending CIA-1 interrupts

        ; Optionally disable CIA-2 NMI (prevents RESTORE key interference)
        LDA #$7F
        STA $DD0D
        LDA $DD0D

        ; Set the raster comparison line (example: line 100 = $64)
        LDA #$64
        STA $D012               ; low 8 bits of raster compare
        LDA $D011
        AND #$7F                ; clear bit 7 (raster line MSB = 0)
        STA $D011

        ; Point the IRQ vector to our handler
        LDA #<irq_handler
        STA $0314
        LDA #>irq_handler
        STA $0315

        ; Enable raster interrupt on VIC-II
        LDA #$01
        STA $D01A

        ; Acknowledge any pending VIC-II interrupts
        LDA #$FF
        STA $D019

        CLI                     ; enable interrupts
        RTS
```

**Important notes:**
- $D011 must be written at least once after enabling raster interrupts via $D01A; the
  VIC-II will not fire raster IRQs until $D011 has been touched.
- CIA-1 timer interrupts must be disabled because they share the same IRQ line as the
  VIC-II raster interrupt. If left enabled, the CPU cannot distinguish which device
  triggered the IRQ without checking both $DC0D and $D019.

### 2.2 Acknowledging the Interrupt

Every raster IRQ handler must acknowledge the interrupt by writing to $D019. The
standard method is:

```asm
irq_handler:
        ASL $D019               ; acknowledge raster interrupt (clears bit 0)
        ; ... or equivalently:
        ; LDA #$01
        ; STA $D019             ; write 1 to bit 0 to clear the flag
```

The `ASL $D019` idiom works because bit 0 (raster IRQ flag) is shifted into the carry
flag (useful for testing) and bit 0 is cleared simultaneously. If you skip the
acknowledgment, the VIC-II immediately re-triggers the interrupt upon RTI.

After performing its work, the handler can either:
- Jump to `$EA31` to run the normal KERNAL IRQ service (keyboard scan, cursor blink)
- Jump to `$EA81` for register restoration only (pull Y, X, A from stack, then RTI)
- Restore registers manually and execute RTI directly

### 2.3 Simple Raster Split

The most basic raster trick changes a VIC-II register when the beam reaches a specific
line. For example, changing the background color partway down the screen:

```asm
irq_handler:
        ASL $D019               ; acknowledge interrupt

        LDA #$02                ; red
        STA $D021               ; change background color

        ; Restore original color at top of next frame via another interrupt
        LDA #<irq_top
        STA $0314
        LDA #>irq_top
        STA $0315
        LDA #$00
        STA $D012               ; trigger at top of screen

        JMP $EA81               ; return from interrupt
```

This produces a "split screen" where the top portion has one background color and the
bottom portion has another.

### 2.4 Interrupt Chains (Multiple Raster IRQs per Frame)

For more complex effects, multiple raster interrupts can be chained within a single
frame. Each handler:
1. Acknowledges the VIC-II interrupt
2. Performs its register changes
3. Sets $D012 (and $D011 bit 7 if needed) to the next trigger line
4. Points $0314/$0315 to the next handler
5. Returns via RTI

The last handler in the chain resets the rasterline and vector back to the first
handler, completing the loop for the next frame.

```asm
; Example: three-way split
irq1:   ASL $D019
        LDA #COLOR1
        STA $D021
        LDA #<irq2
        STA $0314
        LDA #>irq2
        STA $0315
        LDA #LINE2
        STA $D012
        JMP $EA81

irq2:   ASL $D019
        LDA #COLOR2
        STA $D021
        LDA #<irq3
        STA $0314
        LDA #>irq3
        STA $0315
        LDA #LINE3
        STA $D012
        JMP $EA81

irq3:   ASL $D019
        LDA #COLOR3
        STA $D021
        ; Reset chain for next frame
        LDA #<irq1
        STA $0314
        LDA #>irq1
        STA $0315
        LDA #LINE1
        STA $D012
        JMP $EA31               ; call KERNAL for keyboard etc.
```

**Practical limits:** Each raster IRQ has overhead (register saves, acknowledgment,
vector setup). At minimum, two consecutive raster interrupts should be spaced at least
3-4 lines apart, though the exact minimum depends on handler complexity.


---

## 3. Stable Raster Interrupts

### 3.1 The Jitter Problem

When the VIC-II signals a raster interrupt, the 6510 CPU does not respond instantly. The
CPU must finish executing its current instruction before it can service the interrupt.
Since 6510 instructions take between 2 and 7 cycles, the entry point of the IRQ handler
has an inherent timing uncertainty (jitter) of up to 7 cycles.

Additionally, the interrupt acknowledge sequence itself (pushing PC and status register
to the stack, fetching the IRQ vector) consumes 7 cycles. Combined with the instruction-
completion delay, the first instruction of the IRQ handler can begin anywhere within a
window of roughly 7-8 cycles after the VIC-II signals the interrupt.

**Why this matters:** On a PAL C64, one CPU cycle equals one pixel in hi-res mode (8
pixels per character). A 7-cycle jitter means the "stable point" in the handler could be
off by up to 7 pixels -- visible as a shimmering or wavy edge in color splits, scroll
effects, or any cycle-exact manipulation.

For simple color changes in the border area (where exact horizontal position doesn't
matter much), the jitter is acceptable. But for effects that require cycle-exact timing
-- side border opening, FLI, per-line color changes, sprite multiplexing -- the jitter
must be eliminated.

### 3.2 The Double-IRQ Technique

The double-IRQ technique is the most widely used method for achieving stable rasters on
the C64. It uses two chained raster interrupts to reduce jitter from ~7 cycles to 0-1
cycles, then a final correction step to eliminate the last cycle.

#### Phase 1: First IRQ -- Set Up the Second

The first interrupt handler fires with the normal ~7-cycle jitter. It:
1. Acknowledges the VIC-II interrupt
2. Stores the stack pointer (for cleanup after the second IRQ)
3. Sets $D012 to the next rasterline (current + 2 typically)
4. Changes the IRQ vector to point to the second handler
5. Executes CLI to re-enable interrupts
6. Enters a NOP slide (a sequence of two-cycle NOP instructions)

```asm
irq1:
        STA irq_a+1            ; save A (self-modifying)
        STX irq_x+1            ; save X
        STY irq_y+1            ; save Y

        ASL $D019               ; acknowledge raster IRQ

        INC $D012               ; trigger second IRQ on next line
        ; (simple version: just add 1 to current compare line)

        LDA #<irq2
        STA $0314
        LDA #>irq2
        STA $0315

        TSX                     ; save stack pointer
        STX irq_sp+1

        CLI                     ; re-enable interrupts (!)

        ; NOP slide -- second IRQ will fire during this
        NOP
        NOP
        NOP
        NOP
        NOP
        NOP
        NOP
        NOP
        NOP
        ; ... (enough NOPs to guarantee the second IRQ fires)
```

#### Phase 2: Second IRQ -- The NOP Slide

Because the NOP slide consists entirely of 2-cycle instructions, the second raster IRQ
can only interrupt at one of two positions within a NOP: at the start (0 cycles into the
NOP) or 1 cycle into the NOP. This reduces jitter from ~7 cycles to exactly 0 or 1
cycle.

#### Phase 3: Jitter Correction

The second handler uses a trick to detect and compensate for the remaining 0/1 cycle
jitter:

```asm
irq2:
        ; At this point, jitter is 0 or 1 cycle.
        ; The next sequence detects which case we're in.

        LDA #<irq1              ; restore IRQ vector for next frame
        STA $0314
        LDA #>irq1
        STA $0315

        ; Detect jitter: read $D012 twice in succession.
        ; If jitter = 1, the rasterline counter may have already
        ; incremented, making the CMP fail.
        LDA $D012
        CMP $D012
        BEQ no_jitter           ; if equal, we had 0 jitter
no_jitter:
        ; The BEQ takes 2 cycles if not taken, 3 if taken.
        ; This exactly compensates for the 1-cycle difference.
        ; We are now cycle-exact.
```

The `CMP $D012` instruction's 5th cycle overlaps with the fetch of the next instruction.
If the rasterline counter incremented between the LDA and CMP, the Z flag is clear (BEQ
not taken, 2 cycles). If they match, BEQ is taken (3 cycles). Either way, execution
continues at the same absolute cycle position.

#### Stack Cleanup

After the stable point is reached, the handler must clean up the nested interrupt state:

```asm
irq_sp: LDX #$00               ; (self-modified: original SP)
        TXS                     ; restore stack pointer
                                ; (discards the stacked state from
                                ;  the first IRQ and the NOP slide)
```

### 3.3 The CIA Timer Technique

An alternative approach uses a CIA timer to create the stable reference point:

1. Set up CIA Timer A to count down from a known value, synchronized to the rasterline.
2. In the raster IRQ handler, read the CIA timer and use its value to determine the
   exact cycle offset.
3. Execute a calculated delay (using a table of delays) to reach the stable point.

The advantage is that only a single raster interrupt is needed. The disadvantage is
that the CIA timer must be precisely synchronized to the VIC-II raster, which typically
requires a one-time polling loop at startup:

```asm
; Synchronize CIA timer to raster
sync:   LDA #$00
wait:   CMP $D012
        BNE wait                ; wait for line 0 (7-cycle loop)
        ; Timer starts here, synchronized to within 7 cycles
        ; Further refinement may use additional polling
```

### 3.4 Why Stable Rasters Are Essential

Without stable rasters, the following effects are impossible or visibly flawed:
- Side border removal (requires cycle-exact writes at specific X positions)
- FLI mode (requires cycle-exact $D011/$D018 changes every rasterline)
- Sprite multiplexing without visible glitches
- Smooth raster bars without shimmer
- Tech-tech and other per-line scroll effects
- DYCP (Different Y Character Positions) scrollers
- Line crunch / VSP effects


---

## 4. Opening the Borders

### 4.1 How VIC-II Borders Work

The VIC-II uses two internal flip-flops to control border display:

- **Main border flip-flop:** When set, the border color ($D020) is displayed with
  highest priority, covering all graphics and sprites. When cleared, normal graphics
  and sprites are visible.
- **Vertical border flip-flop:** When set, it prevents the main border flip-flop from
  being cleared and forces the graphics sequencer to output the background color only.
  This flip-flop effectively blanks the entire line.

These flip-flops are controlled by comparators that check the beam position against
threshold values determined by the RSEL and CSEL bits:

**Vertical border thresholds (RSEL in $D011 bit 3):**

| RSEL | Top border off (line) | Bottom border on (line) | Display rows |
|------|-----------------------|-------------------------|--------------|
| 0    | 55                    | 247                     | 24 rows (192 px) |
| 1    | 51                    | 251                     | 25 rows (200 px) |

**Horizontal border thresholds (CSEL in $D016 bit 3):**

| CSEL | Left border off (X) | Right border on (X) | Display columns |
|------|---------------------|----------------------|-----------------|
| 0    | 31                  | 335                  | 38 cols (304 px) |
| 1    | 24                  | 344                  | 40 cols (320 px) |

The border logic operates as follows:

1. If the X coordinate reaches the right comparison value, the main border flip-flop is
   **set** (border turns on).
2. If the Y coordinate reaches the bottom comparison value on cycle 63 (the last cycle
   of the line), the vertical border flip-flop is **set**.
3. If the Y coordinate reaches the top comparison value on cycle 63 and the DEN bit
   ($D011 bit 4) is set, the vertical border flip-flop is **cleared**.
4. If the X coordinate reaches the left comparison value and the vertical border
   flip-flop is **not set**, the main border flip-flop is **cleared** (border turns off,
   graphics appear).

The key insight is that the VIC-II checks these conditions at specific moments. If the
RSEL/CSEL bits are changed at exactly the right time, the comparison that would normally
set or clear a flip-flop can be missed entirely, leaving the flip-flop in a state the
designers never intended.

### 4.2 Top/Bottom Border Removal

The vertical borders can be removed by exploiting the vertical border flip-flop logic.

**The trick:** On the last rasterline before the bottom border would normally activate,
switch from 25-row mode (RSEL=1) to 24-row mode (RSEL=0). In 24-row mode, the bottom
border comparison value is line 247 -- which has already passed. The VIC-II checks for
the bottom border at line 251 (25-row threshold), but since RSEL is now 0, it checks
against line 247 instead. Since line 247 has already gone by, the vertical border
flip-flop is never set.

Before the top of the next frame (before line 51), RSEL must be restored to 1 so the
display area opens normally.

**Implementation:**

```asm
; Raster interrupt at line $F9 (249), just before line 251
border_irq:
        ASL $D019               ; acknowledge

        ; Switch to 24-row mode before line 251
        LDA $D011
        AND #$F7                ; clear bit 3 (RSEL=0)
        STA $D011

        ; The bottom border comparison for RSEL=0 is line 247,
        ; which is already past. The VIC-II never sets the
        ; vertical border flip-flop.

        ; Later (e.g., at top of frame), restore 25-row mode:
        ; LDA $D011
        ; ORA #$08              ; set bit 3 (RSEL=1)
        ; STA $D011
```

The timing is forgiving: the switch just needs to happen sometime during the last visible
row area (roughly lines $F2-$FA), before the comparison at line $FB (251).

### 4.3 Side Border Removal

Side border removal is much more demanding than top/bottom removal because it must
happen on every single rasterline, and the timing must be cycle-exact.

**The trick:** On each rasterline, switch from 40-column mode (CSEL=1) to 38-column mode
(CSEL=0) at exactly the right cycle -- just before the right border comparison. In
38-column mode, the right border comparison occurs earlier (X=335) than in 40-column mode
(X=344). If you switch to CSEL=0 after X=335 but before X=344, the VIC-II has already
passed the 38-column comparison point, so the main border flip-flop is never set.

Then, before the start of the next line, restore CSEL=1 so the left border opens normally.

**Critical timing:** The switch must occur within a window of approximately 1 CPU cycle.
If you are one cycle early, the border appears normally. If you are one cycle late, same
result. This is why side border removal requires stable rasters.

```asm
; Must execute STA $D016 at exactly the right cycle on every visible line
; Typically done in an unrolled loop:

        LDA #$C0               ; CSEL=0 (38 columns), other bits as needed
        LDX #$C8               ; CSEL=1 (40 columns)

        ; ... (wait for exact cycle) ...
        STA $D016               ; switch to 38-col: right border missed
        STX $D016               ; restore 40-col for next line
```

**Complications with sprites and badlines:**
- When all 8 sprites are enabled, the sprite DMA steals so many cycles that there may
  not be enough CPU time to perform the switch on every line.
- On badlines, the VIC-II steals 40-43 cycles for character pointer fetches, leaving
  only ~20 cycles for the CPU. Careful scheduling is required.

### 4.4 Full Border Removal

Combining top/bottom and side border removal produces a fully open border -- the entire
screen area from edge to edge is available for display. This requires:

1. Top/bottom removal as described above (comparatively easy, once per frame)
2. Side border removal on every visible line, including those in the former top and
   bottom border area (very CPU-intensive)
3. Stable raster interrupt to ensure cycle-exact timing for side borders

### 4.5 What Can Be Displayed in the Border

When the borders are open, the VIC-II continues its normal rendering process. However,
the border area is outside the normal display window, so:

**Sprites:** The primary content displayed in the border. Sprites can be positioned
anywhere on screen, including the border area, and they render correctly when the border
is opened. This is by far the most common use of border removal: showing sprites in the
border gives the illusion of a larger playfield.

**Idle-state graphics:** When the VIC-II's graphics sequencer is in idle state (which it
is, in the border area), it reads from address $3FFF (or the equivalent address in the
current VIC bank: the last byte of the 16KB bank). The 8-bit value found there is
displayed repeatedly as a pattern. Changing this byte while the beam is in the border
allows rudimentary graphics, though always in the current background/foreground color
relationship.

**Limitations:**
- No color RAM data is available in the border, so character colors cannot change per-cell
- The idle pattern is always the same byte repeated
- For practical purposes, sprites are the only way to display meaningful graphics in the
  border


---

## 5. Classic Raster Effects

### 5.1 Raster Bars (Copper Bars)

Raster bars are horizontal bands of color that appear to float over the screen, often
animated with smooth sine-wave motion. They are created by changing $D020 (border color)
and/or $D021 (background color) on every rasterline.

**Basic implementation:**

```asm
; In a raster interrupt handler (ideally stable):
        LDX #$00                ; line counter
bar_loop:
        LDA color_table,X       ; get color for this line

        ; Wait for the beam to reach the start of the next line
        ; (exact timing via cycle counting or polling $D012)
wait:   CPX $D012
        BNE wait

        STA $D021               ; set background color
        STA $D020               ; set border color (optional)
        INX
        CPX #BAR_HEIGHT
        BNE bar_loop
```

For smooth animation, the color table is offset by a different amount each frame (driven
by a sine table or similar). The colors within a bar typically fade from dark to bright
and back: e.g., $00, $0B, $0C, $0F, $01, $0F, $0C, $0B, $00 for a white bar on black.

**The term "copper bars"** comes from the Amiga's Copper co-processor, which performs
similar per-line register changes in hardware. On the C64, the CPU must do all the work
manually.

### 5.2 Split-Screen: Mixed Graphics Modes

One of the most practical raster tricks is switching graphics modes mid-screen. A typical
game layout might be:

- Top portion: Hi-res or multicolor bitmap mode (the playfield)
- Bottom portion: Text mode (the status bar / score display)

The raster interrupt fires at the boundary line and toggles the relevant bits in $D011
(BMM for bitmap mode, ECM for extended color mode) and $D016 (MCM for multicolor mode).
It may also switch $D018 to point to a different screen/character/bitmap memory area.

```asm
; Switch from bitmap to text mode at a specific line
split_irq:
        ASL $D019
        LDA #$1B                ; text mode: BMM=0, DEN=1, RSEL=1
        STA $D011
        LDA #$15                ; point $D018 to text screen/charset
        STA $D018
        ; ... set up return interrupt at top for bitmap mode ...
```

**Important consideration:** The mode switch should happen during the vertical blanking
area between the two zones, or at least in the border area, to avoid visible glitches.
Switching during active display causes a corrupted line.

### 5.3 Color Washing / Rainbow Effects

Color washing animates the color RAM values to produce a rippling rainbow effect across
text or characters. While not strictly a raster interrupt technique, it is often combined
with raster timing:

- A raster interrupt triggers at the top of the screen
- The handler cycles through color RAM ($D800-$DBE7), incrementing or decrementing each
  color value by a phase offset that varies per frame and per line
- The result is a smooth wave of color flowing through the text

More advanced versions use per-line raster interrupts to change the background or text
colors every scanline, creating gradient effects across individual characters.

### 5.4 Per-Line Scrolling (Tech-Tech)

The "tech-tech" effect produces a wobbly, per-line horizontal distortion of the screen
by changing the horizontal scroll register ($D016 bits 0-2, XSCROLL) on every rasterline.

**Implementation:** A stable raster interrupt fires at the top of the visible area. The
handler then enters a tight loop that:
1. Waits for each rasterline
2. Loads a scroll offset from a sine table (indexed by rasterline + frame offset)
3. Writes it to $D016

```asm
; Per-line XSCROLL for tech-tech effect
        LDY #$00                ; rasterline index
techloop:
        LDA sine_table,Y
        AND #$07                ; keep only scroll bits 0-2
        ORA #$C8                ; preserve CSEL=1, MCM etc.

        ; wait for correct line
wait:   CPY $D012
        BNE wait

        STA $D016               ; apply horizontal scroll
        INY
        CPY #200                ; number of visible lines
        BNE techloop
```

For vertical per-line distortion, the YSCROLL value ($D011 bits 0-2) can be similarly
manipulated on each line, though this interacts with the badline mechanism and requires
careful handling (see FLD in section 6).


---

## 6. Advanced VIC-II Tricks

### 6.1 FLD (Flexible Line Distance)

FLD is a technique for vertically displacing screen content by suppressing badlines. It
was first demonstrated in the "Think Twice" demo by The Judges in 1986.

#### How It Works

A badline occurs when the low 3 bits of the raster counter ($D012 & 7) equal YSCROLL
($D011 bits 0-2). On a badline, the VIC-II fetches a new row of character pointers and
resets the row counter (RC) to 0.

By manipulating YSCROLL so that it never matches the raster counter, badlines are
suppressed. Without a badline, the VIC-II does not start a new character row -- it
simply continues displaying whatever it was displaying before (or idle/blank lines if
no display was active yet).

**To suppress a badline on a given line:** Before the VIC-II checks for the badline
condition (which happens at the start of each line), change YSCROLL so its low 3 bits
do not match the low 3 bits of the current raster counter.

**To resume normal display:** Set YSCROLL so that it matches the current raster counter.
The next line will be a badline, and normal display resumes.

#### Practical Application

FLD allows scrolling the entire screen downward by an arbitrary number of pixels without
moving any screen memory data. The interrupt handler runs during the blank area at the
top of the screen and suppresses badlines by incrementing YSCROLL every line (staying
one step ahead of the raster counter).

```asm
; FLD: suppress N badlines to push display down by N pixels
fld_irq:
        ASL $D019

        LDX fld_offset          ; number of lines to delay (0-199)
        BEQ fld_done

        LDA $D011
        AND #$F8                ; clear YSCROLL bits
        ORA #$18                ; DEN=1, RSEL=1
        STA $D011

fld_loop:
        ; Wait for next rasterline
        LDA $D012
fld_wait:
        CMP $D012
        BEQ fld_wait

        ; Set YSCROLL to NOT match current line
        LDA $D012
        CLC
        ADC #$01                ; YSCROLL = (raster+1) & 7
        AND #$07
        ORA #$18                ; preserve DEN, RSEL
        STA $D011

        DEX
        BNE fld_loop

fld_done:
        ; ... restore normal YSCROLL for display ...
```

**Key timing constraint:** YSCROLL must be modified before cycle 14 of the line (before
the VIC-II asserts the badline condition).

#### Uses

- Vertical scrolling of large bitmaps without data copying
- Sliding individual text lines into position
- Screen wipe effects
- Part of more complex effects like AGSP (Any Given Screen Position)

### 6.2 FLI (Flexible Line Interpretation)

FLI overcomes one of the C64's most frustrating graphics limitations: in bitmap mode,
the color information (stored in screen RAM and color RAM) is shared across all 8 pixel
rows of each 8x8 character cell. FLI forces the VIC-II to reload color data on every
single rasterline, allowing unique colors per pixel row.

#### How It Works

FLI combines two simultaneous tricks on every rasterline:

1. **Force a badline every line** by modifying YSCROLL ($D011 bits 0-2) so it always
   matches the low 3 bits of the raster counter. Normally badlines occur every 8th line;
   FLI forces them on every line.

2. **Switch screen RAM pointer** by changing $D018 on each line. Since the VIC-II
   re-reads screen RAM on every badline, pointing to a different screen RAM block each
   line allows unique color values per pixel row.

#### Memory Layout

FLI requires 8 separate 1KB screen RAM blocks (one for each pixel row within a character
cell) plus the bitmap data, all within a single 16KB VIC bank. A typical layout in bank
1 ($4000-$7FFF):

| Address       | Content                  |
|---------------|--------------------------|
| $4000-$43FF   | Screen RAM 0 (line 0 of each char) |
| $4400-$47FF   | Screen RAM 1 (line 1)    |
| $4800-$4BFF   | Screen RAM 2 (line 2)    |
| $4C00-$4FFF   | Screen RAM 3 (line 3)    |
| $5000-$53FF   | Screen RAM 4 (line 4)    |
| $5400-$57FF   | Screen RAM 5 (line 5)    |
| $5800-$5BFF   | Screen RAM 6 (line 6)    |
| $5C00-$5FFF   | Screen RAM 7 (line 7)    |
| $6000-$7FFF   | Bitmap data (8KB)        |

#### Timing

The code must execute within an extremely tight cycle window on every rasterline:

```asm
; Core FLI loop (unrolled for 8 lines, then repeats)
; Must hit these exact cycles:
;   Cycle ~2-4:  LDA d018_table,y
;   Cycle ~6-8:  STA $D018
;   Cycle ~10-14: STA $D011       ; triggers badline -> CPU stunned until cycle 54

        LDY #$00
fli_line:
        LDA d018_vals,Y          ; load screen RAM pointer for this line
        STA $D018                ; switch screen RAM
        LDA d011_vals,Y          ; load YSCROLL for this line
        STA $D011                ; force badline (YSCROLL matches raster)
        ; VIC-II immediately stuns CPU for ~40 cycles (badline)
        ; When CPU resumes, we're at the start of the next line
        INY
        CPY #200                 ; all visible lines done?
        BNE fli_line
```

#### The FLI Bug

A distinctive artifact of FLI: the leftmost 3 characters (24 pixels) of every character
row display garbage -- typically a solid light-grey block. This happens because when the
forced badline occurs, the VIC-II needs to read character pointers starting from the
left edge, but the CPU's write to $D018 hasn't propagated yet for the first few
characters. The VIC-II reads the data bus (which contains open-bus values, typically $FF),
interpreted as color data.

The FLI bug is an inherent limitation that cannot be fully eliminated, though it can be
partially hidden by placing dark-colored sprites over the affected area.

#### Variants

- **AFLI (Advanced FLI):** Uses hi-res bitmap mode instead of multicolor for higher
  horizontal resolution (320px vs 160px), but with fewer colors per cell.
- **IFLI (Interlace FLI):** Alternates between two FLI screens on consecutive frames
  with a 1-pixel horizontal offset, doubling the effective color resolution through
  persistence of vision.
- **NUFLI / NUFLIX:** Modern variants that use sprites to cover the FLI bug area and
  additional tricks for even more colors.
- **SHIFLI / UIFLI:** Further extensions combining sprites, interlace, and underlay
  techniques.

### 6.3 DMA Delay

DMA delay (also called "partial badline" or "partial rescan") is a technique where a
badline is triggered not at the normal time (cycle 14) but partway through the line, by
modifying $D011 so that YSCROLL matches the raster counter at a later cycle.

#### How It Works

Normally, the VIC-II checks for the badline condition at the beginning of each line (the
comparison is continuous, but the DMA begins at cycle 15 if the condition is true by cycle
14). If you change YSCROLL to match the raster counter at a later cycle (say, cycle 20),
the VIC-II recognizes the badline late and begins its character pointer fetch late.

The result: the character data for that line is shifted horizontally, because the VIC-II
starts reading character pointers from the current position rather than from the
beginning of the line. This produces a visible horizontal displacement.

**Important:** The VIC-II immediately stuns the CPU when the badline is detected, keeping
it stunned until cycle 54 regardless of when the badline started. This provides a
form of automatic raster stabilization -- the CPU always resumes at the same cycle.

#### Uses

- Horizontal scrolling of the entire screen without moving memory (VSP, see below)
- Creating partial-line visual effects
- Raster stabilization (since the CPU always resumes at a known cycle after a forced
  badline)

### 6.4 VSP (Variable Screen Position)

VSP exploits DMA delay to scroll the entire screen horizontally by arbitrary amounts
without copying screen data. It is one of the most powerful -- and most dangerous --
VIC-II tricks.

#### How It Works

By triggering a badline with precise cycle timing during the idle state of the VIC-II's
graphics sequencer, the point at which character pointer fetching begins is offset. This
causes the VIC-II to read character data starting from a different position in screen
RAM, effectively scrolling the display horizontally.

The three conditions required:
1. The VIC-II is in the visible display area (rasterlines $30-$F7)
2. YSCROLL is modified so its low 3 bits match the current raster counter
3. The modification occurs during cycles 15-53 of a line when the sequencer is idle

By varying the exact cycle at which the badline is triggered, the horizontal offset can
be controlled with single-character precision.

#### The VSP Crash Problem

VSP is known as "the forbidden technique" because it can cause random memory corruption
and crashes on some C64 hardware.

**Root cause:** When VSP is triggered, the VIC-II changes its address bus from one value
to another very quickly ($FF to $07 or similar). If the DRAM Row Address Strobe (RAS)
signal arrives during this transition, the DRAM's row register enters a metastable state.
This causes incorrect DRAM refresh: multiple memory rows are briefly connected to the
same sense lines, corrupting their contents.

**Which bytes are affected:** Only "fragile" memory locations are vulnerable -- addresses
whose low nybble is $x7 or $xF (every 8th byte). These are the bytes whose row address
is affected by the bit transition.

**Machine-dependent:** Whether corruption occurs depends on temperature, VIC-II revision,
motherboard trace capacitance, power supply quality, and other analog factors. Some C64s
are entirely immune; others crash frequently.

#### Mitigation Strategies

1. **Identical fragile bytes:** Ensure all fragile bytes within each memory page contain
   the same value, so corruption replaces a value with itself.
2. **Avoid fragile locations:** Use undocumented NOP opcodes ($80 = 2-byte NOP) to skip
   over fragile addresses in code. Design data structures with gaps at every 8th byte.
3. **Continuous repair:** Copy graphics data from a safe backup buffer to the display
   buffer every frame, overwriting any corruption.
4. **Safe VSP (Linus Akesson):** A technique that carefully controls which memory pages
   are affected, ensuring corruption is harmless.

#### Notable Uses

Despite the crash risk, VSP has been used in several famous games:
- Mayhem in Monsterland
- Creatures
- Various demos with full-screen, full-color scrolling

### 6.5 Line Crunch

Line crunch (also called "line skip" or "AGSP" when combined with VSP) compresses
character rows by manipulating the badline mechanism to skip display lines.

#### How It Works

Normally, when a badline occurs at the start of a character row, the row counter (RC) is
reset to 0 and counts up to 7 over the next 8 rasterlines. When RC reaches 7 at cycle
58, VCBASE is loaded from VC, advancing to the next character row.

Line crunch exploits this by:
1. Allowing a badline to begin (RC resets to 0, character pointers are fetched)
2. Immediately canceling the badline by changing YSCROLL before cycle 14 so the condition
   is no longer true
3. The VIC-II sees RC != 7 at cycle 58 and does NOT advance VCBASE
4. But on the next line, a new badline is triggered, resetting RC to 0 again
5. This repeats: each character row is "crunched" into a single rasterline

The effect: the VIC-II advances through screen memory at one character row per rasterline
instead of one row per 8 rasterlines. The screen content scrolls upward rapidly.

```asm
; Crunch one character line into a single rasterline
; Must write $D011 at exactly the right cycle (before cycle 14)
crunch_loop:
        LDA $D012
crunch_wait:
        CMP $D012
        BEQ crunch_wait         ; wait for next line

        ; Change YSCROLL to NOT match current raster (cancel badline)
        LDA $D011
        EOR #$07                ; flip low 3 bits
        AND #$07
        ORA #$18                ; DEN=1, RSEL=1
        STA $D011               ; must hit before cycle 14

        DEX
        BNE crunch_loop
```

**Key timing:** The YSCROLL modification must happen before cycle 14 (the VIC-II's
badline assertion point) but after the badline has already been detected at the start of
the line. The safe write window is extremely narrow -- sometimes only 1 cycle on certain
models.

#### AGSP (Any Given Screen Position)

AGSP combines VSP (horizontal positioning) with line crunch (vertical positioning) to
place the display at any arbitrary pixel position on screen. This enables full-screen
smooth scrolling in all directions without moving large amounts of memory.

### 6.6 Sprite Stretching and Multiplexing

While not strictly raster "tricks" in the same category, these rely on the same
principles:

**Sprite multiplexing:** Re-positioning sprites lower on the screen after they have
been fully drawn. On each rasterline after a sprite's last display line, its Y-position,
pointer, and data can be changed to display a different sprite image lower on screen.
With careful timing, 8 hardware sprites can display 20+ virtual sprites per frame.

**Sprite stretching:** The VIC-II uses an expansion flip-flop for vertically-doubled
sprites that toggles each rasterline. By toggling the Y-expansion register ($D017) at
specific cycles, the flip-flop can be kept in its "repeat this line" state, causing each
sprite data line to repeat an arbitrary number of times. This makes sprites appear
vertically stretched beyond the normal 2x limit.


---

## 7. Hardcore Details

### 7.1 Exact PAL Timing (MOS 6569)

| Parameter            | Value      |
|----------------------|------------|
| System clock (phi2)  | 985,248 Hz |
| Dot clock            | 7,881,984 Hz (8x system clock) |
| Cycles per line      | 63         |
| Pixels per line      | 504        |
| Total raster lines   | 312        |
| Visible raster lines | ~284       |
| Frame rate           | 50.125 Hz  |
| Cycles per frame     | 19,656     |

**First visible line:** ~16 (varies by display/TV)
**First display window line (RSEL=1):** 51
**Last display window line (RSEL=1):** 250
**First display window line (RSEL=0):** 55
**Last display window line (RSEL=0):** 246
**First badline-eligible line:** $30 (48)
**Last badline-eligible line:** $F7 (247)

### 7.2 Exact NTSC Timing

**MOS 6567R8 (most common NTSC):**

| Parameter            | Value       |
|----------------------|-------------|
| System clock (phi2)  | 1,022,727 Hz |
| Dot clock            | 8,181,816 Hz |
| Cycles per line      | 65          |
| Pixels per line      | 520         |
| Total raster lines   | 263         |
| Frame rate           | 59.826 Hz   |
| Cycles per frame     | 17,095      |

**MOS 6567R56A (early NTSC, rare):**

| Parameter            | Value       |
|----------------------|-------------|
| Cycles per line      | 64          |
| Total raster lines   | 262         |
| Cycles per frame     | 16,768      |

NTSC machines have 2 extra cycles per line compared to PAL (65 vs 63), which provides
more CPU time per line. However, they have fewer total lines per frame (263 vs 312),
giving less total CPU time per frame (17,095 vs 19,656 cycles).

### 7.3 Badline Conditions

A badline occurs when ALL of the following are true simultaneously:

1. The raster counter is in the range $30-$F7 (lines 48-247)
2. The low 3 bits of the raster counter equal YSCROLL (bits 0-2 of $D011)
3. The DEN bit ($D011 bit 4) was set at some point during rasterline $30

**Condition 3 is subtle:** DEN only needs to have been set at any cycle during line $30.
Once it has been set during that line, badlines are enabled for the entire frame. Even
if DEN is subsequently cleared, badlines continue until line $30 of the next frame.

**On a badline:**
- The VIC-II asserts the BA (Bus Available) signal LOW three cycles before it needs the
  bus (starting at cycle 12 on PAL)
- At cycle 15, the VIC-II takes over the bus and the CPU is "stunned"
- 40 c-accesses occur during cycles 15-54 (one per cycle, in the phi2 phase)
- The CPU regains the bus after cycle 54
- Total CPU cycles lost: approximately 40-43 (varies due to instruction overlap)
- Available CPU cycles on a badline: approximately 20-23

**On a normal (non-bad) line:**
- The CPU has all 63 cycles available (minus any sprite DMA)
- The VIC-II performs g-accesses (graphics data reads) during phi1, sharing the bus
  without stealing CPU time

### 7.4 VIC-II Internal Counters

The VIC-II maintains several internal counters that are not directly accessible to the
programmer but whose behavior is exploited by every advanced trick:

#### VC (Video Counter) -- 10 bits

Indexes the position within the 1000-byte video matrix (screen RAM). Ranges from 0 to
999 under normal operation.

- **Cycle 14, phase 1:** VC is loaded from VCBASE; VMLI (Video Matrix Line Index) is
  cleared to 0.
- **During display:** VC and VMLI are incremented after each g-access in display state.
- VC effectively tracks "which character column is being drawn."

#### VCBASE (Video Counter Base) -- 10 bits

Stores the starting VC value for the current character row.

- **Cycle 58, phase 1:** If RC=7, VCBASE is loaded from VC (advancing to the next row).
- **Outside lines $30-$F7:** VCBASE is reset to 0.
- VCBASE is what determines which row of characters is displayed; manipulating when and
  whether VCBASE updates is the basis of line crunch and FLD.

#### RC (Row Counter) -- 3 bits

Counts from 0 to 7, tracking which pixel row within the current character cell is being
drawn.

- **Badline:** RC is reset to 0 at cycle 14, phase 1.
- **Cycle 58, phase 1:** If RC=7, the VIC-II enters idle state and updates VCBASE.
  Otherwise, RC is incremented.
- **Normal (non-bad) line:** RC increments at cycle 58 if the VIC-II is in display state.

The RC counter is central to understanding why badlines occur every 8 lines: RC counts
0, 1, 2, 3, 4, 5, 6, 7 across 8 rasterlines, then a new badline resets it to 0.

#### VMLI (Video Matrix Line Index) -- 6 bits

Indexes into the 40-character internal line buffer.

- Cleared at cycle 14 when VC loads from VCBASE.
- Incremented with VC after each g-access in display state.

### 7.5 Cycle-by-Cycle Breakdown (PAL 6569)

#### Normal Line (No Badline, No Sprites)

```
Cycle  1-9:   VIC-II phi1: idle/refresh accesses
               CPU phi2: full speed (9 cycles available)
Cycle 10-14:  VIC-II phi1: refresh accesses (5 total: cycles 1,2,3,4,5 area)
               CPU phi2: full speed
Cycle 15-54:  VIC-II phi1: 40 g-accesses (graphics data fetch)
               CPU phi2: full speed (VIC uses only phi1)
Cycle 55-62:  VIC-II phi1: sprite p-access and s-access (if sprites active)
               CPU phi2: may be stolen if sprite DMA active
Cycle 63:     VIC-II phi1: idle access
               CPU phi2: available
```

On a normal non-badline with no sprites, the CPU has all 63 cycles.

#### Badline

```
Cycle  1-11:  CPU runs normally
Cycle 12:     BA goes LOW (VIC-II signals it will need the bus)
Cycle 13-14:  CPU can finish current instruction but cannot start new reads
              (BA LOW for 3 cycles = "3-cycle bus request")
Cycle 15-54:  VIC-II owns the bus:
              - phi1: g-access (graphics data)
              - phi2: c-access (character pointer + color)
              CPU is stunned -- no execution
Cycle 55-63:  CPU resumes; sprite accesses may steal additional cycles
```

CPU cycles available on a badline: ~20-23 (cycles 1-11 plus some of 55-63, minus sprite
overhead).

#### Refresh Cycles

The VIC-II performs 5 DRAM refresh cycles per line. On the 6569 (PAL), these occur during
cycles 1-5 (phi1 phase). The CPU is not affected because refresh uses only the phi1 phase.

### 7.6 Sprite DMA and Raster Timing

Each sprite that is active on a given rasterline costs the CPU stolen cycles due to DMA.
The exact cost depends on which sprites are enabled and whether they are consecutive.

#### Sprite Cycle Assignments (PAL 6569)

Sprite data access occurs in the cycles following the display area:

| Sprite | p-access (pointer) | s-access (data: 3 bytes) |
|--------|-------------------|--------------------------|
| 0      | Cycle 58          | Cycles 59, 60, 61*       |
| 1      | Cycle 60*         | Cycles 61*, 62, 63       |
| 2      | Cycle 62*         | Cycles 63, 1, 2*         |
| 3      | Cycle 1*          | Cycles 2*, 3, 4          |
| 4      | Cycle 3*          | Cycles 4*, 5, 6          |
| 5      | Cycle 5*          | Cycles 6*, 7, 8          |
| 6      | Cycle 7*          | Cycles 8*, 9, 10         |
| 7      | Cycle 9*          | Cycles 10*, 11, 12*      |

(*Note: Exact cycle numbering follows Christian Bauer's VIC-II article. Some cycles
overlap between sprites.)

#### Bus Takeover Overhead

The VIC-II needs 3 cycles of BA=LOW before it can take the bus. This creates three
cost scenarios:

| Situation | Cycles Stolen | Explanation |
|-----------|--------------|-------------|
| First active sprite (or gap of 2+ inactive sprites before) | 5 | 3 bus takeover + 2 data |
| Consecutive with previous active sprite | 2 | VIC-II already has bus |
| One inactive sprite gap | 4 | VIC-II holds bus through gap |

**Example costs:**
- Sprites 0 only: 5 cycles
- Sprites 0,1: 5+2 = 7 cycles
- Sprites 0,1,2: 5+2+2 = 9 cycles
- Sprites 0,2 (gap at 1): 5+4 = 9 cycles (same as 0,1,2!)
- All 8 sprites: 5+2+2+2+2+2+2+2 = 19 cycles

This means enabling a "gap" sprite doesn't save cycles compared to enabling all sprites
in the range. Sprites 0 and 2 cost the same as sprites 0, 1, and 2.

#### Impact on Raster Effects

Sprite DMA can interfere with cycle-exact raster effects:
- Side border opening may fail when too many sprites are active on the same line
- FLI timing must account for sprite DMA reducing available cycles
- Line crunch windows become tighter with sprites enabled
- Opening side borders with all 8 sprites enabled requires special scheduling where
  sprite DMA cycles are carefully interleaved with border-switch timing

### 7.7 The D011 Trick

Modifying $D011 at specific cycles within a rasterline can produce a wide variety of
effects, all stemming from the way the VIC-II continuously evaluates its internal
conditions:

#### Cycle Windows and Effects

| Modification Timing | Effect |
|---------------------|--------|
| Before cycle 14 | Can prevent or trigger a badline for the current line |
| At cycle 14-15 | Can create a "partial badline" (DMA delay) |
| During cycles 15-53 | Can trigger late badline (DMA delay / VSP) |
| At cycle 58 | Can affect RC=7 check and VCBASE update |
| Any time | Changing RSEL affects border comparisons on subsequent lines |

#### Key Principles

1. **Badline triggering:** Writing YSCROLL to match the current raster counter's low 3
   bits at any cycle where the badline condition is checked will trigger (or re-trigger)
   a badline.

2. **Badline cancellation:** Writing YSCROLL to NOT match the raster counter before
   cycle 14 will prevent the badline for that line.

3. **Late badline (DMA delay):** Writing YSCROLL to match the raster counter after
   cycle 14 but before cycle 54 triggers a late badline. The VIC-II immediately stuns
   the CPU and begins c-accesses from the current position. This shifts the screen
   content horizontally.

4. **DEN manipulation:** If DEN ($D011 bit 4) has never been set during line $30 of
   the current frame, no badlines will occur at all -- the entire screen is blank.
   This can be used for blanking effects or to free up CPU time.

5. **Border manipulation:** Changing RSEL ($D011 bit 3) affects the vertical border
   comparison thresholds. Toggling at the right rasterline "fools" the border flip-flop
   into never activating.


---

## 8. Putting It All Together

### 8.1 Effect Complexity Hierarchy

| Effect | Stable Raster Required? | Difficulty |
|--------|------------------------|------------|
| Background color split | No | Beginner |
| Raster bars (in border) | Helps | Beginner-Intermediate |
| Sprite multiplexing | Recommended | Intermediate |
| Multiple mode splits | No | Intermediate |
| Top/bottom border open | No (timing is forgiving) | Intermediate |
| FLD | No (but helps for smooth results) | Intermediate |
| Smooth raster bars (in display) | Yes | Intermediate-Advanced |
| Tech-tech / per-line scroll | Yes | Advanced |
| Line crunch | Yes | Advanced |
| Side border open | Yes | Advanced |
| FLI | Yes | Advanced |
| VSP | Yes | Advanced (plus crash risk) |
| AGSP | Yes | Expert |
| Full border open + sprites | Yes | Expert |

### 8.2 Common Pitfalls

- **Forgetting to acknowledge $D019:** The interrupt immediately re-triggers, causing the
  system to lock up.
- **Not disabling CIA interrupts:** CIA-1 timer IRQs interfere with VIC-II raster IRQs
  unless explicitly disabled.
- **Badline disruption:** A badline occurring during a cycle-exact routine ruins the
  timing. Always account for which lines are badlines.
- **Sprite DMA disruption:** Active sprites steal cycles at specific points in the line.
  Raster effects must be scheduled around sprite DMA windows.
- **$D011 bit 7 vs $D012:** Forgetting that the raster comparison is 9 bits wide. For
  lines >= 256, bit 7 of $D011 must be set.
- **NTSC vs PAL differences:** Code tuned for 63 cycles/line (PAL) breaks on 65
  cycles/line (NTSC) and vice versa. Portable code must detect the system and adjust.


---

## 9. References

### Primary Technical Documentation

- Christian Bauer, "The MOS 6567/6569 Video Controller (VIC-II) and Its Application
  in the Commodore 64" (1996) -- the definitive VIC-II technical reference:
  https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt
  (Also mirrored at https://www.cebix.net/VIC-Article.txt)

- MOS 6567/6569 Memory Access Patterns (PAL timing):
  https://ist.uwaterloo.ca/~schepers/MJK/ascii/vic2-pal.txt

### Stable Raster Techniques

- Pasi Ojala (Albert), "Making Stable Raster Routines":
  https://www.antimon.org/dl/c64/code/stable.txt

- Codebase64 Wiki, "Stable Raster Routine":
  https://codebase.c64.org/doku.php?id=base:stable_raster_routine

- Codebase64 Wiki, "Introduction to Stable Timing":
  https://codebase.c64.org/doku.php?id=base:introduction_to_stable_timing

- Bumbershoot Software, "Stabilizing the VIC-II Raster":
  https://bumbershootsoft.wordpress.com/2015/12/29/stabilizing-the-vic-ii-raster/

- Bumbershoot Software, "VIC-II Interrupt Timing":
  https://bumbershootsoft.wordpress.com/2015/07/26/vic-ii-interrupt-timing-or-how-i-learned-to-stop-worrying-and-love-unstable-rasters/

### Raster Interrupts

- C64-Wiki, "Raster Interrupt":
  https://www.c64-wiki.com/wiki/Raster_interrupt

- Codebase64 Wiki, "Introduction to Raster IRQs":
  http://codebase.c64.org/doku.php?id=base:introduction_to_raster_irqs

### Border Opening

- Pasi Ojala (Albert), "Opening the Borders":
  https://www.antimon.org/dl/c64/code/opening.txt

- Aart Bik, "Opening Top and Bottom Borders on the Commodore 64":
  https://aartbik.blogspot.com/2019/09/opening-top-and-bottom-borders-on.html

### FLD, FLI, and Advanced Techniques

- Bumbershoot Software, "Flexible Line Distance (FLD)":
  https://bumbershootsoft.wordpress.com/2015/09/17/flexible-line-distance-fld/

- Bumbershoot Software, "FLI, Part 1: 16 Color Mode":
  https://bumbershootsoft.wordpress.com/2016/03/12/fli-part-1-16-color-mode/

- Pasi Ojala (Albert), "FLI - More Color to the Screen":
  http://www.antimon.org/code/fli.txt

- nurpax, "BINTRIS C64: Bad Lines and Flexible Line Distance":
  https://nurpax.github.io/posts/2018-06-19-bintris-on-c64-part-5.html

- Bumbershoot Software, "Partial Badlines: Glitching on Purpose":
  https://bumbershootsoft.wordpress.com/2015/10/18/partial-badlines-glitching-on-purpose/

- C64-Wiki, "NUFLI":
  https://www.c64-wiki.com/wiki/NUFLI

### VSP and Line Crunch

- Bumbershoot Software, "Variable Screen Placement: The VIC-II's Forbidden Technique":
  https://bumbershootsoft.wordpress.com/2015/04/19/variable-screen-placement-the-vic-iis-forbidden-technique/

- Linus Akesson, "Safe VSP":
  https://linusakesson.net/scene/safevsp/index.php

- kodiak64, "The Future of VSP Scrolling on the C64":
  https://kodiak64.co.uk/blog/future-of-VSP-scrolling

- Codebase64 Wiki, "AGSP (Any Given Screen Position)":
  http://codebase.c64.org/doku.php?id=base:agsp_any_given_screen_position

- Codebase64 Wiki, "Smooth Line Crunch":
  https://codebase.c64.org/doku.php?id=base:smooth_linecrunch

- CSDb Forum, "HSP/DMA Delay and Screen Corruption":
  https://csdb.dk/forums/?roomid=11&topicid=53517

- IndividualComputers Wiki, "VSP-Fix":
  https://wiki.icomp.de/wiki/VSP-Fix

### Sprite Timing

- Bumbershoot Software, "Sprites and Raster Timing on the C64":
  https://bumbershootsoft.wordpress.com/2016/02/05/sprites-and-raster-timing-on-the-c64/

### General VIC-II Resources

- C64-Wiki, "VIC":
  https://www.c64-wiki.com/wiki/VIC

- Dustlayer, "VIC-II for Beginners Part 3 - Beyond the Screen":
  https://dustlayer.com/vic-ii/2013/4/25/vic-ii-for-beginners-beyond-the-screen-rasters-cycle

- C64 OS, "VIC-II and FLI Timing" (3-part series):
  https://c64os.com/post/flitiming1

- An Introduction to Programming C-64 Demos:
  https://odd.blog/wp-content/uploads/2008/01/intro-to-programming-c64-demos.html

### VIC-II Register Reference

- Oxyron, "VIC-II Register Reference":
  https://www.oxyron.de/html/registers_vic2.html

- C64-Wiki, "$D011 (53265)":
  https://www.c64-wiki.com/wiki/53265
