# C64 Demo Scene Effects: A Comprehensive Reference

## Table of Contents

1. [Overview](#1-overview)
2. [Text Effects](#2-text-effects)
3. [Color Effects](#3-color-effects)
4. [Geometric Effects](#4-geometric-effects)
5. [Bitmap Effects](#5-bitmap-effects)
6. [Sprite Effects](#6-sprite-effects)
7. [Border Effects](#7-border-effects)
8. [Combined and Advanced Effects](#8-combined-and-advanced-effects)
9. [Hardcore Details](#9-hardcore-details)
10. [References](#10-references)

---

## 1. Overview

### 1.1 What Is the Demoscene?

The demoscene is an international computer art subculture focused on producing **demos**: self-contained, sometimes extremely small, computer programs that produce real-time audiovisual presentations. The purpose is to demonstrate programming, visual art, and musical skills. In 2020, Finland became the first country to add its demoscene to the national UNESCO list of intangible cultural heritage, followed by Germany and Poland. The demoscene is now recognized globally as a unique digital art form.

### 1.2 Why the C64 Became the Ultimate Demo Platform

The Commodore 64 (1982) became the most iconic demo platform for several converging reasons:

- **Custom chips that invite hacking.** The VIC-II video chip and SID audio chip are both highly programmable, with behaviors that extend far beyond their documented specifications. Undocumented side-effects of the VIC-II chip became the foundation for nearly every classic demo effect.
- **Deterministic timing.** The 6510 CPU runs at a fixed clock rate (985 KHz NTSC, 985 KHz PAL) sharing the bus with the VIC-II in a predictable, cycle-exact pattern. This determinism allows programmers to "race the beam" and modify hardware registers at precisely the right moment.
- **A massive installed base.** Over 17 million units were sold, creating the largest audience for 8-bit demo productions.
- **Severe constraints breed creativity.** With only 64 KB of RAM, 16 colors, 320x200 resolution, 8 hardware sprites, and a 1 MHz CPU, every effect required ingenious exploitation of the hardware. The ability to make the machine do things "supposedly beyond its capabilities" is the core ethos of the scene.
- **An enduring community.** Decades after its commercial life ended, the C64 scene remains active, with new demos released every year at parties like X, Revision, and Transmission64.

### 1.3 Origins and History

**The Cracking Scene (early 1980s).** The demo scene grew directly from software piracy. Crackers who removed copy protection from games would prepend short intro screens ("cracktros") to claim credit. These intros evolved from simple text screens to elaborate displays of scrolling text, music, and color effects.

**First Pure Demos (1985-1986).** Simple demo-like music collections were first assembled on the C64 in 1985 by Charles Deenen. The Dutch groups **1001 Crew** and **The Judges**, while competing with each other in 1986, both produced pure demos with original graphics and music involving extensive hardware trickery. These are considered among the earliest true demos.

**The Golden Era (1987-1992).** This period saw the invention of nearly every fundamental demo effect:
- **1986:** DYCP (Different Y Character Position) -- Yip/PureByte (June 27, 1986)
- **1986:** Multi-speed SID music -- Michael Winterberg (July 3, 1986)
- **1987:** Line Crunching -- The Omega Man/Teeside Cracking Service (July 22, 1987)
- **1987:** VSP (Variable Screen Positioning) -- JCB/The Mean Team (October 30, 1987)
- **1988:** Tech-Tech -- Omega Supreme & Moonray/Rawhead/The Shadows (June 13, 1988)
- **1988:** AGSP (Any Given Screen Position) -- Exilon/Microsystems Digital Technologies (November 6, 1988)
- **1989:** FLI (Flexible Line Interpretation) -- Solomon/Beyond Force (January 1989)
- **1989:** FPP (Flexible Pixel Positioning) -- Crossbow/Crest (April 20, 1989)
- **1991:** Kefrens Bars -- Glasnost/Camelot (December 28, 1991)
- **1992:** Real-time Raytracing -- Depeh/Antic (April 20, 1992)

**The Migration and Resurgence (1990s-2000s).** When the Amiga appeared, many C64 coders migrated. But the C64 scene never died. In the late 1990s and 2000s, a resurgence brought productions that surpassed anything from the golden era, armed with deeper understanding of the hardware and modern cross-development tools.

**The Modern Era (2000s-present).** Groups continue to push boundaries with effects that the original engineers never imagined possible. New graphic modes like NUFLI and NUFLIX achieve near-photographic color depth. Advanced IRQ loaders enable seamless multi-part productions with streaming data. The scene community gathers at demoparties and online via CSDb (the Commodore 64 Scene Database) and Pouet.

### 1.4 Major Groups

| Group | Notable For |
|-------|-------------|
| **Crest** | One of the most legendary C64 groups. Crossbow/Crest holds multiple world records including 144 visible sprites (1997), 80 horizontal Kefrens bars (2008), and 9 sprites on a single rasterline (2007). |
| **Oxyron** | German group formed December 1991. Known for exceptional vector routines; Axis/Oxyron holds the 3D vector plots record (484 plots, 2012). Collaborated with Crest on "Deus Ex Machina." |
| **Booze Design** | Created "Edge of Disgrace" (2008), widely considered one of the greatest C64 demos ever made. HCL/Booze Design holds the vertical rasters record (216 rasters, 2002). |
| **Fairlight** | One of the oldest and most respected groups in scene history, known for legendary cracktros and demos across multiple platforms. |
| **Triad** | A long-running Swedish group known for high-quality releases spanning decades. |
| **Focus** | Prolific German group producing demos, intros, and music collections. |
| **Camelot** | Glasnost/Camelot invented Kefrens bars in 1991. |
| **Beyond Force** | Solomon/Beyond Force invented FLI mode in 1989. |
| **Reflex** | Created "Mathematica," a landmark demo. |
| **Smash Designs** | Known for "The Impossible Thing" and other visually innovative productions. |

### 1.5 Landmark Demos

- **Dutch Breeze** (Blackmail, 1997) -- Rating 8.47 on C64.CH; a milestone in production quality.
- **Deus Ex Machina** (Crest & Oxyron, 2000) -- Code by Crossbow, graphics by Deekay, music by Jeff. A defining production of the modern C64 scene.
- **Mathematica** (Reflex) -- Classic demo frequently cited alongside Deus Ex Machina as an all-time favorite.
- **Edge of Disgrace** (Booze Design, 2008) -- Winner at X-2008. 50 fps smooth animation throughout, textured surfaces, vector animations. Exceeded what many believed possible on the C64.
- **A Mind Is Born** (Linus Akesson, 2017) -- A complete audiovisual demo in exactly 256 bytes. Won 1st place at Revision 2017's Oldskool 4K Intro compo. Uses a cellular automaton for visuals and a linear-feedback shift register for 64 bars of music across all three SID voices.
- **Coma Light** series, **Wonderland** series, **Red Storm** -- Frequently cited among must-see C64 demos.

---

## 2. Text Effects

### 2.1 DYCP Scrollers (Different Y Character Position)

The DYCP scroller is one of the most iconic C64 demo effects: a horizontal scroller where each character independently moves up and down in a sine wave pattern as the text flows from right to left.

**Why it is impressive:** The VIC-II chip does not natively support positioning individual characters at different vertical positions. Characters are locked to an 8-pixel grid in character mode. The DYCP effect circumvents this limitation entirely.

**How it works (overview):**
- The effect uses the character set as a bitmap canvas. Instead of displaying normal text, it copies character pixel data into pairs of custom charset entries at different vertical offsets.
- Each column of the scroller uses two consecutive characters in the charset (16 bytes total, representing a 8x16 pixel area). By copying the source character's 8 bytes into different positions within those 16 bytes, the character appears to shift vertically with single-pixel precision.
- The screen RAM is laid out so that each column maps to its own pair of charset entries.
- A full-screen DYCP uses 40 columns x 2 character rows = 80 character definitions.
- A sine table drives the Y offset for each column, with the table index advancing each frame to create the wave animation.
- Smooth horizontal scrolling is achieved by using the VIC-II's hardware scroll register ($D016 bits 0-2) for sub-character movement, combined with shifting the screen data left by one character every 8 pixels.

**Performance challenges:**
- Clearing and replotting the charset bitmap every frame is the main bottleneck.
- Double-buffering (two charset buffers, swapping each frame) avoids visible tearing and reduces clearing overhead, since only the parts that were written need to be cleared.
- Speedcode (unrolled loops) is often used to achieve acceptable frame rates.

**World first:** Yip/PureByte, June 27, 1986.

### 2.2 DXYCP Scrollers (Dynamic XY Character Placement)

An extension of DYCP that allows full X and Y movement of each character, not just vertical displacement along a fixed horizontal scroll.

**Implementation approaches:**
- **Bitmap-based:** Characters are plotted into a bitmap screen at arbitrary pixel positions using pre-shifted font data. A 5x5 font with 32 characters and 8 shift values requires approximately 1,920 bytes of lookup tables. Each frame costs roughly 15,000 cycles and 11,000 bytes of speedcode.
- **Sprite-based:** Characters are rendered into sprite data, with a sprite multiplexer handling positioning. This allows layering over background graphics.
- **Fixed charset layouts:** Using 32x8 or 16x16 character grids where characters are plotted at grid positions.

Characters can overlap, requiring logical OR operations when multiple characters affect the same byte in the target bitmap or charset.

### 2.3 AGSP Scrollers (Any Given Screen Position)

AGSP combines VSP (Variable Screen Positioning) with line crunching to scroll the screen vertically to any arbitrary pixel position.

**How it works:**
- VSP shifts the screen horizontally by manipulating VIC-II DMA timing.
- Line crunching moves the screen upward by "crunching" 8-pixel character lines down to single pixel lines.
- The combination achieves full pixel-precise vertical and horizontal positioning.

**Limitations:**
- VSP can cause a metastability condition in DRAM, leading to random memory corruption (the infamous "VSP crash"). This is a genuine hardware bug where fragile memory cells are corrupted.
- Does not work reliably on all C64 hardware revisions.
- Despite this, AGSP was used in commercial games like "Another World," "Mayhem in Monsterland," and the "Fred's Back" series.

**World first:** Exilon/Microsystems Digital Technologies, November 6, 1988.

### 2.4 Tech-Tech Scrollers

Tech-tech (also called tec-tec or tic-tac) is an effect that assigns a new X-position to every raster line of a graphic. When driven by animated sine waves, it creates horizontal wave distortion, making logos and text appear to undulate, wave, or ripple across the screen.

**How it works:**
- On each raster line, the code writes a new value to the VIC-II's horizontal scroll register ($D016 bits 0-2).
- By varying these values according to a sine table, each line of the graphic shifts left or right independently.
- The sine table index is incremented each frame to animate the wave.
- Cycle-exact timing is required to change the register at the right moment on each raster line.

**World first:** Omega Supreme & Moonray/Rawhead/The Shadows, June 13, 1988.

### 2.5 Big Character Scrollers

Large-font scrollers display text using characters that are 2x2, 4x4, or even larger character blocks, creating visually imposing scrolling messages.

**Implementation:**
- A dynamic NxN scroller builds each large character on-the-fly from a 1x1 font set.
- A 4x4 scroller uses a matrix of 16 character "patterns" representing all possible 4x4 bit combinations within a character block. The 4x4 version of the entire 1x1 font requires only an extra 128 bytes of font data.
- Pre-shifted data is generated at assembly time rather than computing shifts at runtime.
- Tools like CharPad are used to create tile maps and character sets for large fonts.

### 2.6 Sinus Scrollers

A sinus scroller moves text in a sine wave pattern, either vertically (text undulates up and down) or in more complex paths (circular, Lissajous figures).

**Character-mode sinus scrollers** use the DYCP technique to independently position each character column along a sine curve.

**Sprite-based sinus scrollers** render text into sprites and use the sprites' free Y-positioning to trace sine curves. This is simpler but limited by the 8-sprite hardware maximum (mitigated by multiplexing).

The sine data is typically generated from a quarter-wave lookup table (64 entries) that is mirrored both horizontally and vertically to produce a full 256-entry cycle.

### 2.7 Rotating and Zooming Text

Text that rotates, scales, or perspectively distorts combines multiple VIC-II tricks:

- **FPP (Flexible Pixel Positioning)** allows placing any pixel line of a graphic at any Y position, enabling effects like x-axis rotation, barrel distortion, and smooth stretching.
- **Tech-tech** provides per-line X displacement for horizontal distortion.
- Combined, these create the illusion of text rotating in 3D space or zooming toward the viewer.

**World first for FPP:** Crossbow/Crest, April 20, 1989.

---

## 3. Color Effects

### 3.1 Raster Bars (Copper Bars)

Raster bars are animated horizontal bars of color, the single most iconic demo effect on the C64. They work by "racing the beam" -- changing the background and/or border color registers at precisely timed intervals as the electron gun sweeps down the screen.

**Implementation:**
- Set up a raster interrupt (IRQ) triggered at a specific scan line via VIC-II register $D012.
- In the interrupt handler, write new color values to $D020 (border color) and/or $D021 (background color) in tight loops timed to the raster beam.
- On PAL systems, each raster line is 63 CPU cycles. On NTSC, it is 65 cycles.
- A timing loop of exactly 63 cycles (PAL) will change the color once per raster line.
- On bad lines (every 8th line where VIC-II steals 40 cycles for character data fetch), only 23 cycles are available. Timing tables compensate for this.
- Color values are stored in lookup tables that are rotated each frame to create animation.

**Gradient bars** use sequences of colors that fade from dark to bright and back, creating a smooth gradient appearance despite the C64's 16-color palette.

### 3.2 Bouncer Bars

Bouncer bars are wide gradient raster bars that move vertically, bouncing up and down the screen. They create a lava-lamp-like visual of colored bands floating across the display.

**Implementation:**
- A color table defines the gradient pattern of each bar (typically symmetrical: dark edges, bright center).
- The starting raster line for each bar is driven by a sine table, causing smooth vertical bounce.
- On each frame, the raster interrupt handler walks through the color table starting at the bar's current position.
- Multiple overlapping bars are achieved by layering color changes -- when bars overlap, their colors blend by replacing the current value.
- To animate, the color table is rotated (shift values left, wrap the first to the end) each frame, and the sine table index advances to move the bar position.

### 3.3 Plasma Effects

Plasma effects create psychedelic, flowing color patterns that resemble plasma or liquid. On the C64, they typically operate in character mode using color RAM.

**Algorithm (Double Sine Plasma):**
```
For each cell (x, y):
  color(x,y) = color_table[ sine_h[x] + sine_v[y] ]
```

Where:
- `sine_h[x] = sin((t * n1) + (x * s1)) + sin((t * n2) + (x * s2))`
- `sine_v[y] = sin((t * n3) + (y * s3)) + sin((t * n4) + (y * s4))`
- `t` is the frame counter (for animation)
- `n1..n4` control animation speed
- `s1..s4` control spatial frequency (pattern density)

**C64-specific implementation:**
- The sine table is exactly 256 bytes (one page), so 8-bit addition naturally wraps around without overflow checking.
- Values in the table are scaled to 0-255, and the result of adding two values also wraps, directly indexing the color table.
- The color table maps 256 possible sum values to the C64's 16 colors (or a subset).
- Two separate 1D tables (horizontal and vertical) are computed each frame, then added together for the 2D result. This reduces the computation from O(width * height) sine lookups to O(width + height).
- Color RAM ($D800-$DBE7) and/or screen RAM are updated each frame.

### 3.4 Color Cycling (Color Washing)

Color cycling rotates colors through screen elements over time, creating the illusion of flowing or shimmering color.

**Implementation:**
- A raster interrupt fires at a regular interval (typically 50 Hz, but slowed to ~20 Hz for visual appeal using a frame counter).
- The interrupt handler rotates a color table: save the first value, shift all remaining values left by one position, place the saved value at the end.
- The rotated table is written to color RAM for the affected screen region.
- When applied to a static graphic (like a logo), the colors appear to wash across it.

Color cycling can also be combined with raster bars to create bars whose internal gradient shifts over time.

### 3.5 FLI Color Effects

FLI (Flexible Line Interpretation) dramatically increases the number of available colors in bitmap mode by switching color banks on every raster line.

**Standard multicolor bitmap** allows 4 colors per 8x8 cell: background (shared), and 3 from screen/color RAM.

**FLI mode** uses 8 separate color maps (8 x 1000 bytes = 8 KB), rotating through them on each raster line. This allows selecting 3 different foreground colors for each 8-pixel-wide, 1-pixel-tall strip -- effectively breaking the 8x8 color cell restriction vertically.

**The FLI bug:** The first 3 characters (24 pixels) of every character row display garbage because the CPU cannot complete the color bank switch in time. This area typically displays the background color and is masked in artwork.

**Variants:**
- **AFLI (Advanced FLI):** Supports up to 136 colors. Invented by CLF/Origo Dreamline, April 16, 1990.
- **IFLI (Interlaced FLI):** Combines two FLI frames offset by one pixel horizontally, interlaced at 25 Hz each. The eye blends the two frames, effectively doubling color resolution. Invented by Manfred Trenz, 1991.
- **UFLI (Underlayed FLI):** 288x200 AFLI with single-color sprite underlay covering the FLI bug area. Invented by Crossbow & DeeKay/Crest, April 7, 1996.
- **NUFLI (New Underlayed FLI):** A flicker-free 320x200 format combining FLI bitmap with a full-screen sprite underlay. Provides 3 colors per 8x2 area instead of 2 per 8x8. Uses sprites as underlays to enhance both the FLI bug area and the main display.
- **NUFLIX (NUFLI eXtended):** The latest evolution (2024). Instead of building lookup tables, NUFLIX generates the display code itself ahead of time using a modern computer, achieving near-photographic color reproduction.

### 3.6 Rainbow Effects

Rainbow effects apply smoothly cycling color gradients to screen elements using raster interrupts:

- **Rainbow text:** Each raster line of a text line is colored differently via timed writes to color registers, creating rainbow-striped characters.
- **Rainbow borders:** The border color ($D020) is changed on every raster line according to a cycling palette.
- **Split-screen rainbows:** Different screen regions have different color cycling patterns, achieved through chained raster interrupts.

---

## 4. Geometric Effects

### 4.1 Wireframe 3D Vectors

Wireframe vector graphics display 3D objects as line drawings, rotated in real time.

**Pipeline:**
1. **Model definition:** Vertices stored as (x, y, z) coordinates in 8-bit or 16-bit fixed-point. Edges stored as vertex index pairs.
2. **Rotation:** Apply 3x3 rotation matrix using sine/cosine lookup tables and fast multiply routines.
3. **Projection:** Convert 3D to 2D using perspective division: `screen_x = x * focal / z`, `screen_y = y * focal / z`.
4. **Line drawing:** Bresenham's line algorithm implemented in optimized 6502 assembly, drawing to the bitmap screen.
5. **Double buffering:** Two bitmap screens alternate to prevent flicker. One is displayed while the other is drawn to, then they swap.

**Performance:** The 6510's lack of multiply/divide instructions makes this extremely CPU-intensive. Fast multiply tables (see Section 9) are essential. A simple rotating cube is achievable at full frame rate; complex objects require frame skipping.

### 4.2 Filled Vectors (Filled Polygons)

Filled polygon rendering on the C64 is significantly harder than wireframe and was considered a major achievement.

**Techniques:**
- **Scanline fill:** For each polygon, determine the left and right edges on each raster line, then fill the horizontal span. This requires an edge table and fast horizontal line fill.
- **Hidden surface removal:** Back-face culling using surface normals eliminates invisible polygons before rendering, saving significant CPU time.
- **Flat shading:** Each polygon is filled with a single color, sometimes using the polygon's normal angle to select from a small palette.

**Notable examples:** Games like "Stunt Car Racer" and demos by Oxyron demonstrated impressive filled polygon performance.

**Vector types in the demoscene:**
- **Glenz vectors:** Partially see-through models with a diamond-like appearance, achieved by XORing polygon fills.
- **Blenk vectors:** Shiny, metallic aluminum-like models.
- **Rubber/Gel vectors:** Twisting and elastic models that deform over time.

### 4.3 3D Object Rotation

Rotating 3D objects requires efficient matrix-vector multiplication on the 6510.

**Rotation matrices** for the three axes use sine and cosine values from lookup tables. For a rotation around the Y axis:
```
x' = x * cos(a) + z * sin(a)
z' = -x * sin(a) + z * cos(a)
y' = y
```

Each multiplication uses the fast multiply table technique (see Section 9). A full 3D rotation requires 9 multiplications per vertex (or 6 with optimization, since three terms are simple copies).

### 4.4 Starfields

Starfields simulate movement through a field of stars, creating a sense of depth and speed.

**2D parallax starfield:** Multiple layers of dots scroll at different speeds, with distant layers moving slower. Simple and fast -- just offset X coordinates by different amounts per layer each frame.

**3D perspective starfield:**
- Stars are represented as (x, y, z) coordinates in 3D space.
- Each frame, z is decremented (stars move toward viewer).
- Stars are projected to 2D: `screen_x = x * focal / z + center_x`, `screen_y = y * focal / z + center_y`.
- When z reaches a minimum threshold, the star wraps to a large z value (far distance) with new random x, y.
- Stars near the viewer move faster and are brighter (plotted in a lighter color).

### 4.5 Dot Tunnels

A dot tunnel creates the illusion of flying through a circular tunnel composed of dots or rings.

**Implementation:**
- Multiple layers of circles at different Z depths, each composed of dots positioned using sine/cosine tables.
- Each layer's center position wobbles according to sine functions, creating the tunnel curve.
- Layers are projected from 3D to 2D using perspective projection (same as starfield).
- Dots near the viewer are larger/brighter; distant ones are smaller/dimmer.
- The Z position of each layer advances each frame to create forward motion.

### 4.6 Rotozoomer

The rotozoomer simultaneously rotates and zooms a bitmap texture, creating a hypnotic spinning and scaling effect.

**Algorithm:**
For each pixel on the output screen, compute the corresponding source texture coordinate by applying an inverse rotation and scale transform:
```
src_x = screen_x * cos(angle) * scale - screen_y * sin(angle) * scale + offset_x
src_y = screen_x * sin(angle) * scale + screen_y * cos(angle) * scale + offset_y
pixel = texture[src_x mod width][src_y mod height]
```

The modulo operation creates a tiling effect. On the C64, this is done in multicolor mode (160x200 with doubled pixels), often computing only 160x100 real pixels with Y-doubling.

**History:** First invented by Chaos/Sanity on the Amiga 500 in 1989. C64 implementations followed, pushing the machine to its absolute limits. The technique became a foundation for polygon texture mapping in later 3D renderers.

### 4.7 Fractals

Fractal rendering on the C64 is typically not real-time but pre-computed or very slowly drawn. Mandelbrot set explorers exist but take minutes to render a single frame due to the intensive fixed-point arithmetic required for each pixel. Some demos include slowly-rendering fractal sequences as visual interludes.

---

## 5. Bitmap Effects

### 5.1 Fullscreen Images (FLI / IFLI / NUFLI)

The C64's native graphics modes severely limit color usage, but extended modes push far beyond:

| Mode | Resolution | Colors per Cell | Flicker | FLI Bug | Notes |
|------|-----------|----------------|---------|---------|-------|
| Standard Bitmap | 320x200 | 2 per 8x8 | No | No | Hires mode |
| Multicolor Bitmap | 160x200 | 4 per 8x8 | No | No | Double-wide pixels |
| FLI | 160x200 | 4 per 8x1 | No | Yes (24px) | 8 color maps |
| AFLI | 320x200 | Up to 136 total | No | Yes | Advanced FLI |
| IFLI | 160x200 | ~4 per 4x1 | Yes (25Hz) | Yes | Interlaced, eye blends |
| UFLI | 288x200 | Enhanced | No | Masked | Sprite underlay |
| NUFLI | 320x200 | 3 per 8x2 | No | Masked | Sprite underlay, flicker-free |
| NUFLIX | 320x200 | Near-photo | No | Masked | Code-generated display |
| Super HiRes | 96x200 | 16 colors | No | No | Multiplexed sprite layers |

**Memory requirements:** FLI alone requires 8 KB for color maps plus 8 KB for bitmap data. NUFLI additionally requires sprite data for the underlay layer. These modes consume most of the C64's 64 KB.

### 5.2 Image Transitions

Demos use various transitions between fullscreen images:

- **Ocean Loader wipe:** Horizontal lines reveal the picture from top to bottom as data loads into display memory. The visual pattern occurs because the C64's video memory is organized in a non-linear fashion (character rows are interleaved), so sequential loading creates a distinctive banded reveal.
- **Color fade:** Uses bad line elimination to rapidly update color RAM and screen RAM. Normally updating 1000 bytes of color RAM plus 1000 bytes of screen RAM per frame is prohibitive, but suppressing bad lines frees enough CPU time to do it in a single frame, enabling smooth fade-in/out effects.
- **Dissolve/dither transitions:** Pixels are replaced in a pseudo-random order using an LFSR (linear-feedback shift register) pattern, creating a dissolve from one image to another.
- **Scroll transitions:** FLD is used to slide one image off-screen while another slides on, without copying any bitmap data.

### 5.3 Plasma in Bitmap Mode

Bitmap-mode plasma operates on individual pixels rather than character cells, producing smoother results at the cost of much higher CPU usage.

**Implementation:**
- Operates in multicolor bitmap mode (160x100 effective resolution with doubled pixels).
- Uses the same double-sine algorithm as character-mode plasma but writes to the bitmap and color RAM directly.
- Due to the enormous number of writes per frame, bitmap plasma typically runs at reduced frame rates (12-25 fps) or covers only a portion of the screen.
- Speedcode is essential -- unrolled rendering loops trade memory for the speed needed to update the bitmap.

### 5.4 Zooming and Scaling

Bitmap zooming and scaling effects display images that grow, shrink, or pulse.

- **FLD-based zoom:** FLD can stretch an image vertically by inserting blank lines between character rows, creating the illusion of vertical zoom with zero data copying.
- **Sprite-based zoom:** The VIC-II's sprite double-width and double-height flags provide instant 2x scaling per axis. By toggling these flags at different raster positions, sprites can appear to scale smoothly.
- **True bitmap scaling:** Re-sampling a bitmap at different scales is CPU-intensive and typically done in multicolor mode for reduced resolution.

---

## 6. Sprite Effects

### 6.1 Sprite Multiplexing

The VIC-II supports only 8 hardware sprites simultaneously, but this limit applies per raster line. Sprite multiplexing reuses hardware sprites at different vertical positions to display far more than 8 on screen.

**How it works:**
1. Maintain a list of "virtual" sprites sorted by Y-coordinate (top to bottom).
2. Assign the first 8 virtual sprites to the 8 hardware sprites.
3. Set up raster interrupts triggered when the beam passes below each active sprite.
4. In each interrupt handler, reprogram the hardware sprite's Y position, X position, pointer, and color to display the next virtual sprite lower on screen.
5. The sprite that was previously displayed remains "painted" on the current frame; the hardware sprite is free to be reused.

**Records:**
- Up to 120 sprites have been multiplexed in demo productions.
- Crossbow/Crest achieved 144 visible sprites (April 1997).
- The theoretical maximum on a single raster line remains 8 (hardware limit).
- 9 sprites on a single raster line was achieved by Crossbow/Crest (May 13, 2007) using advanced VIC-II timing tricks.

**Challenges:**
- Sprites must be sorted by Y-coordinate each frame.
- If virtual sprites are vertically close together, there may not be enough raster lines between them to reprogram the hardware sprite, causing visual glitches (flicker, partial sprites).
- Bad lines reduce available CPU time, making multiplexing harder in those regions.

**Game usage:** Games like Green Beret, Ghosts'n Goblins, and many others use multiplexing to display more than 8 enemies and bullets simultaneously.

### 6.2 Sprite Stretching

By manipulating the VIC-II's sprite Y-expansion register ($D017) at specific moments, individual sprite lines can be repeated, making sprites appear stretched to arbitrary heights.

**How it works:**
- Clear a sprite's Y-expand bit, then set it back immediately. This makes VIC-II think it has only displayed the first of the two expanded lines, so it repeats the same line.
- By performing this trick on every raster line, the sprite continues displaying the same data line, stretching it vertically.
- By varying which line is repeated and for how long, sprites can be stretched non-uniformly, creating wave or wobble effects.

**Timing:** A loop that lasts exactly 46 clock cycles takes exactly one raster line to execute, allowing per-line control of the stretch effect.

**Applications:**
- Stretching a single sprite line to fill the entire screen height (used as an efficient background pattern).
- Sinus wave distortion of sprites by varying the stretch amount per line.
- Rubber-band and elastic logo effects.

### 6.3 Logo Wobble

Logo wobble effects display a large logo composed of sprites that undulates and distorts in real time.

**Techniques:**
- **Y-stretch wobble:** Sprite stretching (above) varies the number of repetitions per line according to a sine table, making the logo appear to pulse and breathe.
- **X-wobble:** Each raster line, the sprite's X position is modified according to a sine table, creating horizontal wave distortion (similar to tech-tech but for sprites).
- **MISC (Massively Interleaved Sprite Crunch):** An advanced technique by Linus Akesson that overrides the VIC-II sprite collision register to force a "sprite crunch" from one offset to another, enabling complex stretching patterns and animations.

Large animating sprite logos are constructed by arranging multiple sprites into a grid (e.g., 6x3 = 18 sprites using multiplexing) and animating the sprite data each frame.

### 6.4 Sprite Tunnels

Sprite tunnels use multiplexed sprites to create a 3D tunnel effect:
- Rings of sprites at different Z-depths, with perspective projection controlling their size and position.
- Each ring uses sprite expansion for depth cues (distant rings use normal size, near rings use 2x expansion).
- The tunnel appears to rotate and recede into the distance.

---

## 7. Border Effects

### 7.1 Opening the Top and Bottom Borders

The VIC-II chip displays borders when it finishes rendering character rows. By manipulating register $D011, the border can be tricked into not appearing.

**Technique:**
1. Set up a raster interrupt near the bottom of the visible screen area (raster line $F2-$FA).
2. Clear bit 3 of $D011 (switch from 25-row to 24-row mode). This makes VIC-II think the screen ends higher than it actually does.
3. Before the top of the next frame, set bit 3 again (back to 25-row mode).
4. The VIC-II never sees the transition point where it would normally enable the border, so the border remains off.

This must be done every frame (50 times per second on PAL). The timing is not cycle-critical -- it just needs to happen within the correct raster line range. This makes top/bottom border opening relatively easy compared to side borders.

### 7.2 Opening the Side Borders

Side border opening is much more demanding and must be done on every raster line.

**Technique:**
1. When the VIC-II beam reaches the right edge of the visible area, it checks whether to draw the border by comparing the current column position against the screen width setting.
2. Switch from 40-column to 38-column mode ($D016 bit 3) at the exact cycle when VIC-II checks for border start. Since 38-column mode has a narrower visible area, VIC-II thinks the border should have started earlier and does not turn it on.
3. Switch back to 40-column mode before the next line.
4. **This must be cycle-exact** -- one clock cycle off and the side border will not open.

**Bad line complications:** On bad lines, VIC-II steals 40 cycles, leaving only 23 for the CPU. This is often insufficient to open both side borders while maintaining 8 sprites. Solutions include:
- Opening borders on only 7 out of 8 lines (skipping bad lines).
- Using only 6 sprites (freeing enough cycles on bad lines).
- Scrolling the bad line position to prevent the screen from initiating a character fetch.

### 7.3 Sprites in the Border

Once borders are open, the border area displays whatever VIC-II fetches from address $3FFF (the last byte in the current video bank), which is typically garbage data displayed in the background color.

**Sprites in the border** are the primary way to display meaningful graphics in the border area:
- Sprites are positioned at coordinates outside the normal screen area (Y < 50 or Y > 249 for top/bottom, X < 24 or X > 343 for sides).
- Since sprites render independently of the border blanking mechanism, they remain visible in the opened border area.
- Sprite multiplexing in the border allows scrolling text, logos, and other graphics.

**Ghostbyte technique:** Setting sprite priority lower than the "ghostbyte" (the data at $3FFF) and using the ghostbyte as a mask to control which sprite pixels are visible. This allows producing single-pixel-width graphics from expanded sprites.

### 7.4 Fullscreen Demos

A fullscreen demo opens all four borders and fills the entire TV-visible area with graphics:
- Top/bottom borders opened via $D011 trick.
- Side borders opened via $D016 trick on every raster line.
- Sprites positioned throughout the border area.
- The main screen area uses normal bitmap or character graphics.

**Hyperscreen** is the term for the expanded drawable area. The achievable resolution extends beyond 320x200 to roughly 400x270 visible pixels (depending on TV overscan).

Notable achievement: Raistlin/C64Demo created a huge bitmap scrolling through the full screen and side borders while streaming extra data from disk at 25 fps -- previously thought impossible.

---

## 8. Combined and Advanced Effects

### 8.1 Multiple Simultaneous Effects

The pinnacle of demo coding is running multiple effects simultaneously while maintaining smooth animation and music playback. This requires:

- **Careful CPU budget management:** The C64 has approximately 19,656 cycles per frame (PAL). VIC-II DMA steals approximately 1,000 cycles per frame on normal frames (more with sprites). The SID music player typically consumes 2,000-5,000 cycles per frame. The remaining cycles must be divided among all active effects.
- **Raster splitting:** Different effects run in different vertical screen regions, each triggered by its own raster interrupt. A typical demo part might have raster bars in the top border, a DYCP scroller in the main screen, and sprites in the bottom border.
- **Interleaved computation:** Effects that are not time-critical (e.g., updating a sine table index, computing the next frame of a vector animation) can be done during the vertical blank or in the remaining time after raster-critical code completes.

### 8.2 Kefrens Bars

Kefrens bars are a specific variation of raster bars that create vertical colored stripes, originally named after the Amiga group Kefrens who popularized the effect.

On the C64:
- Horizontal raster bars are combined with per-line color changes that create the appearance of vertical bars bending and distorting.
- The vertical bars follow sine wave patterns (double sinus), creating a warping, organic visual.
- Glasnost/Camelot invented the C64 implementation on December 28, 1991.
- **World record:** 80 horizontal Kefrens bars by Crossbow/Crest (December 28, 2008).
- A famous 256-byte version, "Kefrens Without For" (NoName, 2004), implements the effect with vertical rasters and double sinus in just 256 bytes.

### 8.3 Side Border Scrollers

Scrolling graphics through the opened side borders is one of the most technically demanding effects:

- The side borders must be opened on every raster line with cycle-exact timing.
- Sprites in the border area are reprogrammed on each pass to display scrolling text or graphics.
- The sprite data must be updated each frame to advance the scroll position.
- Bad line handling is critical -- the CPU time stolen by bad lines conflicts with the cycle-exact border-opening requirements.
- Raistlin documented a side border bitmap scroller that streams data from disk while scrolling a full-screen bitmap through the side borders at 25 fps.

### 8.4 Music Visualization

Music visualization synchronizes visual effects with SID chip music playback:

- **SID register monitoring:** SIDBlaster and similar tools capture writes to SID registers ($D400-$D41C) without affecting audio quality, extracting frequency, waveform, and amplitude data.
- **Voice-specific visualization:** Each of the SID's 3 voices can drive a separate visual element (e.g., Voice 1 controls raster bar height, Voice 2 controls color palette, Voice 3 controls animation speed).
- **Beat detection:** Monitoring the amplitude envelope of the bass voice to trigger visual events on drum hits.
- **Integrated composition:** Some demo coders compose music specifically designed to drive visual effects, with the music player exposing timing and note data to the visual routines.

### 8.5 Real-Time Decompression

Modern C64 demos pack far more data than fits in 64 KB by using real-time decompression:

**Compression tools:**
- **Exomizer:** Widely used LZ77-based compressor with a fast 6502 decompression routine.
- **Pucrunch:** Hybrid LZ77/RLE compression that generates self-extracting executables. Can decompress simultaneously while loading.
- **ByteBoozer:** Another popular choice optimized for C64 demo use.

**Streaming decompression:** Modern IRQ loaders like Krill's loader support decompressing data on-the-fly during loading, allowing seamless transitions between demo parts without pausing for decompression.

**Memory overlays:** Different demo parts share the same memory regions, with each part being loaded and decompressed into place just before it runs, while the previous part's data is discarded.

---

## 9. Hardcore Details

### 9.1 How DYCP Works Internally

The DYCP effect is built on a precise understanding of how the VIC-II renders character mode:

**VIC-II character rendering:**
- The screen is a 40x25 grid of 8x8 pixel characters.
- Screen RAM ($0400-$07E7) holds character codes (0-255).
- The character set (2048 bytes) contains the 8-byte bitmap for each of the 256 characters.
- The character set base address is selectable via VIC-II register $D018.

**DYCP memory layout:**
- A custom character set is allocated, typically at a 2 KB-aligned address.
- Each screen column is assigned a pair of character codes (e.g., column 0 uses chars 0 and 1, column 1 uses chars 2 and 3, etc.).
- Screen RAM rows are set up so that the top row contains the first character of each pair, and the row below contains the second.
- This creates a 40-column by 2-row "canvas" where each column has 16 bytes (2 characters x 8 bytes) of independently writable bitmap data.

**Per-frame rendering:**
1. **Clear:** Zero out all 80 character bitmaps in the charset (80 x 8 = 640 bytes). With double-buffering, clear only the buffer that was displayed last frame.
2. **Plot:** For each column, look up the current Y offset from the sine table. Copy the source character's 8 bytes into the column's character pair at the computed offset. If the offset is 0, all 8 bytes go into the first character. If the offset is 3, the first 5 bytes go into bytes 3-7 of the first character, and the last 3 bytes go into bytes 0-2 of the second character.
3. **Scroll:** Advance the horizontal scroll position. Every 8 pixels, shift all column assignments left by one and introduce the next text character on the right.
4. **Animate:** Advance the sine table index for each column to move the wave pattern.

**Double-buffering:**
Two character sets are used. Frame N draws to charset A while charset B is displayed. Frame N+1 swaps: display A, draw to B. This eliminates flicker from partial updates.

**Cycle budget:** The clear+plot operations for 40 columns, even with speedcode, consume roughly 10,000-15,000 cycles -- a significant fraction of the ~19,656 available per frame. Optimizations include clearing only the specific bytes that were written (tracked per column) rather than the entire charset.

### 9.2 Tech-Tech Timing

Tech-tech requires changing the horizontal scroll register ($D016 bits 0-2) on every raster line, with the change taking effect before VIC-II reads it for that line's display.

**Timing constraints:**
- The write to $D016 must land within the horizontal blank period (the cycles between the end of one visible line and the start of the next).
- On a normal line (63 cycles PAL), there is ample time for one register write plus other processing.
- On bad lines (23 free cycles), there is barely enough time for the write plus a minimal timing loop.
- The code must account for bad-line timing differences, typically using a timing table that selects different NOP/delay sequences for bad lines vs. normal lines.

**Typical implementation:**
```
; Stable raster loop (conceptual)
loop:
    lda sine_table,x    ; 4 cycles - load next X offset
    sta $D016            ; 4 cycles - set horizontal scroll
    inx                  ; 2 cycles
    ; ... NOPs to fill remaining cycles to exactly 63 ...
    ; ... (or 23 on bad lines) ...
    dey                  ; 2 cycles
    bne loop             ; 3 cycles (taken) / 2 (not taken)
```

The sine table values are pre-computed with values 0-7 (the 3-bit scroll range), with the remaining bits of $D016 preserving the multicolor and column-select settings.

### 9.3 Bouncer Bar Implementation

**Data structures:**
- `bar_colors[N]`: Array of color values defining the gradient (e.g., `0,6,14,3,1,3,14,6,0` for a white-centered gradient).
- `bar_y_table[256]`: Sine table mapping frame counter to the bar's starting raster line.
- `frame_counter`: Global animation counter, incremented each frame.

**Per-frame raster interrupt handler:**
1. Wait for the raster beam to reach the first line of the bar (from `bar_y_table[frame_counter]`).
2. On each subsequent raster line, load the next color from `bar_colors` and write it to $D020 and $D021.
3. After all bar lines are drawn, restore the default background color.
4. For multiple bars, the handler chains through each bar's color table at its current Y position.

**Multiple overlapping bars:** When bars overlap vertically, the last color written wins. Some implementations blend overlapping colors using a lookup table (color A + color B = blended color C), though the C64's 16-color palette limits blending options.

**Smooth animation:** The sine table typically contains 256 entries covering one full cycle. Each bar advances through the table at a different speed (different increment per frame), so bars bounce at different frequencies and phases.

### 9.4 Vector Math on the 6510

The 6510 CPU has no multiply or divide instructions, no floating-point unit, and operates on 8-bit values. Yet C64 demos perform real-time 3D vector mathematics. Here is how.

#### Fast Multiply Using Tables of Squares

The identity `a * b = ((a+b)^2 - (a-b)^2) / 4` allows multiplication via table lookups.

**Implementation:**
- Pre-compute a 512-byte table of `f(n) = n^2 / 4` for n = 0 to 511 (split into low and high byte tables for 16-bit results).
- To multiply two 8-bit values a and b:
  1. Compute `a + b` and `a - b` (handle negative via twos complement or unsigned with offset).
  2. Look up `f(a+b)` and `f(a-b)` in the table.
  3. Subtract: `result = f(a+b) - f(a-b)`.
- This takes approximately 79-83 cycles, compared to 150+ cycles for a shift-and-add multiply loop.
- When multiplying multiple values by the same factor (common in rotation matrices), self-modifying code writes the factor into the lookup address, saving cycles on subsequent multiplies.

#### Sine and Cosine Tables

- A 256-byte table covers one full sine period, with values scaled to the range 0-255 (or -128 to +127 for signed).
- Cosine is simply sine offset by 64 entries (quarter cycle).
- Only the first quarter (64 entries) needs to be stored; the full table can be generated by mirroring. However, for speed, most demos store the complete 256-byte table.
- Values are typically in 8.8 fixed-point format: the table entry represents the fractional part, with the integer part being 0 or +/-1.

#### Fixed-Point Arithmetic

- **8.8 format:** High byte is integer part, low byte is fraction. Allows values from -128.0 to +127.996.
- **Multiplication of fixed-point values:** Multiply the two 16-bit values as integers (using the fast multiply table), then shift the 32-bit result right by 8 bits to adjust the decimal point.
- **Practical shortcut:** For rotation, the sine/cosine values are fractional (always between -1 and 1). Scale to 8-bit: `N = 256 * sin(angle)`. Multiply `N * coordinate` using the fast multiply table. The high byte of the 16-bit result is the answer.

#### Rotation Matrix Application

For rotating a vertex (x, y, z) around three axes:
1. Load sine and cosine of the current angle from tables.
2. Perform 6-9 multiplications per vertex using fast multiply tables.
3. Apply perspective projection (division approximated by table lookup or iterative subtraction).
4. Total cost: approximately 500-800 cycles per vertex, allowing 20-40 vertices per frame at 50 fps.

### 9.5 Plasma Generation Algorithm (Detailed)

**Step 1: Build the sine table (done once at init).**
```
for i = 0 to 255:
    sine_table[i] = 128 + 127 * sin(2 * PI * i / 256)
```
All values are unsigned bytes (0-255). The table occupies one page for efficient indexing.

**Step 2: Build the color lookup table (done once at init).**
Map the 256 possible sum values to C64 colors. A typical mapping cycles through a color gradient:
```
color_table[0..15]   = 0  (black)
color_table[16..31]  = 6  (dark blue)
color_table[32..47]  = 14 (light blue)
color_table[48..63]  = 3  (cyan)
color_table[64..79]  = 13 (light green)
color_table[80..95]  = 5  (green)
...etc, cycling through colors and back
```

**Step 3: Per-frame computation.**
```
; Compute horizontal table (40 entries)
for x = 0 to 39:
    h_table[x] = sine_table[(anim1 + x * step1) AND 255]
               + sine_table[(anim2 + x * step2) AND 255]

; Compute vertical table (25 entries)
for y = 0 to 24:
    v_table[y] = sine_table[(anim3 + y * step3) AND 255]
               + sine_table[(anim4 + y * step4) AND 255]

; Render to color RAM
for y = 0 to 24:
    for x = 0 to 39:
        color_ram[y*40+x] = color_table[(h_table[x] + v_table[y]) AND 255]

; Advance animation counters
anim1 += speed1
anim2 += speed2
anim3 += speed3
anim4 += speed4
```

**Optimization notes:**
- The separation into horizontal and vertical tables reduces computation from 40x25 = 1000 sine lookups to 40 + 25 = 65 sine lookups plus 1000 additions.
- 8-bit addition wraps naturally, so no AND masking is needed in the inner loop on the 6502.
- The inner loop (1000 iterations of load + add + store) is often unrolled as speedcode for maximum throughput.
- The `step` parameters control spatial frequency (pattern density). Smaller steps = broader patterns; larger steps = tighter patterns.
- The `speed` parameters control animation rate. Different speeds for each component create complex, non-repeating motion.

### 9.6 How Modern Demos Push Limits

Modern C64 demos (2000s-present) leverage decades of accumulated knowledge and modern tools:

**Cross-development:** Code is written on modern PCs using cross-assemblers (KickAssembler, ACME, ca65) with macro systems, conditional assembly, and scripting. Complex lookup tables and speedcode are generated programmatically.

**Cycle-exact emulation:** Emulators like VICE provide cycle-accurate debugging, allowing programmers to verify timing down to individual clock cycles without requiring real hardware.

**Advanced VIC-II exploitation:**
- MISC (Massively Interleaved Sprite Crunch) by Linus Akesson discovered that forcing a sprite crunch can manipulate the sprite display in ways never intended, enabling new stretching and animation effects.
- NUFLIX generates display code on a modern computer that is then played back on the C64, using the CPU as a programmable video controller to achieve near-photographic image quality.
- Combined modes (FLI + sprites + line crunching + side borders) are layered to create displays that exceed what any single mode can achieve.

**Algorithmic advances:**
- Better sorting algorithms for sprite multiplexing (field sort using speedcode buckets).
- Optimal code generation for per-line register writes.
- Improved compression ratios from modern research applied to 6502-targeted decompressors.

**Hardware records continue to be broken:**
- 484 3D vector plots (Axis/Oxyron, 2012).
- 144 visible sprites (Crossbow/Crest, 1997).
- 216 vertical rasters (HCL/Booze Design, 2002).
- 80 horizontal Kefrens bars (Crossbow/Crest, 2008).
- 6 sprites over FLI (Ninja/The Dreams, 2004).

### 9.7 Memory Management for Multi-Part Demos

A multi-part demo (also called a "trackmo" or "megademo") consists of many self-contained parts that load and execute sequentially, often with seamless transitions.

**Memory map strategy:**
- The IRQ loader and music player occupy fixed memory locations that persist across all parts (typically in the $0200-$03FF area or under the BASIC/Kernal ROM).
- Each demo part is allocated the remaining memory (approximately 50-58 KB depending on loader size and music location).
- Parts are compressed on disk and decompressed into their target memory range.
- When a part finishes, it signals the loader to begin loading the next part, which overwrites the current part's memory.

**Loader features (e.g., Spindle, Sparkle):**
- **IRQ-safe loading:** Custom interrupts continue running (music, raster effects) while data loads from disk.
- **Scattered loading:** Data segments can be loaded to non-contiguous memory regions.
- **Streaming decompression:** Data is decompressed on-the-fly during loading, reducing both load time and memory usage.
- **Music persistence:** The SID music player and its data remain in memory across loader calls. Spindle can replace all of C64 RAM except the SID tune and its reserved area during a load, without stopping the music.
- **Transition effects:** The loader supports seamless visual transitions between parts -- one part fades out while the next loads in the background.

**Disk organization:**
- Data is stored on the 1541 disk in a custom interleaved format optimized for sequential reading.
- Custom GCR (Group Code Recording) decoding routines bypass the slow KERNAL disk routines.
- Fast serial transfer protocols between the C64 and 1541 drive achieve transfer speeds of 10-25x the standard KERNAL speed.

### 9.8 IRQ Loader Integration

The IRQ loader is the backbone of any multi-part demo, enabling background loading while effects and music continue running.

**How it works:**
1. The loader takes control of the 1541 disk drive's CPU (a separate 6502 running at 1 MHz).
2. A custom drive-side program is uploaded to the 1541's 2 KB RAM.
3. Communication between the C64 and 1541 occurs over the serial bus (directly manipulated via CIA registers, bypassing the slow KERNAL protocol).
4. The drive reads sectors and sends data to the C64 byte-by-byte (or in small blocks) using a fast serial protocol.
5. On the C64 side, the loader runs within the existing IRQ framework, receiving bytes between raster-critical operations.

**Popular loaders:**
- **Sparkle:** An "all-in-one" cross-platform solution with IRQ loading, linking, and file management. Supports EasyFlash cartridge output.
- **Spindle:** Linus Akesson's integrated linking, loading, and decrunching solution. Features state-of-the-art serial transfer, scattered loading, and minimal RAM footprint.
- **Krill's Loader:** Very fast, supports on-the-fly decompression with common packers (Exomizer, ByteBoozer, Doynax LZ).

**Integration with demo framework:**
```
; Typical demo main loop structure (conceptual)
init:
    jsr init_loader     ; Upload drive code, init serial
    jsr init_music      ; Start SID player
    jsr setup_irq       ; Install raster interrupt chain

main_loop:
    jsr run_part_1      ; Execute first demo part
    jsr load_next_part  ; Signal loader, returns when done
    jsr run_part_2      ; Execute second demo part
    jsr load_next_part
    ; ...

irq_handler:
    jsr play_music      ; Update SID registers (every frame)
    jsr raster_effects  ; Run current part's raster code
    jsr check_loader    ; Receive byte from loader if ready
    rti
```

### 9.9 Bad Lines: The Fundamental Constraint

Nearly every advanced VIC-II effect must account for bad lines, making them the most important timing concept in C64 demo coding.

**What happens on a bad line:**
- Every 8 raster lines, the VIC-II needs to fetch 40 bytes of screen RAM (character codes) and 40 bytes of color RAM to know what to display for the next character row.
- To do this, VIC-II pulls the BA (Bus Available) line low for 40 cycles, halting the CPU.
- The CPU gets only 23 cycles on a bad line, compared to 63 on a normal line.
- Bad lines occur on raster lines where `(raster AND 7) == (YSCROLL AND 7)` -- i.e., where the lowest 3 bits of the raster counter match the vertical scroll value.

**Exploiting bad lines:**
- **FLD (Flexible Line Distance):** By ensuring the YSCROLL value never matches the raster counter (changing it each line to always mismatch), bad lines are suppressed entirely. This pushes character rows further apart, creating vertical gaps.
- **Bad line suppression:** Eliminating bad lines frees up 40 cycles per line (from 23 to 63), enabling effects that need more CPU time (like full-screen color updates for fade effects).
- **Forcing bad lines:** Conversely, forcing a bad line when one would not normally occur (by setting YSCROLL to match the current raster) causes VIC-II to re-fetch character data. This is the basis for FLI mode.
- **DMA delay (VSP):** Triggering a bad line at a non-standard cycle position shifts the VIC-II's internal column counter, moving the screen horizontally -- the VSP effect.

**The deterministic nature of bad lines** is what makes cycle-exact demo coding possible. Because the 40-cycle steal occurs at a precisely known moment, programmers can schedule their register writes around it.

### 9.10 Stable Raster Techniques

Many effects require code that executes at exactly the same cycle on every frame. Achieving this "stable raster" is a foundational technique.

**The problem:** When a raster interrupt fires, the CPU may be at any point within a multi-cycle instruction. This introduces up to 7 cycles of jitter between the interrupt trigger and the first instruction of the handler.

**Double-IRQ technique:**
1. Set up the first raster interrupt a few lines before the critical point.
2. In the first handler, set up a second interrupt on the very next raster line.
3. Pad the first handler with code that always takes the same number of cycles (using NOP chains calibrated so the second interrupt always hits during a known instruction).
4. The second handler begins with jitter of at most 1 cycle (the difference between a 2-cycle NOP and a 3-cycle branch).
5. A final calibration loop (reading the raster counter and branching) eliminates even that last cycle of jitter.

**CIA timer technique:** An alternative uses CIA timer B to measure the exact cycle at which the IRQ fired, then compensates with a calculated delay.

The stable raster is the prerequisite for: tech-tech, side border opening, FLI, sprite stretching, per-line color effects, and any other effect requiring cycle-exact register writes.

---

## 10. References

### Primary Technical Resources

- [Codebase64 Wiki - VIC-II Effects](https://codebase64.net/doku.php?id=vic) -- Source code and documentation for VIC-II demo effects.
- [Codebase64 Wiki - AGSP](http://codebase.c64.org/doku.php?id=base:agsp_any_given_screen_position) -- AGSP implementation details and code.
- [Codebase64 Wiki - Sprite Multiplexing](https://codebase.c64.org/doku.php?id=base:sprite_multiplexing) -- Multiplexing techniques and code.
- [Codebase64 Wiki - Line Crunch](https://codebase64.org/doku.php?id=base:linecrunch) -- Line cruncher implementation.
- [Codebase64 Wiki - FPP](https://codebase.c64.org/doku.php?id=base:fpp) -- Flexible Pixel Positioning reference.
- [Codebase64 Wiki - 6502/6510 Math](https://codebase64.net/doku.php?id=base:6502_6510_maths) -- Math routines including fast multiply.
- [Codebase64 Wiki - Speedcode](https://codebase64.pokefinder.org/doku.php?id=base:speedcode) -- Loop unrolling techniques.

### VIC-II Technical Documentation

- [The MOS 6567/6569 Video Controller (VIC-II)](https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt) -- The definitive VIC-II technical reference by Christian Bauer.
- [VIC-II and FLI Timing (Part 1)](https://c64os.com/post/flitiming1) -- Detailed FLI timing analysis.
- [VIC-II and FLI Timing (Part 2)](https://c64os.com/post/flitiming2) -- Bad lines and DMA timing.
- [Flickering Scanlines: The VIC-II and Bad Lines](https://bumbershootsoft.wordpress.com/2014/12/06/flickering-scanlines-the-vic-ii-and-bad-lines/) -- Bad line explanation.
- [Variable Screen Placement: The VIC-II's Forbidden Technique](https://bumbershootsoft.wordpress.com/2015/04/19/variable-screen-placement-the-vic-iis-forbidden-technique/) -- VSP deep dive.
- [Flexible Line Distance (FLD)](https://bumbershootsoft.wordpress.com/2015/09/17/flexible-line-distance-fld/) -- FLD explanation with code.
- [FLI, Part 1: 16 Color Mode](https://bumbershootsoft.wordpress.com/2016/03/12/fli-part-1-16-color-mode/) -- FLI implementation walkthrough.

### Demo Effect Tutorials and Breakdowns

- [An Introduction to Programming C-64 Demos (Linus/Antimon)](http://www.antimon.org/code/Linus/) -- Classic demo coding tutorial.
- [The Raistlin Papers](https://c64demo.com/) -- Advanced demo coding articles by Raistlin.
- [DXYCP Scrollers (Raistlin Papers)](https://c64demo.com/dxycp-scrollers/) -- DXYCP technical breakdown.
- [Side Border Bitmap Scroller (Raistlin Papers)](https://c64demo.com/side-border-bitmap-scroller/) -- Side border scrolling techniques.
- [Break Down of a C64 Demo Effect (nurpax)](https://nurpax.github.io/posts/2018-06-07-c64-filled-sinewave.html) -- Filled sinewave character effect.
- [BINTRIS C64: Bad Lines and FLD (nurpax)](https://nurpax.github.io/posts/2018-06-19-bintris-on-c64-part-5.html) -- Practical FLD usage.
- [Opening the Borders (Antimon)](https://www.antimon.org/dl/c64/code/opening.txt) -- Border opening reference.
- [Stretching Sprites (Antimon)](http://www.antimon.org/dl/c64/code/streech.txt) -- Sprite stretching techniques.
- [DYCP Horizontal Scrolling (Antimon)](http://www.antimon.org/dl/c64/code/dycp.txt) -- DYCP implementation by Pasi 'Albert' Ojala.
- [Simple FLD Effect (0xC64)](http://www.0xc64.com/2015/11/17/simple-fld-effect/) -- FLD tutorial with code.
- [4x4 Dynamic Text Scroller (0xC64)](http://www.0xc64.com/2017/02/12/tutorial-4x4-dynamic-text-scroller/) -- Big character scroller tutorial.
- [Colour Cycling and Interrupts (0xC64)](http://www.0xc64.com/2013/11/22/colour-cycling-interrupts/) -- Color cycling with raster interrupts.
- [How to Implement Smooth Full-Screen Scrolling](http://1amstudios.com/2014/12/07/c64-smooth-scrolling/) -- Scrolling techniques.
- [Massively Interleaved Sprite Crunch (Linus Akesson)](https://www.linusakesson.net/scene/lunatico/misc.php) -- Advanced sprite manipulation.
- [Safe VSP (Linus Akesson)](https://linusakesson.net/scene/safevsp/index.php) -- VSP without crashes.
- [Sprite Multiplexing by Cadaver](https://cadaver.github.io/rants/sprite.html) -- Practical multiplexing guide.
- [A Simple Diskdrive IRQ-Loader Dissected (Cadaver)](https://cadaver.github.io/rants/irqload.html) -- IRQ loader internals.
- [Opening Top and Bottom Borders (Aart Bik)](https://aartbik.blogspot.com/2019/09/opening-top-and-bottom-borders-on.html) -- Border opening tutorial.
- [Opening Borders (emudev)](https://emudev.de/q00-c64/opening-borders/) -- Emulator perspective on border tricks.

### Graphics Modes and Advanced Formats

- [NUFLI (C64-Wiki)](https://www.c64-wiki.com/wiki/NUFLI) -- NUFLI format documentation.
- [Graphics Modes (C64-Wiki)](https://www.c64-wiki.com/wiki/Graphics_Modes) -- All C64 graphics modes.
- [Pushing the Boundaries of C64 Graphics with NUFLIX](https://cobbpg.github.io/articles/nuflix.html) -- NUFLIX technical paper.
- [Image File Formats (C64 OS)](https://c64os.com/post/imageformats) -- C64 image format survey.
- [C64 Graphic Mode Basics (Cosmigo)](https://www.cosmigo.com/promotion/docs/onlinehelp/gfxHardware-c64.htm) -- Graphics mode reference.

### Scene History and Records

- [Commodore 64 Demos (Wikipedia)](https://en.wikipedia.org/wiki/Commodore_64_demos) -- Overview of C64 demo history and techniques.
- [Demo Effect (Wikipedia)](https://en.wikipedia.org/wiki/Demo_effect) -- General demo effects encyclopedia.
- [Demoscene (Wikipedia)](https://en.wikipedia.org/wiki/Demoscene) -- Demoscene history and cultural context.
- [World Firsts and Records (Recollection)](https://www.atlantis-prophecy.org/recollection/?load=world_of_demos) -- Comprehensive list of C64 demo firsts and records.
- [CSDb - The Commodore 64 Scene Database](https://csdb.dk/) -- Database of all C64 scene releases.
- [C64.CH - The C64 Demo Portal](https://c64.ch/productions/demos) -- Demo rankings and downloads.
- [A Journey into the C64 Demoscene of Today (Hugi)](https://www.hugi.scene.org/online/hugi34/hugi%2034%20-%20demoscene%20reports%20jazzcat%20magic%20a%20journey%20into%20the%20commodore%2064%20demoscene%20of%20today.htm) -- Modern scene report.
- [8-Bit Legends](https://8bitlegends.com/) -- Scene personality profiles.

### Loaders and Compression

- [Spindle v2 (Linus Akesson)](https://linusakesson.net/software/spindle/v2.php) -- Spindle IRQ loader.
- [Spindle v3 (Linus Akesson)](https://linusakesson.net/software/spindle/v3.php) -- Latest Spindle version.
- [Sparkle (GitHub)](https://github.com/spartaomg/SparkleCPP) -- Sparkle IRQ loader and linker.
- [IRQ Loader (C64-Wiki)](https://www.c64-wiki.com/wiki/IRQ_loader) -- General IRQ loader reference.
- [Pucrunch (GitHub)](https://github.com/mist64/pucrunch) -- Hybrid LZ77/RLE compression tool.
- [Compression Basics (a1bert)](https://a1bert.kapsi.fi/Dev/pucrunch/packing.html) -- Compression theory for C64.

### Math and Algorithms

- [Fast Multiplication on 6502 (Lysator)](https://www.lysator.liu.se/~nisse/misc/6502-mul.html) -- Multiplication techniques.
- [Multiply Routine Comparison (GitHub)](https://github.com/TobyLobster/multiply_test) -- Benchmarks of 6502 multiply routines.
- [Field Sort (Linus Akesson)](https://www.linusakesson.net/programming/fieldsort/index.php) -- Fast sorting using speedcode buckets.
- [6502 Source Code Library](http://www.6502.org/source/) -- General 6502 code collection.

### SID and Audio

- [SID (C64-Wiki)](https://www.c64-wiki.com/wiki/SID) -- SID chip technical reference.
- [HVSC - High Voltage SID Collection](https://www.hvsc.c64.org/) -- Archive of C64 music.
- [SIDBlaster (Raistlin Papers)](https://c64demo.com/welcome-to-sidblaster/) -- SID visualization tool.

### Code Examples and Repositories

- [C64 Demo Effects (GitHub - jvalen)](https://github.com/jvalen/c64-effects) -- Demo effect examples with source.
- [DYCP Scroller (GitHub - Twilight1971)](https://github.com/Twilight1971/C64--DYCP-Scroller) -- DYCP implementation.
- [C64 Demo (GitHub - nicolacimmino)](https://github.com/nicolacimmino/C64-Demo) -- Raster bar and other effects.
- [C64 Demo Effects (GitHub - geehaf)](https://github.com/geehaf/c64-demo-effects) -- Various effect implementations.

### Notable Demo Productions

- [A Mind Is Born (Linus Akesson)](https://linusakesson.net/scene/a-mind-is-born/) -- 256-byte demo technical writeup.
- [Edge of Disgrace (Pouet)](https://www.pouet.net/prod.php?which=51983) -- Booze Design's landmark demo.
- [Deus Ex Machina (CSDb)](https://csdb.dk/release/?id=11585) -- Crest & Oxyron's classic.
- [Kefrens Without For (Pouet)](https://www.pouet.net/prod.php?which=11675) -- 256-byte Kefrens bars.

### Cultural Heritage

- [Demoscene as UNESCO Intangible Cultural Heritage](https://demoscene-the-art-of-coding.net/) -- Campaign documentation.
- [UNESCO Germany - Demoscene](https://www.unesco.de/en/culture-and-nature/intangible-cultural-heritage/demoscene-culture-digital-real-time-animations) -- Official UNESCO recognition.
