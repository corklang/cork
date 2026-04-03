# Game Programming on the Commodore 64

## Table of Contents

1. [Overview](#1-overview)
2. [Game Loop Architecture](#2-game-loop-architecture)
3. [Tile-Based Engines](#3-tile-based-engines)
4. [Sprite-Based Game Objects](#4-sprite-based-game-objects)
5. [Collision Detection](#5-collision-detection)
6. [Physics and Movement](#6-physics-and-movement)
7. [Game Types and Techniques](#7-game-types-and-techniques)
8. [Sound in Games](#8-sound-in-games)
9. [Level Design and Data](#9-level-design-and-data)
10. [Hardcore Details](#10-hardcore-details)

---

## 1. Overview

### The C64 as a Game Platform

The Commodore 64 was the most commercially successful single computer model in history, and games were its primary use case. Despite severe hardware constraints, thousands of games were produced for it between 1982 and the mid-1990s, with many achieving a quality that still impresses. The machine's game-programming legacy is defined by the creative tension between what programmers wanted to achieve and what the hardware allowed.

### Hardware Constraints for Game Developers

The C64 presents a specific set of constraints that shape every design decision:

- **CPU**: MOS 6510 running at approximately 0.985 MHz (PAL) or 1.023 MHz (NTSC). This is a single-core, 8-bit processor with no multiplication or division instructions. All math must be done in software.
- **RAM**: 64 KB total, shared between the CPU and the VIC-II graphics chip. In practice, a single-load game can use roughly 60 KB. The remainder is consumed by I/O space, the zero-page/stack, and a few bytes of system overhead.
- **Graphics**: The VIC-II chip provides 8 hardware sprites (24x21 pixels mono, 12x21 multicolor), character-mode and bitmap-mode displays, hardware smooth scrolling (0-7 pixels in X and Y), and a 16-color palette. The screen is 320x200 pixels (40x25 characters).
- **Sound**: The SID chip (MOS 6581/8580) provides 3 monophonic synthesizer voices with ADSR envelopes, four waveforms each, a multimode resonant filter, ring modulation, and oscillator sync. All music and sound effects must share these 3 voices.
- **Storage**: The 1541 disk drive holds approximately 170 KB per side. The default serial bus transfer speed is agonizingly slow at roughly 400 bytes/second, though custom fast loaders can achieve 2,500+ bytes/second.

### How Legendary Games Worked Within the Constraints

The greatest C64 games succeeded through a combination of techniques:

- **Assembly language**: Virtually all commercial C64 games were written entirely in 6502/6510 assembly language. The CPU is too slow for compiled or interpreted languages to handle game logic, graphics, and sound simultaneously.
- **Cycle-counted code**: Developers learned exactly how many CPU cycles each instruction consumed and timed their code to the raster beam. A common debugging technique was to change the border color ($D020) before and after a code section -- the width of the colored stripe in the border showed exactly how much raster time that code consumed.
- **Hardware exploitation**: Rather than treating the VIC-II as a simple framebuffer, developers exploited its quirks: raster interrupts for split-screen effects, sprite multiplexing to exceed the 8-sprite limit, hardware scroll registers for smooth scrolling, and undocumented behaviors like VSP (Variable Screen Placement) for extreme optimizations.
- **Data compression**: With only 64 KB of RAM for a single-load game, every byte mattered. Level data was compressed with RLE, dictionary coding, and custom schemes. Character sets served double duty as tile graphics. Maps were built from hierarchical tiles-of-tiles to minimize storage.
- **Clever design**: Game designers worked with the hardware rather than against it. Side-scrolling games were natural because the VIC-II has hardware horizontal scroll support. Character-mode graphics were preferred over bitmaps because they use 8x less RAM and the CPU can update the screen 8x faster. Gameplay was designed around what the machine could do well.

### The Development Environment

In the 1980s, most C64 games were developed directly on the machine using tools like Turbo Assembler (TASS) or Turbo Macro Pro (TMP). The programmer would write assembly code, assemble it, and test it on the same hardware.

Modern C64 game development uses cross-development tools running on a PC:

- **Assemblers**: Kick Assembler (Java-based, with a powerful scripting language), ACME (fast, supports illegal opcodes), ca65 (part of the cc65 suite), 64tass, DASM
- **C Compilers**: cc65 (mature C compiler and toolchain for 6502), KickC (C compiler targeting the C64 specifically), Oscar64 (newer C compiler achieving near-assembly performance)
- **IDEs**: Relaunch64, VS64 (Visual Studio Code extension), CBM prg Studio
- **Graphics Tools**: CharPad (character set and tile map editor, essential since 2003), SpritePad (sprite editor), Spritemate (web-based)
- **Music Tools**: GoatTracker, SID-Wizard, CheeseCutter
- **Emulators**: VICE (the standard, with built-in debugger and monitor), Hoxs64, micro64

---

## 2. Game Loop Architecture

### The Fundamental Game Loop

Every C64 game revolves around a main loop that repeats continuously. In its simplest form:

```
GameLoop:
    jsr ReadInput           ; check joystick/keyboard
    jsr UpdateGameLogic     ; AI, physics, state changes
    jsr UpdateGraphics      ; sprite positions, scrolling, animation
    jsr UpdateSound         ; music player tick, sound effects

    ; Wait for the right moment in the frame
    lda #$F8
.wait:
    cmp $D012              ; compare with current raster line
    bne .wait

    jmp GameLoop
```

The loop runs once per frame, producing either 50 updates/second (PAL) or 60 updates/second (NTSC). The key challenge is fitting all game logic, graphics updates, and sound processing into the time budget of a single frame.

### Frame Timing and the Raster Beam

The C64's display is generated by a raster beam that scans the screen from top to bottom, left to right, 50 times per second on PAL (60 on NTSC). The VIC-II chip provides a raster counter register at $D012 (low 8 bits) and bit 7 of $D011 (high bit), allowing the CPU to know exactly which scanline is currently being drawn.

**PAL timing:**
- 312 raster lines per frame
- 63 CPU cycles per raster line
- 19,656 total CPU cycles per frame
- Visible display area: 200 raster lines (lines 51-250)
- Vertical blanking: 112 raster lines of "free" CPU time

**NTSC timing:**
- 263 raster lines per frame
- 65 CPU cycles per raster line
- 17,095 total CPU cycles per frame
- Visible display area: 200 raster lines
- Vertical blanking: 63 raster lines of "free" CPU time

The difference is significant: PAL games have approximately 15% more CPU time per frame than NTSC games, and nearly twice as much vertical blanking time. This is why many games were developed for PAL first, and NTSC ports sometimes had to cut features or run slower.

### Bad Lines and Stolen Cycles

Not all raster lines give the CPU the same number of cycles. Every 8th raster line within the display area is a "bad line" where the VIC-II steals approximately 40 additional cycles from the CPU to fetch character pointers from screen RAM. On a bad line, only 20-23 CPU cycles are available instead of the normal 63.

This means that during the 200-line visible display:
- 25 lines are bad lines (one per character row), each providing ~23 cycles
- 175 lines are normal, each providing ~63 cycles
- Sprites steal additional cycles: each sprite on a given line costs 2 cycles for the initial fetch plus 2 cycles per sprite

Game programmers must account for bad lines when writing time-critical code. A routine that needs cycle-exact timing (such as a sprite multiplexer or a raster split) must know whether a bad line will occur during its execution window.

### Synchronization Strategies

There are two primary approaches to synchronizing the game loop with the display:

**Polling approach (simple games):**
The main loop does all its work, then busy-waits for a specific raster line (typically $F8 or similar, near the bottom of the visible display or in the vertical blank). This is simple but wastes CPU cycles during the wait.

```
    ; Two-stage wait to avoid re-triggering on the same frame
.notYet:
    lda $D012
    cmp #$F8
    beq .notYet         ; if already on line $F8, wait until we leave it
.waitForLine:
    lda $D012
    cmp #$F8
    bne .waitForLine     ; wait until we reach line $F8
```

**Interrupt-driven approach (most commercial games):**
A raster interrupt fires at a specific scanline. The interrupt handler performs time-critical tasks (updating hardware scroll registers, sprite positions, playing music) and sets a flag. The main loop polls this flag, does its work, then waits for the next interrupt.

```
    ; In the IRQ handler:
    inc FrameReady       ; signal to main loop

    ; In the main loop:
.waitFrame:
    lda FrameReady
    beq .waitFrame       ; wait for next frame
    lda #$00
    sta FrameReady       ; acknowledge

    jsr GameLogic
    jmp .waitFrame
```

### Splitting Work Across Frames

When the game logic is too complex to fit in a single frame, work must be distributed across multiple frames. Common strategies:

- **Alternating tasks**: Update player physics on even frames, enemy AI on odd frames. The game still runs at 50fps visually, but AI updates at 25fps.
- **Task queues**: Maintain a list of deferred work items. Each frame processes a few items from the queue.
- **Phased scrolling**: Screen data copying for scrolling is split across 2-4 frames. For example, copy the upper half of screen RAM on frame 1, the lower half on frame 2, shift color RAM on frame 3, and swap the visible screen on frame 4.
- **State-based processing**: Different game states (title screen, gameplay, pause, game over) have different CPU budgets and can be optimized independently.

### The IRQ-Driven Game Architecture

Most commercial C64 games use a more sophisticated architecture where the raster interrupt does the heavy lifting:

```
Main Program:
    - Title screen / menu loop
    - Level loading / decompression
    - Non-time-critical setup

Raster IRQ Chain (fires every frame):
    IRQ 1 (top of screen):
        - Set up display parameters for top portion
        - Begin sprite multiplexer
    IRQ 2 (mid-screen):
        - Reconfigure sprites for lower screen portion
        - Update scroll registers for split-screen effects
    IRQ 3 (bottom of visible area / score panel):
        - Switch to score panel display mode
        - Update sprite positions for next frame
    IRQ 4 (vertical blank area):
        - Call music player
        - Process input
        - Run game logic
        - Update screen data
```

Multiple chained raster IRQs can fire at different scanlines within a single frame. Each IRQ handler sets up the next one by writing the desired raster line to $D012 and the IRQ handler address to $FFFE/$FFFF (or $0314/$0315 if using the KERNAL).

### Double Buffering

Double buffering prevents visual artifacts (tearing, flickering) by maintaining two copies of screen data. The VIC-II displays one screen while the CPU writes to the other. When the update is complete, the screens are swapped by changing the VIC-II's screen memory pointer in register $D018.

**Implementation:**
- Screen buffer A at, say, $0400 (1024 bytes)
- Screen buffer B at $0800 (1024 bytes)
- Display buffer A while writing to buffer B
- On the next frame swap: change $D018 to point to buffer B, start writing to buffer A

**The color RAM problem:**
Color RAM at $D800-$DBFF is fixed in the I/O address space and cannot be double-buffered. There is only one copy, and it is always visible. This means color RAM updates must be carefully timed to avoid visible tearing. Common solutions:
- Update color RAM during vertical blanking only
- Split color RAM updates across multiple frames, synchronizing each portion to the raster beam
- Use tile-based color assignment (one color per tile rather than per character) to reduce the number of color RAM writes needed
- Accept that color RAM is updated "live" and design graphics to minimize visible artifacts

### VIC Bank Selection

The VIC-II can access only 16 KB of RAM at a time, divided into four selectable banks:

| Bank | Address Range  | CIA-2 $DD00 bits |
|------|---------------|------------------|
| 0    | $0000-$3FFF   | %xxxxxx11        |
| 1    | $4000-$7FFF   | %xxxxxx10        |
| 2    | $8000-$BFFF   | %xxxxxx01        |
| 3    | $C000-$FFFF   | %xxxxxx00        |

Bank 0 is the default. Banks 0 and 2 mirror the character ROM at $1000-$1FFF and $9000-$9FFF respectively, which means custom character sets placed at those addresses will be overridden by the ROM. Banks 1 and 3 do not have this issue and are commonly used for games.

A common game memory layout using VIC Bank 3 ($C000-$FFFF):
- $C000-$C3FF: Screen buffer A (1 KB)
- $C400-$C7FF: Screen buffer B (1 KB)
- $C800-$CFFF: Custom character set (2 KB, 256 chars)
- $D000-$D7FF: (I/O space, not usable for VIC data)
- $D800-$DBFF: Color RAM (fixed, always here)
- $E000-$FFFF: Sprite data (8 KB, up to 128 sprite frames)

---

## 3. Tile-Based Engines

### Why Tiles?

The C64's character mode is the foundation of almost all game graphics. In character mode, the 40x25 screen is composed of 1000 character cells, each referencing one of 256 possible 8x8 pixel patterns from a character set (also called a "font" or "charset"). This is inherently a tile system:

- **Memory efficiency**: A character mode screen uses 1 KB of screen RAM + 1 KB of color RAM + 2 KB for the character set = 4 KB total. A bitmap mode screen uses 8 KB for the bitmap + 1 KB for color = 9 KB, and provides far less flexibility.
- **Speed**: To change a character on screen, the CPU writes a single byte to screen RAM (and possibly one byte to color RAM). To change an 8x8 area in bitmap mode, it must write 8 bytes.
- **Scrolling**: When the hardware scroll registers wrap (every 8 pixels), only 1000 bytes of screen data need to be shifted, versus 8000 bytes for a bitmap.

### Character Set as Tile Graphics

A custom character set defines up to 256 unique 8x8 pixel tiles. In multicolor character mode, each character is 4x8 pixels at the higher resolution but uses 4 colors:
- Background color (shared, from $D021)
- Color RAM color (per-character, from $D800+)
- Screen color bits "01" (shared, from $D022)
- Screen color bits "10" (shared, from $D023)

This means 3 out of 4 colors are shared across the entire screen, which constrains art design but simplifies color management during scrolling.

### Meta-Tiles (Block-Based Maps)

Rather than storing maps as individual characters, games use hierarchical tile systems:

**Level 1 - Characters (8x8 pixels):**
256 unique patterns in the character set.

**Level 2 - Tiles/Blocks (typically 2x2 or 4x4 characters):**
Each tile is a group of characters arranged in a grid. A 2x2 tile is 16x16 pixels and consists of 4 character references. A 4x4 tile is 32x32 pixels and consists of 16 character references. Tiles are identified by a single byte (0-255), so up to 256 unique tiles can be defined.

**Level 3 - Meta-tiles (optional, groups of tiles):**
For large maps, groups of tiles can be combined into even larger meta-tiles. For example, in a platformer, a "brick platform" meta-tile might be 4 tiles wide and 2 tiles tall.

**Level 4 - Map:**
The map itself is a 2D array of tile indices. For a scrolling game with 4x4-character tiles, a map that is 256 tiles wide and 8 tiles tall (covering the full screen height) uses only 2 KB.

This hierarchy provides enormous compression. A 256-screen game world might require:
- Character set: 2 KB
- Tile definitions (256 tiles x 16 bytes each for 4x4): 4 KB
- Map data: 2-8 KB (depending on world size)
- Total: 8-14 KB for a world that would require 256 KB uncompressed

### Tile Data Storage

Tile definitions are stored as sequential character references. For a 4x4 tile:

```
; Tile 0 definition (4x4 characters)
; Stored row by row: top-left, top-2, top-3, top-right, row2-left, ...
TileData:
    .byte $10, $11, $12, $13   ; top row characters
    .byte $14, $15, $16, $17   ; second row
    .byte $18, $19, $1A, $1B   ; third row
    .byte $1C, $1D, $1E, $1F   ; bottom row
```

For fast lookup, tile data addresses are precalculated into lookup tables:

```
TileAddrLo:  .byte <Tile0, <Tile1, <Tile2, ...
TileAddrHi:  .byte >Tile0, >Tile1, >Tile2, ...
```

Map row starting addresses are similarly precalculated:

```
MapRowLo:    .byte <MapRow0, <MapRow1, <MapRow2, ...
MapRowHi:    .byte >MapRow0, >MapRow1, >MapRow2, ...
```

### Color Assignment Strategies

Each character cell has an independent foreground color in color RAM ($D800-$DBFF). Managing per-character color during scrolling is expensive and error-prone. Games use several strategies:

**Per-tile color (most common):**
Each tile definition includes color data alongside character data. When a tile is drawn, both character references and color values are written. This ties color to tile type: a "grass" tile is always green, a "brick" tile is always brown.

**Per-block color with shared palette:**
In multicolor mode, three of the four colors are global. The per-character color is restricted to the lower 8 colors (0-7). Games design their palettes so that most tiles look correct with any of a few standard foreground colors, and color RAM updates are minimized.

**Uniform color (simplest):**
Some games use a single foreground color for the entire playfield, eliminating color RAM management entirely. This works for games with simple, high-contrast graphics.

### Scrolling Tile Maps

Smooth scrolling of tile maps is one of the most technically demanding tasks on the C64. The process combines hardware and software:

**Hardware smooth scroll (0-7 pixels):**
The VIC-II provides 3-bit scroll registers:
- $D016 bits 0-2: horizontal scroll (XSCROLL), 0-7 pixels
- $D011 bits 0-2: vertical scroll (YSCROLL), 0-7 pixels

These registers shift the entire character grid by up to 7 pixels without any CPU intervention. The visible area shrinks by one column (38-column mode, bit 3 of $D016) or one row (24-row mode, bit 3 of $D011) to hide the border where new data appears.

**Software coarse scroll (every 8 pixels):**
When the hardware scroll register wraps from 0 to 7 (or 7 to 0), the CPU must shift all 1000 bytes of screen RAM by one character position and fill in the newly exposed column or row from the map data. Color RAM must be shifted similarly.

**The scrolling cycle for leftward horizontal scrolling:**

1. Set XSCROLL to 7
2. Each frame, decrement XSCROLL by the scroll speed (e.g., 2 pixels/frame)
3. When XSCROLL would go below 0, reset it to 7 and perform a coarse scroll:
   a. Shift all 1000 bytes of screen RAM one character to the left
   b. Fill the rightmost column (25 bytes) from the tile map
   c. Shift all 1000 bytes of color RAM one character to the left
   d. Fill the rightmost column of color RAM from the tile color data
4. Swap the visible screen buffer (if double-buffering)

**Splitting the copy across frames:**
Shifting 1000 bytes takes more than half the raster time of a frame. With double buffering, the copy can be spread across multiple frames in a pattern like:
- Frame 1: Copy upper portion of screen RAM to back buffer
- Frame 2: Copy lower portion of screen RAM to back buffer
- Frame 3: Swap buffers and update color RAM (upper portion, synchronized with raster)
- Frame 4: Update color RAM (lower portion)

### Multidirectional Scrolling

For 8-directional scrolling (as in Turrican or Metal Warrior), both X and Y scroll registers must be managed simultaneously, and the screen shift must handle diagonal movement. Cadaver (Lasse Oorni) developed a well-documented approach:

**Centered scrolling:**
The hardware scroll register stays centered at 3 or 4 when idle, restricting scrolling to one-character steps in 8 directions. When the player moves, the register shifts toward 0 or 7, then snaps back to center after the coarse scroll.

**Freedirectional scrolling:**
Allows arbitrary speeds up to 4 pixels/frame. Uses two frame types:
- Frame type A: Update hardware scroll registers (clamping to 0-7), and if wrapping occurs, shift screen memory
- Frame type B: Shift color memory and swap screen buffers

### Map Coordinate Systems

**Screen-relative coordinates:**
Positions are relative to the visible screen. Simple but problematic for off-screen collision checks and object management.

**World coordinates (preferred for scrolling games):**
Positions are stored in 16-bit fixed-point format where the high byte represents the position in map tiles and the low byte represents the position within a tile. With 4-character (32-pixel) tiles, this gives 3 bits of sub-pixel accuracy. Background collision checking becomes trivial: the map position is directly the coordinate high byte.

### Animated Tiles

Character animation in tile engines is achieved by modifying the character set data at runtime. Since multiple screen positions can reference the same character, changing the character definition animates all instances simultaneously:

```
; Animate water tiles by cycling character definitions
; Characters $40-$43 are water animation frames
AnimateWater:
    ldx WaterFrame
    lda WaterAnimTable,x    ; get source frame offset
    ; Copy 8 bytes of character data to the "water" character slot
    ; All tiles using this character automatically animate
```

Typical animated elements: water, fire, flickering torches, conveyor belts, coin sparkles. The animation rate is controlled by a frame counter, typically updating every 4-8 game frames.

### Tools for Tile-Based Development

**CharPad** (by Subchrist Software) has been the standard tile map development tool since 2003. It supports:
- Tile sizes from 1x1 up to 10x10 characters
- Accurate C64 video mode simulation (hires, multicolor, ECM)
- Map editing with arbitrary dimensions
- Export to assembly-ready data formats
- Color per-character or per-tile

**TileMeDo** is a simpler 4x4 tile map editor focused on rapid iteration.

---

## 4. Sprite-Based Game Objects

### Hardware Sprites Overview

The VIC-II provides 8 hardware sprites with the following characteristics:

| Property          | Hires Mode      | Multicolor Mode |
|-------------------|-----------------|-----------------|
| Resolution        | 24x21 pixels    | 12x21 pixels    |
| Colors            | 1 + transparent | 3 + transparent |
| Data size         | 63 bytes        | 63 bytes        |
| X range           | 0-511 (9 bits)  | 0-511 (9 bits)  |
| Y range           | 0-255 (8 bits)  | 0-255 (8 bits)  |
| Expansion         | 2x horizontal, 2x vertical (independently) |

Key VIC-II sprite registers:
- $D000-$D00F: X/Y positions for sprites 0-7
- $D010: MSB (bit 8) of X positions for all 8 sprites
- $D015: Sprite enable register
- $D017: Sprite Y-expansion
- $D01B: Sprite-background priority
- $D01C: Sprite multicolor mode
- $D01D: Sprite X-expansion
- $D01E: Sprite-sprite collision register (read-and-clear)
- $D01F: Sprite-background collision register (read-and-clear)
- $D025: Shared sprite multicolor 0
- $D026: Shared sprite multicolor 1
- $D027-$D02E: Individual sprite colors 0-7

### Sprite Pointers and Animation

Each sprite's graphic data is selected by a "sprite pointer" -- a single byte located at the end of the current screen memory. By default (screen at $0400), sprite pointers are at $07F8-$07FF. The pointer value multiplied by 64 gives the address of the 63-byte sprite data block.

**Animation by pointer switching:**
To animate a sprite, you store multiple frames of sprite data at 64-byte-aligned addresses, then cycle the sprite pointer through them:

```
; Sprite data blocks:
;   Frame 0 at $2000 (pointer value = $80)
;   Frame 1 at $2040 (pointer value = $81)
;   Frame 2 at $2080 (pointer value = $82)
;   Frame 3 at $20C0 (pointer value = $83)

AnimationTable:
    .byte $80, $81, $82, $83    ; pointer values for each frame

AnimatePlayer:
    dec AnimDelay
    bne .done
    lda #AnimSpeed              ; reset delay counter
    sta AnimDelay

    ldx AnimFrame
    inx
    cpx #4                      ; number of frames
    bne .noWrap
    ldx #0
.noWrap:
    stx AnimFrame

    lda AnimationTable,x
    sta $07F8                   ; update sprite 0 pointer
.done:
    rts
```

Animation frames can be organized by direction and action state. A typical player character might have:
- 3 frames walking right
- 3 frames walking left (either mirrored copies or the same data with horizontal flip via register)
- 1 frame jumping
- 1 frame standing
- 1 frame dying

Total: 9 frames x 64 bytes = 576 bytes of sprite data.

### Sprite Overlays for Extra Colors

A hardware multicolor sprite has only 3 colors plus transparent. To get more colors, games overlay multiple sprites at the same position:

- A multicolor sprite provides the base shape with 3 colors
- A hires sprite overlaid on top adds a 4th color at full resolution

This technique is used extensively in games like Mayhem in Monsterland, where the player character appears to have more colors than a single sprite allows. The overlaid sprites count against the 8-sprite limit, so a 2-layer character uses 2 of the 8 available sprites.

In International Karate, the fighter sprites are stored facing right, and the software copies and mirrors them as needed. In IK+, two fighters are rendered with sprites while the third fighter uses character graphics to work around the sprite limit.

### Movement and Position Management

Sprite X positions require 9 bits (0-511) because the visible screen area extends beyond 255 pixels. The low 8 bits are in registers $D000, $D002, etc., while the 9th bit for all sprites is packed into $D010.

Managing the MSB of X positions is a common source of bugs:

```
; Set sprite 0 X position from a 16-bit value
; XPosLo = low byte, XPosHi = high byte (only bit 0 matters)
SetSpriteX:
    lda XPosLo
    sta $D000           ; set low 8 bits

    lda $D010           ; get current MSB register
    and #$FE            ; clear bit 0 (sprite 0's MSB)
    ora XPosHi          ; set bit 0 if XPosHi bit 0 is set
    sta $D010
    rts
```

### Sprite Multiplexing

The VIC-II can display at most 8 sprites per raster line, but most games need more than 8 on-screen objects. Sprite multiplexing reuses the 8 hardware sprites at different vertical positions within a single frame.

**Core principle:** Once the raster beam passes below a sprite's Y position + 21 (the sprite height), that hardware sprite can be repositioned to display a different game object lower on the screen. This is accomplished through carefully timed raster interrupts.

**Virtual sprites:** The game engine maintains a table of "virtual" or "logical" sprites -- the conceptual game objects. The multiplexer maps these to the 8 "physical" hardware sprites, cycling through them as the raster descends.

#### Sorting

Virtual sprites must be sorted by Y-coordinate (ascending) so the multiplexer can assign them to physical sprites in raster order. Common sorting algorithms:

**Insertion sort (Ocean algorithm):**
Maintains a persistent sorted order array. Each frame, sprites are adjusted in the list based on their new positions. Very fast when sprites move slowly (most frames require minimal reordering). This approach was used in games like Dragon Breed and SWIV.

**Bubble sort:**
O(n^2) but simple. Adequate for small numbers of virtual sprites (12-16).

**Radix sort:**
Two-pass bucket sort using Y mod 16, then Y/16. Guarantees correct order and is O(n), but requires more memory and is more complex to implement.

#### The Multiplexer IRQ Chain

The multiplexer operates through a chain of raster interrupts:

1. **Initialization (vertical blank):** Sort virtual sprites by Y. Assign the first 8 virtual sprites to hardware sprites 0-7. Set their positions, colors, and pointers.

2. **Reuse interrupts (during frame):** For each additional virtual sprite beyond 8, schedule an interrupt slightly before its Y position. The interrupt handler reconfigures the appropriate hardware sprite (the one that has finished displaying highest on the screen).

3. **Safety checks:** Before setting the raster interrupt line, compare it against the current raster position. If the current raster is already past the target line, skip the interrupt and mark the sprite as "missed" to avoid display corruption.

**Timing constraints:**
- The Y position of the next virtual sprite must be set before the raster reaches it
- Frame pointer and color registers need approximately 1 raster line to propagate
- If two virtual sprites are within 21 lines of each other vertically, they cannot share the same physical sprite
- On a bad line, the available time is severely reduced

**Practical limits:**
Most multiplexers handle 16-24 virtual sprites reliably. The theoretical maximum depends on vertical spacing -- if all sprites are at different Y positions with at least 21 lines between them, up to about 14-15 reuses per physical sprite are possible across the full screen height.

#### Toggle-Plexing

An alternative technique for situations where standard multiplexing doesn't suffice. Toggle-plexing alternates a sprite between two different game objects on successive frames:

- Even frames: sprite configured for object A
- Odd frames: sprite configured for object B

Each object is visible at 25 Hz (PAL) instead of 50 Hz. This produces visible flicker for stationary objects but works acceptably for:
- Fast-moving projectiles (the brain interpolates the positions)
- Effects with inherent instability (fire, electricity, energy beams)
- Short-lived objects (explosions)

Software collision detection is required since hardware collision only detects rendered sprites.

#### Multiplexer Implementations in Commercial Games

**Armalyte:** Used only 6 hardware sprites for the multiplexer (reserving 2 for the two-player mode to avoid priority flicker). The sort routine was based on Gary Liddon's algorithm, optimized by the development team. This multiplexer became legendary for displaying far more enemies than any previous C64 shmup.

**Cadaver's framework (Metal Warrior, MW ULTRA, BOFH):** Uses double-buffered sort tables. While the raster IRQ chain processes the current frame's sorted list, the main program sorts the next frame's list in a separate buffer. Physical sprites cycle sequentially (virtual sprite 9 maps to physical sprite 1, etc.). A priority system assigns sprites to LOW/MEDIUM/HIGH classes for strategic allocation.

---

## 5. Collision Detection

### Hardware Sprite-Sprite Collision ($D01E)

The VIC-II automatically detects when non-transparent pixels of two or more sprites overlap. The collision register at $D01E reports which sprites are involved:

- Bit N is set if sprite N is involved in a collision with any other sprite
- The register is **read-and-clear**: reading it returns the current collision flags and resets them all to 0
- Collisions are pixel-accurate (only opaque pixels count)
- In multicolor mode, bit pair "01" (the background color) is considered transparent

**Limitations:**
- You cannot tell which specific pair of sprites collided -- only which sprites are "involved"
- If sprites A collides with B and C collides with D simultaneously, all four bits are set, making it impossible to distinguish the two separate collisions
- The register only reflects collisions since the last read, so it must be read exactly once per frame
- With multiplexed sprites, the collision register reflects the physical sprite assignments, not the logical game objects -- making it nearly useless for multiplexed games

### Hardware Sprite-Background Collision ($D01F)

The VIC-II detects when a sprite's non-transparent pixels overlap with non-transparent background pixels:

- Bit N is set if sprite N has a collision with background graphics
- Also read-and-clear
- Pixel-accurate

**Critical limitation:** In multicolor character mode, background pixels with bit pair "01" (mapped to the color in register $D022) are treated as "background" and do **not** trigger collision. This means sprites pass through graphics drawn with that color without registering a hit. This makes hardware background collision unreliable for many multicolor games.

### Why Most Games Use Software Collision

Despite the availability of hardware collision detection, most commercial C64 games implement collision detection entirely in software. Reasons:

1. **Control over hitbox size**: Hardware detection uses the full sprite shape. Software collision allows a smaller "hitbox" so that grazing the edge of a sprite doesn't count as a hit, which feels more fair to the player.
2. **Identifying collision pairs**: Software collision tests specific pairs of objects and can handle the result appropriately (e.g., "bullet hit enemy" vs "player hit enemy").
3. **Multiplexer compatibility**: Hardware registers reflect physical sprite slots, not logical game objects.
4. **Background collision specificity**: Software can check specific map tiles at the player's feet for "solid ground" rather than relying on the VIC-II's pixel-based detection.

### Bounding Box Collision

The most common software collision approach. Each game object has a rectangular bounding box defined by offsets from its position:

```
; Check collision between object A and object B
; Each object has: X position, Y position, width, height
; Returns carry set if collision

CheckBoundingBox:
    ; Check: A.x + A.width > B.x
    lda ObjectA_X
    clc
    adc ObjectA_Width
    cmp ObjectB_X
    bcc .noCollision       ; A is entirely left of B

    ; Check: B.x + B.width > A.x
    lda ObjectB_X
    clc
    adc ObjectB_Width
    cmp ObjectA_X
    bcc .noCollision       ; B is entirely left of A

    ; Check: A.y + A.height > B.y
    lda ObjectA_Y
    clc
    adc ObjectA_Height
    cmp ObjectB_Y
    bcc .noCollision       ; A is entirely above B

    ; Check: B.y + B.height > A.y
    lda ObjectB_Y
    clc
    adc ObjectB_Height
    cmp ObjectA_Y
    bcc .noCollision       ; B is entirely above A

    sec                     ; collision detected
    rts

.noCollision:
    clc
    rts
```

For games with many objects, a full N-vs-N collision check is O(n^2). Optimizations include:
- Only checking bullets against enemies (not enemies against enemies)
- Skipping checks for objects that are far apart in Y (quick rejection based on Y distance)
- Maintaining separate lists for different object types

### Character-Level Collision (Background)

For platformers and other games with tile-based worlds, the most efficient background collision detection operates at the character/tile level:

```
; Check what tile is at position (X, Y) in world coordinates
; X and Y are in pixels, relative to the world origin

GetTileAtPosition:
    ; Convert pixel X to character column: divide by 8
    lda WorldX+1           ; high byte of X position
    lsr                     ; shift to get character column
    ; (implementation depends on tile size and coordinate system)

    ; Look up the tile at (column, row) in the map array
    ; Use precalculated row address tables for speed
    tay
    lda (MapRowAddr),y      ; get tile index at this position
    tax
    lda TileProperties,x    ; look up tile properties (solid, harmful, etc.)
    rts
```

Tile properties are stored in a lookup table indexed by tile number:
- Bit 0: Solid (blocks movement)
- Bit 1: Platform (solid only from above)
- Bit 2: Harmful (damages player on contact)
- Bit 3: Climbable (ladder)
- Bit 4: Collectible (coin, gem)
- Bit 5: Destructible
- Bits 6-7: Reserved

### Combining Approaches

A typical platformer uses multiple collision methods:

1. **Bounding box** for sprite-vs-sprite (player vs enemies, player vs collectibles)
2. **Character/tile lookup** for sprite-vs-background (standing on platforms, hitting walls)
3. **Point tests** for projectiles (check the single pixel at the bullet's tip against the map)
4. **Hardware sprite-background** occasionally used as a quick first-pass check, with software refinement

### Collision Response

Detecting a collision is only half the problem. The response depends on the game type:

- **Platform landing**: If the player's feet overlap a solid tile, push the player up to the tile surface and set the "on ground" flag.
- **Wall blocking**: If the player's side overlaps a solid tile, push the player back to the tile edge and zero the horizontal velocity.
- **Enemy hit**: Decrease player health, trigger invincibility frames, play sound effect, flash the sprite.
- **Bullet hit**: Remove the bullet, trigger enemy damage/death animation, increment score.

---

## 6. Physics and Movement

### Fixed-Point Arithmetic for Sub-Pixel Movement

The 6502 processor has no floating-point capability and limited integer math. To achieve smooth sub-pixel movement, C64 games use **fixed-point arithmetic**: 16-bit values where the high byte is the pixel position and the low byte is the fractional (sub-pixel) part.

```
; Position format: high byte = pixel, low byte = fraction (8.8 fixed point)
; Example: $0A80 = pixel 10, 50% (10.5 pixels)

PlayerX_Lo:  .byte $00    ; fractional X
PlayerX_Hi:  .byte $80    ; pixel X
PlayerY_Lo:  .byte $00    ; fractional Y
PlayerY_Hi:  .byte $64    ; pixel Y

VelocityX_Lo: .byte $00   ; fractional X velocity
VelocityX_Hi: .byte $00   ; pixel X velocity
VelocityY_Lo: .byte $00   ; fractional Y velocity
VelocityY_Hi: .byte $00   ; pixel Y velocity
```

Movement is performed by adding the velocity to the position each frame:

```
; Update X position: position += velocity (16-bit addition)
    clc
    lda PlayerX_Lo
    adc VelocityX_Lo
    sta PlayerX_Lo
    lda PlayerX_Hi
    adc VelocityX_Hi
    sta PlayerX_Hi
```

This allows velocities much smaller than 1 pixel per frame. A velocity of $0040 (0.25 pixels/frame) produces smooth, slow movement at 12.5 pixels per second (PAL).

### Gravity and Jumping

Gravity is simulated by adding a constant downward acceleration to the Y velocity each frame:

```
GRAVITY = $004C          ; approximately 0.3 pixels/frame^2
MAX_FALL_SPEED = $0400   ; terminal velocity: 4 pixels/frame

ApplyGravity:
    ; velocity_y += gravity
    clc
    lda VelocityY_Lo
    adc #<GRAVITY
    sta VelocityY_Lo
    lda VelocityY_Hi
    adc #>GRAVITY
    sta VelocityY_Hi

    ; Clamp to terminal velocity
    lda VelocityY_Hi
    cmp #>MAX_FALL_SPEED
    bcc .done
    bne .clamp
    lda VelocityY_Lo
    cmp #<MAX_FALL_SPEED
    bcc .done
.clamp:
    lda #<MAX_FALL_SPEED
    sta VelocityY_Lo
    lda #>MAX_FALL_SPEED
    sta VelocityY_Hi
.done:
    rts
```

**Initiating a jump:**
Set the Y velocity to a negative value (upward). Gravity pulls it back to zero and then positive (downward), creating a natural parabolic arc:

```
JUMP_VELOCITY = $FA00    ; -6 pixels/frame (signed)

StartJump:
    lda OnGround
    beq .cantJump          ; can only jump when on ground

    lda #<JUMP_VELOCITY
    sta VelocityY_Lo
    lda #>JUMP_VELOCITY
    sta VelocityY_Hi

    lda #0
    sta OnGround
.cantJump:
    rts
```

**Jump feel tuning parameters:**
- **Jump velocity**: Determines jump height. Typical values: -4 to -8 pixels/frame.
- **Gravity**: Determines hang time and fall speed. Typical: 0.2-0.5 pixels/frame^2.
- **Variable jump height**: Many games allow the player to control jump height by holding or releasing the button. When the button is released during upward movement, gravity is increased (or upward velocity is reduced) for a shorter jump.
- **Coyote time**: Allow jumping for a few frames after walking off a ledge.

### Acceleration and Deceleration

Rather than setting velocity directly from input, apply acceleration for smoother movement:

```
ACCELERATION  = $0030    ; ~0.19 pixels/frame^2
DECELERATION  = $0050    ; ~0.31 pixels/frame^2 (faster stop for tighter control)
MAX_SPEED     = $0240    ; ~2.25 pixels/frame

UpdateHorizontalMovement:
    lda JoystickLeft
    bne .accelerateLeft
    lda JoystickRight
    bne .accelerateRight

    ; No input: decelerate toward zero
    jsr Decelerate
    rts

.accelerateRight:
    ; velocity_x += acceleration (clamped to max_speed)
    clc
    lda VelocityX_Lo
    adc #<ACCELERATION
    sta VelocityX_Lo
    lda VelocityX_Hi
    adc #>ACCELERATION
    sta VelocityX_Hi
    ; Clamp to MAX_SPEED...
    rts

.accelerateLeft:
    ; velocity_x -= acceleration (clamped to -max_speed)
    sec
    lda VelocityX_Lo
    sbc #<ACCELERATION
    sta VelocityX_Lo
    lda VelocityX_Hi
    sbc #>ACCELERATION
    sta VelocityX_Hi
    ; Clamp to -MAX_SPEED...
    rts
```

### Platform Collision Response

When the player overlaps a solid tile after movement, the position must be corrected:

**Landing on a platform (downward collision):**
1. After applying gravity and updating Y position, check the tiles under the player's feet
2. If a solid tile is detected, snap the player's Y to the top of that tile
3. Zero the Y velocity
4. Set the OnGround flag

**Hitting a ceiling (upward collision):**
1. After jumping upward, check the tiles above the player's head
2. If solid, snap Y to the bottom of the tile and zero (or reverse) Y velocity

**Wall collision (horizontal):**
1. After horizontal movement, check tiles at the player's leading edge
2. If solid, snap X to the tile edge and zero X velocity

**Order of operations matters.** Most games apply and resolve horizontal movement first, then vertical, to avoid corner-case issues where diagonal movement clips through thin platforms.

### Lookup Tables vs. Runtime Calculation

Some C64 games avoid runtime gravity/physics calculations entirely by using lookup tables for jump trajectories:

```
; Predefined Y-offset table for a jump arc
JumpTable:
    .byte -6, -5, -4, -3, -3, -2, -2, -1, -1, -1, 0
    .byte  0,  1,  1,  1,  2,  2,  3,  3,  4,  5,  6

; Each frame during a jump, add JumpTable[frame] to Y position
```

This is deterministic, uses no multiplication or division, and is very fast. The downside is that it cannot respond dynamically to collisions mid-jump (though most games simply terminate the table lookup when a landing is detected).

### Slopes and Advanced Terrain

Simple platformers treat all terrain as axis-aligned boxes. More advanced games implement slopes:

- A "slope map" defines the height of the ground at each pixel column within a tile
- When the player stands on a sloped tile, their Y position is adjusted to the slope height at their X position within that tile
- This requires per-pixel lookup tables for each slope type

Few C64 games implement slopes due to the CPU cost. Those that do typically limit slopes to a few fixed angles (45 degrees, shallow angles).

---

## 7. Game Types and Techniques

### Platformers

The C64's hardware scroll registers and character-mode graphics make it natural for side-scrolling platformers. Key techniques:

**Scrolling:**
- Horizontal scrolling using XSCROLL ($D016 bits 0-2) for pixel-level smoothness
- Double-buffered screen RAM with coarse scroll every 8 pixels
- 38-column mode to hide the transition column
- Color RAM updated in sync with the raster to avoid flicker

**Player mechanics:**
- Fixed-point position and velocity for smooth movement
- Gravity + jump velocity for the arc
- Tile-based collision for standing on platforms, hitting walls, and detecting hazards
- State machine for player states: idle, walking, jumping, falling, climbing, dying

**Enemy behavior:**
- Simple patrol patterns (walk left/right between edges or walls)
- State machines for complex behavior: patrol -> chase -> attack -> return
- Lookup table-driven movement for predetermined paths

**Scrolling camera:**
- Track player position, scroll when player moves beyond a threshold near screen center
- "Dead zone" in the center of the screen where the player can move without triggering scrolling

**Notable examples:**
- *Mayhem in Monsterland* (1993): Used VSP (Variable Screen Placement) for incredibly fast hardware scrolling, sprite color flashing for expanded palette, and sprite/background priority for depth layering
- *Creatures* (1990): Introduced VSP scrolling to commercial games
- *Great Giana Sisters* (1987): Classic tile-based scrolling with tight controls
- *Impossible Mission* (1984): Room-based platformer with complex enemy AI and puzzle elements

### Shooters / Shmups (Shoot 'em Ups)

Shoot 'em ups push the C64 to its limits with many simultaneously moving objects:

**Sprite multiplexing (essential):**
- 16-24+ sprites on screen simultaneously
- Sort by Y-coordinate, assign to physical sprites via raster interrupts
- The Armalyte multiplexer (1988) was a landmark: using only 6 physical sprites, it displayed vast numbers of enemies and bullets

**Bullet management:**
- Object pools: pre-allocate a fixed number of bullet slots (e.g., 16)
- Each bullet has: X, Y, deltaX, deltaY, sprite pointer, active flag
- Bullets are recycled when they leave the screen or hit a target
- Simple linear movement (no need for acceleration)

**Background scrolling:**
- Vertical or horizontal scrolling at constant speed
- Map data streamed from the edge
- Parallax effects using character set animation or raster splits

**Wave patterns:**
- Enemy formation paths stored as delta tables or using simple math (sine tables for wave patterns)
- Spawn triggers based on scroll position in the map data

**Performance optimization:**
- Unrolled loops for critical inner loops
- Lookup tables for sine/cosine movement patterns
- Skip collision checks for pairs that cannot possibly overlap (Y-distance rejection)

**Notable examples:**
- *Armalyte* (1988): 6-sprite multiplexer, massive enemy waves
- *Delta* (1987): Stavros Fasoulas' original shmup with advanced audio
- *R-Type* (port): Demonstrated the multiplexer could handle complex gameplay
- *Katakis* (1988): Rainbow Arts shmup with sophisticated scrolling

### Racing Games

Racing games on the C64 use several approaches to create the illusion of speed and perspective:

**Pseudo-3D road rendering:**
- The road is drawn using character mode or bitmap mode, with each horizontal strip scaled to create a perspective effect
- Lookup tables define the road width and position at each screen row
- The road "moves" by shifting the lookup table offset each frame
- Curves are simulated by adding a horizontal offset that increases toward the bottom of the screen

**Split-screen multiplayer:**
- *Pitstop II* (1984) pioneered split-screen two-player racing, with each half of the screen showing an independent view. This requires rendering two separate viewpoints in a single frame, doubling the CPU load.

**True 3D rendering:**
- *Stunt Car Racer* (1989) by Geoff Crammond used actual 3D polygon rendering for the track and car, a remarkable technical achievement
- *Revs* by Crammond used sophisticated physics modeling for tire grip and car handling

**Sprite-based vehicles:**
- Other cars/opponents are rendered as sprites that scale (using X/Y expansion) based on distance

### RPGs

RPGs on the C64 tend to be less demanding on real-time performance but require sophisticated data management:

**Tile-based world display:**
- Overhead view using character mode tile maps (*Ultima* series)
- First-person dungeon views using character mode or bitmap (*Bard's Tale*, *Wizardry*)
- Scrolling or screen-by-screen map traversal

**Data management:**
- Character stats, inventory, quest flags stored in RAM tables
- Large game worlds require multi-load from disk
- Text and dialogue compressed to save space

**Character-mode dungeon rendering:**
- First-person views in *Bard's Tale* used bitmap multicolor mode to draw the pseudo-3D dungeon perspective
- The visible depth is limited (typically 3-4 tiles deep) to keep the rendering fast
- Walls, doors, and objects are drawn as predefined shapes scaled by distance

**Notable examples:**
- *Ultima IV* (1985): Huge tile-based world, multi-disk
- *Bard's Tale* (1985): Bitmap multicolor first-person dungeon crawling
- *Pool of Radiance* (1988): Complex tactical combat on character-mode grids
- *Wasteland* (1988): Open-world RPG with paragraph-book-style text

### Puzzle Games

Puzzle games are often simpler technically but require clean, responsive input handling:

**Character animation for game elements:**
- *Boulder Dash* (1984): The entire game runs in character mode. Rocks, diamonds, dirt, and the player (Rockford) are all character-set animations. When Rockford stands idle, the character set cycles through blinking and foot-tapping frames. Falling rocks and diamonds use animated characters that cycle as they move.
- *Tetris* clones: Piece rotation is handled by lookup tables mapping each rotation state to a set of character positions. Line detection scans complete rows in screen RAM.

**Grid-based logic:**
- Game state stored as a 2D array (often directly in screen RAM or a shadow buffer)
- Updates triggered by timer or player input, not continuous physics
- Color RAM changes for visual feedback (flashing, highlighting)

**Notable examples:**
- *Boulder Dash* (1984): Scrolling character-mode puzzle-action with physics simulation for falling objects
- *Lode Runner* (1983): Character-mode platformer-puzzle
- *Puzznic* (1990): Block-matching puzzle

### Sports Games

Sports games on the C64 face the challenge of displaying many player characters simultaneously:

**Sprite management:**
- A soccer game needs 22 players, far exceeding the 8-sprite limit
- Aggressive multiplexing is required, sometimes combined with toggle-plexing
- *World Games* reportedly used approximately 100 sprites for its weightlifting event
- *International Karate+* renders two fighters as sprites and the third as character graphics

**Sprite overlays for large characters:**
- Fighting and sports games use multiple overlaid sprites to create larger, more detailed characters
- Each character might use 2-4 sprites (body segments), consuming a large portion of the sprite budget
- The overlay technique (hires sprite on top of multicolor sprite) adds extra detail and colors

**Animation frames:**
- Fighting games require extensive animation: kicks, punches, jumps, blocks
- Frame data is stored in tables, with state machines controlling transitions
- *International Karate*: Fighter graphics are stored facing right; software mirrors them as needed

**Perspective tricks:**
- Top-down or isometric views for field sports
- Side-view for fighting and athletics
- Scaled sprites for depth (near/far players)

---

## 8. Sound in Games

### The SID Chip for Game Audio

The MOS 6581/8580 SID provides:
- 3 independent oscillator voices
- 4 waveforms per voice: triangle, sawtooth, pulse (with adjustable width), noise
- Per-voice ADSR envelope generators (Attack, Decay, Sustain, Release)
- A single multimode resonant filter (low-pass, high-pass, band-pass, notch, or combinations)
- Ring modulation between voices 1/3 and oscillator sync between voices 1/3
- 8-octave frequency range per voice

The SID's 29 control registers are at $D400-$D41C:
- $D400-$D406: Voice 1 (frequency, pulse width, control, ADSR)
- $D407-$D40D: Voice 2
- $D40E-$D414: Voice 3
- $D415-$D418: Filter cutoff, resonance, volume, filter mode

### Music Driver Architecture

Game music is played by a "music driver" or "music player" -- a software routine that reads sequence data and programs the SID registers accordingly. The driver is called once per frame (50 times/second on PAL, 60 on NTSC) via the raster interrupt.

**Typical driver structure:**
1. Maintain a "current position" pointer for each voice's sequence data
2. Each frame, check if the current note's duration has elapsed
3. If so, read the next note/command from the sequence
4. Program the SID registers: frequency, waveform, ADSR, pulse width, etc.
5. Apply per-frame effects: vibrato, portamento, pulse width modulation, arpeggio

**Notable music drivers:**
- **Rob Hubbard's driver**: ~900-1000 bytes, frame-accurate parameter control. Hubbard's approach was to manipulate SID registers directly in real time, treating the driver as a synthesizer controller. His method of drum synthesis combined a single frame of noise (simulating the stick attack) with a pitch-modulated pulse wave (simulating the body/decay).
- **Martin Galway's driver**: Discovered PCM sample playback via the SID's volume register ($D418), exploiting DC offset between channels. This was an unintended capability ("hidden affordance") that transformed percussion synthesis on the SID.
- **GoatTracker format**: Modern cross-platform tracker with SID export. Widely used for new C64 game development.
- **SID-Wizard**: Another modern SID tracker with advanced features.

### Sound Effects Alongside Music

With only 3 voices for both music and sound effects, games must make compromises. Common strategies:

**Dedicated voice for effects (2+1 approach):**
Music is composed for 2 voices, and voice 3 is reserved exclusively for sound effects. This guarantees sound effects always play but limits the musical complexity.

Example games: *The Human Race* (Mastertronic, 1985)

**Voice stealing / note stealing:**
All 3 voices play music normally. When a sound effect triggers, it temporarily takes over one voice (usually voice 3), silencing that voice's music. When the effect ends, music resumes on that voice.

This is the most common approach. The music is composed so that voice 3 carries "less important" musical content (bass notes, harmony) that can be interrupted without destroying the melody.

Example games: *Commando* (1985), *Thing on a Spring* (1985), and most action games.

**Music vs. effects toggle:**
Some games let the player choose between music and sound effects, since both cannot coexist without compromise. This is typically used when the music requires all 3 voices for its full effect.

Example games: *Delta* (1987)

**Integrated music/SFX engine:**
Advanced drivers have built-in sound effect support. The driver knows which voices are available and can prioritize effects over music intelligently. Effects can specify a priority level, and low-priority effects are skipped if all voices are occupied by higher-priority effects or music.

### Sound Effect Design

Sound effects on the SID are designed using the same synthesis parameters as music:

**Explosion:**
- White noise waveform on voice 3
- High frequency, rapidly decaying
- Short attack, long release
- Optional filter sweep (high to low cutoff)

**Jump:**
- Triangle or pulse waveform
- Rapid upward pitch sweep
- Short envelope

**Collecting an item:**
- Pulse waveform
- Quick arpeggio (ascending chord)
- Very short envelope

**Gunshot:**
- Noise waveform, very short
- Immediate attack, near-zero sustain

### Timing and Integration

The music driver must be called exactly once per frame, at a consistent point in the raster cycle. It is typically called from the raster interrupt handler, often at the beginning of the vertical blank:

```
IRQHandler:
    ; Acknowledge interrupt
    lda #$FF
    sta $D019

    ; Call the music player
    jsr MusicPlay

    ; (Other game tasks...)

    ; Restore registers and return
    pla
    tay
    pla
    tax
    pla
    rti
```

The music player typically consumes 5-15 raster lines per frame (300-1000 cycles), depending on the complexity of the music and effects. This is a significant portion of the frame budget and must be accounted for in the game's timing.

### Pseudo-Polyphony: Arpeggiation

To simulate chords with single voices, the SID rapidly cycles through multiple pitches within a single frame. At 50 Hz update rate, cycling through 3 pitches produces a characteristic "warbling" chord effect. This technique is a hallmark of SID music and is used extensively to create rich-sounding compositions from just 3 monophonic voices.

### Pulse Width Modulation

The pulse waveform has a programmable duty cycle (pulse width). By slowly modulating the pulse width over time, timbral variations are produced that make the sound more alive and less static. This is often described as "the holy grail" of SID sound design -- most classic SID tunes use PWM on at least one voice.

---

## 9. Level Design and Data

### Level Data Storage

Level data organization depends on the game type, but the core challenge is always the same: represent a large game world in minimal RAM.

**Direct character map (small games):**
The simplest approach: the level is stored as a complete screen of 40x25 bytes. Each byte is a character index. This costs 1 KB per screen. Suitable for single-screen games or games with few screens.

**Tile map (most scrolling games):**
The level is a 2D array of tile indices, where each tile represents a group of characters (typically 2x2 or 4x4). This provides compression proportional to tile size:
- 2x2 tiles: 4:1 compression (plus the tile definition table)
- 4x4 tiles: 16:1 compression

**Hierarchical maps:**
For very large worlds, additional levels of indirection are used:
- Map -> Super-tiles (e.g., 4x4 tiles) -> Tiles (e.g., 2x2 characters) -> Characters
- Each level multiplies the compression ratio

### Level Data Compression

Beyond the tile hierarchy, level data is further compressed using various encoding schemes:

**Run-Length Encoding (RLE):**
Consecutive identical tiles are encoded as a count + tile pair:
```
; Format: literal bytes $00-$FE are tile indices
;         $FF = escape code for RLE
;         $FF, count, tile = repeat 'tile' 'count' times
;         $FF, $00 = end of data
```
RLE is simple to decompress (important for runtime speed) but yields modest compression. In Cadaver's Steel Ranger, RLE reduced a city level from ~12 KB to ~8 KB.

**Dictionary / LZ-style compression:**
More advanced schemes reference previously decompressed data:
```
; MW ULTRA encoding:
; $00-$CF: Direct tile references
; $D0: End marker
; $D1+tile: Escape for tile values $D0-$FF
; $D2-$E7: Dictionary sequences (3-24 bytes, 256-byte lookback)
; $E8-$FF: RLE sequences (3-24 repetitions)
```
The decompressor uses a ring buffer in screen memory, keeping the last 256 output bytes available for dictionary references.

**Two-stage compression (disk + runtime):**
Many modern C64 games use a two-pass approach:
1. Level data is encoded in a custom format (RLE, dictionary, etc.) for efficient runtime decompression
2. The encoded data is then compressed again with a general-purpose compressor (Exomizer, ZX0) for disk storage
3. At load time, the general compressor is unpacked first, then the game-specific format is decoded during zone transitions

Example: Steel Ranger's city level went from 12 KB uncompressed -> 8 KB with RLE -> 3 KB on disk with Exomizer.

**Tile renumbering optimization:**
Since tile numbers are arbitrary, a post-processing step can renumber tiles to optimize dictionary compression. In MW ULTRA, "tile numbers are never referred to in game code," allowing the build process to reorder tiles for better compression without affecting gameplay logic.

### Zone-Based World Structure

Large scrolling games divide the world into zones or areas:

**Zone architecture (Cadaver's framework):**
- Each zone is a separate rectangular scrolling area
- Zone map data is stored compressed and decompressed when the zone is entered
- Each zone can contain "objects" (doorways, triggers) and "actors" (items, enemies)
- Zone transitions load and decompress the new zone's data, replacing the previous zone in memory
- The world map itself is a graph of connected zones

This approach allows game worlds much larger than 64 KB by loading zones from disk on demand.

### Procedural Generation

Some C64 games generate content algorithmically instead of storing it:

**Deterministic generation:**
- *Impossible Mission*: Rooms are randomized at game start using a pseudo-random number generator. However, since the PRNG was not seeded from a time source, the "random" rooms are actually deterministic -- the same sequence every time.
- *Elite*: Star systems and their properties are generated from a seed value, allowing a universe of thousands of systems to exist in minimal RAM.
- *Seven Cities of Gold*: Generated exploration maps procedurally.

**Why procedural generation was rare:**
The entire game had to be designed around it, and the C64's limited CPU made complex generation algorithms impractical for real-time use. Most procedural generation was done at level-start rather than on-the-fly.

### Multi-Load Levels

For games too large to fit in memory at once, data is loaded from disk during gameplay:

**The loading challenge:**
The C64's default disk loading speed (~400 bytes/second) makes any load noticeable. A 10 KB level takes 25 seconds to load at default speed.

**Fast loaders:**
Custom fast-loading routines replace the default KERNAL routines:
- The game disk includes a small bootstrap program loaded normally
- This bootstrap installs custom communication routines in both the C64 and the 1541 drive
- Subsequent loads use the fast protocol, achieving 2,500+ bytes/second

**IRQ loaders:**
The most sophisticated approach: loading occurs in the background while the game continues to run. The loader operates via interrupt-driven I/O, transferring small chunks of data between disk access cycles:
- *Krill's Loader* (2009): Popular open-source IRQ loader
- *Spindle* (2013): Another well-known IRQ loader for games and demos

With an IRQ loader, a scrolling game can stream level data from disk as the player progresses, making the game world effectively unlimited in size.

**Level transition strategies:**
- **Hard load**: Game pauses, displays a "loading" screen, loads next level entirely, then resumes. Simple but breaks immersion.
- **Streaming**: IRQ loader loads the next zone in the background while the current zone is still playing. When the player reaches the zone boundary, data is already in RAM.
- **Bank switching**: Cartridge-based games (like the EasyFlash) can map 1 MB+ of ROM into the C64's address space, making disk loading unnecessary.

---

## 10. Hardcore Details

### How Impossible Mission's Room Engine Works

*Impossible Mission* (1984, by Dennis Caswell for Epyx) is a landmark platformer whose room engine was ahead of its time.

**Room structure:**
The game takes place in Elvin Atombender's fortress, consisting of 32 rooms. Each room is a single screen (no scrolling) with platforms, elevators, and furniture objects. Rooms are connected via hallways with vertical scrolling elevators.

**Room randomization:**
The room layout (which furniture contains puzzle pieces, which rooms connect to which) is randomized at game start using a pseudo-random number generator. Because the PRNG was not seeded with a variable source (like a timer), the randomization is deterministic -- every game starts with the same configuration.

**Platform and elevator system:**
- Platforms are placed at fixed Y positions within each room
- Elevators are sprites that move vertically along fixed columns
- The player interacts with elevators by standing on them, and can ride them between platforms
- Platform edges are detected via character-level collision

**Robot AI:**
Each robot has an independent behavior program:
- Some robots patrol fixed paths
- Some track the player's position and pursue
- Some can "see" the player only in line-of-sight
- Some fire projectiles (lightning bolts)
- Speed varies per robot
- Robot configuration is stored as compact behavioral parameters, not unique code per robot

**Display technique:**
Rooms use character mode for the background (platforms, furniture, walls). The player and robots are hardware sprites. The combination allows detailed background art with smooth-moving foreground characters.

### Turrican's Scrolling Engine

*Turrican* (1990, by Manfred Trenz for Rainbow Arts) achieved a feat previously considered impossible on the C64: 8-directional parallax scrolling at 50 FPS in 16-color 160x200 multicolor mode.

**The scrolling system:**
- The game world is built from tiles (which Trenz drew by hand in notebooks, then digitized with custom tools)
- Horizontal and vertical scroll registers are updated every frame for pixel-smooth movement in any direction
- When the scroll registers wrap, the screen data is shifted and new tiles are drawn at the edges
- The scrolling engine was the most technically challenging part of development

**Level design tools:**
Trenz built his own tools for creating graphics and levels using 8x8 blocks. He prepared grids where each square corresponded to a real game screen, and hand-drew the level layouts.

**Performance:**
The scrolling itself consumed a huge portion of the frame budget. For *Turrican II*, "all we really kept were the scrolling routines. Everything else was optimized and rewritten because we needed more processing time for the sprites and animation."

**World design:**
Turrican levels are enormous, with both horizontal and vertical sections. The game switches between side-scrolling and vertical-scrolling sections seamlessly, using the same multidirectional engine throughout.

### VSP (Variable Screen Placement) Scrolling

VSP is an undocumented VIC-II technique that enables hardware-accelerated horizontal scrolling far more efficient than the standard approach.

**How it works:**
Normally, when the hardware XSCROLL wraps from 0 to 7 (or 7 to 0), the CPU must shift all 1000 bytes of screen RAM. VSP tricks the VIC-II into reading character data from a different memory offset by creating a "Bad Line Condition" during the VIC-II's idle state. This effectively shifts the screen pointer by a variable amount without any CPU memory copying.

**The technique:**
By manipulating register $D011 (the vertical scroll / display mode register) at a precise cycle during a specific raster line, the VIC-II's internal row counter is disrupted. This causes the chip to fetch character data from an offset position in screen RAM, producing an apparent horizontal scroll of up to 320 pixels.

Combined with the XSCROLL register for fine pixel scrolling, VSP eliminates the need for software screen shifting entirely, freeing enormous CPU time for game logic.

**The VSP bug:**
On certain VIC-II revisions, VSP can corrupt DRAM refresh. The VIC-II generates memory addresses and refresh commands on separate internal buses. Under VSP timing, these can desynchronize, causing the chip to refresh memory cells with data from wrong addresses. Symptoms: random data corruption, crashes, or visual glitches. The bug affects memory cells at addresses ending in $x7 or $xF.

**Mitigations (developed by Linus Akesson):**
- Ensure all "fragile" memory locations (those at risk of corruption) contain identical data
- Use undocumented 6502 opcodes to skip problematic addresses in code
- Continuously restore graphics data from a safe copy in RAM
- A hardware fix ("VSP-Fix") can be installed in the C64

**Games using VSP:**
- *Mayhem in Monsterland* (1993): First known commercial game to use VSP, achieving scroll speeds never before seen on the C64
- *Creatures* (1990): Used VSP for fast parallax scrolling
- *Fred's Back*: Another VSP-based title
- *Super Mario Bros. 64* (homebrew): Modern VSP-based platform game

### Multiplexer Implementations in Commercial Games

**Armalyte (1988, Thalamus):**
The Armalyte multiplexer used only 6 of the 8 hardware sprites to avoid priority flicker in two-player mode. The sort routine originated from an algorithm by Gary Liddon, optimized specifically for Armalyte's requirements. The multiplexer code from Rob Stevens' *Barbarian II* served as a foundation.

**Ocean games (Dragon Breed, SWIV):**
Used a continuous insertion sort (the "Ocean algorithm") that maintains a persistent sorted order. Rather than re-sorting from scratch each frame, the sort incrementally adjusts when sprites change position. This is extremely fast for the common case where sprites move slowly relative to each other.

**Cadaver's framework (Metal Warrior series, MW ULTRA, BOFH):**
- Double-buffered sorted arrays: while the raster IRQ processes frame N's sprite list, the main program sorts frame N+1's list
- Priority-based physical sprite assignment (LOW/MEDIUM/HIGH classes)
- Physical sprites cycle sequentially through virtual sprites
- Safety checks compare proposed raster interrupt lines against current raster position to prevent late interrupts
- Uses radix sorting for guaranteed O(n) performance

### Memory Budgets and Fitting a Complete Game

A single-load C64 game has approximately 60 KB of usable RAM. A typical breakdown for a scrolling action game:

| Component              | Size (approximate) |
|------------------------|--------------------|
| Game code              | 8-16 KB            |
| Music driver + data    | 2-6 KB             |
| Character set          | 2 KB               |
| Tile definitions       | 2-4 KB             |
| Level/map data         | 4-16 KB (compressed)|
| Sprite data            | 4-8 KB             |
| Screen buffers (x2)    | 2 KB               |
| Color RAM              | 1 KB (fixed)       |
| Sprite multiplexer     | 0.5-1 KB           |
| Variables/tables       | 1-4 KB             |
| Lookup tables          | 1-2 KB             |
| Sound effects data     | 0.5-1 KB           |
| **Total**              | **28-62 KB**       |

**Strategies for fitting more content:**

1. **Tile compression**: Hierarchical tiles (meta-tiles of tiles of characters) provide enormous compression of level data.
2. **Character set sharing**: Use the same character set for multiple levels by careful art design.
3. **Sprite data reuse**: Mirror sprites in software rather than storing left/right versions. Share animation frames between similar enemies.
4. **Code density**: Assembly language produces very compact code. Experienced C64 programmers achieve high code density through:
   - Self-modifying code (using code as data and vice versa)
   - Lookup tables instead of branching logic
   - Shared subroutines with parameterization
5. **RAM under I/O**: The 4 KB under the I/O space ($D000-$DFFF) can store data accessed by banking out I/O temporarily. This is commonly used for sprite data or level data.
6. **RAM under KERNAL/BASIC ROM**: Banking out the KERNAL ($E000-$FFFF, 8 KB) and BASIC ($A000-$BFFF, 8 KB) ROMs exposes 16 KB of additional RAM. Most games disable BASIC immediately.
7. **Zero-page optimization**: The 256 bytes of zero page ($00-$FF) provide the fastest memory access on the 6502. Games use zero page for frequently accessed variables (positions, velocities, counters).

### Common Engine Architectures

**The "Cadaver" architecture (Lasse Oorni):**
A fully documented open-source framework (c64gameframework on GitHub) representing a mature C64 game engine:
- Zone-based world with compressed map data, decompressed on zone entry
- Screen redraw writes both screen and color data in a single frame without double buffering
- Logical sprites (composed of multiple hardware sprites) defined in a sprite editor
- World editor with 2x2 block reduction for the C64-side data
- IRQ loader for streaming zone data from disk
- Complete actor/object system with spawn/despawn based on proximity to the visible area
- Actors near the screen are "active" (processed every frame); distant objects are stored as compact positional data only

**The "SEUCK-derived" architecture:**
The Shoot 'Em Up Construction Kit (SEUCK, 1987) defined a common architecture for C64 shmups:
- Vertical or horizontal scrolling background
- Block-based map data
- 8 hardware sprites for player and enemies
- Simple collision detection
- Fixed game loop structure

Many indie games used SEUCK as a starting point, then optimized. Modern SEUCK enhancements include better multiplexers, improved sort/collision routines, and optimized scrolling with unrolled drawing loops and off-screen buffer swapping.

**The "state machine" architecture:**
Games with complex behavior systems use state machines extensively:
- Player states: idle, walk, run, jump, fall, climb, attack, hurt, die
- Enemy states: patrol, chase, attack, flee, stunned
- Game states: title, menu, gameplay, pause, game-over, cutscene

State transitions are driven by input (player) or AI logic (enemies). For efficiency on the 6502, state numbers index into lookup tables of handler addresses, and an indirect jump (JMP (addr)) dispatches to the correct routine:

```
; State dispatch via lookup table
    ldx CurrentState
    lda StateHandlerLo,x
    sta JmpAddr
    lda StateHandlerHi,x
    sta JmpAddr+1
    jmp (JmpAddr)

StateHandlerLo: .byte <StateIdle, <StateWalk, <StateJump, ...
StateHandlerHi: .byte >StateIdle, >StateWalk, >StateJump, ...
```

### The Border Color Debugging Technique

A universal technique among C64 game developers for measuring performance:

```
    inc $D020       ; change border color (marks start of section)
    jsr ExpensiveRoutine
    dec $D020       ; restore border color (marks end of section)
```

The width of the colored stripe in the border directly corresponds to how many raster lines the routine consumed. This provides visual, real-time profiling without any external tools. Different colors can be used for different subsystems:

- Red stripe: game logic
- Green stripe: sprite multiplexer
- Blue stripe: music player
- Yellow stripe: scrolling

If the total colored area exceeds the visible frame (stripes extend into the next frame's display area), the game is running over budget and will drop frames.

### PAL vs. NTSC Compatibility

Many commercial games were developed for one system and exhibit problems on the other:

**PAL game on NTSC:**
- 15% less CPU time per frame (17,095 vs. 19,656 cycles)
- 44% less vertical blanking time (63 vs. 112 non-visible raster lines)
- Music plays 20% faster (60 Hz vs. 50 Hz driver calls)
- Scrolling and animation run faster
- Raster effects may glitch due to timing differences

**NTSC game on PAL:**
- Everything runs ~16% slower
- Music plays slower (50 Hz vs. 60 Hz)
- More vertical blank time available (potential for NTSC games that were tight on time)

**Solutions:**
- Detect PAL/NTSC at startup by measuring raster line counts
- Adjust timing constants, scroll speeds, and music tempo accordingly
- Some games maintain separate PAL and NTSC code paths
- Modern homebrew typically targets PAL (the larger market in Europe where the C64 was most popular) and tests NTSC compatibility

---

## References

### Web Resources

- [The Ideal C64 Game Loop -- Retro-Programming](https://retro-programming.com/the-ideal-c64-game-loop/)
- [Codebase64 Wiki: Guide to Programming Games](https://codebase64.org/doku.php?id=base:guide_to_programming_games)
- [Multidirectional Scrolling and the "Game World" by Cadaver](https://cadaver.github.io/rants/scroll.html)
- [Sprite Multiplexing by Cadaver](https://cadaver.github.io/rants/sprite.html)
- [Tile Map Data Compression by Cadaver](https://cadaver.github.io/rants/tilemap.html)
- [Cadaver's C64 Game Framework (GitHub)](https://github.com/cadaver/c64gameframework)
- [How to Implement Smooth Full-Screen Scrolling on C64 -- 1am Studios](http://1amstudios.com/2014/12/07/c64-smooth-scrolling/)
- [Bank-Switched Double-Buffer Scrolling -- Kodiak64](https://kodiak64.com/blog/bank-switched-double-buffer-scrolling)
- [Toggle-Plexing Sprites for Games -- Kodiak64](https://kodiak64.com/blog/toggleplexing-sprites-c64)
- [The Future of VSP Scrolling on the C64 -- Kodiak64](https://kodiak64.co.uk/blog/future-of-VSP-scrolling)
- [Variable Screen Placement: The VIC-II's Forbidden Technique -- Bumbershoot Software](https://bumbershootsoft.wordpress.com/2015/04/19/variable-screen-placement-the-vic-iis-forbidden-technique/)
- [C64: Putting Sprite Multiplexing to Work -- Bumbershoot Software](https://bumbershootsoft.wordpress.com/2026/02/28/c64-putting-sprite-multiplexing-to-work/)
- [Flickering Scanlines: The VIC-II and Bad Lines -- Bumbershoot Software](https://bumbershootsoft.wordpress.com/2014/12/06/flickering-scanlines-the-vic-ii-and-bad-lines/)
- [Safe VSP by Linus Akesson](https://linusakesson.net/scene/safevsp/index.php)
- [VIC-II for Beginners Part 3: Beyond the Screen -- Dustlayer](https://dustlayer.com/vic-ii/2013/4/25/vic-ii-for-beginners-beyond-the-screen-rasters-cycle)
- [C64 Raster Interrupt -- C64-Wiki](https://www.c64-wiki.com/wiki/Raster_interrupt)
- [C64 Raster Time -- C64-Wiki](https://www.c64-wiki.com/wiki/raster_time)
- [C64 Scrolling -- C64-Wiki](https://www.c64-wiki.com/wiki/Scrolling)
- [C64 Sprite -- C64-Wiki](https://www.c64-wiki.com/wiki/Sprite)
- [C64 VIC Bank -- C64-Wiki](https://www.c64-wiki.com/wiki/VIC_bank)
- [C64 Memory Map -- C64-Wiki](https://www.c64-wiki.com/wiki/Memory_Map)
- [C64 Bank Switching -- C64-Wiki](https://www.c64-wiki.com/wiki/Bank_Switching)
- [C64 Fast Loader -- C64-Wiki](https://www.c64-wiki.com/wiki/Fast_loader)
- [C64 SID -- C64-Wiki](https://www.c64-wiki.com/wiki/SID)
- [PAL/NTSC Differences -- Codebase64](http://codebase.c64.org/doku.php?id=base:ntsc_pal_differences)
- [Sprite Multiplexing -- Codebase64](https://codebase.c64.org/doku.php?id=base:sprite_multiplexing)
- [VIC-II Memory Organizing -- Codebase64](https://codebase.c64.org/doku.php?id=base:vicii_memory_organizing)
- [Sprite Multiplexing Tutorial -- selmiak](http://selmiak.bplaced.net/games/c64/index.php?lang=eng&game=Tutorials&page=Sprite-Multiplexing)

### Technical Articles and Analyses

- [Driving the SID Chip: Assembly Language, Composition, and Sound Design for the C64 -- G|A|M|E Journal](https://www.gamejournal.it/driving-the-sid-chip-assembly-language-composition-and-sound-design-for-the-c64/)
- [Rethinking the Memory Map -- C64 OS](https://c64os.com/post/rethinkingthememmap)
- [Raster Interrupts and Splitscreen -- C64 OS](https://c64os.com/post/rasterinterruptsplitscreen)
- [C64 Macro State Machine -- Pink Squirrel Labs](https://pinksquirrellabs.com/blog/2018/10/05/c64-macro-state-machine/)
- [A Full C64 Game in 2013 -- GameDev.net](https://www.gamedev.net/tutorials/programming/general-and-gameplay-programming/a-full-c64-game-in-2013-r3179/)
- [A C64 Game Step by Step -- Georg Rottensteiner](https://georg-rottensteiner.de/c64/projectj/step1/step1b.html)
- [Writing a Commodore 64 Game in the 2020s -- Sven Krasser](https://www.skrasser.com/blog/2023/03/03/writing-a-commodore-64-game-in-the-2020s-a-retrospective/)
- [Working with Maps, Tiles, Chars and Compression -- Breadbin Legacy Wiki](https://github.com/cbmeeks/breadbin-legacy/wiki/Working-with-Maps,-Tiles,-Chars-and-Compression)

### Game-Specific References

- [Impossible Mission -- C64-Wiki](https://www.c64-wiki.com/wiki/Impossible_Mission)
- [Turrican -- C64-Wiki](https://www.c64-wiki.com/wiki/Turrican)
- [Manfred Trenz -- C64-Wiki](https://www.c64-wiki.com/wiki/Manfred_Trenz)
- [Mayhem in Monsterland -- Wikipedia](https://en.wikipedia.org/wiki/Mayhem_in_Monsterland)
- [Mayhem in Monsterland -- How's It Done? (Lemon64 Forum)](https://www.lemon64.com/forum/viewtopic.php?t=27443)
- [Boulder Dash Inside FAQ](http://www.gratissaugen.de/erbsen/BD-Inside-FAQ.html)
- [Armalyte Multiplexor Source Discussion (Lemon64 Forum)](https://www.lemon64.com/forum/viewtopic.php?t=5839)
- [Elite: Harmless -- Disassembled C64 Elite (Lemon64 Forum)](https://www.lemon64.com/forum/viewtopic.php?t=71333)
- [Documented Source Code for Elite on the C64, BBC Micro and Others -- Hackaday](https://hackaday.com/2024/12/15/documented-source-code-for-elite-on-the-c64-bbc-micro-and-others/)

### Tools

- [CharPad C64 (Free Edition) -- Subchrist Software](https://subchristsoftware.itch.io/charpad-c64-free)
- [CharPad C64 Pro -- Subchrist Software](https://subchristsoftware.itch.io/charpad-c64-pro)
- [SpritePad -- Subchrist Software](https://subchristsoftware.itch.io/spritepad-c64-pro)
- [GoatTracker -- Cadaver](https://cadaver.github.io/tools.html)
- [MusicStudio -- Martin Piper](https://martin-piper.itch.io/musicstudio)
- [KickAssembler -- C64-Wiki](https://www.c64-wiki.com/wiki/KickAssembler)
- [VICE Emulator](https://vice-emu.sourceforge.io/)

### Books

- *Retro Game Dev: C64 Edition* by Derek Morris (2017)
- *Commodore 64 Assembly Language Arcade Game Programming* by Steve Bress (1985)
- *Machine Language for the Commodore 64 and Other Commodore Computers* by Jim Butterfield

### Community Forums

- [Lemon64 Forums -- Commodore 64 Community](https://www.lemon64.com/forum/)
- [CSDb -- Commodore Scene Database](https://csdb.dk/)
- [Lemon64 Assembly Tutorials](https://www.lemon64.com/page/assembly-chapter-1-building-a-game-prototype)
