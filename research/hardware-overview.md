# Commodore 64 Hardware Overview

A comprehensive system-level reference to the Commodore 64 hardware, covering
architecture, main chips, board revisions, connectors, PAL/NTSC differences, and
low-level bus timing details.


---

## 1. Overview

The Commodore 64, released in January 1982 at the Consumer Electronics Show and
shipping from August 1982, is the best-selling single personal computer model of all
time, with estimates of 12.5 to 17 million units sold during its 1982-1994 production
run. Its retail price at launch was US$595.

The machine was designed by a small team at MOS Technology/Commodore Semiconductor
Group, with key contributions from Robert Yannes (SID chip), Al Charpentier and
Charles Winterble (VIC-II), and Robert Russell (system architecture). Rather than
being designed around off-the-shelf components, the C64 was built around custom
silicon -- the VIC-II graphics chip and SID sound chip -- that had been designed in
advance and were waiting for a product to live in.

The design philosophy favored mature semiconductor processes for high chip yields and
low cost over compact die layouts. This kept manufacturing costs low enough to compete
directly with game consoles while delivering substantially more capability. By 1983
the manufacturing cost was under US$135, enabling aggressive price cuts that helped
drive competitors like Texas Instruments and Atari out of the low-end market.

The result was a machine with capabilities far ahead of its price point: 64KB RAM,
hardware sprites, smooth scrolling, raster interrupts, a three-voice synthesizer with
programmable filters, and a flexible bank-switching memory architecture -- all in a
single keyboard-integrated enclosure powered by a simple external power brick.


---

## 2. System Architecture

### Block Diagram

```
                    +-----------+
                    | Cartridge |
                    |   Port    |
                    +-----+-----+
                          |
                  ROML, ROMH, GAME, EXROM, DMA, NMI, IRQ
                          |
  +-------+   PHI0   +---+----+          +----------+
  | 8701  |--------->| VIC-II |--------->| RF Mod / |
  | Clock |   DOT    | 6569/  |  Luma    | A/V Out  |
  | Gen   |--------->| 6567   |  Chroma  +----------+
  +---+---+          +---+----+  Comp.
      |  Color            |
      |  Clock       AEC, BA, PHI0
      |                   |
      |      +------------+------------+
      |      |            |            |
      |  +---+---+   +----+----+  +---+----+
      |  | 64 KB |   |  PLA    |  | Color  |
      |  | DRAM  |   | 906114  |  | RAM    |
      |  | (8x   |   | (82S100)|  | 2114   |
      |  | 4164) |   +----+----+  | 1Kx4   |
      |  +---+---+        |       +---+----+
      |      |        Chip Select      |
      |      |        Signals          |
      |      |            |            |
  +---+------+------------+------------+-------+
  |              System Bus                     |
  |    16-bit Address / 8-bit Data / R/W       |
  +---+--------+--------+--------+--------+---+
      |        |        |        |        |
  +---+---+ +--+--+ +---+--+ +--+---+ +--+---+
  | 6510  | | ROM | | ROM  | | CIA  | | CIA  |
  | CPU   | |BASIC| |KERNAL| | #1   | | #2   |
  |       | | 8KB | | 8KB  | | 6526 | | 6526 |
  +---+---+ +-----+ +--+---+ +--+---+ +--+---+
      |               |          |        |
   Cassette        Char ROM    Keyboard  Serial/IEC
   Motor/Sense     4KB        Joystick   User Port
   LORAM/HIRAM                Paddle
   CHAREN
```

### Bus Architecture

The C64 uses a shared 16-bit address bus and 8-bit data bus. The critical design
challenge is that both the 6510 CPU and the VIC-II video chip need access to the
64KB DRAM. This is solved by time-division multiplexing: the system clock is divided
into two phases, and the CPU and VIC-II alternate access every half cycle.

**PHI1 (phase 1, clock low):** The VIC-II accesses the bus. It reads character
pointers, bitmap data, sprite data, and performs DRAM refresh.

**PHI2 (phase 2, clock high):** The CPU accesses the bus. It performs instruction
fetches, reads, and writes.

This interleaving happens every single clock cycle, giving both chips continuous
memory access without the CPU needing to wait -- except during "bad lines" and
sprite DMA, when the VIC-II steals extra cycles (see Section 7).

### Clock Speeds

| Parameter       | PAL              | NTSC             |
|-----------------|------------------|------------------|
| Crystal (Y1)    | 17.734475 MHz    | 14.31818 MHz     |
| Color clock     | 4.433618 MHz     | 3.579545 MHz     |
| Dot clock       | 7.881984 MHz     | 8.181816 MHz     |
| System clock    | 0.985248 MHz     | 1.022727 MHz     |
| Cycle time      | ~1.015 us        | ~0.978 us        |

The crystal frequency is chosen to be exactly 4x the television color subcarrier
frequency (4.433618 MHz for PAL, 3.579545 MHz for NTSC). The 8701 clock generator
chip uses a PLL to derive the dot clock from the color clock, and the VIC-II divides
the dot clock by 8 to produce PHI0, the system clock.

**PAL derivation:** 17.734475 / 18 = 0.985248 MHz (dot clock = color x 32/18,
system = dot / 8)

**NTSC derivation:** 14.31818 / 14 = 1.022727 MHz (dot clock = color x 16/7,
system = dot / 8)

### The PLA (Programmable Logic Array)

The PLA (part number 906114-01, using a Signetics 82S100 die) is the "glue logic"
chip that ties the entire system together. Designed by Dave DiOrio, it is a
combinatorial logic device with no internal state. Its job is to generate chip-select
signals that determine which device responds on the data bus for any given memory
address.

**Inputs (20 active):**

| Signal   | Source          | Function                                  |
|----------|-----------------|-------------------------------------------|
| A15-A12  | Address bus     | Upper address bits for region decode       |
| VA14-VA12| VIC-II          | VIC address bits for VIC memory access     |
| CHAREN   | 6510 I/O port   | Character ROM vs I/O select                |
| HIRAM    | 6510 I/O port   | KERNAL ROM enable                          |
| LORAM    | 6510 I/O port   | BASIC ROM enable                           |
| GAME     | Cartridge port  | Cartridge memory configuration             |
| EXROM    | Cartridge port  | Cartridge memory configuration             |
| BA       | VIC-II          | Bus Available signal                       |
| AEC      | VIC-II          | Address Enable Control (active low)        |
| R/W      | 6510 CPU        | Read/Write signal                          |
| CAS      | VIC-II          | Column Address Strobe for DRAM             |

**Outputs (8):**

| Signal  | Function                                    |
|---------|---------------------------------------------|
| CASRAM  | DRAM chip select (active low)               |
| BASIC   | BASIC ROM chip select (active low)          |
| KERNAL  | KERNAL ROM chip select (active low)         |
| CHAROM  | Character ROM chip select (active low)      |
| GR/W    | Color RAM write enable                      |
| I/O     | I/O area chip select (active low)           |
| ROML    | Cartridge ROM Low select (active low)       |
| ROMH    | Cartridge ROM High select (active low)      |

The PLA's reaction time is approximately 20-40ns, fast enough to decode addresses
within the setup time of the bus cycle. It is one of the most failure-prone chips in
the C64 and a common cause of dead machines; third-party replacements (SuperPLA,
PLA20V8, EPROM-based substitutes) are widely used.

#### Memory Configuration Modes

The five control lines LORAM, HIRAM, CHAREN, GAME, and EXROM create 32 theoretical
combinations, of which 14 produce distinct memory maps. Key configurations:

**Mode 31 (default, $37 in processor port):** BASIC ROM at $A000-$BFFF, KERNAL ROM
at $E000-$FFFF, I/O at $D000-$DFFF, Character ROM accessible only to VIC-II, RAM
everywhere else.

**Mode 27 ($33):** Character ROM visible to CPU at $D000-$DFFF instead of I/O.

**Mode 24 ($30):** All RAM visible to CPU -- BASIC, KERNAL, and Character ROMs all
banked out. CPU sees 64KB of contiguous RAM.

**Ultimax mode (GAME=0, EXROM=1):** A special cartridge configuration inherited from
the unreleased MAX Machine. Only 4KB of internal RAM is available ($0000-$0FFF);
address ranges $1000-$7FFF and $A000-$CFFF are unmapped (open bus). Cartridge ROM
appears at $8000-$9FFF (ROML) and $E000-$FFFF (ROMH). This mode is used by some
cartridges for DMA and system takeover.

The VIC-II always sees RAM (and character ROM in banks 0 and 2) regardless of the
CPU-side bank configuration, since VIC memory access occurs during PHI1 when
different PLA logic applies.


---

## 3. Main Chips

### MOS 6510 CPU

| Parameter       | Value                                  |
|-----------------|----------------------------------------|
| Architecture    | 8-bit, derived from MOS 6502           |
| Package         | 40-pin DIP                             |
| Address bus     | 16-bit (64KB address space)            |
| Data bus        | 8-bit                                  |
| Clock           | 0.985 MHz (PAL) / 1.023 MHz (NTSC)    |
| Process         | NMOS (6510) / HMOS-II (8500)           |
| Registers       | A, X, Y (8-bit), SP (8-bit), PC (16-bit), P (status) |
| Stack           | 256 bytes at $0100-$01FF               |
| Addressing modes| 13 (including zero page, indexed, indirect) |
| Interrupts      | IRQ (maskable), NMI (non-maskable), RESET |

**Differences from the 6502:**

The 6510 is essentially a 6502 with one key addition: a built-in 6-bit bidirectional
I/O port, exposed at CPU address $0000 (data direction register) and $0001 (data
register). This port directly controls critical system signals:

| Bit | Signal    | Direction | Function                          |
|-----|-----------|-----------|-----------------------------------|
| 0   | LORAM     | Output    | BASIC ROM enable (1=ROM, 0=RAM)   |
| 1   | HIRAM     | Output    | KERNAL ROM enable (1=ROM, 0=RAM)  |
| 2   | CHAREN    | Output    | Char ROM vs I/O (1=I/O, 0=Char)  |
| 3   | CASS WRT  | Output    | Cassette data write               |
| 4   | CASS SENSE| Input     | Cassette key detect (0=pressed)   |
| 5   | CASS MOTOR| Output    | Cassette motor control (0=on)     |
| 6-7 | --        | --        | Not connected on C64              |

The default value after reset is DDR=$2F, DATA=$37 (BASIC, KERNAL, and I/O visible).

The 6510 also adds the ability to tri-state the address bus (via the AEC line from
the VIC-II), allowing the VIC-II to take over the bus during its access phases. The
later HMOS-II version (MOS 8500) is functionally identical but manufactured on a
smaller process for lower power consumption.

### MOS 6569/6567 VIC-II (Video Interface Controller II)

| Parameter              | 6569 (PAL)     | 6567R8 (NTSC)   |
|------------------------|----------------|------------------|
| Package                | 40-pin DIP     | 40-pin DIP       |
| Process                | NMOS / HMOS-II | NMOS / HMOS-II   |
| Address bus            | 14-bit (16KB)  | 14-bit (16KB)    |
| Data bus               | 12-bit (8 CPU + 4 Color RAM)    ||
| System clock output    | 985.248 kHz    | 1022.727 kHz     |
| Dot clock input        | 7.882 MHz      | 8.182 MHz        |
| Raster lines/frame     | 312            | 263              |
| Cycles/line            | 63             | 65               |
| Visible lines          | 284            | 235              |
| Display resolution     | 320x200 (hires) / 160x200 (multicolor) ||
| Sprites                | 8 hardware sprites, 24x21 pixels each  ||
| Colors                 | 16 fixed palette                        ||
| Video modes            | 5 usable (+ 3 invalid)                  ||
| Registers              | 47 ($D000-$D02E)                        ||
| HMOS-II variants       | 8565 (PAL)     | 8562 (NTSC)      |

The VIC-II generates the system clock (PHI0), manages DRAM refresh (5 refresh cycles
per raster line), and provides raster interrupt capability. It has only 14 address
lines, limiting it to a 16KB window of memory; the upper two address bits are supplied
by CIA #2 (port A, bits 0-1, active low), allowing selection of four 16KB banks.

The VIC-II's 12-bit data bus is unique: 8 bits come from the main data bus (for
character/bitmap/sprite data) and 4 bits come directly from Color RAM (for per-cell
color information). This is why Color RAM is wired separately from the main memory bus.

### MOS 6581/8580 SID (Sound Interface Device)

| Parameter         | 6581             | 8580             |
|-------------------|------------------|------------------|
| Package           | 28-pin DIP       | 28-pin DIP       |
| Process           | NMOS             | HMOS-II          |
| Supply voltage    | +5V (VCC), +12V (VDD) | +5V (VCC), +9V (VDD) |
| Voices            | 3                | 3                |
| Frequency range   | 0-4 kHz (16-bit resolution)        ||
| Waveforms         | Sawtooth, Triangle, Pulse (PWM), Noise ||
| Envelope          | ADSR per voice (0-24 sec range)    ||
| Modulation        | Ring mod, oscillator sync          ||
| Filters           | Programmable multimode (LP/BP/HP/Notch) ||
| Filter caps       | 470 pF           | 22 nF            |
| Master volume     | 16 steps (4-bit)                   ||
| A/D converters    | 2x 8-bit (paddle inputs)           ||
| External audio in | Yes              | Yes              |
| Registers         | 29 ($D400-$D41C)                   ||

**Key differences between 6581 and 8580:**

- **Voltage:** The 6581 requires 12V on its VDD pin; the 8580 uses 9V. Putting an
  8580 in a board wired for 12V will damage it.
- **Filters:** The 6581 has stronger, more characterful analog filters; the 8580
  filters are more accurate but sound thinner to many listeners. Different filter
  capacitor values (470pF vs 22nF) contribute to the distinct sound.
- **Digi playback:** The 6581's volume register ($D418) produces an audible click when
  changed. By rapidly toggling the lower 4 bits, 4-bit digital sample playback is
  possible. The 8580 "fixed" this behavior, making raw digi playback nearly
  inaudible without a hardware modification (a resistor between EXT IN and GND).
- **Waveform combination:** Combined waveforms (e.g., triangle + sawtooth) produce
  different results on 6581 vs 8580 due to internal pull-up differences.
- **Release behavior:** The 6581 does not fully silence a voice during release,
  producing a subtle reverb/sustain effect absent on the 8580.

### MOS 6526 CIA (Complex Interface Adapter) x2

| Parameter         | Value                              |
|-------------------|------------------------------------|
| Package           | 40-pin DIP                         |
| Process           | NMOS (6526) / HMOS-II (8521)       |
| I/O ports         | 2x 8-bit bidirectional (Port A, Port B) |
| Timers            | 2x 16-bit countdown (Timer A, Timer B) |
| TOD clock         | 24-hour BCD (hours:minutes:seconds:tenths) |
| Shift register    | 8-bit serial I/O                   |
| Interrupts        | 5 sources per CIA (timer, TOD alarm, shift register, flag) |
| Handshaking       | PC output, FLAG input              |
| Registers         | 16 ($DC00-$DC0F for CIA#1, $DD00-$DD0F for CIA#2) |

**CIA #1 ($DC00-$DC0F):**
- Port A: Keyboard column select, joystick port 2
- Port B: Keyboard row read, joystick port 1
- Timer A/B: General-purpose timing, raster synchronization
- TOD: Real-time clock (driven by mains frequency via power supply)
- IRQ output connected to CPU IRQ line

**CIA #2 ($DD00-$DD0F):**
- Port A bits 0-1: VIC-II bank select (active low)
- Port A bits 2-7: Serial/IEC bus (DATA, CLK, ATN)
- Port B: User port parallel data lines
- Timer A/B: Serial bus timing, general purpose
- Shift register: User port serial I/O
- NMI output connected to CPU NMI line

The TOD clock is driven by the 50/60 Hz signal derived from the 9VAC power supply
line (via a frequency divider at U27), not by the system clock. This makes it
accurate for real-time timekeeping but means it requires the correct mains frequency
to keep proper time. The SX-64 portable uses a different TOD clock source since it
has an internal power supply.

### Memory

**64KB DRAM:**
- Early boards: 8x 4164 (64K x 1-bit) chips
- ASSY 250466: 2x 41256 (32K x 8-bit) chips
- ASSY 250469: 2x 41464 (64K x 4-bit) chips
- Access time: 150ns typical
- Refresh: Handled by VIC-II (5 RAS-only refresh cycles per raster line)
- All 64KB visible to both CPU and VIC-II at all times (ROM overlays for CPU only)

**1KB Color RAM (Static):**
- Chip: 2114 (1024 x 4-bit SRAM) on early/mid boards; integrated into the custom
  PLA/MMU chip (252535-01) on ASSY 250469 Rev. B
- 4 bits wide (stores color index 0-15 per character cell)
- Mapped at $D800-$DBFF (only low nibble significant)
- Connected directly to VIC-II's upper 4 data lines (D8-D11)
- Directly accessible by both CPU and VIC-II
- Always visible regardless of bank switching configuration
- Not controlled by the PLA in the same way as other chips; wired semi-independently

**ROMs:**

| ROM         | Size | Address (CPU)  | Contents                          |
|-------------|------|----------------|-----------------------------------|
| BASIC V2    | 8 KB | $A000-$BFFF    | Commodore BASIC 2.0 interpreter   |
| KERNAL      | 8 KB | $E000-$FFFF    | Operating system, I/O routines    |
| Character   | 4 KB | $D000-$DFFF    | Two character sets (uppercase/graphics, upper/lowercase) |

The Character ROM is unusual: it is normally visible only to the VIC-II (at
$1000-$1FFF or $9000-$9FFF in VIC address space, banks 0 and 2 respectively).
The CPU can access it by setting CHAREN=0, which maps it at $D000-$DFFF in place
of I/O. On ASSY 250469 boards, BASIC and KERNAL ROMs are combined into a single
16KB ROM chip.


---

## 4. Board Revisions

The Commodore 64 was manufactured on at least seven distinct motherboard revisions
over its production life, with progressive cost reduction and chip integration.

### ASSY 326298 (1982) -- "Silver Label" / Original Breadbin

| Detail        | Value                                  |
|---------------|----------------------------------------|
| Schematic     | 326106                                 |
| Revisions     | Rev. 6, A, B, C                        |
| A/V connector | 5-pin DIN (some later: 8-pin)          |
| RAM           | 8x 4164 (64K x 1-bit)                 |
| PLA           | 82S100 (discrete)                      |
| Clock gen     | LM556 + 74LS629 (discrete PLL)        |
| VIC-II        | 6567 (NTSC) / 6569 (PAL), ceramic package |
| SID           | 6581                                   |
| CPU           | 6510                                   |

First production motherboard, shown at CES Las Vegas in January 1982. Features the
highest chip count of any C64 revision. The 5-pin A/V connector lacks a separate
chrominance output, limiting video quality. The ceramic-packaged VIC-II was used
early on for thermal management but was expensive. Some compatibility issues exist
with certain cartridges that reset the C64. Highly collectible.

### KU-14194HB (1982) -- European Interim Board

| Detail        | Value                                  |
|---------------|----------------------------------------|
| Fab No.       | 251022                                 |
| Revisions     | A, B                                   |
| A/V connector | 8-pin DIN                              |

A short-lived European-only board not documented in American service manuals.
Rare; quickly superseded by the ASSY 250407.

### ASSY 250407 (1983) -- Most Common Breadbin Board

| Detail        | Value                                  |
|---------------|----------------------------------------|
| Schematic     | 251138                                 |
| Revisions     | A, B, C                                |
| Designation   | "A (CR)" (Cost-Reduced)                |
| A/V connector | 8-pin DIN (standard from this revision)|
| RAM           | 8x 4164                                |
| PLA           | 82S100                                 |
| Clock gen     | MOS 7701/8701                          |

The most widely produced C64 board, manufactured during peak popularity (1983-1984).
The 8-pin A/V connector provides separate luminance and chrominance for S-Video
output. The discrete PLL clock circuit was replaced by the MOS 7701/8701 custom
clock generator. Much improved reliability over the 326298.

### ASSY 250425 (1984) -- "64B"

| Detail        | Value                                  |
|---------------|----------------------------------------|
| Schematic     | 251469                                 |
| Revisions     | A, B                                   |
| Notable       | IEC bus protection diodes added        |

Further cost-reduced design. The VIC-II moved from a metal-can package to a plastic
DIP. PLA relocated next to the VIC-II; SID moved near the serial port. Known for
excellent video output quality when paired with contemporary VIC-II chip dates.
Considered by many collectors to be the most desirable breadbin board for authentic
C64 experience.

### ASSY 250466 (1986) -- "B-3" / 64C Long Board

| Detail        | Value                                  |
|---------------|----------------------------------------|
| Schematic     | 252278                                 |
| Revisions     | A                                      |
| RAM           | 2x 41256 (32K x 8-bit)                |
| VIC-II        | 6569 R5 (PAL) or equivalent            |
| SID           | 6581 R4AR                              |
| PLA           | 82S100                                 |

Transitional board appearing in late breadbin cases and early C64C cases. The eight
RAM chips were reduced to two. Still uses the original NMOS chip set (6581 SID at
12V). Highly regarded for video quality. Has a horizontal fuse behind the cartridge
port.

### ASSY 250469 (1987-1993) -- "E" / 64C Short Board

| Detail        | Value                                  |
|---------------|----------------------------------------|
| Schematic     | 252312                                 |
| PCB No.       | 252311                                 |
| Revisions     | 1, 3, 4, A, B                          |
| RAM           | 2x 41464 (64K x 4-bit)                |
| PLA           | 251715-01 custom 64-pin IC (Rev 1-A) / 252535-01 with integrated Color RAM (Rev B) |
| Clock gen     | 8701T6                                 |
| CPU           | 8500 (HMOS-II)                         |
| VIC-II        | 8562 (NTSC) / 8565 (PAL), HMOS-II     |
| SID           | 8580 (HMOS-II), 9V                     |

The final and most cost-reduced C64 board, colloquially called the "short board" due
to its smaller PCB. The lowest chip count of any C64 motherboard. All major chips
switched to HMOS-II versions with lower power consumption. The discrete PLA was
replaced by a 64-pin custom IC combining PLA logic with additional glue logic.

**Rev. B** is the most integrated version: the 252535-01 custom IC incorporates
Color RAM, eliminating the separate 2114 SRAM chip. It also introduces solder
jumpers (J10-J18) for flexible ROM configuration. Rev. B is rare in the USA.

Boards are designated -09 (NTSC) or -10 (PAL) after the ASSY number.

A vertical fuse near the cartridge port replaced the horizontal fuse of earlier
boards.

### Summary of Chip Evolution

| Component   | Early (326298-250425) | Mid (250466) | Late (250469)       |
|-------------|-----------------------|--------------|---------------------|
| CPU         | 6510                  | 6510         | 8500                |
| VIC-II      | 6567/6569 (NMOS)      | 6569 R5      | 8562/8565 (HMOS-II) |
| SID         | 6581 (12V)            | 6581 (12V)   | 8580 (9V)           |
| CIA         | 6526                  | 6526         | 6526 or 8521        |
| PLA         | 82S100                | 82S100       | 251715/252535 custom |
| RAM         | 8x 4164               | 2x 41256     | 2x 41464            |
| ROM         | 3 separate chips      | 3 separate   | Combined 16KB       |
| Clock gen   | Discrete / 7701       | 8701         | 8701T6              |

### Special Variants

- **SX-64 (1983):** Portable with built-in 5" CRT and 1541 drive. ASSY 250408-01.
- **C64GS (1990):** Game console variant. Modified ASSY 250469 Rev. B with expansion
  port rotated 90 degrees upward, no keyboard connector or serial port.
- **PET 64/CBM 4064:** Standard ASSY 326298 in a PET-style case with monochrome
  monitor.
- **Educator 64:** Same as PET 64 but for the educational market.
- **C64 Gold Edition (1987):** Standard ASSY 250407 in a gold-colored case.


---

## 5. Connectors and Ports

### Cartridge / Expansion Port

| Detail           | Value                                       |
|------------------|---------------------------------------------|
| Connector        | 44-pin (2x22) PCB edge connector            |
| Pitch            | 2.54 mm (0.1")                              |
| Location         | Rear of machine, top edge                   |

**Pinout (active-low signals prefixed with /):**

| Pin | Signal    | Pin | Signal  |
|-----|-----------|-----|---------|
| 1   | GND       | A   | GND     |
| 2   | +5V       | B   | /ROMH   |
| 3   | +5V       | C   | /RESET  |
| 4   | /IRQ      | D   | /NMI    |
| 5   | R/W       | E   | PHI2    |
| 6   | DOT CLK   | F   | A15     |
| 7   | /IO1      | H   | A14     |
| 8   | /GAME     | J   | A13     |
| 9   | /EXROM    | K   | A12     |
| 10  | /IO2      | L   | A11     |
| 11  | /ROML     | M   | A10     |
| 12  | BA        | N   | A9      |
| 13  | /DMA      | P   | A8      |
| 14  | D7        | R   | A7      |
| 15  | D6        | S   | A6      |
| 16  | D5        | T   | A5      |
| 17  | D4        | U   | A4      |
| 18  | D3        | V   | A3      |
| 19  | D2        | W   | A2      |
| 20  | D1        | X   | A1      |
| 21  | D0        | Y   | A0      |
| 22  | GND       | Z   | GND     |

The expansion port provides full access to the address and data buses, plus control
signals for memory mapping (GAME, EXROM, ROML, ROMH), interrupts (IRQ, NMI), and
bus control (BA, DMA). Cartridges must listen passively to the bus and respond within
the bus timing constraints. The /DMA line allows external hardware to request direct
memory access, though the KERNAL does not natively support DMA.

### User Port

| Detail           | Value                                       |
|------------------|---------------------------------------------|
| Connector        | 24-pin (2x12) PCB edge connector            |
| Pitch            | 3.96 mm (0.156")                            |
| Card thickness   | 1.57 mm                                     |

**Pinout:**

| Pin | Signal  | Pin | Signal   |
|-----|---------|-----|----------|
| 1   | GND     | A   | GND      |
| 2   | +5V     | B   | /FLAG2   |
| 3   | /RESET  | C   | PB0      |
| 4   | CNT1    | D   | PB1      |
| 5   | SP1     | E   | PB2      |
| 6   | CNT2    | F   | PB3      |
| 7   | SP2     | G   | PB4      |
| 8   | /PC2    | H   | PB5      |
| 9   | ATN     | J   | PB6      |
| 10  | 9VAC    | K   | PB7      |
| 11  | 9VAC    | L   | PA2      |
| 12  | GND     | M   | GND      |

PB0-PB7 are the 8 data lines of CIA #2 Port B, providing a freely programmable
8-bit parallel port. SP1/SP2 and CNT1/CNT2 are CIA serial shift register lines.
The user port is commonly used for RS-232 communication (at TTL levels, not true
RS-232 voltage), parallel printer interfaces, and custom hardware. The RS-232
implementation is software-based (bit-banged via CIA #2), not hardware UART,
limiting reliable speeds to about 1200 baud (2400 baud with UP9600 software).

### Serial / IEC Port

| Detail           | Value                                       |
|------------------|---------------------------------------------|
| Connector        | 6-pin DIN 45322                             |
| Protocol         | Commodore serial IEC bus                    |

**Pinout:**

| Pin | Signal  | Function                               |
|-----|---------|----------------------------------------|
| 1   | /SRQ IN | Service Request In (active low; Fast Serial CLK on C128) |
| 2   | GND     | Ground                                 |
| 3   | ATN OUT | Attention (active low; device selection)              |
| 4   | CLK     | Serial clock (bidirectional)           |
| 5   | DATA    | Serial data (bidirectional)            |
| 6   | /RESET  | System reset (active low)              |

The serial bus is Commodore's proprietary adaptation of the parallel IEEE-488
(GPIB) bus, reduced to a serial implementation to save cost. All signals are
active-low, open-collector. Standard transfer rate is approximately 400 bytes/sec
with the 1541 disk drive. The protocol supports device numbers 4-30.

### Cassette Port

| Detail           | Value                                       |
|------------------|---------------------------------------------|
| Connector        | 12-pin (2x6) PCB edge connector             |
| Pitch            | 3.96 mm                                     |

**Pinout:**

| Pin | Signal  | Function                               |
|-----|---------|----------------------------------------|
| A/1 | GND     | Ground                                 |
| B/2 | +5V     | +5V DC power                           |
| C/3 | MOTOR   | Motor control (~6V supply)             |
| D/4 | READ    | Data input (connected to CIA #1 /FLAG) |
| E/5 | WRITE   | Data output (from 6510 port bit 3)     |
| F/6 | SENSE   | Key detect (0=key pressed)             |

The WRITE, SENSE, and MOTOR signals are directly connected to the 6510's on-chip
I/O port (bits 3, 4, and 5 of address $0001 respectively). The READ line connects
to CIA #1's FLAG pin. The Datasette records data as square wave pulses with timing
encoding; typical data rate is about 50 bytes/second (300 baud).

### Control Ports 1 and 2

| Detail           | Value                                       |
|------------------|---------------------------------------------|
| Connector        | DB-9 (DE-9) male                            |
| Location         | Right side of machine                       |

**Pinout (same for each port):**

| Pin | Signal   | Function                              |
|-----|----------|---------------------------------------|
| 1   | JOY0     | Up                                    |
| 2   | JOY1     | Down                                  |
| 3   | JOY2     | Left                                  |
| 4   | JOY3     | Right                                 |
| 5   | POT Y    | Paddle Y / Analog Y                  |
| 6   | FIRE     | Fire button / Light pen trigger       |
| 7   | +5V      | +5V DC (100mA max)                    |
| 8   | GND      | Ground                                |
| 9   | POT X    | Paddle X / Analog X                  |

Control port 1 is active by default in many games; however, its digital lines are
read via CIA #1 Port B (shared with keyboard column reads), while control
port 2 is read via CIA #1 Port A (shared with keyboard row output). This means
port 2 is slightly easier to read without keyboard ghosting issues.

The paddle inputs (POT X/Y) are read through the SID chip's A/D converters
(registers $D419-$D41C). A light pen connected to control port 1 triggers via the
LP pin on the VIC-II.

### A/V Output

| Detail           | Value                                       |
|------------------|---------------------------------------------|
| Connector        | 8-pin DIN (262-degree horseshoe)            |
| Note             | Early 326298 boards: 5-pin DIN              |

**Pinout (8-pin DIN):**

| Pin | Signal         | Function                       |
|-----|----------------|--------------------------------|
| 1   | Luminance      | Y signal (S-Video luma)        |
| 2   | GND            | Ground                         |
| 3   | Audio Out      | Mono audio output              |
| 4   | Composite Video| Combined video signal          |
| 5   | Audio In       | Audio input (mixed into SID)   |
| 6   | Chrominance    | C signal (S-Video chroma)      |
| 7   | +5V            | +5V DC (active on some boards) |
| 8   | --             | Not connected                  |

The 8-pin 262-degree horseshoe DIN connector is NOT compatible with the more common
270-degree DIN connectors. The chrominance signal output is approximately 1V
peak-to-peak, which is higher than the 0.3Vpp expected by many displays; a 75-ohm
series resistor is typically needed for proper S-Video connection.

### RF Output

| Detail           | Value                                       |
|------------------|---------------------------------------------|
| Connector        | RCA jack                                    |
| Output           | Channel 3 or 4 (switchable on some models)  |
| Standard         | PAL / NTSC (matching system)                |

The RF modulator is a shielded metal box on the motherboard that takes luminance
and chrominance signals from the VIC-II, combines and modulates them onto an RF
carrier. It also serves as the video signal amplifier/buffer for the A/V DIN output --
even the composite and S-Video signals pass through the RF modulator's circuitry.
This is a significant source of video quality degradation, and modern replacement
boards that bypass the RF modulator and provide clean S-Video output are popular
modifications.

### Power Connector

| Detail           | Value                                       |
|------------------|---------------------------------------------|
| Connector        | 7-pin DIN (round, keyed)                    |
| Input            | +5V DC and 9V AC                            |

**Pinout:**

| Pin | Signal  |
|-----|---------|
| 1   | GND     |
| 2   | GND     |
| 3   | +5V DC  |
| 4   | +5V DC  |
| 5   | 9V AC   |
| 6   | 9V AC   |
| 7   | GND     |


---

## 6. PAL vs NTSC Differences

The Commodore 64 was manufactured in both PAL and NTSC versions. The differences
run deeper than just video encoding -- they affect clock speed, available CPU time,
screen dimensions, and software compatibility.

### Timing Comparison

| Parameter              | PAL (6569)       | NTSC (6567R8)    | NTSC (6567R56A)  |
|------------------------|------------------|------------------|------------------|
| Crystal frequency      | 17.734475 MHz    | 14.31818 MHz     | 14.31818 MHz     |
| Color subcarrier       | 4.433619 MHz     | 3.579545 MHz     | 3.579545 MHz     |
| Dot clock              | 7.881984 MHz     | 8.181816 MHz     | 8.181816 MHz     |
| System clock           | 985.248 kHz      | 1022.727 kHz     | 1022.727 kHz     |
| Cycle time             | 1.015 us         | 0.978 us         | 0.978 us         |
| Cycles per line        | 63               | 65               | 64               |
| Total raster lines     | 312              | 263              | 262              |
| Visible raster lines   | 284              | 235              | 234              |
| Visible pixels/line    | 403              | 418              | 411              |
| Cycles per frame       | 19656            | 17095            | 16768            |
| Frame rate             | 50.125 Hz        | 59.826 Hz        | 60.988 Hz        |
| CPU time per frame     | ~19.95 ms        | ~16.72 ms        | ~16.39 ms        |
| First VBlank line      | 300              | 13               | 13               |
| Last VBlank line       | 15               | 40               | 40               |

### Color Encoding

PAL systems encode color using the Phase Alternating Line system (4.43 MHz
subcarrier), while NTSC uses the National Television System Committee standard
(3.58 MHz subcarrier). PAL's phase alternation makes it more resistant to color
errors but slightly reduces color bandwidth. The 16-color palette is defined
identically in both systems, but the actual displayed hues differ perceptibly due
to the encoding differences and the VIC-II's color generation circuitry.

### VIC-II Chip Variants

| Variant    | Standard | Process  | Notes                              |
|------------|----------|----------|------------------------------------|
| 6567R56A   | NTSC     | NMOS     | Early NTSC, 64 cycles/line (rare)  |
| 6567R8     | NTSC     | NMOS     | Standard NTSC, 65 cycles/line      |
| 6569R1-R5  | PAL-B    | NMOS     | Standard PAL, 63 cycles/line       |
| 6572       | PAL-N    | NMOS     | Argentina/Uruguay                  |
| 6573       | PAL-M    | NMOS     | Brazil                             |
| 8562       | NTSC     | HMOS-II  | C64C NTSC                          |
| 8565       | PAL-B    | HMOS-II  | C64C PAL                           |

VIC-II chips are NOT interchangeable between PAL and NTSC systems. Beyond the
different timing parameters, the color clock generation circuitry is different,
and swapping chips will produce no usable video output.

### Software Compatibility Issues

- **Speed:** NTSC machines run about 3.8% faster per clock cycle and have about
  17% more CPU time per visible screen line (65 vs 63 cycles), but the higher
  frame rate (60 Hz vs 50 Hz) means 13% less total CPU time per frame.
- **Raster timing:** Cycle-exact effects (raster bars, FLD, FLI, DYCP) must be
  rewritten for the different number of cycles per line and lines per frame. Code
  that relies on specific raster positions will break.
- **Music:** SID music composed on PAL plays approximately 20% faster on NTSC
  (and vice versa) if the playback routine is tied to the frame interrupt, since
  the interrupt fires at 60 Hz instead of 50 Hz.
- **Screen area:** PAL has 49 more visible raster lines (284 vs 235), giving it a
  taller visible display. NTSC has a wider visible line (418 vs 403 pixels).
  Programs that use the border area or assume specific screen dimensions may
  display incorrectly.
- **Bad lines:** PAL has 23 free CPU cycles during a bad line (63 - 40 = 23);
  NTSC has 25 (65 - 40 = 25). Tight bad-line code written for PAL may have
  different timing characteristics on NTSC.


---

## 7. Hardcore Details

### Bus Timing: PHI1 and PHI2

The C64's bus timing is built around the two-phase clock generated by the VIC-II:

```
        PHI2 LOW (Phase 1)          PHI2 HIGH (Phase 2)
       |<--- ~508ns (PAL) --->|<--- ~508ns (PAL) --->|
       |                      |                      |
  PHI2 _______________________/^^^^^^^^^^^^^^^^^^^^^^^\___
       |                      |                      |
       |   VIC-II accesses    |   CPU accesses       |
       |   bus (character     |   bus (instruction   |
       |   data, sprites,     |   fetch, data        |
       |   bitmap, refresh)   |   read/write)        |
```

**Detailed timing within a CPU read cycle (PHI2 high phase):**

1. PHI2 rises. CPU begins driving the address bus.
2. Address valid approximately 60-75ns after PHI2 rising edge.
3. PLA decodes address and asserts chip select (~20-40ns decode time).
4. Selected memory/device places data on bus.
5. Data must be valid at least 10ns before PHI2 falling edge.
6. CPU latches data on PHI2 falling edge.
7. Total address-to-data window: approximately 370ns.

**CPU write cycle timing:**

1. PHI2 rises. CPU drives address bus.
2. R/W goes low approximately 40ns after PHI2 rising edge.
3. CPU drives data bus approximately 150-200ns after PHI2 rising edge.
4. Data is written on PHI2 falling edge.

**VIC-II access (PHI1, PHI2 low phase):**

During PHI1, the VIC-II drives the address bus directly. The CPU's address bus
outputs are tri-stated via the AEC signal. The VIC-II reads 8 bits from the data
bus (character patterns, bitmap data, sprite data) and simultaneously reads 4 bits
from Color RAM on its dedicated color data lines.

### AEC and BA Signals

The VIC-II and CPU share the bus using two critical control signals:

**AEC (Address Enable Control):**
- Generated by the VIC-II.
- When AEC is LOW, the CPU's address bus outputs are tri-stated (disconnected),
  and the VIC-II drives the address bus.
- When AEC is HIGH, the CPU drives the address bus normally.
- During normal operation, AEC follows the clock: LOW during PHI1, HIGH during PHI2.

**BA (Bus Available):**
- Generated by the VIC-II, active low.
- When BA is HIGH, the bus is available to the CPU during PHI2 (normal operation).
- When BA goes LOW, the VIC-II is signaling that it needs exclusive bus access
  during upcoming PHI2 phases (stealing CPU cycles).
- BA goes low 3 cycles BEFORE the VIC-II actually takes over PHI2 access. This
  3-cycle warning allows the 6510 to complete any pending write operations (a write
  instruction takes at most 3 successive write cycles).
- The 6510's RDY input is directly connected to BA. When RDY goes low, the CPU
  halts after completing the current write cycle (reads can be halted immediately).

**Bad line cycle stealing:**

During a "bad line" (every 8th raster line in the display area, when the VIC-II must
fetch 40 bytes of character pointers from the video matrix), the VIC-II steals
approximately 40-43 cycles:

```
  Normal line:   VIC|CPU|VIC|CPU|VIC|CPU|VIC|CPU|...  (63/65 CPU cycles)
                 PHI1 PHI2 PHI1 PHI2 ...

  Bad line:      VIC|CPU|VIC|CPU|VIC|VIC|VIC|VIC|...|VIC|CPU|VIC|CPU|...
                                  ^                    ^
                            BA goes low         BA goes high
                            3 cycles before     after 40 c-accesses
                            takeover
```

On a PAL bad line, only 23 CPU cycles remain (out of 63). On NTSC, 25 remain
(out of 65).

**Sprite DMA:**

Each enabled sprite costs 2 additional stolen cycles per raster line during the
sprite's vertical range. The VIC-II fetches sprite pointers (p-access) and 3 bytes
of sprite data (s-access) during these stolen cycles. With all 8 sprites enabled,
up to 16 additional cycles are stolen.

### PLA Logic

The PLA implements the following simplified decode logic (active-low outputs):

```
CASRAM:  Directly from CAS signal when address is in RAM range
         (asserted for most addresses unless ROM/IO is selected)

BASIC:   A15=1, A14=0, A13=1 ($A000-$BFFF)
         AND HIRAM=1, LORAM=1
         AND AEC=1 (CPU access only)
         AND (GAME=1 OR EXROM=1)

KERNAL:  A15=1, A14=1, A13=1 ($E000-$FFFF)
         AND HIRAM=1
         AND AEC=1
         AND (GAME=1 OR (GAME=0 AND EXROM=0))

CHAROM:  A15=1, A14=1, A13=0, A12=1 ($D000-$DFFF, CPU)
         AND HIRAM=1 (or LORAM=1), CHAREN=0, AEC=1
         OR VA14=0, VA13=0, VA12=1 (VIC banks 0,2: $1000-$1FFF)
         AND AEC=0

I/O:     A15=1, A14=1, A13=0, A12=1 ($D000-$DFFF)
         AND CHAREN=1 (or HIRAM=1/LORAM=1)
         AND AEC=1

ROML:    A15=1, A14=0, A13=0 ($8000-$9FFF)
         AND EXROM=0, AEC=1
         (exact logic depends on GAME state)

ROMH:    Logic depends on GAME/EXROM configuration
         Standard: $A000-$BFFF when GAME=1, EXROM=0
         Ultimax:  $E000-$FFFF when GAME=0, EXROM=1

GR/W:    Directly controlled by R/W and address decode for $D800-$DBFF
```

The PLA's 20 input lines and 8 output lines implement a sum-of-products logic array
with approximately 60 product terms. The original 82S100 is a fuse-programmed PLA;
the later 251715-01 and 252535-01 custom ICs implement equivalent logic in gate arrays.

### Power Supply Design

The C64 uses an external power supply (the "power brick") providing two voltage rails:

| Rail    | Specification      | Current  | On-board usage                     |
|---------|--------------------|----------|------------------------------------|
| +5V DC  | 4.95-5.10V nominal | 1.5-1.7A | All digital logic, RAM, CPU, ROMs  |
| 9V AC   | ~9V RMS            | 1.0-1.1A | SID VDD (rectified to 12V or 9V), TOD clock, cassette motor |

**9V AC usage on the motherboard:**

1. **SID supply:** Rectified and regulated to +12V (6581 boards) or +9V (8580 boards)
   for the SID's VDD pin. This higher voltage is needed for the SID's analog filter
   circuitry.
2. **TOD clock:** A frequency divider circuit (U27) derives a 50 or 60 Hz square wave
   from the 9VAC mains-derived signal to drive the CIA TOD clock inputs.
3. **Cassette motor:** The 9VAC supplies approximately 6V (through switching) to the
   Datasette motor via the cassette port.

**Known failure modes ("Brick of Death"):**

The original linear-regulated power supply is infamous for a catastrophic failure
mode: the internal 5V regulator can fail short-circuit, passing unregulated voltage
(7-9V DC or more) directly to the motherboard. This destroys RAM chips first, then
potentially the CPU, CIAs, VIC-II, SID, and PLA. The failure is accelerated by:

- Encapsulation in thermally-insulating potting compound
- Heat buildup from the linear regulator under continuous load
- Age-related capacitor degradation

**Voltage danger thresholds:**

| Voltage  | Risk Level                                    |
|----------|-----------------------------------------------|
| 5.0-5.1V | Normal operating range                        |
| 5.1-5.2V | Marginal; monitor regularly                   |
| 5.2-5.5V | Failing; replace immediately                  |
| 5.5-6.0V | Active damage; chips degrading                |
| >6.0V    | Critical; rapid chip destruction               |

Modern replacement power supplies use switching regulators with overvoltage
protection and are strongly recommended for any C64 in regular use.

### RF / Video Output Circuit

The video signal path from VIC-II to output:

1. **VIC-II** generates separate luminance (Y) and chrominance (C) signals, plus a
   composite sync signal.
2. These signals are routed to the **RF modulator** box on the motherboard.
3. Inside the RF modulator:
   - Y and C are buffered/amplified
   - Composite video is created by combining Y + C
   - RF output is generated by modulating the composite signal onto a TV channel
     carrier (Ch. 3 or 4)
4. The **A/V DIN connector** provides buffered luminance, chrominance, and composite
   video signals that have also passed through the RF modulator's circuitry.

**Video quality issues:**

The VIC-II generates significant "clock noise" at the dot-clock frequency (~8 MHz),
which appears as vertical banding ("jail bars") on the display. The RF modulator's
combining of luminance and chrominance for composite output causes color bleeding.
Both artifacts are visible on composite connections; S-Video (separate Y/C) output
eliminates the color bleeding but not the banding.

Modern modifications include RF modulator replacement boards that provide clean
buffered S-Video output with reduced noise coupling, and the "LumaFix" board that
reduces VIC-II clock noise.

### Color RAM Wiring

Color RAM occupies a unique position in the C64's architecture:

- **Physical chip:** 2114 SRAM (1024 x 4-bit) at position U6 on most boards.
- **CPU address:** $D800-$DBFF (1024 bytes, only low nibble valid).
- **VIC-II connection:** Directly wired to VIC-II data lines D8-D11 (the upper
  4 bits of the VIC-II's 12-bit data bus).
- **Bus coupling:** An LS245 buffer (U16) couples the Color RAM's 4 data lines to
  the CPU's data bus (D0-D3) during CPU access. During VIC-II access, the buffer
  is disabled and the Color RAM talks directly to the VIC-II.
- **Always visible:** Color RAM is accessible regardless of bank switching
  configuration. It does not go through the PLA's normal chip select logic in the
  same way as ROM and I/O.
- **VIC-II reads:** During every c-access (character pointer fetch), the VIC-II
  simultaneously reads 8 bits of character data from DRAM and 4 bits of color data
  from Color RAM, forming a 12-bit word. This is how each character cell gets both
  its character code and its color in a single bus cycle.

### Clock Generation Circuit

The clock generation circuit evolved across board revisions:

**Early boards (326298):** Discrete circuit using an LM556 dual timer and 74LS629
voltage-controlled oscillator to create a PLL that locks the dot clock to the color
clock crystal.

**Later boards (250407+):** Replaced by the MOS 7701/8701 custom clock generator IC.

**8701 Clock Generator:**

| Pin | Signal       | Function                           |
|-----|--------------|------------------------------------|
| 1   | COLOR IN     | Color clock from crystal (4x subcarrier) |
| 2   | COLOR OUT    | Buffered color clock to VIC-II     |
| 3   | DOT OUT      | Dot clock output to VIC-II         |
| 4   | GND          | Ground                             |
| 5   | PHI0 IN      | System clock from VIC-II           |
| 6   | PHI0 OUT     | Buffered system clock to CPU       |
| 7   | +5V          | Power                              |
| 8   | Crystal      | Crystal oscillator connection      |

The 8701 maintains a fixed phase relationship between the color clock and dot clock
using an internal PLL. This phase lock is essential for proper color encoding in the
video output.

**Clock derivation chain:**

```
  Crystal (Y1)                  8701 PLL              VIC-II
  17.734 MHz (PAL)  ------>  Dot Clock  -------->  PHI0 = DOT/8
  14.318 MHz (NTSC)           7.882 MHz (PAL)       0.985 MHz (PAL)
                              8.182 MHz (NTSC)      1.023 MHz (NTSC)
                                  |
                              Color Clock
                              4.434 MHz (PAL)  ----> VIC-II Color
                              3.580 MHz (NTSC)       Encoding
```

### Known Hardware Bugs and Quirks

**VIC-II bugs:**

- **Sparkle bug (early NMOS revisions):** Light-colored pixels appear randomly on
  dark backgrounds when the chip heats up. Fixed in later silicon revisions.
- **Grey dot sparkle (later revisions):** Grey ($0F) dots appear when registers are
  changed mid-raster line. Specific to "new" VIC-II revisions.
- **$D011 crash:** Writing certain values to the control register $D011 at specific
  raster positions can crash the VIC-II, requiring a power cycle. Affects all
  known revisions. The exact cause is not fully understood.
- **6567R56A timing:** The earliest NTSC VIC-II has 64 cycles per line instead of
  65, and 262 lines instead of 263. Software written for the R8 (65 cycles) may
  malfunction on R56A machines. This revision was quickly replaced.
- **Lightpen timing:** The raster position latched by lightpen input varies by one
  pixel between VIC-II revisions.
- **Sprite/bitmap color glitch:** On lines with heavy sprite DMA in hires bitmap
  mode, incorrect color fetches can produce wrong colors and flickering hundreds
  of raster lines after bank switches and $D016 writes.
- **Overheating:** Plastic-packaged VIC-IIs are prone to overheating due to the
  high-speed (8 MHz) die in a thermally-limited package. Commodore used the RF
  shield as a heatsink, but failures from heat are a common repair issue.

**SID bugs:**

- **Volume register click (6581):** Any write to the volume register ($D418)
  produces an audible click. This was exploited for 4-bit digital sample playback
  and became a beloved "feature." The 8580 fixed this, to the dismay of the demo
  scene.
- **ADSR bug (all versions):** The ADSR envelope generator has a known timing
  glitch where the envelope can skip steps or jump to unexpected values during
  fast attacks. This is caused by the internal rate counter comparison logic
  and affects both 6581 and 8580.
- **Combined waveform differences:** Selecting multiple waveform outputs
  simultaneously (e.g., triangle + sawtooth) produces different results on 6581
  vs 8580 due to different internal pull-up/pull-down characteristics. Some
  music relies on the specific 6581 behavior.
- **Filter inconsistency (6581):** The 6581's analog filters vary significantly
  between individual chips due to manufacturing tolerances, making the exact
  filter sound non-reproducible between machines.

**CIA bugs:**

- **TOD clock accuracy:** The TOD clock depends on a stable 50/60 Hz input from
  the mains-derived 9VAC line. On a different mains frequency (e.g., PAL machine
  on 60 Hz power), the TOD clock runs at the wrong speed.
- **Timer B one-shot bug (6526):** In one-shot mode counting Timer A underflows,
  Timer B can start counting one cycle too early. This was fixed in the 6526A.

**PLA failure:**

The original 82S100 PLA is one of the most failure-prone chips in the C64. Symptoms
of PLA failure include garbled screen, wrong colors, crashes on startup, and
inability to load software. The failure rate is attributed to thermal stress and
the relatively complex fuse-link structure of the 82S100. The 251715-01 custom IC
used in later boards is significantly more reliable.


---

## References

### Primary Technical Documents

- [The MOS 6567/6569 Video Controller (VIC-II) and Its Application in the Commodore 64](https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt) -- Christian Bauer's definitive VIC-II reference
- [The C64 PLA Dissected](https://skoe.de/docs/c64-dissected/pla/c64_pla_dissected_a4ds.pdf) -- Thomas 'skoe' Giesel's analysis of the PLA
- [C64 Service Manual (314001-02)](https://portcommodore.com/rcarlsen/cbm/c64/C64_SERVICE_MANUAL.pdf) -- Commodore's official service manual

### C64-Wiki References

- [Hardware Internals of the C64](https://www.c64-wiki.com/wiki/Hardware_internals_of_the_C64)
- [Motherboard Revisions](https://www.c64-wiki.com/wiki/Motherboard)
- [PLA (C64 Chip)](https://www.c64-wiki.com/wiki/PLA_(C64_chip))
- [Bank Switching](https://www.c64-wiki.com/wiki/Bank_Switching)
- [SID](https://www.c64-wiki.com/wiki/SID)
- [CIA](https://www.c64-wiki.com/wiki/CIA)
- [Expansion Port](https://www.c64-wiki.com/wiki/Expansion_Port)
- [User Port](https://www.c64-wiki.com/wiki/User_Port)
- [Serial Port](https://www.c64-wiki.com/wiki/Serial_Port)
- [Cassette Port](https://www.c64-wiki.com/wiki/Cassette_Port)
- [Control Port](https://www.c64-wiki.com/wiki/Control_Port)
- [A/V Jack](https://www.c64-wiki.com/wiki/A/V_Jack)
- [Power Supply](https://www.c64-wiki.com/wiki/Power_Supply)
- [Color RAM](https://www.c64-wiki.com/wiki/Color_RAM)
- [RAM](https://www.c64-wiki.com/wiki/RAM)
- [Raster Time](https://www.c64-wiki.com/wiki/raster_time)

### Other References

- [Dustlayer: Hardware Basics Part 1 -- Tick Tock, Know Your Clock](https://dustlayer.com/c64-architecture/2013/5/7/hardware-basics-part-1-tick-tock-know-your-clock)
- [Dustlayer: Hardware Basics Part 2 -- A Complicated Relationship](https://dustlayer.com/c64-architecture/2013/5/7/hardware-basics-part-2-a-complicated-relationship)
- [Commodore 64 Motherboard Revisions -- The Silicon Underground](https://dfarq.homeip.net/commodore-64-motherboard-revisions/)
- [breadbox64.com: C64 Hardware](https://breadbox64.com/c64-hardware/)
- [C64 Schematics Archive](http://www.zimmers.net/anonftp/pub/cbm/schematics/computers/c64/index.html)
- [Creating the Commodore 64: The Engineers' Story -- IEEE Spectrum](https://spectrum.ieee.org/commodore-64)
- [Commodore 64 -- Wikipedia](https://en.wikipedia.org/wiki/Commodore_64)
- [MOS Technology VIC-II -- Wikipedia](https://en.wikipedia.org/wiki/MOS_Technology_VIC-II)
- [MOS Technology 6581 -- Wikipedia](https://en.wikipedia.org/wiki/MOS_Technology_6581)
- [MOS Technology CIA -- Wikipedia](https://en.wikipedia.org/wiki/MOS_Technology_CIA)
- [Scope on the C64 -- Oscilloscope Measurements](http://tech.guitarsite.de/c64_scope.html)
- [C64 Clock Generation Discussion -- 6502.org Forum](http://forum.6502.org/viewtopic.php?f=4&t=4967)
- [CSG 8701 Replacement -- Individual Computers Wiki](https://wiki.icomp.de/wiki/CSG8701-replacement)
- [The 6510 Processor Port -- C64 OS](https://www.c64os.com/post/6510procport)
- [Connectors Commodore 64 -- My Old Computer](https://myoldcomputer.nl/technical-info/datasheets/connector-pinouts/connectors-commodore-64/)
