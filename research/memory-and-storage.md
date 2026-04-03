# Memory Management, Bank Switching, Disk Storage, and Related Tricks

A comprehensive reference covering how the Commodore 64 manages its 64KB address space,
communicates with the 1541 disk drive, loads data at speed, compresses programs, and
transcends its apparent memory limits through cartridges, REUs, and multi-load techniques.

---

## Table of Contents

1. [Overview](#1-overview)
2. [RAM/ROM Banking](#2-ramrom-banking)
3. [VIC-II Memory Banking](#3-vic-ii-memory-banking)
4. [The 1541 Disk Drive](#4-the-1541-disk-drive)
5. [Fast Loaders](#5-fast-loaders)
6. [Compression Techniques](#6-compression-techniques)
7. [Cartridge Memory](#7-cartridge-memory)
8. [Programs Larger Than Memory](#8-programs-larger-than-memory)
9. [Hardcore Details](#9-hardcore-details)
10. [References](#10-references)

---

## 1. Overview

### The 64KB Challenge

The Commodore 64 has a 16-bit address bus providing exactly 65,536 addressable locations,
each 8 bits wide. That is 64 kilobytes -- the entire world the CPU can see. Yet the machine
shipped with 64KB of RAM, 20KB of ROM (8KB KERNAL + 8KB BASIC + 4KB character generator),
and a block of I/O registers, all competing for the same address space.

The result is a system where the CPU can never see all of its own RAM at once. At power-on
in the default configuration, only about 39KB of RAM is directly visible to the CPU. The
rest is hidden behind ROM and I/O overlays.

### Why This Matters

Every ambitious C64 program -- whether a game, a demo, or a productivity application --
must grapple with this constraint:

- **Games** need space for graphics, music, level data, and code, often totaling far more
  than 64KB. They must load from disk in stages, compress assets, and carefully plan memory
  layouts.
- **Demos** push the hardware to extremes, needing to swap effects in and out of memory
  while maintaining unbroken music playback and visual continuity.
- **Productivity software** like GEOS uses overlays, swapping code segments on demand from
  disk, effectively implementing a form of virtual memory on a 1MHz 8-bit computer.

The techniques described in this document are how C64 programmers conquered the 64KB wall.

### The Memory Landscape at a Glance

```
$0000-$9FFF   RAM (40KB, always visible to CPU for writes)
$A000-$BFFF   BASIC ROM / RAM (8KB, switchable)
$C000-$CFFF   RAM (4KB, always visible)
$D000-$DFFF   I/O / Char ROM / RAM (4KB, switchable)
$E000-$FFFF   KERNAL ROM / RAM (8KB, switchable)
```

The VIC-II video chip has its own view of memory (always seeing RAM and character ROM, never
KERNAL or BASIC ROM). The SID and CIA chips occupy the I/O block. The 1541 disk drive is an
entirely separate computer with its own 6502 CPU and 2KB of RAM. Understanding how all these
pieces interact is the key to mastering the C64.

---

## 2. RAM/ROM Banking

### The Processor Port ($0000-$0001)

The 6510 CPU (a variant of the 6502) has a built-in 8-bit I/O port at addresses $0000 and
$0001. This is the primary mechanism for controlling the C64's memory map.

**$0000 -- Data Direction Register (DDR)**

Each bit controls whether the corresponding bit of the data register ($0001) is an input
(0) or output (1). The default value is $2F (bits 0-3 and 5 are outputs).

```
Bit 0: LORAM direction    (1 = output)
Bit 1: HIRAM direction    (1 = output)
Bit 2: CHAREN direction   (1 = output)
Bit 3: Cassette write     (1 = output)
Bit 4: Cassette sense     (0 = input)
Bit 5: Cassette motor     (1 = output)
Bits 6-7: Not connected
```

**$0001 -- Data Register**

The three least significant bits are the bank-switching control lines:

```
Bit 0: LORAM   - Controls BASIC ROM ($A000-$BFFF)
                  1 = BASIC ROM visible, 0 = RAM visible
Bit 1: HIRAM   - Controls KERNAL ROM ($E000-$FFFF)
                  1 = KERNAL ROM visible, 0 = RAM visible
Bit 2: CHAREN  - Controls character ROM vs I/O ($D000-$DFFF)
                  1 = I/O visible, 0 = Character ROM visible
                  (Only effective when HIRAM or LORAM is set)
```

The default value at power-on is $37 (all three control bits set to 1).

### Dependency Hierarchy

The banking bits have a dependency hierarchy enforced by the PLA (Programmable Logic Array):

1. If HIRAM=0 and LORAM=0, the I/O and character ROM areas also become RAM, regardless
   of the CHAREN bit. This gives full 64KB RAM access.
2. BASIC ROM requires KERNAL ROM -- setting LORAM=1 with HIRAM=0 does NOT show BASIC;
   you get RAM at $A000-$BFFF instead.
3. The CHAREN bit only has effect when at least one of HIRAM or LORAM is set to 1.

### The Eight Standard Banking Configurations

With no cartridge connected (GAME=1, EXROM=1 at the expansion port), the three CPU port
bits produce these eight configurations:

```
Mode  LORAM HIRAM CHAREN  $A000-$BFFF  $D000-$DFFF  $E000-$FFFF  $0001 Value
-----+------+-----+-------+------------+------------+------------+-----------
 31     1     1      1     BASIC ROM    I/O          KERNAL ROM     $37 (default)
 30     0     1      1     RAM          I/O          KERNAL ROM     $36
 29     1     0      1     RAM          I/O          RAM            $35
 28     0     0      1     RAM          RAM          RAM            $34
 27     1     1      0     BASIC ROM    CHAR ROM     KERNAL ROM     $33
 26     0     1      0     RAM          CHAR ROM     KERNAL ROM     $32
 25     1     0      0     RAM          CHAR ROM     RAM            $31
 24     0     0      0     RAM          RAM          RAM            $30
```

**Important notes:**
- Modes 28 and 24 both give all-RAM, since CHAREN has no effect when HIRAM and LORAM are
  both 0.
- In modes 29 and 25, BASIC ROM disappears even though LORAM=1, because HIRAM=0 removes
  the dependency that BASIC needs KERNAL.
- The $0001 values above assume bits 3-5 remain at their default states ($x7).

### Practical Banking Configurations

**Mode $37 (default) -- Normal BASIC operation**
```
$0000-$9FFF: RAM
$A000-$BFFF: BASIC ROM
$C000-$CFFF: RAM
$D000-$DFFF: I/O (SID, VIC-II, CIAs, Color RAM)
$E000-$FFFF: KERNAL ROM
```

**Mode $36 -- BASIC banked out, KERNAL and I/O remain**
```
$0000-$CFFF: RAM (48KB continuous!)
$D000-$DFFF: I/O
$E000-$FFFF: KERNAL ROM
```
This is the most common configuration for machine language programs. You get 48KB of
continuous RAM from $0000 to $CFFF plus the 4KB at $D000-$DFFF is available as RAM for
writes (which go through to RAM beneath I/O). The KERNAL remains available for IRQ
handling and I/O routines.

**Mode $35 -- I/O only, no ROM**
```
$0000-$CFFF: RAM
$D000-$DFFF: I/O
$E000-$FFFF: RAM
```
Used when you need the RAM under KERNAL but still want I/O access. You must provide your
own interrupt handler since the KERNAL vectors point into RAM, not ROM.

**Mode $34 -- All 64KB RAM**
```
$0000-$FFFF: RAM (full 64KB!)
```
Maximum RAM mode. No ROM, no I/O. You cannot read the SID, VIC-II, or CIAs. You cannot
use the KERNAL. Interrupts must be disabled before switching here, or you must have set
up valid interrupt vectors in RAM first. Writes to $D000-$DFFF still reach the I/O
hardware (you can blind-write to SID, for example), but reads return RAM contents.

**Mode $33 -- Character ROM visible**
```
$D000-$DFFF: Character generator ROM (4KB)
```
Used to copy the character ROM into RAM for modification. Typically done briefly:
```asm
        SEI                 ; Disable interrupts (no I/O access!)
        LDA #$33
        STA $01             ; Bank in character ROM
        ; ... copy $D000-$DFFF to RAM elsewhere ...
        LDA #$37
        STA $01             ; Restore default configuration
        CLI                 ; Re-enable interrupts
```

### Writing RAM Under ROM

A critical property of the C64's memory architecture: **writes always go to RAM**, even
when ROM is mapped at the read address. This means:

- You can store data in the RAM beneath BASIC ROM ($A000-$BFFF) by writing to those
  addresses while BASIC ROM is visible. The writes pass through to RAM.
- To read that data back, you must bank out the ROM first.
- This effectively gives you "hidden" storage under any ROM area.

Example -- storing data under KERNAL ROM:
```asm
        ; Write to RAM under KERNAL (ROM is visible, but writes hit RAM)
        LDA #$42
        STA $E000           ; Written to RAM, reads would return KERNAL ROM byte

        ; To read it back:
        SEI
        LDA #$35            ; Bank out KERNAL, keep I/O
        STA $01
        LDA $E000           ; Now reads the $42 we stored
        LDX #$37
        STX $01             ; Restore default banking
        CLI
```

### The PLA and Expansion Port Lines

The full memory configuration is determined by five signals fed into the PLA:

1. **LORAM** (CPU port bit 0)
2. **HIRAM** (CPU port bit 1)
3. **CHAREN** (CPU port bit 2)
4. **GAME** (expansion port pin 8, active-low)
5. **EXROM** (expansion port pin 9, active-low)

GAME and EXROM are active-low signals at the expansion port, normally pulled high internally.
Cartridges ground these lines to activate cartridge ROM mapping. With 5 signals there are 32 possible
combinations, but due to redundancies only 14 distinct memory configurations actually exist.

The full PLA truth table (key cartridge modes):

```
EXROM GAME  Effect
  1     1   No cartridge (standard 8 modes above)
  0     1   8K cartridge: ROML at $8000-$9FFF
  0     0   16K cartridge: ROML at $8000-$9FFF, ROMH at $A000-$BFFF
  1     0   Ultimax mode: ROML at $8000-$9FFF, ROMH at $E000-$FFFF, only 4KB RAM
```

### Using Full 64KB -- Practical Technique

To use the entire 64KB for a machine language program:

1. Set up a valid NMI and IRQ handler in RAM (or disable interrupts).
2. Copy the interrupt vectors (normally at $FFFA-$FFFF in KERNAL ROM) to the same RAM
   locations. The CPU reads these from whatever is mapped, so in all-RAM mode they must be
   in RAM.
3. Switch to mode $34 or $35 as needed.
4. Avoid calling any KERNAL routines (they are not mapped in).

```asm
        SEI                 ; Disable interrupts
        LDA #$7F
        STA $DC0D           ; Disable CIA1 interrupts
        STA $DD0D           ; Disable CIA2 interrupts
        LDA $DC0D           ; Acknowledge pending
        LDA $DD0D

        ; Set up RAM vectors at $FFFE/$FFFF (IRQ) etc. before switching
        LDA #<my_irq
        STA $FFFE
        LDA #>my_irq
        STA $FFFF

        LDA #$35            ; I/O visible, all ROM banked out
        STA $01
        CLI                 ; Now IRQs use the RAM vector
```

---

## 3. VIC-II Memory Banking

### The 16KB Limitation

The VIC-II video chip has only 14 address lines, so it can address at most 16KB (16,384
bytes) of memory at a time. The 64KB address space is divided into four 16KB "VIC banks,"
and only one bank is active at a time.

### CIA2 Port A ($DD00)

The two least significant bits of CIA2's Port A register select which 16KB bank the VIC-II
sees. Note the **inverse** relationship -- lower bit values select higher addresses:

```
$DD00 Bits 1-0   VIC Bank   Address Range      Character ROM Shadow
      %xxxxxx11     0       $0000-$3FFF        Yes ($1000-$1FFF)
      %xxxxxx10     1       $4000-$7FFF        No
      %xxxxxx01     2       $8000-$BFFF        Yes ($9000-$9FFF)
      %xxxxxx00     3       $C000-$FFFF        No
```

The inversion exists because the address lines pass through inverters in the hardware.

**Setting the VIC bank (preserve upper bits):**
```asm
        LDA $DD00
        AND #$FC            ; Clear bits 0-1
        ORA #$02            ; Select bank 1 ($4000-$7FFF)
        STA $DD00
```

Or in BASIC:
```basic
POKE 56578, PEEK(56578) OR 3       : REM Set bits 0-1 as outputs
POKE 56576, (PEEK(56576) AND 252) OR 2  : REM Select bank 1
```

**$DD02 -- Data Direction Register for Port A:**
The two low bits must be set as outputs (1) for bank selection to work. The default value
is $03, which already configures them correctly. However, some programs may have changed
this, so it is good practice to verify:

```asm
        LDA $DD02
        ORA #$03            ; Ensure bits 0-1 are outputs
        STA $DD02
```

### Character ROM Shadows

In VIC banks 0 and 2, the character generator ROM is automatically "shadowed" into the
bank. The VIC-II chip sees the character ROM at these offsets regardless of what the CPU
has stored in RAM at those addresses:

```
Bank 0: Character ROM at $1000-$1FFF (VIC sees ROM, CPU sees RAM)
Bank 2: Character ROM at $9000-$9FFF (VIC sees ROM, CPU sees RAM)
```

This means:
- In banks 0 and 2, you get the built-in character sets "for free" without copying them.
- But you lose 4KB of usable VIC memory at those locations (the VIC cannot see your RAM
  data there).
- In banks 1 and 3, there is no character ROM shadow. You must copy the character set
  into RAM if you want to use it, but you gain the full 16KB for VIC data.

### Resource Counts by Bank Type

```
                              Banks 0 & 2    Banks 1 & 3
                              (ROM shadow)   (no shadow)
Available for sprites:          192            256
Available text screens:          12             16
Available character sets:         6              8
Bitmap + screen RAM:           Yes            Yes
```

In banks 0 and 2, one high-resolution bitmap screen has a visible "scar" of character
ROM data in the lower portion, because the VIC reads character ROM instead of your bitmap
data at the shadowed addresses.

### Memory Layout Within a VIC Bank

Within the selected 16KB bank, VIC-II registers control where specific resources are
located:

**$D018 -- VIC Memory Control Register:**
```
Bits 7-4: Screen memory offset (×1024 within bank)
           %0000 = +$0000, %0001 = +$0400, ... %1111 = +$3C00
Bits 3-1: Character memory / bitmap data offset (×2048 within bank)
           %000 = +$0000, %001 = +$0800, ... %111 = +$3800
Bit 0:    Unused
```

Example -- screen at $0400, characters at $1000 (default setup in bank 0):
```asm
        LDA #$14            ; %0001 0100 -> screen at +$0400, chars at +$1000
        STA $D018
```

### Practical VIC Bank Strategies

**Strategy 1: Use bank 0 (default) with ROM character set**
Simplest setup. Screen at $0400, character ROM at $1000. Good for text-based programs.
Drawback: zero page, stack, and BASIC work area are in this bank, reducing usable space.

**Strategy 2: Move to bank 1 ($4000-$7FFF) or bank 3 ($C000-$FFFF)**
No character ROM shadow means full 16KB is available. You must copy any needed character
data into this bank. Bank 3 ($C000-$FFFF) keeps VIC data separate from program code in
lower memory.

**Strategy 3: Bank 2 ($8000-$BFFF) for mixed use**
Character ROM available at $9000. Useful for programs that need the ROM font but want
to keep zero page and stack clear of VIC conflicts.

### VIC-II vs CPU Memory Views

A critical distinction: the VIC-II and CPU see different things at the same addresses:

```
Address Range     CPU Reads          VIC-II Reads
$1000-$1FFF      RAM                Character ROM (in banks 0/2)
$9000-$9FFF      RAM                Character ROM (in banks 0/2)
$D000-$DFFF      I/O or ROM or RAM  RAM (always)
$A000-$BFFF      BASIC ROM or RAM   RAM (always)
$E000-$FFFF      KERNAL ROM or RAM  RAM (always)
```

The VIC-II always sees RAM (except for the character ROM shadows). It never sees BASIC ROM,
KERNAL ROM, or I/O registers. This means:
- If you want the VIC to display data stored under KERNAL ROM, just write it there. The
  VIC will see the RAM.
- The VIC can read your bitmap data at $E000 while the CPU reads KERNAL ROM at the same
  address.

---

## 4. The 1541 Disk Drive

### A Computer Within a Computer

The Commodore 1541 disk drive is not a dumb peripheral. It is an independent computer
containing:

- **MOS 6502 CPU** running at 1 MHz (same family as the C64's 6510, but without the I/O
  port)
- **2KB of RAM** ($0000-$07FF)
- **16KB of ROM** ($C000-$FFFF) containing the Commodore DOS (CBM DOS 2.6)
- **Two VIA 6522 chips** for serial bus communication and drive motor/head control
- A single-sided, double-density 5.25" floppy mechanism

The drive has its own operating system, processes commands independently, and communicates
with the C64 over the slow serial bus. This independence is the foundation of both copy
protection schemes and fast loader techniques.

### Physical Disk Specifications

```
Disk diameter:       5.25 inches
Sides used:          1 (single-sided)
Rotation speed:      300 RPM (5 revolutions per second)
Standard tracks:     35 (numbered 1-35)
Physical positions:  Up to 80+ half-track positions (40+ full tracks)
Recording method:    Group Code Recording (GCR)
```

### Track and Sector Layout

The 1541 uses zone bit recording (ZBR) with four speed zones. Outer tracks spin past the
head faster and can hold more data:

```
Zone   Tracks    Sectors/Track   Bit Rate     Bytes/Track
  3     1-17         21          307.692 kbps    ~7820
  2    18-24         19          285.714 kbps    ~7170
  1    25-30         18          266.667 kbps    ~6820
  0    31-35         17          250.000 kbps    ~6440
```

**Total formatted capacity:**

```
Zone 3:  17 tracks x 21 sectors x 256 bytes =  91,392 bytes
Zone 2:   7 tracks x 19 sectors x 256 bytes =  34,048 bytes
Zone 1:   6 tracks x 18 sectors x 256 bytes =  27,648 bytes
Zone 0:   5 tracks x 17 sectors x 256 bytes =  21,760 bytes
                                     Total  = 174,848 bytes (683 sectors)

Track 18 is reserved for directory and BAM = -19 sectors
Usable storage:  664 sectors x 254 bytes   = ~168,656 bytes (~165KB)
```

Each sector holds 256 bytes. In data files, the first two bytes of each sector are the
track/sector link to the next sector in the chain (or $00/count for the last sector),
leaving 254 usable bytes per sector.

### Sector Interleave

The default file interleave is 10 -- after reading sector N, the next sector in the chain
is N+10 (mod sectors-per-track). This gives the drive time to process the just-read sector
before the next one rotates under the head. The directory interleave is 3.

### GCR Encoding

The 1541 stores data using Group Code Recording, which converts each 4-bit nybble into a
5-bit pattern. This 4-to-5 expansion ensures sufficient magnetic flux transitions for
reliable clocking:

```
Nybble  Binary   GCR       Nybble  Binary   GCR
  $0    0000    01010        $8    1000    01001
  $1    0001    01011        $9    1001    11001
  $2    0010    10010        $A    1010    11010
  $3    0011    10011        $B    1011    11011
  $4    0100    01110        $C    1100    01101
  $5    0101    01111        $D    1101    11101
  $6    0110    10110        $E    1110    11110
  $7    0111    10111        $F    1111    10101
```

**Key properties of GCR encoding:**
- No more than 2 consecutive zero-bits in any valid GCR sequence (ensures clock recovery)
- No more than 8 consecutive one-bits in valid data (allows sync marks to be distinguished)
- Each 256-byte sector becomes 320 bytes after GCR encoding (256 x 10/8 = 320)
- The expansion ratio is exactly 5:4

### Sector Format on Disk

Each sector on disk consists of two blocks separated by gaps:

```
[SYNC] [Header Block] [Header Gap] [SYNC] [Data Block] [Inter-Sector Gap]
```

**Sync mark:** 5 bytes of $FF (40 one-bits). Because valid GCR data never produces more
than 8 consecutive one-bits, 40 one-bits are unmistakable as a sync marker.

**Header block (10 GCR bytes = 8 data bytes):**
```
Byte 0:    $08 (header block ID)
Byte 1:    Header checksum (XOR of bytes 2-5)
Byte 2:    Sector number
Byte 3:    Track number
Byte 4:    Disk ID byte 2
Byte 5:    Disk ID byte 1
Bytes 6-7: $0F, $0F (padding)
```

**Header gap:** 9 bytes of $55 (standard format).

**Data block (325 GCR bytes = 260 data bytes):**
```
Byte 0:      $07 (data block ID)
Bytes 1-256: 256 bytes of sector data
Byte 257:    Data checksum (XOR of all 256 data bytes)
Bytes 258-259: $00, $00 (padding, filled with zeroes)
```

**Inter-sector gap:** Variable length, filled with $55. The gap size depends on the speed
zone and accounts for motor speed variations.

### Block Availability Map (BAM)

The BAM resides at track 18, sector 0 and tracks which sectors are free or allocated:

```
Bytes $00-$01: Track/sector of first directory sector (18/1)
Byte  $02:     DOS version ($41 = 'A')
Byte  $03:     Unused ($00)
Bytes $04-$8F: BAM entries for tracks 1-35 (4 bytes each)
Bytes $90-$9F: Disk name (16 characters, padded with $A0)
Bytes $A0-$A1: $A0, $A0
Bytes $A2-$A3: Disk ID (2 characters)
Byte  $A4:     $A0
Bytes $A5-$A6: DOS type ("2A")
Bytes $A7-$AA: $A0 padding
```

Each BAM entry (4 bytes per track):
```
Byte 0:   Number of free sectors on this track
Bytes 1-3: Bitmap (1 = free, 0 = allocated), bit 0 of byte 1 = sector 0
```

### Directory Structure

The directory begins at track 18, sector 1 and chains through additional sectors on
track 18 using 3-sector interleave. Each directory sector holds 8 file entries of 32
bytes each:

```
Offset  Description
$00-$01 Track/sector link to next directory sector ($00/$FF for last)
$02     File type and flags:
          Bits 0-3: File type (0=DEL, 1=SEQ, 2=PRG, 3=USR, 4=REL)
          Bit 5:    @-replacement flag
          Bit 6:    Locked flag (>)
          Bit 7:    Closed flag (must be set for valid file)
$03-$04 Track/sector of first data sector
$05-$14 Filename (16 characters, padded with $A0)
$15-$16 Track/sector of first side-sector block (REL files only)
$17     REL file record length
$1C-$1D File size in sectors (low/high byte)
```

---

## 5. Fast Loaders

### Why Standard Loading is Slow

The C64's standard serial bus protocol transfers data at approximately 300-400 bytes per
second with a 1541 drive. A full disk (170KB) takes roughly 8 minutes to load. The reasons:

**The VIC-20 Bug Legacy:**
The Commodore serial bus was originally designed for the IEEE-488 parallel standard, capable
of transferring entire bytes simultaneously. For the VIC-20, Commodore switched to a serial
implementation to save connector costs. The MOS 6522 VIA chip's shift register had a bug
that prevented hardware-assisted serial transfers, forcing a software bit-banging
implementation.

**Software Bit-Banging:**
Each bit requires explicit software handling with handshaking. The protocol uses two
signal lines (CLK and DATA) and transfers one bit at a time with individual handshakes:

1. Sender pulls CLK low, places bit on DATA
2. Sender releases CLK (minimum 60us hold time)
3. Receiver reads DATA during CLK high period
4. Repeat for all 8 bits

**The C64 Made It Worse:**
The C64 inherited the VIC-20's serial protocol. But the VIC-II chip steals CPU cycles for
DMA (fetching screen data, sprite data, etc.), causing ~40us gaps every ~500us. To
compensate, the hold time per bit was increased from 20us (VIC-20) to 60us (C64), further
slowing transfers.

**The Result:**
```
Theoretical maximum (original IEEE-488 design): ~10 KB/sec
VIC-20 serial (20us timing):                    ~2 KB/sec (theoretical)
C64 standard KERNAL serial:                     ~0.3-0.4 KB/sec (actual)
```

### How Fast Loaders Work

Fast loaders bypass the KERNAL serial routines with custom transfer code installed on both
the C64 and inside the 1541's RAM:

**Step 1: Bootstrap**
A small stub program is loaded using the standard slow KERNAL routines. This stub uploads
a custom drive-side program into the 1541's RAM via M-W (Memory Write) commands.

**Step 2: Custom Protocol**
The fast loader replaces the bit-at-a-time protocol with faster approaches:

- **2-bit parallel transfer:** Send one bit on DATA and one bit on CLK simultaneously,
  halving the number of transfer steps from 8 to 4 per byte.
- **Synchronized timing:** Since both CPUs run at ~1MHz, the sender and receiver can agree
  on exact cycle counts instead of using handshakes for every bit. They synchronize once
  per byte (or per block), then free-run.
- **ATN as a signaling line:** Some protocols use the ATN line (normally reserved for bus
  commands) as an additional data line, enabling 2-bit+ATN transfers.

**Step 3: Drive-Side Optimization**
The custom code in the 1541 can:
- Bypass CBM DOS overhead (job queue, error handling)
- Read GCR data directly and decode it in a custom buffer
- Eliminate sector interleave delays by reading entire tracks
- Use the 1541's own CPU to decode and send data with minimal overhead

### Speed Comparison

```
Method                          Speed (approx.)    Speedup
Standard KERNAL                 ~400 bytes/sec     1x
JiffyDOS (1541)                 ~2,400 bytes/sec   6x
JiffyDOS (SD2IEC)               ~8,600 bytes/sec   22x
Typical fast loader             ~3,000-6,000 B/s   8-15x
Best fast loaders (1541)        ~7,000-8,000 B/s   18-20x
Theoretical max (serial bus)    ~10,000 B/s        25x
```

### IRQ Loaders

An IRQ loader is a fast loader that operates in the background, allowing the C64 to
continue running effects, music, and animations while data loads from disk. This is
essential for demos and games that need seamless transitions.

**Key characteristics:**
- Drive-side code runs autonomously, reading sectors and buffering data
- Host-side code is called from the IRQ handler, transferring a few bytes per interrupt
- The main program continues executing between transfer bursts
- Music playback and raster effects are uninterrupted

**Transfer protocols used by IRQ loaders:**

```
Protocol Type          Description                        Speed Impact
1-bit synchronous      One bit per exchange, timed         Slower but simple
2-bit asynchronous     Two bits per exchange, handshaked   Good balance
2-bit+ATN synchronous  Two bits + ATN line, timed          Fastest
```

**Notable IRQ loaders:**

| Loader           | Author          | Protocol             | Features              |
|------------------|-----------------|----------------------|-----------------------|
| Krill's Loader   | Krill           | 2-bit+ATN sync/async | Wide drive compat     |
| Spindle          | Linus Akesson   | 2-bit+ATN sync       | Integrated crunching  |
| Sparkle          | Sparta          | Various              | Full toolchain        |
| DreamLoad        | The Dreams      | 2-bit+ATN sync       | Open source           |
| Covert BitOps    | Lasse Oorni     | 1-bit synchronous    | Minimal footprint     |
| ULoad            | MagerValp       | 2-bit asynchronous   | Load + save support   |

### JiffyDOS

JiffyDOS is a ROM replacement for both the C64 KERNAL and the 1541 DOS ROM. It replaces
the serial protocol at the firmware level:

- Uses byte-level synchronization instead of bit-level handshaking
- Transfers 2 bits at a time (CLK + DATA)
- Achieves ~2.4 KB/sec with a 1541 (6x speedup)
- Compatible with standard serial bus devices (falls back to slow protocol)
- Requires physical ROM chip replacement in both C64 and drive

---

## 6. Compression Techniques

### Why Compression Matters on C64

With only 170KB per disk side and 64KB of RAM, compression is not a luxury -- it is a
necessity. A typical C64 demo or game might contain:

- Full-screen bitmap: 9KB (8KB bitmap + 1KB screen RAM + color RAM)
- Character set: 2KB
- SID music: 2-8KB
- Sprite data: up to 16KB
- Program code: 10-40KB+

Without compression, even a single disk cannot hold much content. With compression ratios
of 40-70%, disk capacity effectively doubles or triples.

### Run-Length Encoding (RLE)

The simplest compression technique: replace consecutive identical bytes with a count and
the byte value.

```
Uncompressed: AA AA AA AA AA 42 42 42 00 00 00 00 00 00
RLE encoded:  [5, AA] [3, 42] [6, 00]
```

**C64 RLE implementations typically use:**
- An escape byte to signal compressed runs (e.g., $FF)
- Literal bytes pass through unchanged if they are not the escape byte
- The escape byte followed by a count and value indicates a run

**Pros:** Tiny decompressor (often under 50 bytes of 6502 code), very fast decompression.
**Cons:** Only effective for data with many repeated bytes (solid backgrounds, empty areas).
Ineffective or counterproductive for varied data.

**Best suited for:** Bitmap graphics with large uniform areas, sprite data with empty
regions, screen RAM with repeated characters.

### LZ77 / LZSS Variants

The workhorse of C64 compression. LZ77 replaces repeated sequences with back-references
(offset, length pairs) pointing to earlier occurrences of the same data.

```
Uncompressed: "ABCABCABC"
LZ77 encoded: "ABC" [offset=3, length=6]  (copy 6 bytes from 3 bytes back)
```

On the C64, LZ77 variants are typically implemented as:
- Backward-decompressing (from end to start) to allow in-place decompression
- Using Elias gamma coding or similar for variable-length offset/length encoding
- Keeping the decompressor small (150-300 bytes) to minimize RAM overhead

**LZSS** is a common refinement that uses a flag bit to distinguish literal bytes from
match references, reducing overhead for non-compressible data.

### Huffman Coding

Assigns shorter bit sequences to more frequent bytes and longer sequences to rare bytes.
Rarely used alone on C64 due to the complexity of the decoder, but sometimes combined
with LZ77 (as in Exomizer).

### Popular C64 Crunchers

**Exomizer** (by Magnus Lind)
- Algorithm: LZSS with Huffman-coded match parameters
- Compression ratio: Best in class -- typically 5-15% better than competitors
- Decompression speed: Moderate (~3x slower than simpler LZ decompressors)
- Decompressor size: ~170 bytes of 6502 code + ~156 bytes of working buffer
- Memory requirement: Needs a 156-byte auxiliary buffer during decompression
- Supports forward and backward decompression
- Widely used in demos and games
- Can create self-extracting executables

**ByteBoozer** (by David Malmborg / HCL)
- Algorithm: LZSS variant optimized for C64
- Version 2.0 released 2016
- Decompression: Can decrunch on-the-fly during loading (no separate buffer needed)
- No sector buffer or decrunch buffer required on C64 side
- Integrates with BoozeLoader for trackmo development
- Popular in the demoscene, especially with Booze Design productions

**PuCrunch** (by Pasi Ojala)
- Algorithm: Hybrid LZ77 + RLE with Elias gamma coding
- Handles LZ77 matches and RLE runs in a single pass
- Uses symbol ranking -- more frequent RLE bytes get shorter codes
- Decompression speed: Faster than Exomizer
- Compression ratio: Within 2-5% of Exomizer (slightly worse)
- Creates self-extracting PRG files
- Supports C64, C128, VIC-20, and Plus/4

**Other notable crunchers:**
- **Time Cruncher** -- Historical cruncher from the early scene
- **Doynax LZ** -- Very fast decompression, moderate compression
- **ZX0** (Einar Saukas) -- Originally for ZX Spectrum, ported to 6502; excellent ratio
  with very small decompressor
- **Subsizer** -- Good compression with fast decompression
- **TSCrunch** -- Tiny decompressor, suitable for size-limited intros

### Compression in Practice

**Self-extracting programs:**
The most common use. A cruncher compresses a PRG file and prepends a small decompressor.
When loaded and run, the decompressor restores the original data and jumps to the start
address. Exomizer and PuCrunch both support this directly.

**Asset compression:**
Individual data blocks (graphics, music, level data) are compressed separately and
decompressed into their target memory locations as needed. This allows selective loading
and decompression.

**Streaming decompression:**
Used by IRQ loaders (especially Spindle), data is decompressed on-the-fly as it arrives
from disk. The compressed data is read sector by sector and fed into a decompressor that
outputs to scattered memory locations.

**Comparison (typical compression ratios):**
```
Data Type               Exomizer    PuCrunch    ByteBoozer 2
Bitmap graphics         55-65%      58-68%      58-68%
Character set           40-60%      42-62%      43-63%
SID music               50-70%      52-72%      52-72%
6502 machine code       55-70%      58-72%      58-72%
Mixed program (PRG)     50-65%      52-67%      53-68%
```
(Lower percentages = better compression. Values are approximate and highly data-dependent.)

### Real-Time Decompression Performance

```
Cruncher         Approx. Decompression Speed    Decompressor Size
Exomizer         ~3 KB/sec                      ~170 bytes + 156 buffer
PuCrunch         ~5 KB/sec                      ~350 bytes
ByteBoozer 2     ~5-6 KB/sec                    ~150 bytes
Doynax LZ        ~8-10 KB/sec                   ~120 bytes
```

These speeds assume PAL C64 at 985,248 cycles/sec with no VIC badlines.

---

## 7. Cartridge Memory

### Standard Cartridge Types

Cartridges connect through the expansion port and can map ROM into the CPU's address space
using the GAME and EXROM lines:

**8K Cartridge (EXROM=0, GAME=1)**
```
$8000-$9FFF: Cartridge ROML (8KB, read-only)
$A000-$BFFF: BASIC ROM (unchanged)
$D000-$DFFF: I/O (unchanged)
$E000-$FFFF: KERNAL ROM (unchanged)
```
The simplest cartridge type. The ROML chip appears at $8000. If the cartridge ROM begins
with the magic bytes $C3 $C2 $CD $38 $30 ("CBM80") at $8004-$8008, the KERNAL auto-starts
the cartridge on power-up by jumping to the address at $8000.

**16K Cartridge (EXROM=0, GAME=0)**
```
$8000-$9FFF: Cartridge ROML (8KB, read-only)
$A000-$BFFF: Cartridge ROMH (8KB, replaces BASIC ROM)
$D000-$DFFF: I/O (unchanged)
$E000-$FFFF: KERNAL ROM (unchanged)
```
Both ROML and ROMH are visible. BASIC ROM is replaced by the upper half of the cartridge.

**Ultimax Mode (EXROM=1, GAME=0)**
```
$0000-$0FFF: RAM (4KB only!)
$1000-$7FFF: Open bus / unmapped
$8000-$9FFF: Cartridge ROML (8KB)
$A000-$BFFF: Open bus / unmapped
$D000-$DFFF: I/O
$E000-$FFFF: Cartridge ROMH (8KB, replaces KERNAL)
```
Ultimax mode (named after the Japanese MAX Machine, a C64 predecessor) is the most extreme
configuration. Only 4KB of RAM is visible. ROMH replaces the KERNAL, including the
interrupt vectors. This mode is used by some freezer cartridges to gain complete control
of the machine.

### Bank-Switching Cartridges

To provide more than 16KB, cartridges use bank switching -- dividing their ROM into
multiple banks and switching between them by writing to registers in the I/O1 or I/O2
areas ($DE00-$DEFF or $DF00-$DFFF).

**Ocean Type 1** (128KB, 256KB, or 512KB)
```
Banks 0-15:  Mapped at $8000-$9FFF (ROML)
Banks 16-31: Mapped at $A000-$BFFF (ROMH)
Bank select: Write to $DE00
```
Used by many commercial games (Batman, Chase HQ, Robocop, etc.). The 256KB variant maps
lower 128KB into ROML and upper 128KB into ROMH.

**Magic Desk / Domark / HES Australia**
```
64-128KB in 8KB banks
Mapped at $8000-$9FFF
Bank select: Write to $DE00 (bits 0-3 = bank, bit 7 = disable cart)
```

**Funplay / Power Play**
```
128KB in 8KB banks
Mapped at $8000-$9FFF
Bank select: Write to $DE00 with encoded bank number
```

### EasyFlash

The EasyFlash is a modern, programmable flash cartridge offering 1MB of storage:

**Hardware:**
- 2 x 512KB flash ROM chips (AM29F040 or compatible)
- 256 bytes of RAM (accessible at $DF00-$DFFF)
- 64 banks of 16KB each (8KB ROML + 8KB ROMH per bank)

**Registers:**
```
$DE00 -- Bank Register (write-only)
         Bits 5-0: Bank number (0-63)
         Bits 7-6: Unused

$DE02 -- Control Register (write-only)
         Bit 7:    Active LED control
         Bit 2:    Game mode (directly controls GAME line)
         Bit 1:    EXROM mode (inverted, directly controls EXROM line)
         Bit 0:    Active cartridge flag
         Default after reset: $00
```

**Memory mapping:**
```
Each bank provides:
  ROML: 8KB at $8000-$9FFF
  ROMH: 8KB at $A000-$BFFF (16K mode) or $E000-$FFFF (Ultimax mode)

Total: 64 banks x 16KB = 1024KB (1MB)
```

**EAPI (EasyFlash API):**
A standardized interface for in-system flash programming, requiring 768 bytes of RAM.
The EAPI is stored in the cartridge ROM at a reserved location and copied to RAM before
use. This allows programs to save state (game saves, high scores) directly to the
cartridge flash.

**EasyFlash 3:**
An enhanced version offering:
- Seven independent EasyFlash cartridge slots
- Eight selectable KERNAL ROM replacements
- Freezer functionality
- USB interface for programming
- Soft-selectable boot configuration

### RAM Expansion Unit (REU)

The REU is Commodore's official RAM expansion, connecting via the expansion port and
providing DMA-accelerated memory transfers.

**Models:**
```
Model    RAM       Banks    Designed For
1700     128 KB    2        C128
1750     512 KB    8        C128
1764     256 KB    4        C64
```

Third-party and modified REUs can reach 2MB or even 16MB.

**How it works:**

The REU contains a MOS 8726 REC (RAM Expansion Controller) chip that performs DMA
transfers between C64 main RAM and the expansion RAM. The C64 CPU is temporarily
disconnected from the bus during transfers.

**Register Map ($DF00-$DF0A):**

```
$DF00 -- Status Register (read-only)
         Bit 7: Interrupt pending
         Bit 6: End of block (transfer complete)
         Bit 5: Fault (verify mismatch found)
         Bit 4: Size bit (1 = 256KB chips, i.e., 1750)
         Bits 3-0: Version/revision

$DF01 -- Command Register (write-only)
         Bit 7: Execute (1 = start transfer)
         Bit 5: Autoload (1 = don't update address/length registers)
         Bit 4: FF00 trigger (0 = start on $FF00 write, 1 = start immediately)
         Bits 1-0: Transfer type
                   %00 = Stash  (C64 -> REU)
                   %01 = Fetch  (REU -> C64)
                   %10 = Swap   (exchange C64 <-> REU)
                   %11 = Verify (compare C64 vs REU)

$DF02 -- C64 Base Address Low byte
$DF03 -- C64 Base Address High byte

$DF04 -- REU Base Address Low byte
$DF05 -- REU Base Address High byte
$DF06 -- REU Bank number (bits 2-0 for standard models, more for expanded)

$DF07 -- Transfer Length Low byte
$DF08 -- Transfer Length High byte
         (Length of $0000 means 65,536 bytes / 64KB)

$DF09 -- Interrupt Mask Register
         Bit 7: Enable interrupts
         Bit 6: Interrupt on end-of-block
         Bit 5: Interrupt on verify error

$DF0A -- Address Control Register
         Bit 7: Fix C64 address (1 = don't increment, use same address repeatedly)
         Bit 6: Fix REU address (1 = don't increment, use same address repeatedly)
```

**DMA Transfer Speed:**
```
Stash/Fetch:  ~1 byte per clock cycle = ~985 KB/sec (PAL)
Swap:         ~2 cycles per byte      = ~492 KB/sec (PAL)
Verify:       ~1 cycle per byte (stops at first mismatch)
```

With the screen enabled, the VIC-II steals some cycles, slightly reducing throughput.
With screen blanked (bit 4 of $D011 cleared), transfers run at full speed.

**Programming example -- Stash 256 bytes from $4000 to REU bank 0, address $0000:**
```asm
        LDA #$00
        STA $DF04           ; REU address low = $00
        STA $DF05           ; REU address high = $00
        STA $DF06           ; REU bank = 0
        LDA #$00
        STA $DF02           ; C64 address low = $00
        LDA #$40
        STA $DF03           ; C64 address high = $40
        LDA #$00
        STA $DF07           ; Length low = $00
        LDA #$01
        STA $DF08           ; Length high = $01 (256 bytes)
        LDA #$B0            ; Execute + FF00 immediate + Stash
        STA $DF01           ; Start transfer!
```

**Detecting the REU:**
Write test values to registers $DF02-$DF08, read them back. If the values persist, an
REU is present (these addresses are normally open bus / floating).

**GeoRAM:**
An alternative RAM expansion by Berkeley Softworks (creators of GEOS). Unlike the REU,
GeoRAM uses page-based access without DMA:
```
$DE00: Page select within window (256 pages)
$DE01: Window select (selects which 256-page block)
$DF00-$DFFF: 256-byte window into selected page
```
Simpler to program but slower than REU DMA, as data must be copied byte-by-byte through
the 256-byte window.

---

## 8. Programs Larger Than Memory

### The Fundamental Problem

A C64 program is limited to approximately 51KB of usable RAM at most (in all-RAM mode with
a small kernel replacement). Many programs need far more:

- A game with 20 levels of graphics, each 10KB: 200KB of graphics alone
- A demo with 15 distinct effects: easily 300KB+ of code and data
- A productivity app like GEOS: megabytes of application code and fonts

The solution is to treat disk (or REU, or cartridge) as an extension of memory.

### Multi-Load Technique

The simplest approach: divide the program into parts that fit in memory, and load each
part when needed.

```
Disk Layout:
  BOOT      - Small bootstrap loader (loads first)
  PART1     - Main menu + first level
  PART2     - Second level
  PART3     - Third level
  MUSIC     - Background music (may persist across parts)
  ...
```

The boot program loads a fast loader, which then loads each part as needed. Each part
overwrites the previous one in the same memory region.

**Advantages:** Simple to implement, each part gets nearly full memory.
**Disadvantages:** Loading pauses are visible, requires careful memory map planning.

### Overlay Technique

A more sophisticated version of multi-loading where specific memory regions are designated
as "overlay slots" that get swapped with different code modules:

```
Memory Map:
  $0800-$1FFF:  Resident kernel (always present)
  $2000-$3FFF:  Overlay slot (swapped as needed)
  $4000-$7FFF:  Data area
  $8000-$BFFF:  Graphics
  $C000-$CFFF:  Music player
```

Different overlay modules are loaded into the $2000-$3FFF slot:
- Overlay A: Level editor
- Overlay B: Game engine
- Overlay C: Inventory screen
- Overlay D: Save/load screen

**GEOS -- The Master of Overlays:**
GEOS is perhaps the most famous example. Its applications like geoWrite use a 4KB overlay
slot, with up to 7 code modules that swap in and out. The application maintains a
"resident" core in memory and loads specialized functions from disk as the user invokes
them.

GEOS also implements a form of virtual memory, using floppy disk as swap space. Code and
data are constantly swapped between disk and RAM, with a built-in fast loader (diskTurbo)
to minimize the performance impact.

### Disk as Virtual Memory

Using the disk drive as extended storage, loading data on demand:

1. **Level streaming:** Load the next level's data while the current level is being played.
   An IRQ loader fetches data in the background while music and gameplay continue.

2. **Asset caching:** Keep recently used assets in RAM and evict the oldest when space is
   needed. Similar to a CPU cache but managed in software.

3. **Page swapping:** Divide RAM into fixed-size pages. When a page is needed that is not
   in RAM, load it from disk (and optionally write the evicted page back if it was
   modified).

### REU as Extended Memory

With an REU, the effective RAM grows to 128KB-16MB. The DMA transfers are nearly
instantaneous compared to disk loading:

```
Operation               1541 Disk       REU DMA
Transfer 8KB            ~20 seconds     ~8 milliseconds
Transfer 16KB           ~40 seconds     ~16 milliseconds
Full C64 RAM swap       ~2.5 minutes    ~65 milliseconds
```

**Common REU usage patterns:**

- **Screen buffer swapping:** Store multiple screens in REU, DMA them into VIC-visible RAM
  for instant screen changes.
- **Music/SFX library:** Store multiple SID tunes in REU, fetch the current one into RAM.
- **Decompression buffer:** Decompress data from disk into REU, then DMA portions into
  C64 RAM as needed.
- **Undo buffer:** In productivity apps, store previous states for undo functionality.

### Demo Multi-Part Loaders (Trackmos)

A "trackmo" is a demo that uses custom disk formatting and an integrated IRQ loader to
stream content continuously from disk. The disk is not formatted with CBM DOS -- instead,
it uses a custom layout optimized for sequential reading.

**How a trackmo works:**

1. **Boot:** A small loader is loaded via standard KERNAL, installs the fast loader.
2. **Drive code:** Custom firmware is uploaded to the 1541's RAM, replacing CBM DOS.
3. **Sequential reading:** The drive reads tracks sequentially, sending compressed data
   to the C64.
4. **Scattered loading:** Data chunks are decompressed and placed at their target memory
   locations, which may be non-contiguous.
5. **Seamless transitions:** While one effect runs, the next effect's data loads in the
   background. Music never stops.

**Spindle -- The State of the Art:**
Spindle (by Linus Akesson) is the premier integrated solution for trackmo development:

- Handles linking, loading, crunching, and disk image creation
- Scattered loading: chunks can target any memory location, split across page boundaries
- GCR decoding on the fly (inside the 1541)
- State-of-the-art serial transfer protocol (2-bit+ATN synchronous)
- Can replace all of C64 RAM (except SID tune and Spindle's resident code) in a single
  load call without stopping music
- Supports 40-track disks for extra capacity
- Minimal host-side memory footprint
- Automatic scheduling of load operations for optimal throughput

**Sparkle:**
Another popular solution, offering:
- Cross-platform toolchain
- Easy-to-use build system
- Support for multiple drive types
- Integrated compression

### Streaming Techniques

For effects that consume data continuously (e.g., full-motion video, scrolling landscapes):

1. **Double buffering:** Maintain two data buffers. While one is being displayed/used, the
   other is being filled from disk. Swap when the new buffer is ready.

2. **Ring buffer:** A circular buffer in RAM that is continuously filled from disk and
   consumed by the display routine. The loader stays ahead of the consumer.

3. **Just-in-time loading:** Calculate exactly when data will be needed and schedule disk
   reads to arrive just before that point. Spindle's scheduling system automates this.

---

## 9. Hardcore Details

### Custom GCR Encoding

The standard 1541 GCR encoding maps 4-bit nybbles to 5-bit patterns. Some programs and
copy protection schemes use custom GCR tables or non-standard data formats:

**Non-standard sector sizes:**
Instead of 256-byte sectors, custom formats can use arbitrary block sizes. The 1541's
firmware mandates 256 bytes, but custom drive code can read and write any amount of data.

**Eliminating headers:**
The standard sector format uses separate header and data blocks, each with their own sync
marks. Custom formats can eliminate headers entirely, using position counting instead.
This saves ~20 bytes per sector of overhead.

**Optimal theoretical encoding:**
The standard GCR encoding allows at most 8 consecutive one-bits in valid data. Some
theoretically optimal schemes push this boundary, allowing slightly denser encoding at
the cost of reduced noise margin.

**Minimizing gaps:**
Standard formatting uses 9-byte header gaps and variable inter-sector gaps. Aggressive
custom formats reduce header gaps to 2 bytes and inter-sector gaps to the minimum needed
for motor speed tolerance, gaining 1-2 extra sectors per track.

### Extra Tracks 36-40 (and Beyond)

The 1541's stepper motor can physically reach beyond track 35:

```
Track Range    Status
1-35           Standard formatted area
36-40          Accessible on most drives (beyond standard format)
41-42          Accessible on many drives (mechanical limit varies)
Half-tracks    Available between any full tracks
```

**Using extra tracks:**
```
Extra tracks at zone 0 (17 sectors each):
  Track 36: 17 sectors x 256 bytes = 4,352 bytes
  Track 37: 17 sectors x 256 bytes = 4,352 bytes
  Track 38: 17 sectors x 256 bytes = 4,352 bytes
  Track 39: 17 sectors x 256 bytes = 4,352 bytes
  Track 40: 17 sectors x 256 bytes = 4,352 bytes
  Extra capacity: ~21 KB (5 tracks)
```

Using speed zone 3 (21 sectors) on extra tracks instead of zone 0 pushes capacity further
but exceeds density specifications and reduces reliability.

**Capacity enhancement summary:**

```
Technique                      Capacity    Increase    Compatibility
Standard format (35 tracks)    170 KB      baseline    Full DOS compat
Extra tracks 36-40             ~191 KB     +12%        Read-only DOS
Speed zone 3 on all tracks     ~215 KB     +26%        Custom code only
Minimal gaps                   ~181 KB     +6%         Read-only DOS
Minimal gaps + extra tracks    ~216 KB     +27%        Custom code only
Full optimization              ~246 KB     +44%        Custom code only
```

### Half-Tracks

The 1541 uses an 80-position stepper motor but only uses 35 of the 40 possible full-track
positions in standard operation. This leaves 84 half-track positions available.

**Half-track mechanism:**
Each full track move requires two stepper steps. A single step moves the head to a
half-track position -- between two full tracks. The standard DOS never uses half-track
positions.

**Copy protection uses:**
- Write data on half-tracks that standard copiers skip
- Use "fat tracks" -- write the same data on a full track and adjacent half-tracks,
  making it readable from multiple head positions
- Write different data on adjacent half-tracks; protection checks which one is readable

**Programming half-tracks:**
```asm
; In 1541 drive code:
; VIA 2 Port B ($1C00) controls the stepper motor
; Bits 0-1 select the stepper phase

; Move head one half-track inward:
        LDA $1C00
        CLC
        ADC #$01            ; Advance stepper by 1 phase (half-track)
        AND #$03            ; Keep only stepper bits
        STA $1C00
        ; Wait for head to settle (~20ms)
```

### 1541 Memory Map and Programming

**Complete 1541 RAM layout:**

```
$0000-$00FF  Zero Page
  $00:       Job code / status for buffer 0
  $01:       Job code / status for buffer 1
  $02:       Job code / status for buffer 2
  $03:       Job code / status for buffer 3
  $04:       Job code / status for buffer 4 (BAM)
  $06-$07:   Track/sector for buffer 0
  $08-$09:   Track/sector for buffer 1
  $0A-$0B:   Track/sector for buffer 2
  $0C-$0D:   Track/sector for buffer 3
  $0E-$0F:   Track/sector for buffer 4
  $12-$13:   ID bytes of current disk
  $16:       GCR byte count (overflow) buffer
  $17-$1A:   GCR work bytes
  $22:       Current track number (for head positioning)
  $30-$35:   GCR encoding temporary nybbles
  $36-$3D:   GCR encoding temporary result
  $3E:       Byte counter for data block
  $43:       Current drive number
  $47-$48:   Pointer to active buffer
  $4A:       Byte-ready flag for GCR
  $51:       Current sector of data under head
  $52:       Read/write temporary
  $56-$5D:   GCR overflow nybbles (5 bytes expanded)
  $62:       Pending track for step
  $6F-$72:   Last read disk ID
  $7C:       Error recovery flag

$0100-$01FF  Processor Stack and GCR Overflow
  $0100-$0145: Stack (shared with system variables)
  $01BA-$01FF: GCR overflow buffer

$0200-$02FF  Command Buffer and DOS Variables
  $0200-$0229: Input command buffer (42 bytes)
  $022A:       Command code
  $022B-$024A: Various DOS working variables
  $024B:       Number of active buffers
  $02D5-$02F8: Error message buffer

$0300-$03FF  Buffer 0 (data buffer)
$0400-$04FF  Buffer 1 (data buffer)
$0500-$05FF  Buffer 2 (data buffer)
$0600-$06FF  Buffer 3 (data buffer)
$0700-$07FF  Buffer 4 (data buffer, also used for BAM)
```

**VIA (Versatile Interface Adapter) Chips:**

```
VIA 1 ($1800-$180F) -- Serial Bus Interface
  $1800: Port B data register
    Bit 0: DATA IN (active low)
    Bit 1: DATA OUT (active low)
    Bit 2: CLK IN (active low)
    Bit 3: CLK OUT (active low)
    Bit 4: ATN Acknowledge (directly active-low on bus)
    Bit 7: ATN IN (directly from bus, active-low)
  $1801: Port A data register
  $1802: Port B DDR
  $180B: ACR (Auxiliary Control Register)
  $180D: IFR (Interrupt Flag Register)
  $180E: IER (Interrupt Enable Register)

VIA 2 ($1C00-$1C0F) -- Drive Mechanics
  $1C00: Port B data register
    Bits 0-1: Stepper motor phase (head positioning)
    Bit 2:    Drive motor on/off (1 = on)
    Bit 3:    LED on/off (1 = on)
    Bit 4:    Write protect sense (1 = protected)
    Bits 5-6: Data density select (speed zone)
              %00 = zone 0 (slowest, 250 kbps)
              %01 = zone 1 (267 kbps)
              %10 = zone 2 (286 kbps)
              %11 = zone 3 (fastest, 308 kbps)
    Bit 7:    SYNC detected (read-only, 1 = sync found)
  $1C01: Port A -- GCR data byte read/written from/to disk head
  $1C04-$1C05: Timer 1 (used for byte-ready timing)
  $1C0B: ACR (Auxiliary Control Register)
  $1C0C: PCR (Peripheral Control Register)
    Bit 1: Byte-ready flag (signals new byte available in $1C01)
```

**Job Queue System:**

The 1541 DOS uses a job queue to manage disk operations. Jobs are submitted by writing
a command code to the appropriate zero-page location and waiting for the result:

```
Job Codes:
  $80: Read sector
  $90: Write sector
  $A0: Verify sector (automatic after write)
  $B0: Seek to track
  $C0: Bump (move head to track 0/1 for calibration)
  $D0: Jump to code in buffer (execute buffer as code)
  $E0: Execute program in buffer (similar to $D0)

Status/Error Codes (returned in same location):
  $01: OK (no error)
  $02: Header block not found
  $03: Sync mark not found
  $04: Data block not found
  $05: Data block checksum error
  $07: Verify error
  $08: Write-protect error
  $09: Header block checksum error
  $0B: Disk ID mismatch
  $0F: Drive not ready
```

**Programming example -- reading a sector via job queue:**
```asm
; In drive code (running inside 1541):
        LDA #18             ; Track 18
        STA $06             ; Buffer 0 track
        LDA #0              ; Sector 0
        STA $07             ; Buffer 0 sector
        LDA #$80            ; Read command
        STA $00             ; Submit job for buffer 0
wait:   LDA $00             ; Check status
        BMI wait            ; Bit 7 set = still executing
        CMP #$01            ; Check for OK
        BNE error           ; Handle error
        ; Data now in buffer 0 ($0300-$03FF)
```

### Serial Bus Protocol Timing

**Signal lines (active-low, open collector):**
```
Pin   Name    Function
1     SRQ     Service Request (unused on most devices)
2     GND     Ground
3     ATN     Attention (controller to devices)
4     CLK     Clock (sender to receiver)
5     DATA    Data (bidirectional)
6     RESET   Reset all devices
```

**Standard byte transfer protocol (C64 KERNAL):**

```
Step   Time        Action
 1     ---         Sender holds CLK (not ready), receivers hold DATA (not ready)
 2     ---         Sender releases CLK (ready to send)
 3     ---         Receivers release DATA when ready to receive
 4     0 us        Sender pulls CLK, puts bit 0 on DATA
 5     60 us       Sender releases CLK (bit valid)
 6     120 us      Sender pulls CLK, puts bit 1 on DATA
 7     180 us      Sender releases CLK (bit valid)
 ...   (repeat for bits 2-7)
18     840 us      Last bit released
19     900 us      Sender pulls CLK (byte complete)
20     <1000 us    Receiver pulls DATA (acknowledge)
```

**Key timing parameters:**
```
Parameter              Symbol   Duration
ATN Response           Tat      < 1000 us
Non-EOI Response       Tne      40-200 us
Bit Setup Time         Ts       20-70 us (60 us on C64 due to VIC DMA)
Data Valid Time        Tv       20 us minimum
Frame Handshake        Tf       0-1000 us
Between Bytes          Tbb      100 us minimum
EOI Response           Tye      200-250 us
EOI Acknowledge        Tei      60 us minimum
```

**EOI (End of Information) signaling:**
When the sender has sent the last byte, it delays releasing CLK for more than 200us after
the receivers signal ready. The receivers interpret this delay as an EOI condition and
acknowledge by pulling DATA for at least 60us.

**Bus turnaround (switching sender/receiver roles):**
1. New receiver pulls DATA, releases CLK
2. Both parties see CLK released (open collector)
3. New sender pulls CLK, releases DATA
4. Bus is now in correct initial state for new direction

### REU DMA Mechanism

**How DMA works internally:**

1. The CPU writes the command register ($DF01) with the execute bit set.
2. If the FF00 trigger bit (bit 4) is clear, DMA waits for any write to address $FF00.
   If bit 4 is set, DMA begins immediately after the command register write.
3. The REC (RAM Expansion Controller) asserts the DMA line on the expansion port.
4. The DMA signal forces the 6510 CPU to tri-state its address bus, data bus, and R/W
   line, effectively disconnecting it from the bus.
5. The REC takes over the bus and performs transfers at one byte per phi2 clock cycle.
6. If the VIC-II chip needs bus access (BA goes low), the DMA pauses temporarily.
7. When the transfer is complete, the REC releases DMA, and the CPU resumes execution.

**The $FF00 trick:**
The delayed trigger mode (bit 4 = 0) is essential for transferring data to/from the I/O
area ($D000-$DFFF). Since the command register is at $DF01 (inside I/O space), you cannot
simultaneously have I/O mapped and access RAM at $D000. The solution:

1. Set up all REU registers with I/O visible.
2. Write command with bit 4 = 0 (delayed trigger).
3. Bank out I/O (e.g., set $01 = $34).
4. Write any value to $FF00 -- this triggers the DMA, which now sees RAM at $D000-$DFFF.

An even more elegant approach uses the RMW (Read-Modify-Write) property: an INC $FF00 or
DEC $FF00 instruction performs two writes to $FF00. The REC triggers on the first write
and completes the DMA before the second write. The CPU is frozen during DMA and resumes
seamlessly.

```asm
; Transfer involving I/O area - use $FF00 trigger
        ; Set up REU registers...
        LDA #$A0            ; Execute + delayed trigger + Stash
        STA $DF01           ; Command written (DMA not started yet)
        INC $FF00           ; Two writes to $FF00:
                            ; First write triggers DMA
                            ; CPU freezes during transfer
                            ; CPU resumes for second write
                            ; (value at $FF00 incremented, harmless)
```

**Address fixing:**
Setting bit 7 or bit 6 of $DF0A "fixes" the corresponding address counter. The fixed
address is used repeatedly for every byte of the transfer. This enables:

- **Fill memory:** Fix REU address, set C64 address to start of target area. A single byte
  in REU fills an entire C64 memory region.
- **Sample playback:** Fix C64 address to SID volume register ($D418), set REU to sample
  data. DMA plays raw 4-bit samples at ~1MHz sample rate.
- **Port scanning:** Fix C64 address to an I/O port, stream results to REU.

### Turbo Tape Formats

Standard Commodore Datasette encoding uses pulse-width modulation:

```
Standard KERNAL encoding:
  "0" bit: Short pulse + medium pulse
  "1" bit: Medium pulse + short pulse
  Byte marker: Long pulse + medium pulse

Approximate pulse durations:
  Short:   352 us  ($30 timer value)
  Medium:  512 us  ($42 timer value)
  Long:    672 us  ($56 timer value)

Effective data rate: ~300 baud (37.5 bytes/sec)
```

**Turbo tape formats** replace the KERNAL tape routines with custom interrupt-driven code:

```
Turbo Tape 64 format:
  "0" bit: Single short pulse (~211 us)
  "1" bit: Single long pulse (~324 us)
  No parity bit (saves time, relies on checksums)
  Lead-in byte: $02
  Sync sequence: $09, $08, $07, $06, $05, $04, $03, $02, $01

Effective data rate: ~540 bytes/sec (Turbo Tape 250)
                     Up to ~3000 baud for aggressive turbos
```

**Key turbo tape techniques:**
1. Single pulse per bit (instead of two pulses in KERNAL format)
2. Shorter pulse durations (tighter timing tolerances)
3. Elimination of inter-byte gaps
4. Removal of parity bits (checksum-only error detection)
5. Custom interrupt handler reads bits without main CPU involvement
6. Some turbos use PWM with 3+ pulse lengths for higher density

### 1541 Analog vs Digital Data Separation

A subtle but important hardware detail for copy protection:

**1541 and 1541-C (analog data separator):**
When encountering invalid GCR sequences or unformatted regions, the analog circuit
produces genuinely random output -- different values on every read. This randomness is
exploited by "weak bit" protection schemes: the protection checks that reading the same
sector twice produces different results, proving it is an original disk (not a bit-perfect
copy, which would produce identical reads).

**1541-II (digital data separator):**
The digital circuit produces consistent, repeating patterns when reading invalid regions.
This actually broke some copy protection that relied on random behavior, causing legitimate
originals to fail verification on newer drives.

---

## 10. References

### Official Documentation and Wikis

- [C64-Wiki: Bank Switching](https://www.c64-wiki.com/wiki/Bank_Switching)
- [C64-Wiki: Memory Map](https://www.c64-wiki.com/wiki/Memory_Map)
- [C64-Wiki: VIC Bank](https://www.c64-wiki.com/wiki/VIC_bank)
- [C64-Wiki: Serial Port](https://www.c64-wiki.com/wiki/Serial_Port)
- [C64-Wiki: Fast Loader](https://www.c64-wiki.com/wiki/Fast_loader)
- [C64-Wiki: IRQ Loader](https://www.c64-wiki.com/wiki/IRQ_loader)
- [C64-Wiki: Comparison of IRQ Loaders](https://www.c64-wiki.com/wiki/Comparison_of_IRQ_loaders)
- [C64-Wiki: EasyFlash](https://www.c64-wiki.com/wiki/EasyFlash)
- [C64-Wiki: REU](https://www.c64-wiki.com/wiki/REU)
- [C64-Wiki: Commodore REU](https://www.c64-wiki.com/wiki/Commodore_REU)
- [C64-Wiki: Commodore 1541](https://www.c64-wiki.com/wiki/Commodore_1541)
- [C64-Wiki: Overlay](https://www.c64-wiki.com/wiki/overlay)
- [C64-Wiki: Datassette Encoding](https://www.c64-wiki.com/wiki/Datassette_Encoding)
- [C64-Wiki: Cartridge](https://www.c64-wiki.com/wiki/Cartridge)

### Technical References

- [Commodore 1541 Drive Memory Map (sta.c64.org)](https://sta.c64.org/cbm1541mem.html)
- [Commodore 64 Memory Map (sta.c64.org)](https://sta.c64.org/cbm64mem.html)
- [Codebase64: REU Programming](https://codebase64.net/doku.php?id=base:reu_programming)
- [Codebase64: VIC-II Memory Organizing](https://codebase.c64.org/doku.php?id=base:vicii_memory_organizing)
- [The 6510 Processor Port (C64 OS)](https://www.c64os.com/post/6510procport)
- [How Does the 1541 Drive Work (C64 OS)](https://c64os.com/post/howdoes1541work)
- [Rethinking the Memory Map (C64 OS)](https://www.c64os.com/post/?p=57)
- [The C64 PLA Dissected (Thomas Giesel / skoe)](https://skoe.de/docs/c64-dissected/pla/c64_pla_dissected_a4ds.pdf)

### Pagetable.com (Michael Steil)

- [Fitting 44% More Data on a 1541 Floppy](https://www.pagetable.com/?p=1107)
- [Commodore Peripheral Bus: Part 4: Standard Serial](https://www.pagetable.com/?p=1135)
- [A 256-Byte Autostart Fast Loader](https://www.pagetable.com/?p=568)
- [Inside geoWrite: The Overlay System](https://www.pagetable.com/?p=1425)
- [Visualizing 1541 Disk Contents](https://www.pagetable.com/?p=1070)

### Compression Tools

- [Exomizer (Magnus Lind)](https://bitbucket.org/magli143/exomizer/)
- [PuCrunch (Pasi Ojala / mist64)](https://github.com/mist64/pucrunch)
- [ByteBoozer 2 (Luigi Di Fraia fork)](https://github.com/luigidifraia/ByteBoozer2)
- [ZX0 (Einar Saukas)](https://github.com/einar-saukas/ZX0)
- [PuCrunch Technical Description](https://a1bert.kapsi.fi/Dev/pucrunch/)

### Loader Frameworks

- [Spindle v3 (Linus Akesson)](https://linusakesson.net/software/spindle/v3.php)
- [Sparkle (Sparta)](https://github.com/spartaomg/SparkleCPP)
- [A Simple Disk Drive IRQ Loader Dissected (Cadaver)](https://cadaver.github.io/rants/irqload.html)

### Cartridge Hardware

- [EasyFlash Programmer's Guide (Thomas Giesel)](http://skoe.de/easyflash/files/devdocs/EasyFlash-ProgRef.pdf)
- [EasyFlash Introduction (skoe.de)](https://skoe.de/easyflash/efintro/)
- [Bank Switching Cartridges (hackup.net)](https://www.hackup.net/2019/07/bank-switching-cartridges/)
- [C64 Cartridges: Theory of Operation (Luigi Di Fraia)](https://luigidifraia.wordpress.com/2021/05/08/commodore-64-cartridges-theory-of-operation-and-ocean-bank-switching-described/)

### Copy Protection and Disk Internals

- [The 1541 Drive and GCR Encoding (C64 Copy Protection Reference)](https://www.commodoregames.net/copyprotection/the-1541-drive.asp)
- [GCR Decoding on the Fly (Linus Akesson)](https://www.linusakesson.net/programming/gcr-decoding/index.php)
- [Commodore Serial Bus Protocol Documentation (mist64)](https://github.com/mist64/cbmbus_doc)
- [D64 File Format Description](https://ist.uwaterloo.ca/~schepers/formats/D64.TXT)

### Community and Historical

- [Commodore 1541 (Wikipedia)](https://en.wikipedia.org/wiki/Commodore_1541)
- [Commodore REU (Wikipedia)](https://en.wikipedia.org/wiki/Commodore_REU)
- [Fast Loader (Wikipedia)](https://en.wikipedia.org/wiki/Fast_loader)
- [1541: The Floppy Disk (Ruud Baltissen)](http://www.baltissen.org/newhtm/1541c.htm)
- [CSDb: ByteBoozer 2.0](https://csdb.dk/release/?id=145031)
- [Restore64: C64 PRG Disassembler (370+ packers)](https://restore64.dev/)
