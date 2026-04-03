# C64 File Formats and Program Structure

Comprehensive reference covering how programs are stored, loaded, and executed on
the Commodore 64 — from raw PRG binaries to disk images, tape containers, and
cartridge ROMs.

---

## 1. Overview

The Commodore 64 ecosystem uses a layered set of file formats spanning physical
media (floppy disks, cassette tapes, cartridge ROMs) and their modern emulator
equivalents (disk images, tape images, CRT files). Understanding these formats is
essential for developing, preserving, and distributing C64 software.

### Storage Hierarchy

```
Physical Media          Emulator Image       Individual Files
-----------------       ----------------     ----------------
5.25" floppy disk  -->  .D64 / .G64 / .NIB  --> .PRG, .SEQ, .USR, .REL
3.5" floppy disk   -->  .D81                 --> .PRG, .SEQ, .USR, .REL
Cassette tape      -->  .TAP / .T64          --> .PRG
Cartridge ROM      -->  .CRT                 --> (self-contained)
```

### How Programs Are Loaded

The standard loading sequence on a real C64:

1. User types `LOAD "filename",8` (disk) or `LOAD` (tape)
2. The KERNAL ROM reads the first two bytes of the file as a load address
3. The remaining bytes are placed into memory starting at that address
4. For BASIC programs: `RUN` interprets the tokenized BASIC text
5. For ML programs: the BASIC stub `SYS xxxxx` jumps to the machine code entry

Two load modes exist:

| Command              | Behavior                                           |
|----------------------|----------------------------------------------------|
| `LOAD "file",8`     | Loads to default BASIC area ($0801)                |
| `LOAD "file",8,1`   | Loads to the address specified in the 2-byte header|

---

## 2. PRG File Format

The PRG (program) file is the fundamental C64 executable format. It is the
simplest possible container: a 2-byte load address header followed by raw binary
data.

### Structure

```
Offset  Size  Description
------  ----  -----------
$0000   2     Load address (little-endian, low byte first)
$0002   n     Program data (raw bytes loaded into memory)
```

**Example:** A BASIC program loading at $0801:

```
Byte 0: $01   (low byte of $0801)
Byte 1: $08   (high byte of $0801)
Byte 2+: tokenized BASIC or machine code
```

### Common Load Addresses

| Address | Hex    | Usage                                              |
|---------|--------|----------------------------------------------------|
| 2049    | $0801  | Default BASIC program start                        |
| 2061    | $080D  | After a typical 1-line SYS stub                    |
| 4096    | $1000  | Common ML location below BASIC ROM                 |
| 8192    | $2000  | Bitmap screen data                                 |
| 16384   | $4000  | VIC bank 1 base                                    |
| 32768   | $8000  | Upper RAM, cartridge space                         |
| 49152   | $C000  | Upper RAM, above BASIC ROM                         |

### BASIC Stub for Auto-Run (SYS xxxx)

Most machine language programs include a short BASIC stub so the user can simply
type `RUN` after loading. The stub is a single tokenized BASIC line that calls
`SYS` to transfer control to the ML entry point.

**Typical stub for entry at $080D (2061):**

```
Address  Bytes           Meaning
-------  --------------  ----------------------------------
$0801    $0B $08         Pointer to next BASIC line ($080B)
$0803    $0A $00         Line number 10 ($000A)
$0805    $9E             SYS token
$0806    $20             Space character (PETSCII $20)
$0807    $32 $30 $36 $31 "2061" in PETSCII
$080B    $00             End-of-line marker (null terminator)
$080C    $00 $00         End-of-program marker (null link)
```

The SYS token ($9E) is followed by the entry address as a PETSCII string, not
a binary value. The BASIC interpreter parses this string to determine the target
address.

**Minimal stub hex dump:**

```
01 08 0B 08 0A 00 9E 20 32 30 36 31 00 00 00
```

### SEQ (Sequential) Files

SEQ files are headerless data files — raw byte streams without the 2-byte load
address. They are used for text, data, and sequential I/O. On a D64 disk image
they appear with file type $81. When extracted from a disk image, they typically
have no header and contain pure data.

### USR (User) Files

USR files have the same on-disk structure as SEQ files but are marked with type
$83. They were intended for user-defined purposes and are often used by
applications for proprietary data formats.

---

## 3. D64 Disk Image Format

The D64 format is the most common C64 disk image, representing a standard 1541
floppy disk. It is a sector-by-sector dump of the entire disk surface.

### File Size Variants

| Variant                      | Size (bytes) | Tracks | Sectors |
|------------------------------|-------------|--------|---------|
| 35-track, no errors          | 174,848     | 35     | 683     |
| 35-track, with error info    | 175,531     | 35     | 683     |
| 40-track, no errors          | 196,608     | 40     | 768     |
| 40-track, with error info    | 197,376     | 40     | 768     |
| 42-track, no errors          | 205,312     | 42     | 802     |
| 42-track, with error info    | 206,114     | 42     | 802     |

Standard D64 = 174,848 bytes (35 tracks, 683 sectors of 256 bytes each).

### Track and Sector Layout

The 1541 uses variable sectors per track (zone bit recording) to maximize
capacity on a constant-angular-velocity drive:

| Zone | Tracks | Sectors/Track | Total Sectors | Speed Zone |
|------|--------|---------------|---------------|------------|
| 1    | 1-17   | 21            | 357           | 3 (fastest)|
| 2    | 18-24  | 19            | 133           | 2          |
| 3    | 25-30  | 18            | 108           | 1          |
| 4    | 31-35  | 17            | 85            | 0 (slowest)|
|      |        | **Total:**    | **683**       |            |

Extended tracks (36-40, 41-42) all use 17 sectors/track.

**Sector offset calculation:**

```
offset = (cumulative_sectors_before_track + sector) * 256
```

### BAM (Block Availability Map) — Track 18, Sector 0

The BAM occupies the first sector of track 18 and contains the disk name, ID,
DOS version, and a bitmap of free/used sectors for every track.

```
Offset  Size  Description
------  ----  -----------
$00     1     Track of first directory sector (usually 18)
$01     1     Sector of first directory sector (usually 1)
$02     1     DOS version type ($41 = "A" for CBM DOS 2.6)
$03     1     Reserved ($00; $80 = double-sided flag on 1571)
$04-$8F 140   BAM entries: 4 bytes per track, tracks 1-35
$90-$9F 16    Disk name (PETASCII, padded with $A0)
$A0-$A1 2     Shift-space fill bytes ($A0)
$A2-$A3 2     Disk ID
$A4     1     $A0 fill byte
$A5-$A6 2     DOS type ("2A" for standard disks)
$A7-$AA 4     $A0 fill bytes
$AB-$FF 85    Unused (normally $00)
```

**Extended BAM for 40-track images (stored in different locations by DOS
variant):**

| DOS Variant   | BAM Location (tracks 36-40) |
|---------------|-----------------------------|
| SPEED DOS     | $C0-$D3                     |
| DOLPHIN DOS   | $AC-$BF                     |
| PrologicDOS   | $90-$A3 (overwrites name!)  |

**BAM entry format (4 bytes per track):**

```
Byte 0:   Number of free sectors on this track
Byte 1-3: Bitmap — each bit represents one sector (1=free, 0=used)
          Byte 1 bits 0-7 = sectors 0-7
          Byte 2 bits 0-7 = sectors 8-15
          Byte 3 bits 0-7 = sectors 16-23 (unused bits set to 0)
```

**Example:** Track 1 has 21 sectors. BAM entry: `15 FF FF 1F`
- $15 = 21 free sectors
- $FF $FF $1F = bits 0-20 set (sectors 0-20 free), bits 21-23 clear

### Directory Format — Track 18, Sectors 1-18

The directory occupies sectors on track 18, linked in a chain with interleave 3:
18/1 -> 18/4 -> 18/7 -> 18/10 -> 18/13 -> 18/16 -> 18/2 -> ...

Each sector holds 8 directory entries of 32 bytes each. Maximum: 144 entries
(18 sectors x 8 entries).

**Directory entry format (32 bytes):**

```
Offset  Size  Description
------  ----  -----------
$00-$01 2     Track/sector of next directory sector (first entry only;
              remaining 7 entries: $00/$00)
$02     1     File type byte (see below)
$03-$04 2     Track/sector of first data sector of the file
$05-$14 16    Filename (PETASCII, padded with $A0)
$15-$16 2     Track/sector of first side-sector block (REL files only)
$17     1     REL file record length (1-254; 0 for non-REL files)
$18-$1D 6     Unused (used by GEOS for file structure info)
$1E-$1F 2     File size in sectors (little-endian)
```

**File type byte ($02):**

```
Bit 7:   Closed flag (1 = properly closed file)
Bit 6:   Locked flag (1 = file is locked, shown as ">")
Bit 5-4: Unused
Bit 3-0: File type:
          $0 = DEL (deleted/scratched)
          $1 = SEQ (sequential)
          $2 = PRG (program)
          $3 = USR (user)
          $4 = REL (relative)
```

| Byte | Displayed | Meaning                           |
|------|-----------|-----------------------------------|
| $00  | *DEL      | Scratched entry (splat/unclosed)  |
| $80  | DEL       | Closed deleted entry              |
| $81  | SEQ       | Sequential data file              |
| $82  | PRG       | Program file                      |
| $83  | USR       | User-defined data file            |
| $84  | REL       | Relative (random-access) file     |
| $C2  | PRG>      | Locked program file               |
| $01  | *SEQ      | Unclosed sequential (splat file)  |

An asterisk (*) before the type indicates the file was not properly closed
("splat file"), which usually means a write error or interrupted save.

### Data Sector Chain

File data is stored as a linked list of sectors. Each sector has:

```
Byte 0: Track of next sector (0 = this is the last sector)
Byte 1: Sector of next sector (or: number of valid bytes in
         last sector, when byte 0 = 0)
Bytes 2-255: 254 bytes of file data
```

The standard file interleave is **10** — the next sector allocated is typically
10 sectors ahead of the current one, allowing the drive head time to process
data between reads.

### Error Info Block

When present, the error info block appends one byte per sector after all track
data. Each byte encodes the read status:

| Code | Error Type   | Description                              |
|------|-------------|------------------------------------------|
| $01  | (none)      | No error                                 |
| $02  | 20, READ    | Header block descriptor not found        |
| $03  | 21, READ    | No SYNC sequence found                   |
| $04  | 22, READ    | Data block descriptor byte not found     |
| $05  | 23, READ    | Data block checksum error                |
| $06  | 24, WRITE   | Write verify (on format)                 |
| $07  | 25, WRITE   | Write verify error                       |
| $08  | 26, WRITE   | Write-protect on                         |
| $09  | 27, READ    | Header block checksum error              |
| $0A  | 28, WRITE   | Write error (data block too long)        |
| $0B  | 29, READ    | Disk ID mismatch                         |
| $0F  | 74, READ    | Drive not ready                          |

Copy protection schemes deliberately write sectors with specific error codes
(commonly $05 data checksum errors or $09 header checksum errors) that the
game checks at load time to verify an original disk.

---

## 4. Other Disk Formats

### D71 — 1571 Double-Sided Disk

The Commodore 1571 drive (bundled with the C128) reads both sides of a 5.25"
disk. The D71 format is essentially two D64 images concatenated.

| Property            | Value                          |
|---------------------|--------------------------------|
| Tracks              | 70 (35 per side)               |
| Sectors per track   | Same zone layout as D64        |
| Total sectors       | 1,366                          |
| File size           | 349,696 bytes (no errors)      |
| File size w/errors  | 351,062 bytes                  |
| BAM                 | Track 18 (side 1) + Track 53 (side 2) |
| Directory           | Track 18, same as D64          |

The second side's tracks are numbered 36-70, with the same sector-per-track
zones mirrored. The BAM for the second side is stored on track 53, sector 0.
CBM DOS 3.0 on the 1571 supports double-sided operation natively.

### D81 — 1581 3.5" Disk

The Commodore 1581 uses standard 3.5" DD disks with MFM encoding (unlike the
GCR used by 1541/1571).

| Property            | Value                          |
|---------------------|--------------------------------|
| Tracks              | 80 (40 per side, double-sided) |
| Sectors per track   | 40 (logical 256-byte sectors)  |
| Total sectors       | 3,200                          |
| Capacity            | 800 KB (819,200 bytes)         |
| File size           | 819,200 bytes                  |
| BAM                 | Track 40, sectors 1-2          |
| Directory           | Track 40, sector 3+            |
| Encoding            | MFM (not GCR)                  |

**Sector offset formula:**

```
offset = (40 * (track - 1) + sector) * 256
```

The 1581 supports partitions (sub-directories), a feature unique among
Commodore drives of the era.

### G64 — Raw GCR Disk Image

G64 preserves the raw GCR-encoded bit stream of every track, including sync
marks, gaps, and non-standard data. This makes it suitable for preserving
copy-protected disks that use non-standard sector layouts.

**Header structure:**

```
Offset   Size  Description
------   ----  -----------
$0000-07 8     Signature: "GCR-1541"
$0008    1     Version ($00)
$0009    1     Number of tracks (typically $54 = 84 half-tracks)
$000A-0B 2     Maximum track size in bytes (little-endian; standard: 7928)
```

**Track offset table ($000C - $015B):**

84 entries of 4 bytes each (one per half-track), containing absolute file
offsets to track data. A zero offset means no data for that track/half-track.

**Speed zone table ($015C - $01AB):**

84 entries of 4 bytes each. Values 0-3 indicate the speed zone for the entire
track. Values > 3 are file offsets to a per-sector speed zone block.

**Track data format:**

```
Bytes 0-1:  Actual track data size (little-endian)
Bytes 2+:   Raw GCR data (padded to max track size)
```

**Standard track sizes by zone:**

| Tracks | GCR Bytes/Track | Speed Zone |
|--------|-----------------|------------|
| 1-17   | 7,692           | 3          |
| 18-24  | 7,142           | 2          |
| 25-30  | 6,666           | 1          |
| 31-35  | 6,250           | 0          |

### NIB — Nibble-Level Disk Image

The NIB format was the standard output of MNIB (Markus Brenner's parallel-cable
nibbler). Each track is stored as a fixed-length block of raw GCR bytes,
typically 8,192 bytes per track position, padded with sync data if shorter.

**Key characteristics:**

- Preserves raw bit stream including sync marks, bad sectors, and non-standard
  formats
- Does **not** store per-track density/speed zone information (assumes standard
  zone densities)
- Cannot represent variable-density tracks or half-tracks reliably
- Has been largely superseded by G64 for new captures
- Bulk of the community's non-D64 archive from 2000-2010 is in NIB format

**NIB vs. raw flux capture:**

NIB reads decoded bits through the 1541's data separator. True flux-level
capture devices (KryoFlux, SuperCard Pro) measure exact microsecond timing
between magnetic transitions, bypassing the drive electronics entirely.

---

## 5. Tape Formats

### TAP File Format — Raw Tape Image

The TAP format stores the raw pulse timing data from a Commodore Datasette
recording. Each byte after the header represents the duration of one pulse.

**Header structure (20 bytes):**

```
Offset    Size  Description
------    ----  -----------
$0000-0B  12    Signature: "C64-TAPE-RAW"
$000C     1     TAP version ($00 = original, $01 = updated)
$000D-0F  3     Reserved (future expansion)
$0010-13  4     Data size in bytes (little-endian, excludes header)
```

**Pulse encoding (version $00):**

Each data byte represents a pulse length. The actual duration in seconds:

```
duration = (8 * byte_value) / clock_frequency
```

Where clock frequency is:
- PAL:  985,248 Hz
- NTSC: 1,022,730 Hz

A byte value of $00 indicates an overflow — a pulse too long to encode in one
byte (> 255 * 8 = 2040 cycles).

**Pulse encoding (version $01):**

Byte value $00 is re-coded as an escape: it is followed by three bytes
containing the actual pulse duration in CPU clock cycles (little-endian, 24-bit
value). This provides single-cycle precision for long pauses and leader tones.

All other byte values encode pulses identically to version $00.

### Standard Commodore Tape Encoding (ROM Loader)

The C64's built-in ROM tape loader uses three distinct pulse durations:

| Pulse  | NTSC Period | PAL Period | TAP Value | Frequency |
|--------|-------------|------------|-----------|-----------|
| Short  | ~176 us     | ~183 us    | $30       | ~2840 Hz  |
| Medium | ~256 us     | ~266 us    | $42       | ~1953 Hz  |
| Long   | ~336 us     | ~349 us    | $56       | ~1488 Hz  |

**Bit encoding (pulse pairs):**

| Pulse Pair      | Meaning           |
|-----------------|-------------------|
| Short + Medium  | Binary 0          |
| Medium + Short  | Binary 1          |
| Long + Medium   | New-data marker   |
| Long + Short    | End-of-data marker|

**Byte encoding:**

Each byte is transmitted as 20 pulses (10 pulse pairs):

```
1. New-data marker (Long + Medium)
2. Bit 0 (LSB first)
3. Bit 1
4. Bit 2
5. Bit 3
6. Bit 4
7. Bit 5
8. Bit 6
9. Bit 7
10. Parity bit (odd parity: 1 XOR bit0 XOR bit1 ... XOR bit7)
```

Transmission rate: approximately 8.96 ms per byte (~112 bytes/second).

**Tape data block structure:**

Each file on tape consists of:

1. **Sync leader** — continuous short pulses
   - 10 seconds for the first block
   - 2 seconds for subsequent blocks
2. **Countdown sequence** — bytes $89 down to $81 (first copy) or $09 to $01
   (second copy)
3. **Data block** — 192 bytes of payload
4. **Checksum** — 1 byte (XOR of $00 with all payload bytes)
5. **Inter-record gap** — 1 long pulse + 60 short pulses
6. **Repeat** — entire block is recorded twice for error detection

**Header block format (first 192-byte block of a file):**

```
Byte    Size  Description
------  ----  -----------
$00     1     Header type:
                $01 = Relocatable BASIC program
                $02 = SEQ data block
                $03 = Non-relocatable ML program
                $04 = SEQ file header
                $05 = End-of-tape marker
$01-02  2     Start address (little-endian)
$03-04  2     End address (little-endian)
$05-14  16    Filename (PETSCII, padded with $20)
$15-BF  171   Extended filename / padding ($20)
```

### T64 — Tape Container Format

The T64 format was created by Miha Peternel for the C64S emulator. Unlike TAP
(which stores raw pulses), T64 is a container holding one or more extracted
PRG-like files with metadata.

**File header (64 bytes, $0000-$003F):**

```
Offset    Size  Description
------    ----  -----------
$00-$1F   32    Tape signature (e.g., "C64S tape image file\0...")
                Must begin with "C64" for identification
$20-$21   2     Tape version ($0100 or $0101, little-endian)
$22-$23   2     Maximum number of directory entries (little-endian)
$24-$25   2     Number of used entries (little-endian)
$26-$27   2     Reserved
$28-$3F   24    Tape container name (PETASCII, padded with $20)
```

**Directory entries (32 bytes each, starting at $0040):**

```
Offset    Size  Description
------    ----  -----------
$00       1     C64S file type:
                  $00 = Free (empty) entry
                  $01 = Normal tape file
                  $02 = Tape file with header (reserved)
                  $03 = Memory snapshot (FRZ), uncompressed
$01       1     1541 file type ($82 = PRG, $81 = SEQ, etc.)
$02-$03   2     Start address / load address (little-endian)
$04-$05   2     End address (little-endian)
$06-$07   2     Reserved
$08-$0B   4     Offset into container file where data begins
                (little-endian, absolute from file start)
$0C-$0F   4     Reserved
$10-$1F   16    C64 filename (PETASCII, padded with $20, not $A0)
```

**Data section:**

File data begins immediately after the directory area. Each entry's data is
located at the absolute file offset specified in bytes $08-$0B of its directory
entry. The data length is calculated as (end address - start address).

**Known issues:**

Many T64 files in the wild do not strictly follow the specification. Common
problems include incorrect "used entries" counts and invalid end addresses.
Emulators typically apply heuristics to handle malformed T64 files.

### Turbo Tape Formats

Almost every commercially released C64 tape game uses a custom "turbo loader"
to achieve faster loading speeds than the standard ROM loader (~112 bytes/sec).

**How turbo loading works:**

1. A small boot loader is saved in standard ROM format
2. The user loads and runs the boot loader normally
3. The boot loader installs a custom tape routine in RAM
4. Subsequent data blocks use the custom encoding

**Common turbo encoding (e.g., Turbotape):**

| Pulse  | Duration | TAP Value | Meaning |
|--------|----------|-----------|---------|
| Short  | ~211 us  | $1A       | Binary 0|
| Long   | ~324 us  | $28       | Binary 1|

Key differences from standard encoding:
- Only 2 pulse types (not 3)
- 1 pulse per bit (not 2 pulses per bit)
- No parity bit
- Shorter sync leaders
- Speeds of 2-6x faster than standard

Different publishers used different turbo schemes (Novaload, Pavloda,
Freeload, Cyberload, etc.), each with its own pulse timings and protocols.

---

## 6. Cartridge Formats

### CRT File Format

The CRT format was defined by Per Hakan Sundell for the CCS64 emulator and has
become the standard cartridge image format across all major C64 emulators.

**File header ($0000-$003F, 64 bytes):**

```
Offset    Size  Description
------    ----  -----------
$0000-0F  16    Signature: "C64 CARTRIDGE   " (padded with spaces)
$0010-13  4     Header length (big-endian, always $00000040 = 64)
$0014-15  2     CRT version (big-endian, currently $0100 = v1.0)
$0016-17  2     Hardware type ID (big-endian, see table below)
$0018     1     EXROM line state (0 = inactive/low, 1 = active/high)
$0019     1     GAME line state (0 = inactive/low, 1 = active/high)
$001A-1F  6     Reserved (zero-filled)
$0020-3F  32    Cartridge name (null-padded ASCII)
```

**NOTE:** All multi-byte values in the CRT header and CHIP packets use
**big-endian** byte order, unlike the C64's native little-endian format.

### CHIP Packets

Following the 64-byte header, the CRT file contains one or more CHIP packets,
each holding a ROM/RAM bank image.

**CHIP packet structure:**

```
Offset    Size  Description
------    ----  -----------
$0000-03  4     Signature: "CHIP"
$0004-07  4     Total packet length (big-endian, includes this header)
$0008-09  2     Chip type (big-endian):
                  $0000 = ROM
                  $0001 = RAM (no data follows)
                  $0002 = Flash ROM
$000A-0B  2     Bank number (big-endian)
$000C-0D  2     Load address (big-endian, $8000 or $A000/$E000)
$000E-0F  2     ROM image size (big-endian, typically $2000 or $4000)
$0010+    n     ROM data (size specified above)
```

### Standard 8K Cartridge (Type 0)

The simplest cartridge type. One 8KB ROM mapped at $8000-$9FFF.

- EXROM = 0, GAME = 1
- Single CHIP packet: bank 0, load address $8000, size $2000
- The cartridge ROM must contain valid reset and NMI vectors at the "signature"
  area: bytes $8004-$8008 contain "CBM80" ($C3 $C2 $CD $38 $30)
- Cold-start vector at $8000-$8001 (little-endian)
- Warm-start/NMI vector at $8002-$8003

### Standard 16K Cartridge (Type 0)

Two 8KB banks: ROML at $8000-$9FFF and ROMH at $A000-$BFFF.

- EXROM = 0, GAME = 0
- Typically one CHIP packet with size $4000 (16KB) at $8000
- Or two CHIP packets: one at $8000 ($2000) and one at $A000 ($2000)

### Ultimax Mode (GAME = 0, EXROM = 1)

A special configuration where ROMH is mapped at $E000-$FFFF (replacing the
KERNAL) and ROML at $8000-$9FFF. Most RAM is inaccessible. Used by some
early cartridge games (e.g., Zaxxon).

### Cartridge Hardware Type IDs

| ID | Name                    | Size       | EXROM | GAME | Banks   |
|----|-------------------------|------------|-------|------|---------|
| 0  | Normal cartridge        | 8K/16K     | 0     | 0/1  | 1       |
| 1  | Action Replay           | 32K        | 0     | 0    | 4x8K    |
| 2  | KCS Power Cartridge     | 16K        | 0     | 0    | 2x8K    |
| 3  | Final Cartridge III     | 64K        | 1     | 1    | 4x16K   |
| 4  | Simons' BASIC           | 16K        | 0     | 1    | 2x8K    |
| 5  | Ocean Type 1            | 128-512K   | 0     | 0    | 16-64x8K|
| 6  | Expert Cartridge        | 8K         | 1     | 1    | 1x8K    |
| 7  | Fun Play, Power Play    | 128K       | 0     | 0    | 16x8K   |
| 8  | Super Games             | 64K        | 0     | 0    | 4x16K   |
| 9  | Atomic Power            | 32K        | 0     | 0    | 4x8K    |
| 10 | Epyx Fastload           | 8K         | 1     | 1    | 1x8K    |
| 11 | Westermann Learning     | 16K        | 0     | 0    | 1x16K   |
| 12 | Rex Utility             | 8K         | 0     | 1    | 1x8K    |
| 13 | Final Cartridge I       | 16K        | 1     | 1    | 1x16K   |
| 14 | Magic Formel            | 64K        | 0     | 0    | 8x8K    |
| 15 | C64 Game System (System 3)| 512K     | 1     | 0    | 64x8K   |
| 16 | WarpSpeed               | 16K        | 1     | 1    | 1x16K   |
| 17 | Dinamic                 | 128K       | 1     | 0    | 16x8K   |
| 18 | Zaxxon / Super Zaxxon   | 20K        | 1     | 1    | special |
| 19 | Magic Desk / Domark / HES| 32-128K   | 1     | 0    | 4-16x8K |
| 20 | Super Snapshot v5       | 64K        | 1     | 1    | 4x16K   |
| 21 | Comal-80                | 64K        | 1     | 1    | 4x16K   |
| 22 | Structured BASIC        | 16K        | 1     | 0    | 2x8K    |
| 23 | Ross                    | 16-32K     | 1     | 1    | 2-4x8K  |
| 32 | EasyFlash               | 1M         | 1     | 0    | 64x16K  |

### Bank-Switching Types (Details)

**Ocean Type 1 (ID 5):**

Bank switching via writes to $DE00. The lowest 6 bits select the bank number
(0-63). Bit 7 is always set. Each CHIP packet is 8KB at $8000-$9FFF.
Games using 256KB or 512KB: banks are mapped to ROML only.

```
STA $DE00    ; bits 0-5 = bank number, bit 7 = 1
```

**Magic Desk / Domark / HES Australia (ID 19):**

Similar to Ocean but bit 7 controls ROM enable:
- Bit 7 = 0: ROM bank is enabled at $8000-$9FFF
- Bit 7 = 1: ROM is disabled, RAM at $8000-$9FFF

```
LDA #$03     ; select bank 3, ROM enabled
STA $DE00
LDA #$80     ; disable cartridge ROM
STA $DE00
```

**EasyFlash (ID 32):**

Two I/O registers for flexible bank switching:

| Register | Address | Function                                      |
|----------|---------|-----------------------------------------------|
| Bank     | $DE00   | Bits 0-5: bank number (0-63)                  |
| Control  | $DE02   | Bit 0: GAME line, Bit 1: EXROM line, Bit 2: LED|

64 banks of 16KB each (8KB ROML at $8000 + 8KB ROMH at $A000 or $E000).
Total: 1 MB of flash storage. Supports mixed ROM/RAM configurations and
in-system flash programming.

```
LDA #$05       ; select bank 5
STA $DE00
LDA #%00000100 ; GAME=0, EXROM=0, LED=1
STA $DE02
```

**C64 Game System / System 3 (ID 15):**

Bank switching via reads from $DE00+X (not writes):

```
LDA $DE00,X   ; X = bank number (0-63)
```

**Dinamic (ID 17):**

Also uses read-based bank switching:

```
LDA $DE00,X   ; X = bank number (0-15)
```

**Action Replay (ID 1):**

Write to $DE00 to control bank and mode:

```
Bit 0-1: Bank number (0-3)
Bit 2:   Freeze mode (active low)
Bit 3:   Disable cartridge
Bit 4:   GAME line
Bit 5:   EXROM line
```

---

## 7. Program Startup

### Boot Sequence: LOAD and RUN

The standard C64 boot sequence for running a program from disk:

```
LOAD "filename",8     ; or LOAD "*",8 for first file
RUN
```

**Step-by-step internals:**

1. BASIC parses `LOAD` and calls KERNAL LOAD routine ($FFD5)
2. KERNAL opens a channel to device 8 via IEC serial bus
3. KERNAL sends filename to 1541, which locates it in the directory
4. 1541 sends the file data byte-by-byte over IEC serial
5. KERNAL reads first 2 bytes as load address
6. Without `,1`: loads starting at $0801 (ignores embedded address)
   With `,1`: loads to the address specified by the 2-byte header
7. Remaining bytes are deposited sequentially into memory
8. KERNAL updates BASIC end-of-program pointers ($2D-$2E)
9. `RUN` resets BASIC variable space and begins executing from the
   first line

### Auto-Boot Techniques

**Method 1: BASIC stub with SYS (standard)**

The most common approach. A single BASIC line `10 SYS 2061` transfers control
to machine language immediately following the stub.

**Method 2: Wedge the READY vector**

Hook the BASIC "ready" vector at $0302-$0303 to point to custom initialization
code. When BASIC finishes loading and tries to print "READY.", it instead jumps
to your code. This achieves auto-run without the user typing RUN.

```asm
        ; At the end of your loader, before the main program:
        LDA #<start
        STA $0302
        LDA #>start
        STA $0303
        RTS         ; Return to BASIC, which calls "READY" -> your code
```

**Method 3: Exploit the LOAD end-address**

Some disk loaders overwrite $0302-$0303 as part of the loaded data, achieving
the same effect as Method 2 but embedded in the file itself.

**Method 4: Cartridge auto-start**

Cartridges bypass BASIC entirely. The KERNAL checks $8004-$8008 for the
"CBM80" signature on reset. If found, it jumps through the cold-start vector
at $8000 instead of initializing BASIC.

### BASIC Stub Setup (Detailed)

To create a proper BASIC stub in your assembler source:

```asm
        * = $0801           ; Start at BASIC program area

        ; BASIC line: 10 SYS 2061
        .word $080D         ; Pointer to next line (address after this line)
        .word $000A         ; Line number (10)
        .byte $9E           ; SYS token
        .byte " 2061",0    ; Address as PETSCII string + null terminator

        ; End-of-BASIC marker
        .word $0000         ; Null pointer = end of program

        ; ML entry point at $080D (2061)
start:  SEI
        ; ... your code here ...
```

The next-line pointer must accurately point to the byte immediately after the
null terminator. An incorrect pointer causes LIST to display garbled output but
does not affect RUN.

### Typical ML Initialization Sequence

A standard machine language program initializes the hardware in this order:

```asm
start:
        SEI                 ; Disable interrupts

        ; 1. Set up memory configuration
        LDA #$35            ; RAM under BASIC + KERNAL visible, I/O visible
        STA $01             ; (or #$34 for all-RAM + I/O)

        ; 2. Disable CIA interrupts
        LDA #$7F
        STA $DC0D           ; CIA1: disable all interrupt sources
        STA $DD0D           ; CIA2: disable all interrupt sources
        LDA $DC0D           ; Acknowledge any pending CIA1 interrupt
        LDA $DD0D           ; Acknowledge any pending CIA2 interrupt

        ; 3. Configure VIC-II
        LDA #$1B            ; Default: 25 rows, screen on, text mode
        STA $D011
        LDA #$08            ; Default: 40 columns, no multicolor
        STA $D016
        LDA #$14            ; Screen at $0400, charset at $1000
        STA $D018

        ; 4. Set up raster interrupt
        LDA #<irq_handler
        STA $FFFE           ; IRQ vector (hardware vector, not KERNAL)
        LDA #>irq_handler
        STA $FFFF
        LDA #$00            ; Raster line for interrupt
        STA $D012
        LDA $D011
        AND #$7F            ; Clear bit 8 of raster compare
        STA $D011
        LDA #$01
        STA $D01A           ; Enable raster interrupt

        ; 5. Enable interrupts and enter main loop
        CLI
        JMP main_loop
```

---

## 8. Hardcore Details

### Exact BASIC Tokenized Line Format

BASIC programs are stored in memory as a linked list of tokenized lines,
starting at $0801 by default.

**Line structure:**

```
Offset  Size  Description
------  ----  -----------
+0      2     Pointer to next line (little-endian absolute address)
               $0000 = end of program
+2      2     Line number (little-endian, 0-63999)
+4      n     Tokenized line content:
               - Keywords are replaced by single-byte tokens ($80-$CB)
               - Everything else stored as literal PETSCII bytes
               - Quoted strings are NOT tokenized
               - Numbers are stored as PETSCII digit characters
+4+n    1     Null terminator ($00)
```

**Worked example:** `10 PRINT "HELLO":GOTO 10`

```
Address  Byte  Meaning
-------  ----  -------
$0801    $15   Low byte of pointer to next line ($0815)
$0802    $08   High byte of pointer to next line
$0803    $0A   Low byte of line number (10)
$0804    $00   High byte of line number
$0805    $99   PRINT token
$0806    $20   Space (PETSCII)
$0807    $22   Quote mark
$0808    $48   'H'
$0809    $45   'E'
$080A    $4C   'L'
$080B    $4C   'L'
$080C    $4F   'O'
$080D    $22   Quote mark
$080E    $3A   ':' (colon — statement separator)
$080F    $89   GOTO token
$0810    $20   Space
$0811    $31   '1'
$0812    $30   '0'
$0813    $00   Null terminator (end of line)
$0814    $00   ) End-of-program marker
$0815    $00   ) (null pointer)
```

**Key pointers maintained by BASIC:**

| Address     | Name       | Description                        |
|-------------|------------|------------------------------------|
| $002B-$002C | TXTTAB     | Start of BASIC text ($0801)        |
| $002D-$002E | VARTAB     | Start of variables (end of program)|
| $002F-$0030 | ARYTAB     | Start of arrays                    |
| $0031-$0032 | STREND     | End of arrays                      |
| $0033-$0034 | FRETOP     | Bottom of string storage           |
| $0037-$0038 | MEMSIZ     | Top of BASIC memory                |

### Complete BASIC V2.0 Token Table

All tokens used by Commodore BASIC V2.0 on the C64:

**Statement tokens ($80-$A2):**

| Token | Keyword  | Token | Keyword  | Token | Keyword  |
|-------|----------|-------|----------|-------|----------|
| $80   | END      | $8B   | IF       | $96   | DEF      |
| $81   | FOR      | $8C   | RESTORE  | $97   | POKE     |
| $82   | NEXT     | $8D   | GOSUB    | $98   | PRINT#   |
| $83   | DATA     | $8E   | RETURN   | $99   | PRINT    |
| $84   | INPUT#   | $8F   | REM      | $9A   | CONT     |
| $85   | INPUT    | $90   | STOP     | $9B   | LIST     |
| $86   | DIM      | $91   | ON       | $9C   | CLR      |
| $87   | READ     | $92   | WAIT     | $9D   | CMD      |
| $88   | LET      | $93   | LOAD     | $9E   | SYS      |
| $89   | GOTO     | $94   | SAVE     | $9F   | OPEN     |
| $8A   | RUN      | $95   | VERIFY   | $A0   | CLOSE    |
|       |          |       |          | $A1   | GET      |
|       |          |       |          | $A2   | NEW      |

**Secondary keyword / operator tokens ($A3-$CB):**

| Token | Keyword  | Token | Keyword  | Token | Keyword  |
|-------|----------|-------|----------|-------|----------|
| $A3   | TAB(     | $B1   | >        | $BF   | SIN      |
| $A4   | TO       | $B2   | =        | $C0   | TAN      |
| $A5   | FN       | $B3   | <        | $C1   | ATN      |
| $A6   | SPC(     | $B4   | SGN      | $C2   | PEEK     |
| $A7   | THEN     | $B5   | INT      | $C3   | LEN      |
| $A8   | NOT      | $B6   | ABS      | $C4   | STR$     |
| $A9   | STEP     | $B7   | USR      | $C5   | VAL      |
| $AA   | +        | $B8   | FRE      | $C6   | ASC      |
| $AB   | -        | $B9   | POS      | $C7   | CHR$     |
| $AC   | *        | $BA   | SQR      | $C8   | LEFT$    |
| $AD   | /        | $BB   | RND      | $C9   | RIGHT$   |
| $AE   | ^        | $BC   | LOG      | $CA   | MID$     |
| $AF   | AND      | $BD   | EXP      | $CB   | GO       |
| $B0   | OR       | $BE   | COS      |       |          |

**Special token:**

| Token | Keyword |
|-------|---------|
| $FF   | (pi)    |

The $FF token represents the pi constant character. It is not technically a
BASIC keyword token but is treated specially by the tokenizer.

Note: Inside quoted strings and REM statements, bytes $80-$FF are treated as
literal PETSCII characters, not tokens.

### D64 Interleave Patterns

The 1541 uses sector interleaving to account for the time the drive needs to
process each sector before reading the next.

**Standard interleave values:**

| Context            | Interleave | Reason                                   |
|--------------------|------------|------------------------------------------|
| File data          | 10         | Time for drive CPU to decode GCR + send  |
| Directory          | 3          | Directory sectors are small, fast to parse|
| BAM                | N/A        | Single sector, always at 18/0            |

**File data sector chain example (track 1):**

```
Sector sequence: 0, 10, 20, 9, 19, 8, 18, 7, 17, 6, 16, 5, 15, 4, 14, 3, 13, 2, 12, 1, 11
```

After sector 0, the next is sector 10 (0+10). After sector 10, the next is
sector 20 (10+10). After sector 20, the next wraps around: (20+10) mod 21 = 9.

**Why interleave 10?**

The 1541 has a 1 MHz 6502 CPU with 2KB RAM. After reading a 256-byte sector,
it must:
1. Decode 325 bytes of GCR data into 260 bytes (256 data + header/checksum)
2. Verify the checksum
3. Transfer 256 bytes to the C64 over the serial bus
4. Locate and lock onto the next sector's sync mark

This takes approximately 10 sector times at the disk rotation speed. An
incorrect interleave forces the drive to wait for a full disk revolution
(200ms) to catch the missed sector.

**Optimal interleave with fast loaders:**

Fast loaders that use parallel transfer or burst mode can reduce the interleave.
JiffyDOS uses interleave 6. Some turbo loaders achieve interleave 1 (no
skipping) by reading sectors on the fly without waiting for serial transfer.

### GCR Encoding Details

#### The 4-to-5 Bit Conversion Table

The 1541 encodes every 4 bits (nybble) of data as 5 bits on disk. The 5-bit
codes are chosen so that no more than two consecutive 0-bits ever appear,
preventing the read head from losing sync with the bit stream.

| Nybble (4-bit) | GCR (5-bit) | Hex  |
|----------------|-------------|------|
| 0000 ($0)      | 01010       | $0A  |
| 0001 ($1)      | 01011       | $0B  |
| 0010 ($2)      | 10010       | $12  |
| 0011 ($3)      | 10011       | $13  |
| 0100 ($4)      | 01110       | $0E  |
| 0101 ($5)      | 01111       | $0F  |
| 0110 ($6)      | 10110       | $16  |
| 0111 ($7)      | 10111       | $17  |
| 1000 ($8)      | 01001       | $09  |
| 1001 ($9)      | 11001       | $19  |
| 1010 ($A)      | 11010       | $1A  |
| 1011 ($B)      | 11011       | $1B  |
| 1100 ($C)      | 01101       | $0D  |
| 1101 ($D)      | 11101       | $1D  |
| 1110 ($E)      | 11110       | $1E  |
| 1111 ($F)      | 10101       | $15  |

**Encoding process:**

1. Take 4 bytes of data (32 bits)
2. Split into 8 nybbles
3. Convert each nybble to 5 GCR bits using the table above
4. Pack the 40 GCR bits into 5 bytes
5. Write the 5 bytes to disk

The least common multiple of 4 (data bits per nybble) and 8 (bits per byte) is
40, so the encoding operates on groups of 4 data bytes -> 5 GCR bytes.

**Data expansion:** 256 data bytes become 320 GCR bytes (25% expansion).

#### On-Disk Sector Format

Each sector on a 1541 disk consists of the following elements:

```
Element            Size (bytes)  Description
-----------------  ------------  -----------
Header SYNC        5             40 "1" bits (FF FF FF FF FF in GCR)
Header data        10            8 data bytes GCR-encoded to 10 bytes
Header gap         9             $55 bytes (never read by drive)
Data SYNC          5             40 "1" bits (FF FF FF FF FF in GCR)
Data block         325           260 data bytes GCR-encoded to 325 bytes
Tail gap           4-19          Variable padding (never read)
```

**Header block (8 bytes before GCR encoding):**

```
Byte  Description
----  -----------
0     Block ID: $08 (header block identifier)
1     Header checksum (XOR of bytes 2-5)
2     Sector number
3     Track number
4     Format ID byte 2 (from disk format command)
5     Format ID byte 1
6     $0F padding
7     $0F padding
```

**Data block (260 bytes before GCR encoding):**

```
Byte    Description
------  -----------
0       Block ID: $07 (data block identifier)
1-256   256 bytes of sector data
257     Data checksum (XOR of bytes 1-256)
258     $00 padding
259     $00 padding
```

**SYNC marks:**

The SYNC mark consists of at least 40 consecutive "1" bits. This pattern is
impossible in valid GCR-encoded data (the encoding guarantees no more than 8
consecutive 1-bits in normal data). The drive hardware detects the SYNC mark
and uses it to byte-align the read circuitry.

#### Speed Zones and Timing

The disk rotates at 300 RPM (5 revolutions per second, 200ms per revolution).
The 1541 uses 4 speed zones to pack more data on outer tracks:

| Zone | Tracks | Sectors | Bit Rate | Bytes/Track | us/Byte (GCR) |
|------|--------|---------|----------|-------------|----------------|
| 3    | 1-17   | 21      | Fastest  | ~7,692      | ~26            |
| 2    | 18-24  | 19      | Fast     | ~7,142      | ~28            |
| 1    | 25-30  | 18      | Slow     | ~6,666      | ~30            |
| 0    | 31-35  | 17      | Slowest  | ~6,250      | ~32            |

At the fastest rate (zone 3), a new raw GCR byte arrives every ~26
microseconds. The 1 MHz 6502 CPU in the 1541 has only ~26 cycles to process
each byte — an extremely tight timing constraint that dominates the drive
firmware design.

### 1541 File Loading Internals

#### IEC Serial Bus Protocol

The C64 communicates with the 1541 over a simplified serial version of the
IEEE-488 bus, using just 5 signal lines:

| Pin | Signal | Function                                    |
|-----|--------|---------------------------------------------|
| 1   | SRQ    | Service Request (active low, active on C128) |
| 2   | GND    | Ground                                       |
| 3   | ATN    | Attention (active low, controlled by C64)    |
| 4   | CLK    | Clock (active low, active talker drives it)  |
| 5   | DATA   | Data (active low, active talker drives it)   |
| 6   | RESET  | Reset (active low)                           |

All signal lines use open-collector (active-low) logic with pull-up resistors.
Multiple devices share the bus via wired-AND: any device can pull a line low,
but all must release it for it to go high.

**CIA2 Port A ($DD00) bit assignments:**

```
Bit 3: ATN OUT    (directly active)
Bit 4: CLK OUT    (directly active)
Bit 5: DATA OUT   (directly active)
Bit 6: CLK IN     (directly active)
Bit 7: DATA IN    (directly active)
```

#### Byte Transfer Protocol

Each byte is transmitted serially, LSB first, using a handshake protocol:

**Initialization:**
1. Sender holds CLK low (not ready to send)
2. Receiver holds DATA low (not ready to receive)
3. Sender releases CLK (ready to send)
4. All receivers release DATA when ready

**Bit transfer (repeated 8 times):**
1. Sender pulls CLK low, places data bit on DATA line
2. After minimum hold time, sender releases CLK (data valid signal)
3. Receiver samples DATA while CLK is high
4. Sender pulls CLK low again (data invalid)

**Timing:**

| C64 Model | Hold Time | Effective Speed | Reason               |
|-----------|-----------|-----------------|----------------------|
| VIC-20    | 20 us     | ~2 KB/sec       | 6522 VIA bug         |
| C64       | 60 us     | ~400 bytes/sec  | VIC-II DMA steals    |

The C64's VIC-II chip halts the CPU for approximately 40 us every ~500 us
(during "bad lines" for sprite/character DMA). This makes the 20 us timing
window of the VIC-20 protocol unreliable, so Commodore increased it to 60 us,
severely reducing throughput.

**EOI (End Or Identify) signaling:**

Rather than a dedicated control line, the sender signals "last byte" by
delaying the start of transmission by more than 200 us after receivers
indicate readiness. Receivers acknowledge EOI by briefly pulsing DATA low
for 60+ us.

**ATN (Attention) handling:**

The controller (C64) can interrupt any transfer by pulling ATN low. All devices
must respond within 1000 us by pulling DATA low. The controller then sends
command bytes (device address + channel operations) using the standard byte
protocol.

#### The LOAD Process Step by Step

```
C64 (KERNAL)                    1541 (Drive CPU)
============                    ================
1. Pull ATN, send LISTEN + device address
                                Acknowledge, enter listener mode

2. Send OPEN + secondary address + filename
                                Store filename, acknowledge

3. Release ATN
                                Search directory for filename
                                Open file, read first sector into buffer

4. Pull ATN, send TALK + device address
                                Enter talker mode

5. Release ATN (bus turnaround)
                                Take control of CLK line

6. Receive bytes:
   Loop:
     Wait for CLK release       Place byte on DATA, toggle CLK
     Sample DATA on CLK edge    (read next byte from buffer)
     If EOI: acknowledge        (if buffer empty: read next sector)
   Until EOI received           Send last byte with EOI delay

7. Pull ATN, send UNTALK
                                Release bus, close channel
```

#### Fast Loaders

Fast loaders bypass the slow KERNAL serial protocol by installing custom
transfer code on both the C64 and the 1541 (which has its own 6502 CPU
and 2KB RAM). Common techniques:

1. **Parallel transfer:** Use the VIA user port lines for 8-bit parallel
   transfer, achieving near-maximum disk read speed (~4 KB/sec)

2. **2-bit protocol:** Send 2 bits at a time using CLK and DATA as a
   2-bit bus (4 transfers per byte instead of 8). Used by SpeedDOS,
   DolphinDOS, etc.

3. **Burst mode (1571/1581):** The drive sends bytes using hardware
   shift registers, achieving ~3.5 KB/sec without custom drive code

4. **Custom GCR decoding:** Decode sectors in drive RAM without
   transferring to C64, reducing seek overhead. Combined with optimized
   interleave.

5. **Track caching:** Read an entire track into drive RAM (or extended
   RAM with expansions like RAMBOard), then transfer all sectors at once.

### Custom Disk Formats

Copy-protected games often use non-standard disk formats that break
assumptions made by standard copy programs:

**Common protection techniques:**

- **Extra tracks (36-40):** Standard DOS only formats tracks 1-35.
  Data hidden on tracks 36-40 is invisible to normal copy.

- **Half-tracks:** The 1541 stepper motor can position the head between
  standard tracks (e.g., track 17.5). Data written here is missed by
  track-by-track copiers.

- **Non-standard sector counts:** Writing more or fewer sectors per track
  than the standard layout (e.g., 22 sectors on track 1 instead of 21).

- **Modified GCR data:** Writing invalid GCR patterns that decode to
  specific "illegal" nybble values. The protection check reads the raw
  GCR and verifies the expected patterns.

- **Sync mark manipulation:** Changing the length of sync marks or
  placing sync marks at non-standard positions.

- **Deliberate errors:** Writing sectors with intentional checksum errors
  (error codes $05/$09). The protection code reads the sector and expects
  the specific error. A bit-copy produces a valid sector, failing the check.

- **Density mismatches:** Writing a track at a different speed zone than
  normal, causing standard sector reads to fail while custom code
  compensates.

- **Fat tracks:** Writing the same data to multiple adjacent half-tracks,
  making it readable from any of them but confusing copiers that read
  each half-track independently.

### REL File Structure

REL (relative) files provide random-access record-oriented storage on the
1541. Unlike PRG/SEQ files which are purely sequential, REL files allow
direct access to any record by number.

#### Structure Overview

A REL file consists of three components:
1. **Data sectors** — linked list of sectors containing record data
2. **Side-sector blocks** — index sectors mapping record positions to
   track/sector locations
3. **Directory entry** — contains side-sector pointer and record length

#### Side-Sector Block Format (256 bytes)

```
Offset    Size  Description
------    ----  -----------
$00-$01   2     Track/sector of next side-sector block
                ($00/$00 = last block in chain)
$02       1     Side-sector number (0-5)
$03       1     Record length (same as directory entry byte $17)
$04-$0F   12    Track/sector pointers to all side-sector blocks
                (6 entries of 2 bytes each; $00/$00 = unused)
$10-$FF   240   Track/sector pointers to 120 data sectors
                (120 entries of 2 bytes each; $00/$00 = unused)
```

Each side-sector block can reference up to 120 data sectors. With 6 side-sector
blocks maximum, a REL file can reference up to 720 data sectors.

#### Constraints

| Property                  | Value                        |
|---------------------------|------------------------------|
| Maximum record length     | 254 bytes                    |
| Maximum side-sector blocks| 6                            |
| Data sectors per SS block | 120                          |
| Maximum data sectors      | 720                          |
| Maximum data bytes        | 720 x 254 = 182,880         |
| Maximum file size (total) | ~183 KB (data + side-sectors)|
| Maximum records           | 65,535 (theoretical)         |

Records are stored sequentially in data sectors, spanning sector boundaries.
Partially filled records are padded with null bytes ($00). DOS considers the
last non-null byte in a record as the end of valid data.

### Turbo Loader Bypass and Tape Protection

**Standard tape protection schemes:**

- **Modified pilot tones:** Changing the leader frequency or length
- **Custom byte encoding:** Using non-standard pulse pair assignments
- **Encrypted payloads:** Decryption key embedded in the loader
- **Timing-sensitive checks:** Code that measures exact pulse timing

**Novaload (Rob Hubbard's loader):**

One of the most sophisticated commercial turbo loaders. Uses variable-speed
encoding where different sections of the tape use different pulse timings,
making static analysis and duplication difficult.

---

## References

### Primary Specifications

- [D64 Format Specification — Peter Schepers](https://ist.uwaterloo.ca/~schepers/formats/D64.TXT)
- [T64 Format Specification — Peter Schepers](https://ist.uwaterloo.ca/~schepers/formats/T64.TXT)
- [TAP Format Specification — Peter Schepers](https://ist.uwaterloo.ca/~schepers/formats/TAP.TXT)
- [G64 Format Specification — Peter Schepers](https://ist.uwaterloo.ca/~schepers/formats/G64.TXT)
- [CRT File Format — Codebase 64 Wiki](https://codebase.c64.org/doku.php?id=base:crt_file_format)
- [The C64 File Formats List — Peter Schepers](https://ist.uwaterloo.ca/~schepers/formats.html)

### Technical References

- [D64 Format Details — unusedino.de](http://unusedino.de/ec64/technical/formats/d64.html)
- [CRT Format Details — unusedino.de](http://unusedino.de/ec64/technical/formats/crt.html)
- [TAP Format Details — unusedino.de](http://unusedino.de/ec64/technical/formats/tap.html)
- [T64 Format Details — unusedino.de](http://unusedino.de/ec64/technical/formats/t64.html)
- [G64 Format Details — unusedino.de](http://unusedino.de/ec64/technical/formats/g64.html)
- [D81 Format Details — unusedino.de](http://www.unusedino.de/ec64/technical/formats/d81.html)

### GCR Encoding and Disk Internals

- [GCR Decoding on the Fly — Linus Akesson](https://www.linusakesson.net/programming/gcr-decoding/index.php)
- [The 1541 Drive and GCR Encoding — C64 Copy Protection Reference](https://www.commodoregames.net/copyprotection/the-1541-drive.asp)
- [How Does the 1541 Drive Work — C64 OS](https://c64os.com/post/howdoes1541work)
- [Fitting 44% More Data on a C64/1541 Floppy Disk — pagetable.com](https://www.pagetable.com/?p=1107)
- [GCR — C64-Wiki](https://www.c64-wiki.com/wiki/GCR)

### IEC Serial Bus Protocol

- [Commodore Peripheral Bus: Standard Serial — pagetable.com](https://www.pagetable.com/?p=1135)
- [Commodore Peripheral Bus: Overview — pagetable.com](https://www.pagetable.com/?p=1018)
- [Serial Port — C64-Wiki](https://www.c64-wiki.com/wiki/Serial_Port)
- [IEC Bus Documentation — J. Derogee (PDF)](https://retro-bobbel.de/zimmers/cbm/programming/serial-bus.pdf)

### BASIC and Program Loading

- [BASIC Token Reference — C64-Wiki](https://www.c64-wiki.com/wiki/BASIC_token)
- [BASIC Tokens Sorted by Value — sta.c64.org](https://sta.c64.org/cbm64basins2.html)
- [Running ML from BASIC — Codebase 64](https://codebase.c64.org/doku.php?id=base:runasmfrombasic)
- [C64 Startup Code In Detail — Bumbershoot Software](https://bumbershootsoft.wordpress.com/2020/11/16/c64-startup-code-in-detail/)
- [Loading Sequential Files — C64 OS](https://c64os.com/post/sequentialloading)

### Tape Encoding

- [Datassette Encoding — C64-Wiki](https://www.c64-wiki.com/wiki/Datassette_Encoding)
- [How Commodore Tapes Work — WAV-PRG](https://wav-prg.sourceforge.io/tape.html)
- [Tape Format — SID Preservation Project](http://sidpreservation.6581.org/tape-format/)
- [A Minimal C64 Datasette Program Loader — pagetable.com](https://www.pagetable.com/?p=964)

### Cartridge Hardware

- [EasyFlash — C64-Wiki](https://www.c64-wiki.com/wiki/EasyFlash)
- [Bank Switching Cartridges — hackup.net](https://www.hackup.net/2019/07/bank-switching-cartridges/)
- [Cartridge Theory of Operation — Luigi Di Fraia](https://luigidifraia.wordpress.com/2021/05/08/commodore-64-cartridges-theory-of-operation-and-ocean-bank-switching-described/)

### Disk Image Formats

- [Disk Image — C64-Wiki](https://www.c64-wiki.com/wiki/Disk_Image)
- [D71 — C64-Wiki](https://www.c64-wiki.com/wiki/D71)
- [Commodore 1571 — C64-Wiki](https://www.c64-wiki.com/wiki/Commodore_1571)
- [Commodore 1581 — C64-Wiki](https://www.c64-wiki.com/wiki/Commodore_1581)
- [Emulation and Archiving — C64 Copy Protection Reference](https://www.commodoregames.net/copyprotection/emulation-archiving.asp)
- [Power64 File Formats Documentation](https://www.infinite-loop.at/Power64/Documentation/Power64-ReadMe/AE-File_Formats.html)
- [VICE Emulator File Formats — VICE Manual](https://vice-emu.sourceforge.io/vice_17.html)

### Books

- *Inside Commodore DOS* — Richard Immers & Gerald Neufeld (1984).
  The definitive reference on 1541 internals, GCR encoding, and CBM DOS.
- *The Anatomy of the 1541 Disk Drive* — Commodore (1984).
  Official Commodore technical reference with full ROM disassembly.
