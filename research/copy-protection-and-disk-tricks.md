# Copy Protection Methods and Advanced Disk Tricks

A comprehensive reference covering the history and technical details of copy protection on the
Commodore 64, from simple bad-sector checks to sophisticated GCR manipulation, plus advanced
disk tricks that pushed the 1541 drive far beyond its intended capabilities.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Standard Disk Protection Methods](#2-standard-disk-protection-methods)
3. [Advanced Protection Techniques](#3-advanced-protection-techniques)
4. [Software Protection](#4-software-protection)
5. [The Cracking Scene](#5-the-cracking-scene)
6. [Drive Programming](#6-drive-programming)
7. [Hardcore Details](#7-hardcore-details)
8. [References](#8-references)

---

## 1. Overview

### The Cat-and-Mouse Game

From the moment software became a commercial product on the Commodore 64, a war began.
Publishers wanted to sell disks that could not be duplicated. Pirates wanted to duplicate
them anyway. The battlefield was a $5 floppy disk spinning at 300 RPM inside a drive that
happened to be a fully programmable computer in its own right.

The story of C64 copy protection is inseparable from the story of the 1541 disk drive.
Unlike the simple tape interface or even contemporary PC floppy controllers, the 1541
contained its own MOS 6502 CPU, 2 KB of RAM, and 16 KB of DOS ROM. It was, in every
meaningful sense, a second computer attached to your computer. This architecture -- designed
for cost savings and flexibility -- accidentally created the most fertile ground for copy
protection innovation in the history of home computing.

### A Timeline of Escalation

**1982-1983: The Early Days**

The first C64 games shipped with minimal or no protection. Commodore's own DOS was slow
and buggy, and the market was still small. The earliest protections were simple: bad sectors
on specific tracks that the game's loader expected to find. If a copy program had "fixed"
the errors, the game would refuse to run. Tools like the standard Commodore disk copier
would faithfully reproduce errors, but that was by accident rather than design.

**1984-1985: The Arms Race Begins**

As the C64 became the best-selling home computer in the world, the stakes rose. Publishers
hired dedicated protection engineers. Electronic Arts introduced fat-track protection.
Epyx developed Vorpal, a custom loader that rewrote the rules of how a 1541 reads data.
The first freezer cartridges appeared, allowing users to dump running programs from memory.
Nibbler programs -- tools that read raw GCR data rather than decoded sectors -- entered the
scene.

**1986-1988: The Golden Age of Protection**

This period saw the most sophisticated protections ever devised for the platform. Harald
Seeley's V-MAX! evolved through seven versions, each more resistant to copying than the
last. Accolade deployed RapidLok, which reformatted the entire disk in a proprietary layout.
TIMEX introduced runtime decryption driven by raster interrupts, so that the game existed
in memory only in encrypted form at any given moment. German publisher Rainbow Arts used
factory index-hole alignment (RADWAR) to create disks that were physically impossible to
reproduce on a consumer drive. Freezer cartridges like the Action Replay and Final Cartridge
III became sophisticated enough to snapshot entire system states, prompting software
developers to add anti-freeze detection to their protection schemes.

**1989-1992: The Final Generation**

By the late 1980s, protection had reached a plateau of complexity. Systems like Rubicon
(an enhanced TIMEX variant) contained 20+ verification routines. Syncr0l0k verified the
precise spacing of sync marks. XELOCK offset the entire disk layout so all tracks appeared
to read from wrong positions. But the C64 market was shrinking. The Amiga and PC were
ascendant. Many publishers began to view elaborate protection as more trouble than it was
worth -- it caused compatibility problems, frustrated legitimate customers, and was
ultimately always defeated.

**1993 onward: The Preservation Era**

With commercial software production for the C64 effectively over, attention shifted to
preservation. Could these protected disks be archived before the magnetic media degraded?
The answer required inventing entirely new tools -- flux-level imaging devices that could
capture the raw magnetic transitions on a disk surface, preserving every nuance that the
original protection engineers had exploited.

### Why the 1541 Made It All Possible

The key insight is that the 1541 was not a dumb peripheral. A protection scheme could send
a custom program to the drive's RAM using M-W (Memory-Write) and M-E (Memory-Execute)
commands, then have that program do things the standard DOS firmware never would:

- Read non-standard track formats
- Execute precisely timed head movements
- Decode proprietary GCR encoding
- Check for physical disk signatures
- Measure timing relationships between tracks

The drive's 2 KB of RAM was a severe constraint -- a single track of GCR data holds roughly
7-8 KB -- but protection engineers turned this limitation into an advantage. Custom decoders
that processed data on-the-fly, never buffering an entire track, were inherently harder to
reverse-engineer than simple read-and-compare schemes.

---

## 2. Standard Disk Protection Methods

### Bad Sectors

The oldest and simplest protection method. During disk mastering, specific sectors were
deliberately corrupted -- missing sync marks, bad checksums, wrong header IDs, or missing
data blocks. The game's loader would attempt to read these sectors and expect specific
error codes from the 1541's DOS error channel (channel 15).

The trick: a standard copier that "fixed" errors during copying would produce sectors
that read without errors, causing the protection check to fail.

**Error codes commonly exploited:**

| Error Code | Meaning | Protection Use |
|------------|---------|----------------|
| 20 | Read error (header block not found) | Header deliberately omitted |
| 21 | Read error (no sync character) | Sync mark stripped from sector |
| 22 | Read error (data block not present) | Data block omitted after header |
| 23 | Read error (checksum error in data) | Data deliberately corrupted |
| 27 | Read error (checksum error in header) | Header bytes tampered |
| 29 | Disk ID mismatch | Sector written with non-matching disk ID |

A protection routine would typically look like:

```
    LDA #$01        ; command channel
    LDX #$08        ; device 8
    LDY #$0F        ; secondary address 15
    JSR $FFB1        ; LISTEN
    ; ... send "B-R" command for protected track/sector ...
    ; ... read error channel ...
    CMP #'2'         ; first digit of error code
    BNE fail
    ; ... check second digit matches expected error ...
```

Bad-sector protection was weak but persistent. It appeared in thousands of titles from
1983 through 1990 and was easily handled by parameter-based copiers.

### Extra Tracks (Tracks 36-40)

The standard 1541 format uses 35 tracks. The stepper motor, however, can physically
reach tracks 36 through 40 (and sometimes 41-42 on well-aligned drives). Protection
engineers stored key data on these extra tracks, knowing that:

1. Standard copiers respected the 35-track firmware limit
2. D64 disk images only stored 35 tracks
3. Many drives had poor alignment at the extreme inner tracks, making exact reproduction
   unreliable

The protection loader would step the head beyond track 35, read the hidden data, and use
it as a decryption key or validation token. This was particularly effective because even
if a copier could step to track 36, the data there might be written at a different speed
zone than the copier expected.

**Commonly used extra-track layouts:**

- Tracks 36-40 at speed zone 0 (17 sectors each): +85 sectors, ~21 KB
- Single authentication track at 36 with custom encoding
- Key data split across tracks 36 and 38 with verification on 37

### Custom Sector Interleave

The standard 1541 DOS uses a sector interleave of 10 -- after reading sector 0, the next
logical sector is physically 10 positions ahead on the track, giving the drive time to
process the data before the next sector arrives under the head. Protection schemes changed
this interleave to non-standard values, or used a completely custom sector ordering that
only their own loader could follow efficiently.

A standard copier reading sectors in numerical order (0, 1, 2, 3...) would take multiple
disk revolutions per track, but would still get all the data. The real protection came
from combining non-standard interleave with non-standard sector counts or modified headers,
so the copier could not even find all sectors.

### Non-Standard Sector Counts

The standard 1541 format defines a fixed number of sectors per track based on the speed
zone:

| Zone | Tracks | Standard Sectors | Bits per Track |
|------|--------|------------------|----------------|
| 3 | 1-17 | 21 | ~7,820 bytes |
| 2 | 18-24 | 19 | ~7,170 bytes |
| 1 | 25-30 | 18 | ~6,300 bytes |
| 0 | 31-35 | 17 | ~6,020 bytes |

Protection schemes could:

- **Add sectors**: Squeeze 22 sectors onto zone 3 tracks by reducing inter-sector gaps.
  The standard gaps are generous; by trimming header gaps from 9 to 2 bytes and
  eliminating trailing gaps, one additional sector could fit per track.
- **Remove sectors**: Use only 15 sectors on a track that normally has 21, with the
  remaining space filled by a signature pattern or protection data.
- **Mix sector sizes**: Use non-256-byte data blocks that the standard DOS cannot parse.

### Half-Track Protection

The 1541's stepper motor moves in discrete steps controlled by two output lines (STP0 and
STP1) on VIA #2. A full track step requires two motor phases. By advancing only one phase,
the head stops at a half-track position -- physically between two normal tracks.

The stepper motor uses a four-phase cycle:

```
Phase:   0  1  2  3  0  1  2  3  ...
Track:  1.0   1.5   2.0   2.5   3.0
```

Protection engineers wrote data at half-track positions (e.g., track 17.5, 35.5). Standard
copiers step in full-track increments and never see this data. Even nibblers that copy
tracks 1-35 in raw GCR miss the half-track content entirely.

Big Five Software and System 3 were notable users of half-track protection. The technique
was particularly effective because:

1. The head picks up interference from adjacent tracks at half-track positions, producing
   a weaker signal with more noise
2. Reading a half-track from a standard full-track position returns a blended signal --
   partial data from both adjacent full tracks, appearing as garbage
3. Even if a copier wrote data at a half-track position, alignment tolerances meant the
   copy might not be readable on a different drive

### Long Tracks

A long track contains more data than physically fits in one revolution of the disk at the
nominal bit rate. How is this possible? By writing the track at a slightly higher bit rate
than the zone normally uses, or by exploiting the tolerance margins of the disk speed.

When a standard nibbler attempts to read a long track, it starts reading at some arbitrary
point on the track (the 1541 has no index hole sensor). By the time it has read through
the entire track and comes back to where it started, it has already overwritten the
beginning of its buffer with data from the second revolution. The data appears truncated
or corrupt.

Long tracks were used by Mindscape and other publishers. The protection typically verified
that a specific region on the track produced exactly the right amount of data -- a length
that could only result from a track written at the factory with precise speed control.

### Sector Header Modifications

Each sector on a 1541 disk has a header block containing:

```
Byte 0:     $08 (header block identifier)
Byte 1:     Checksum (XOR of bytes 2-5)
Byte 2:     Sector number
Byte 3:     Track number
Bytes 4-5:  Disk ID (same for all sectors on the disk)
Bytes 6-7:  Padding ($0F, $0F typically)
```

Protection schemes modified these fields in ways that confused standard copiers:

- **Wrong track numbers**: A sector on track 18 might claim to be on track 35. The
  standard DOS would reject it, but a custom loader that ignores the track field would
  read it fine.
- **Duplicate sector IDs**: Two sectors on the same track with the same sector number.
  The standard DOS finds whichever one the head encounters first; a custom loader could
  skip the first and read the second.
- **Non-matching disk IDs**: Sectors on the same track with different disk IDs. The
  standard DOS treats this as an error; a custom loader does not care.
- **Modified checksums**: Deliberate checksum mismatches that cause error 27 on the
  standard DOS but are expected by the protection routine.

### Fat Tracks

Fat-track protection exploits a fundamental asymmetry between disk mastering equipment and
consumer drives. Professional mastering machines had write heads significantly wider than
the 1541's read head. By writing the same data across two adjacent track positions
simultaneously (e.g., tracks 18 and 18.5), the mastering machine created a "fat" track
that could be read from either position.

The protection would:
1. Read data from track N
2. Step to the half-track position N.5
3. Read data again
4. Verify that both reads returned the same data

On an original disk, both reads succeed because the fat track spans both positions. On a
copy made by a 1541 (which writes at normal track width), the half-track read fails because
the data is only present at the full-track position.

Electronic Arts pioneered fat-track protection around 1984-1985. Activision later adopted
it under the name "XEMAG" and used it for years. The technique was fundamentally impossible
to reproduce with a consumer 1541 drive because the write head was too narrow to create a
true fat track.

---

## 3. Advanced Protection Techniques

### Custom GCR Encoding

The 1541 uses Commodore's Group Code Recording to store data on disk. Standard GCR
converts each 4-bit nibble into a 5-bit code, ensuring no more than two consecutive zero
bits appear in the data stream (a requirement for reliable clock recovery by the drive's
data separator circuit). There are exactly 16 valid 5-bit codes out of 32 possible.

Protection engineers exploited the 16 *invalid* GCR codes -- the 5-bit patterns that
violate the two-zero-maximum rule. When the drive's data separator encounters these
patterns, its behavior depends on the hardware variant:

- **Original 1541 (analog PLL data separator)**: Produces genuinely random output. Each
  read of the same invalid region yields different bytes. This randomness is the basis
  of weak-bit protection.
- **1541-II (digital data separator)**: Produces a fixed, repeating pattern. The digital
  circuit locks onto a deterministic interpretation of the ambiguous signal.

This hardware difference had a devastating consequence: disks that correctly passed their
own protection on an original 1541 could *fail* on a 1541-II, because the protection
expected randomness and got consistency. Many game publishers received complaints from
customers with newer drives who could not run legitimately purchased software.

Custom GCR was also used offensively. V-MAX! and RapidLok both used entirely custom
encoding schemes that bore no resemblance to standard Commodore GCR. Their decoders were
downloaded into the drive's RAM and processed the alien data stream on-the-fly.

### Sync Mark Manipulation

Sync marks are the signposts of a 1541 disk. A sync mark is a sequence of ten or more
consecutive 1-bits. In standard GCR, no valid data byte can contain more than eight
consecutive 1-bits, so sync marks are unambiguous delimiters. The 1541 firmware writes
sync marks as 40 bits (five bytes of $FF).

Protection schemes manipulated sync marks in several ways:

**All-sync (killer) tracks**: An entire track filled with $FF bytes. The drive enters
an infinite sync-detection loop and never finds any data. Copy programs that attempt to
read such a track hang. Activision's Keydos protection used killer tracks as one of three
simultaneous signature-track varieties.

**No-sync tracks**: Tracks written with no sync marks at all. The standard firmware cannot
find where data blocks begin. Epyx's Vorpal used this technique -- its custom decoder read
data purely by timing, counting magnetic transitions rather than looking for sync marks.

**Sync counting**: The exact number of sync marks on a track is verified. Standard copiers
might produce tracks with slightly different sync mark counts due to alignment tolerances.

**Framing drift**: By placing non-standard-length sync marks (e.g., 11 bits instead of 10),
the protection can cause the drive's byte-boundary detection to shift by one bit, changing
the interpretation of all subsequent data on the track.

**Sync-mark spacing patterns (syncr0l0k)**: The spacing between sync marks is deliberately
uneven. A copy made with evenly spaced sync marks fails verification. Reproducing the exact
original spacing requires flux-level hardware.

### Track-to-Track Synchronization

Factory disk-mastering equipment uses the disk's index hole to align every track to the
same rotational starting point. On an original factory-pressed disk, track 5 always starts
at exactly the same angular position relative to track 6. The relationship is deterministic
and repeatable.

The 1541 has no index hole sensor. When it writes a track, the data starts at whatever
angular position the disk happens to be at when writing begins. This means that on a
home-made copy, the angular relationship between tracks is random.

Protection exploiting track synchronization works like this:

1. Start reading track N, triggering a CIA timer
2. Step the head to track N+1 while the disk continues spinning
3. Begin reading track N+1 and note the timer value
4. Compare the elapsed time against the expected value for a factory-aligned disk

If the timing matches, the disk is an original. If the timing is random, it is a copy.

This was one of the most powerful protection techniques because even a perfect bit-for-bit
copy of every track would fail -- the *content* was identical, but the *angular alignment*
was wrong. Only a mastering machine with index-hole sensing could reproduce it.

Fat-track protection is essentially a special case of track synchronization where the
requirement is that two adjacent tracks start at exactly the same angle (zero offset).

### Disk Wobble Detection

No floppy disk spins perfectly. There is always a slight wobble -- a periodic variation in
the rotational speed caused by the disk not being perfectly centered on the spindle. This
wobble is unique to each physical disk, like a fingerprint.

Some protection schemes read the same track multiple times and measured the timing
variations. An original disk had a specific wobble pattern determined by the physical
properties of that particular disk. A copy would have a different wobble pattern (that of
the blank disk it was written to), failing the check.

This technique was rare because it required extremely precise timing and was sensitive to
drive-to-drive variations, but it represents the theoretical extreme of physical media
authentication.

### Timing-Based Protection

The CIA (Complex Interface Adapter) chips in both the C64 and the 1541 contain precision
timers that can count microseconds. Protection schemes used these timers to measure:

- **Track-to-track seek time**: The exact time to step from one track to another, which
  varies slightly between drives but is consistent for a given drive
- **Sector spacing**: The time between two specific sectors on a track, which depends on
  the physical layout
- **Revolution timing**: The time for a complete disk revolution (nominally 200ms at
  300 RPM), with original disks having characteristic speed variations
- **Gap lengths**: The time to traverse inter-sector gaps, which differs between factory
  mastered disks and home-formatted copies

CYAN Loader used measured gap lengths as a protection mechanism. The protection wrote
specific key bytes in the inter-sector gaps and verified both the bytes and the time to
traverse the gaps. Standard copiers discarded gap data entirely.

---

## 4. Software Protection

### Encrypted Code

Nearly all serious protection schemes encrypted the game code on disk. The simplest form
was XOR (EOR in 6502 mnemonics) encryption, where each byte was XORed with a key byte
before writing to disk and XORed again with the same key during loading to recover the
original code.

More sophisticated approaches included:

- **Rolling key encryption**: The key changed for each byte, often derived from the
  previous encrypted byte (cipher-block chaining)
- **Multi-pass encryption**: Code encrypted multiple times with different keys, requiring
  multiple decryption passes
- **Position-dependent keys**: The decryption key depended on the disk position (track and
  sector) from which the data was loaded, so the same data moved to a different location
  would not decrypt correctly

The "Lords of Midnight" protection used a clever self-revealing decryption loop:

```
    ; The EOR instruction itself is initially encrypted
    ; The loader shifts and rotates the three bytes that
    ; form the EOR instruction to reveal it
    ; Then the revealed EOR instruction decrypts 768 bytes
    ; using the key stored at address $66
    LDY #$00
loop:
    LDA $0900,Y
    EOR $66
    STA $0900,Y
    INY
    BNE loop
```

GEOS used a serial-number system: on first boot, GEOS generated a random 16-bit serial
number and stored it in the KERNAL binary. Applications cached this serial on first run
and compared it on subsequent runs. Running a copy on a different GEOS system yielded a
different serial, causing the application to subtly sabotage itself rather than obviously
refusing to run.

### Self-Modifying Code

Self-modifying code (SMC) was the primary defense against static disassembly. Rather than
having a fixed program that a cracker could disassemble and analyze at leisure, the code
modified itself during execution:

- **Instruction rewriting**: An instruction at one address would modify the opcode or
  operand of an instruction at another address before it was executed. The disassembled
  listing showed the *initial* state, not the *runtime* state.

- **Decryption-on-the-fly**: Small blocks of code were decrypted just before execution
  and re-encrypted afterward. At no point did the entire program exist in decrypted form
  in memory.

- **Stack tricks**: Code was pushed onto the stack byte by byte and then executed by
  manipulating the stack pointer, so the executable code never appeared at a fixed address.

- **Computed jumps**: JMP and JSR targets were calculated at runtime from values scattered
  across memory, making control flow impossible to follow statically.

A classic anti-disassembly trick used the overlap between multi-byte instructions:

```
    ; What appears to be:
    BIT $A9        ; 3-byte instruction: $2C $A9 $xx
    LDA #$xx       ; ... but the $A9 is actually LDA #imm
                   ; when jumped to at offset +1

    ; A disassembler following the first instruction sees BIT $A9xx
    ; But execution jumping to the second byte sees LDA #$xx
```

### Anti-Debugging Tricks

Professional protection included multiple layers to detect and thwart debugging tools:

**NMI vector monitoring**: Freezer cartridges work by triggering a Non-Maskable Interrupt.
Protection routines periodically checked the NMI vector at $0318/$0319 (or the hardware
vector at $FFFA/$FFFB). If these pointed to cartridge ROM instead of the expected KERNAL
addresses, a freezer was detected.

**IRQ chain verification**: The protection installed its own IRQ handler and verified that
the chain was intact. If a debugger or monitor had inserted itself into the IRQ chain, the
protection detected the tampering.

**CIA timer tricks**: Freezer cartridges must stop the CIA timers to capture system state,
but they cannot perfectly restore the timer values because the act of reading the timer
changes it. Protection routines set up CIA timers with known values and verified them after
a delay. If the timer value was wrong (because a freezer had interrupted and imperfectly
restored it), the protection triggered.

```
    ; Anti-freeze timer check
    LDA #$FF
    STA $DC04        ; Timer A low byte
    STA $DC05        ; Timer A high byte
    LDA #$11
    STA $DC0E        ; Start timer, one-shot mode
    ; ... do some precisely timed work ...
    LDA $DC04        ; Read timer
    CMP #expected    ; Compare with calculated value
    BNE tampered     ; If wrong, someone froze and resumed
```

**Memory-mapped I/O scanning**: Protection routines wrote to the $DE00-$DEFF address range
(the I/O expansion area). Most cartridges, including Action Replay and Final Cartridge III,
have registers or ROM mapped here. If a written value persisted at $DE00, a cartridge was
present:

```
    LDA #$55
    STA $DE00        ; Write to I/O expansion area
    CMP $DE00        ; Read it back
    BEQ cartridge    ; If value persists, cartridge detected
```

**Specific cartridge detection**:

- Final Cartridge III: System vectors at $0302-$030A point to FCIII ROM addresses; reading
  $DE01 returns specific ROM bytes ("STA $DFFF / RTS")
- Action Replay: Reading the control register at $DE00 on real hardware causes a system
  crash, which can be used as a detection fingerprint
- Retro Replay: Writing bit 2 of $DE01 (the NoFreeze bit) disables the freeze button

### ROM Checksum Verification

The C64's ROMs (BASIC at $A000-$BFFF, KERNAL at $E000-$FFFF, character ROM at $D000-$DFFF)
have known checksums. Commodore built all ROMs so that the 8-bit checksum of each ROM
block matched the upper 8 bits of its starting address. The KERNAL has a checksum-adjust
byte at $E4AC specifically to make the total come out right.

Protection routines checksummed the ROM areas to detect:

- Modified ROMs (indicating a patched KERNAL, possibly for fast-load or anti-protection)
- Cartridge ROM overlays (the KERNAL area reads differently when a cartridge is mapped in)
- BASIC ROM absence (some debuggers bank out BASIC to gain RAM)

```
    ; ROM checksum verification
    LDA #$00
    LDX #$00
    CLC
loop:
    ADC $E000,X      ; Sum all KERNAL bytes
    INX
    BNE loop
    INC loop+2       ; Advance high byte
    LDY loop+2
    CPY #$00         ; Wrapped past $FFFF?
    BNE loop
    CMP #$E0         ; Expected checksum = high byte of start address
    BNE tampered
```

### CIA Timer Tricks to Detect Freeze Cartridges

Freeze cartridges like the Action Replay and Final Cartridge III work by triggering an NMI,
saving the entire system state to cartridge RAM (or compressed into available C64 RAM), and
then later restoring it. The fundamental weakness is that certain hardware state cannot be
perfectly preserved:

- **SID registers**: Write-only. The freezer cannot read the current SID state, so it
  either zeros the volume or makes guesses. The Final Cartridge III "simply sets the
  volume to 0 and does not touch any of the other registers."
- **CIA timer values**: Reading a timer affects it. The timer continues to count during the
  NMI handler's execution. Restoring "the value at the time of the freeze" is impossible
  because the NMI handler consumed unknown cycles.
- **VIC-II state**: Some VIC-II registers are read-only or have different read/write
  behavior. The current raster position at the moment of freeze cannot be meaningfully
  restored.

Protection exploited these weaknesses by setting up precisely timed sequences and verifying
them:

1. Start CIA Timer A with a known value
2. Execute a precisely timed code sequence (cycle-counted NOPs and known instructions)
3. Read the timer and compare against the expected remainder
4. If the value is off by more than a few cycles, an NMI occurred between steps 1 and 3

The TIMEX protection system took this further: the game loaded into memory fully encrypted,
with a raster IRQ routine continuously decrypting code during execution. Freezing the
system captured the encrypted state; without the background decryption routine running, the
frozen image was useless garbage.

---

## 5. The Cracking Scene

### Origins

The practice of removing copy protection -- cracking -- began almost as soon as software was
sold. On the C64, the cracking scene emerged in 1983, initially in West Germany and the
Netherlands. Early crackers were individuals: technically skilled programmers who saw
defeating protection as an intellectual challenge. They shared their work through physical
floppy-disk trading networks and, later, through Bulletin Board Systems (BBSes).

The early scene operated on a simple principle: the cracker removed the protection, replaced
the custom loader with a standard one, and redistributed the game on a normal-format disk
that anyone could copy.

### The Rise of Cracking Groups

By 1984-1985, individual crackers had organized into groups with names, logos, and
hierarchies. The groups competed fiercely for "first releases" -- being the first to crack
and distribute a new game was a matter of immense prestige.

**Major C64 cracking groups included:**

- **Eagle Soft Incorporated (ESI)**: Dominant US group in 1987-1988. Known for cracking the
  hottest games during the C64's golden era and for their "massively arrogant, elitist
  attitude which furthered their reputation."
- **Triad**: Swedish group founded in 1986, one of the oldest surviving scene groups. Known
  for their iconic PETSCII logo and the slogan "Dealer Quality Software."
- **Fairlight (FLT)**: Founded in April 1987 in Sweden by former members of West Coast
  Crackers. Became legendary across multiple platforms.
- **1001 Crew**: Dutch group whose cracktros pushed technical boundaries. They were the first
  to make the C64's entire screen border disappear -- a technical feat that directly
  contributed to the birth of the demo scene.
- **Ikari + Talent**: Major European groups known for high-quality cracks and fast
  distribution.
- **Hotline**: Prolific crackers active throughout the mid-to-late 1980s.
- **JEDI**: One of the earliest German cracking groups, active from around 1983.
- **ABC Crackings**: Early Dutch group that included signatures in game loader screens.

### Crack Intros as Art Form

When crackers removed the original protection and loader, they replaced it with their own
boot sequence -- the crack intro (or "cracktro"). Initially these were simple scrolling
text messages: "Cracked by [name], greetings to [friends], call [BBS number]."

But competition drove innovation. If your rival's cracktro had a scrolling message, yours
needed a scrolling message *and* a color-cycling logo. Then music. Then raster effects.
Then sprite multiplexing. Then things the C64 was not supposed to be able to do.

The escalation followed a predictable pattern:

1. **1983-1984**: Static text screens with the cracker's name
2. **1984-1985**: Scrolling text, simple character graphics
3. **1985-1986**: Custom character sets, raster color bars, SID music
4. **1986-1987**: Bitmap graphics, sprite animations, border effects
5. **1987-1988**: Full-screen effects, smooth scrolling, multi-channel music
6. **1988+**: Effects rivaling or exceeding the games they were attached to

### The Birth of the Demo Scene

The pivotal moment came when 1001 Crew discovered how to open the C64's screen borders --
placing graphical elements in the normally blank border area by exploiting VIC-II register
timing. This was such a significant technical achievement that they released it as a
standalone production, without any attached crack. People watched it not because it gave
them a free game, but because it was impressive in its own right.

This was the moment the cracktro became the demo. Programmers began creating standalone
visual and audio demonstrations purely to showcase their technical skills. The demo scene
that emerged from this tradition became a global creative community that persists to this
day, with annual competitions (demoparties) held worldwide.

The demo scene's cultural norms -- respect for technical skill, competition through
creation rather than destruction, the tradition of sharing knowledge -- all trace directly
back to the C64 cracking scene of the mid-1980s.

### Tools of the Trade

**Freezer Cartridges:**

Freezer cartridges were the cracker's most powerful tool. By pressing a button, the
cartridge triggered an NMI, captured the entire system state, and presented a menu
allowing the user to save the frozen program to disk. The principle was simple: if the
program is running in memory, it can be captured, regardless of how it was loaded.

Key freezer cartridges:

- **Action Replay** (Datel Electronics): The most popular freezer, going through multiple
  revisions (MK I through MK VI). Featured a built-in machine-language monitor, sprite
  killer, and POKEfinder for cheats.
- **Final Cartridge III** (Home & Personal Computers): Used a clever NMI + Ultimax mode
  combination. When the freeze button was pressed, the NMI line dropped immediately while
  a hardware counter delayed the GAME line activation by 7 cycles. This forced the CPU
  to read interrupt vectors from cartridge ROM. The FCIII had no onboard RAM, so it
  searched the C64's memory for 103 and 87 consecutive bytes with the same value to use
  as workspace.
- **ISEPIC** (Starpoint Software): An early freezer with a different approach -- it
  monitored the bus and recorded all I/O operations rather than snapshotting memory.
- **Super Snapshot** (LMS Technologies): Combined freezer, fast loader, and machine
  language monitor in one cartridge.

**Nibbler Programs:**

Nibblers read raw GCR data from disk rather than relying on the DOS to decode sectors.
This allowed copying non-standard formats that the DOS could not parse.

- **Fast Hack'em** (Basement Boys Software, 1985): One of the two best software-only copy
  programs. Included a nibbler that used "parameters" -- small configuration scripts that
  told the nibbler how to handle specific titles' protections.
- **Maverick** (Software Support International): Originally named "Renegade" (renamed for
  trademark reasons). The final-generation C64 disk copier that the scene converged on
  during the last commercial years. Supported parameter disks with hundreds of title-
  specific configurations.
- **Copy II 64/128** (Central Point Software): The Commodore port of their famous PC
  product line. Served as both a parameter copier and standard disk copier.
- **Burst Nibbler**: Required a parallel cable connecting the C64's user port to the
  1541's VIA chip, bypassing the slow serial bus. Could transfer an entire track of raw
  GCR data in a fraction of a second.

**Disk Analyzers and Editors:**

- **Disk Doctor**: Allowed direct examination and modification of individual sectors,
  tracks, and raw GCR data on disk.
- **Kracker Jax** (Computer Mart): Published parameter disks specifically designed to
  defeat named protection schemes on specific titles. Essentially a database of
  "here's how to copy game X."

---

## 6. Drive Programming

### The 1541 as a Separate Computer

The Commodore 1541 disk drive is not a peripheral in the modern sense. It is a complete,
self-contained computer:

| Component | Specification |
|-----------|---------------|
| CPU | MOS 6502 @ 1 MHz |
| RAM | 2,048 bytes ($0000-$07FF) |
| ROM | 16,384 bytes ($C000-$FFFF) |
| VIA #1 | MOS 6522 at $1800-$180F (serial bus interface) |
| VIA #2 | MOS 6522 at $1C00-$1C0F (drive mechanism control) |

The drive runs its own operating system (Commodore DOS 2.6) from ROM. It manages the
serial bus communication with the C64, handles file operations, controls the physical drive
mechanism, and performs GCR encoding/decoding -- all independently of the host computer.

This architecture means that any program running on the C64 can upload custom code to the
drive and have it execute autonomously. The drive becomes a co-processor with direct access
to the raw disk surface -- a capability that both protection engineers and crackers
exploited to the fullest.

### Drive Memory Map

**Zero Page ($0000-$00FF):**

The zero page is the drive's most critical memory area, containing the job queue, hardware
control registers, and operating system state.

```
$0000-$0004   Job queue: Buffer command/status registers (buffers 0-4)
              Bit 7 = 1: command pending; Bit 7 = 0: status/result
$0005         Not used
$0006-$0007   Track and sector for buffer 0
$0008-$0009   Track and sector for buffer 1
$000A-$000B   Track and sector for buffer 2
$000C-$000D   Track and sector for buffer 3
$000E-$000F   Track and sector for buffer 4
$0012-$001D   Header identification and disk change detection
$0020-$0021   Disk controller state
$0022-$0023   Communication speed settings
$0024-$0037   GCR encoding/decoding workspace and counters
$0038-$003F   Checksum validation and unit selection
$0040-$0050   Seeking parameters, motor control, GCR status flags
$0051-$0076   Formatting state, temp data, user command pointers
$0077-$007F   Serial bus state (LISTEN, TALK, ATN signals)
$0080-$00FF   File operations, buffer pointers, channel management
```

**Stack and Auxiliary ($0100-$01FF):**

```
$0100         Usually unused (overflow from zero page)
$0101         BAM version code (expected: $41 = "A")
$0103-$0145   Processor stack
$01BA-$01FF   GCR encoding/decoding auxiliary buffer
```

**Command Buffer and State ($0200-$02FF):**

```
$0200-$0229   Input buffer for host commands (42 bytes)
$022A         DOS command identifier
$022B-$0250   Channel assignment and buffer management
$0251-$02FF   File search state, BAM tracking, directory state
```

**Data Buffers ($0300-$07FF):**

Five 256-byte data buffers occupy consecutive memory:

```
$0300-$03FF   Buffer 0
$0400-$04FF   Buffer 1
$0500-$05FF   Buffer 2
$0600-$06FF   Buffer 3
$0700-$07FF   Buffer 4 (also used for BAM cache)
```

These buffers are the workspace for all disk operations. When reading a sector, the raw
GCR data is decoded into one of these buffers. When writing, data from a buffer is GCR-
encoded and written to disk. The severe limitation -- only 2 KB total RAM, with 1.25 KB
consumed by these five buffers -- means there is minimal space for custom code.

**VIA #1 -- Serial Bus Interface ($1800-$180F):**

```
$1800   Port B: Serial bus data lines (DATA IN/OUT, CLK IN/OUT, ATN IN)
$1801   Port A: ATN interrupt acknowledgment
$1802   Data Direction Register B
$1803   Data Direction Register A
$1804   Timer 1 Low Byte (counter)
$1805   Timer 1 High Byte (counter)
$1806   Timer 1 Low Byte (latch)
$1807   Timer 1 High Byte (latch)
$180D   Interrupt Flag Register
$180E   Interrupt Enable Register
```

**VIA #2 -- Drive Mechanism Control ($1C00-$1C0F):**

```
$1C00   Port B:
          Bit 0-1: Head stepper motor phase (STP0, STP1)
          Bit 2:   Drive motor (1 = on)
          Bit 3:   Drive LED (1 = on)
          Bit 4:   Write protect sense (input)
          Bit 5-6: Density select (speed zone: 00-11)
          Bit 7:   SYNC detect (input, active low)
$1C01   Port A: Data byte read/write port
$1C02   Data Direction Register B
$1C03   Data Direction Register A
$1C04   Timer 1 Low Byte
$1C05   Timer 1 High Byte
$1C0C   Peripheral Control Register (bit 1: read/write head mode)
$1C0D   Interrupt Flag Register
$1C0E   Interrupt Enable Register
```

The critical register is $1C00. By manipulating bits 0-1, custom code can step the head
to any position, including half-tracks. Bits 5-6 select the speed zone, allowing reading
and writing at non-standard bit rates. Bit 7 provides direct hardware feedback on whether
the head is currently over a sync mark.

### Uploading Custom Code: M-W and M-E

The Commodore DOS supports two commands for direct memory access from the host computer:

**M-W (Memory-Write):**

```
OPEN 15,8,15,"M-W" + CHR$(low) + CHR$(high) + CHR$(count) + data
```

Writes up to 34 bytes at a time to any address in the drive's memory space. The limitation
of ~34 bytes per command (constrained by the 42-byte input buffer minus the command overhead)
means that uploading a substantial program requires multiple M-W commands in sequence.

Example -- uploading a small routine to buffer 3 ($0600):

```basic
10 OPEN 15,8,15
20 PRINT#15,"M-W"CHR$(0)CHR$(6)CHR$(10)CHR$(169)CHR$(0)CHR$(141)...
30 ; ... more M-W commands to fill the buffer ...
80 PRINT#15,"M-E"CHR$(0)CHR$(6)
90 CLOSE 15
```

**M-E (Memory-Execute):**

```
OPEN 15,8,15,"M-E" + CHR$(low) + CHR$(high)
```

Begins executing code at the specified address. The drive's CPU jumps to this address and
runs until an RTS is encountered (for the job-loop variant) or indefinitely (for code that
replaces the DOS entirely).

**Block-Execute (B-E):**

An alternative approach loads a sector from disk into a buffer and executes it:

```
OPEN 15,8,15,"B-E" + CHR$(channel) + CHR$(drive) + CHR$(track) + CHR$(sector)
```

This loads the specified sector into the channel's buffer and begins execution. It was
often used to bootstrap larger drive programs: the first sector loaded by B-E would
contain code to read additional sectors and build up the full drive program.

### The Job Queue System

The 1541's disk controller operates through a job queue at $0000-$0004. Each entry
corresponds to one of the five buffers. To perform a disk operation, the host (or custom
drive code) writes a job command to the appropriate queue entry and the track/sector to
the corresponding track/sector registers at $0006-$000F. The disk controller runs as a
background process and sets bit 7 of the queue entry to 0 when the job is complete,
with the result code in bits 0-6.

**Job command codes:**

| Code | Operation | Description |
|------|-----------|-------------|
| $80 | READ | Read sector into buffer |
| $90 | WRITE | Write buffer contents to sector |
| $A0 | VERIFY | Compare buffer with sector on disk |
| $B0 | SEEK | Move head to specified track |
| $C0 | BUMP | Move head to track 1 (home position) |
| $D0 | JUMP | Execute code in associated buffer |
| $E0 | EXECUTE | Turn on motor, seek to track, then execute code in buffer |

The JUMP ($D0) and EXECUTE ($E0) commands are the key to drive programming. JUMP starts
running the code in the specified buffer immediately. EXECUTE first ensures the motor is
spinning and the head is on the correct track, then transfers control to the buffer.

Custom code running in a buffer can:
- Directly manipulate the VIA registers for raw hardware access
- Step the head to any position (including half-tracks)
- Read and write raw GCR data
- Control the motor speed
- Bypass all DOS firmware for completely custom disk formats
- Communicate with the C64 by manipulating the serial bus lines

### Direct Head Movement

Custom drive code moves the head by manipulating the stepper motor phase bits at $1C00:

```
    ; Move head one half-track inward (toward higher track numbers)
    LDA $1C00
    CLC
    ADC #$01         ; Advance phase by one step
    AND #$03         ; Keep only phase bits
    STA $18          ; Save new phase
    LDA $1C00
    AND #$FC         ; Clear old phase bits
    ORA $18          ; Set new phase bits
    STA $1C00
    ; Wait for head to settle (~20ms for reliable operation)
```

Two half-track steps equal one full track. The head has no position sensor -- the drive
firmware tracks position in software. After a power-on or error, the drive "bumps" the
head against the mechanical stop at track 1 to re-establish a known position.

### Reading Raw GCR

To read raw GCR data directly, custom code bypasses the DOS entirely:

```
    ; Wait for sync mark
wait_sync:
    LDA $1C00
    BPL wait_sync    ; Bit 7 = 0 when sync detected

    ; Read GCR bytes as they arrive
    LDA $1C01        ; Reading this register clears the byte-ready flag
read_loop:
    BIT $1C00        ; Check bit 7 of VIA (byte ready when set)
    BMI read_loop    ; Wait for next byte
    LDA $1C01        ; Read the byte
    STA ($FB),Y      ; Store in buffer
    INY
    BNE read_loop
```

This code reads raw GCR bytes at the hardware level, without any DOS interpretation.
The timing is critical: at the fastest bit rate (zone 3, 307,692 bps), a new byte arrives
every 26 microseconds (26 CPU cycles at 1 MHz). The read loop must complete within this
window or data is lost.

### Custom Disk Formats for Maximum Storage

By bypassing the standard DOS entirely, custom formats can dramatically increase the
storage capacity of a 1541 disk.

**Standard capacity**: 170 KB (683 sectors x 256 bytes) across 35 tracks.

**Optimization strategies:**

1. **Add extra tracks (36-41)**: +85 sectors at zone 0, bringing total to 196 KB (115%
   of standard).

2. **Minimize overhead**: Reduce header gaps from 9 to 2 bytes, eliminate trailing gaps,
   saving ~18 bytes per sector. This allows one additional sector per track on most zones,
   reaching ~181 KB (106%).

3. **Eliminate headers entirely**: Use a single sync mark per track with continuous data.
   No per-sector headers means no per-sector overhead. This sacrifices random access
   (you must read the entire track sequentially) but maximizes throughput.

4. **Use fastest speed zone everywhere**: Write all tracks at zone 3 speed (including
   inner tracks normally at zones 0-2). This packs 21+ sectors per track across all 41
   tracks, reaching 235-246 KB (138-144%). However, the inner tracks now exceed the
   nominal density specification by up to 34% (7,740 bits per inch versus the 5,900 bpi
   spec), reducing reliability.

5. **Custom GCR with relaxed constraints**: Standard Commodore GCR uses 4-to-5 encoding,
   wasting 20% of capacity on the encoding overhead. A custom 8-to-7 encoding scheme that
   allows three consecutive zeros (instead of the standard maximum of two) yields ~9%
   more capacity but requires a completely custom decoder.

**Theoretical maximum**: Combining all optimizations -- 41 tracks, all at zone 3 speed,
single sync mark per track, no headers, custom GCR -- yields approximately 246 KB per disk
(144% of standard). This requires drive speed accuracy within 0.1% of 300 RPM and is
unreliable on all but the best-aligned drives.

---

## 7. Hardcore Details

### GCR Encoding Table

Commodore's GCR converts each 4-bit nibble to a 5-bit code. The constraint: no code
may contain more than two consecutive zeros, ensuring reliable clock recovery.

```
  Nibble  Binary   GCR Code  Binary     Nibble  Binary   GCR Code  Binary
  ------  ------   --------  ------     ------  ------   --------  ------
  $0      0000     $0A       01010      $8      1000     $09       01001
  $1      0001     $0B       01011      $9      1001     $19       11001
  $2      0010     $12       10010      $A      1010     $1A       11010
  $3      0011     $13       10011      $B      1011     $1B       11011
  $4      0100     $0E       01110      $C      1100     $0D       01101
  $5      0101     $0F       01111      $D      1101     $1D       11101
  $6      0110     $16       10110      $E      1110     $1E       11110
  $7      0111     $17       10111      $F      1111     $15       10101
```

**Encoding process**: Four data bytes (32 bits) are split into eight 4-bit nibbles. Each
nibble is converted to its 5-bit GCR code, producing 40 bits (5 bytes). Thus every 4 bytes
of data require 5 bytes on disk -- a 25% overhead.

**Invalid GCR codes**: The 16 unused 5-bit patterns (those with three or more consecutive
zeros) are: $00, $01, $02, $03, $04, $05, $06, $07, $08, $10, $11, $14, $18, $1C, $20-$1F.
When encountered, the drive's data separator produces undefined behavior -- the basis for
weak-bit protection.

**Decoding challenge**: Because GCR codes span byte boundaries (8 nibbles = 40 bits = 5
bytes, but the nibble boundaries do not align with byte boundaries), decoding requires
a lookup table or shift-and-mask operation. The standard DOS ROM uses a 256-byte lookup
table at $F8A0. Custom decoders (like V-MAX!) used optimized approaches that decoded
on-the-fly as bytes arrived from the disk.

### Disk Rotation Speed and Bit Rate Per Zone

The 1541 spins the disk at a constant 300 RPM (5 revolutions per second). One revolution
takes exactly 200 milliseconds. The total number of bits that can be stored on one track
varies by zone because the outer tracks have more physical circumference.

```
Zone  Tracks   Bit Rate     Bits/Rev    Bytes/Rev    Sectors  Data Bytes
----  ------   --------     --------    ---------    -------  ----------
  3    1-17    307,692 bps   61,538      7,692         21       5,376
  2   18-24    285,714 bps   57,143      7,143         19       4,864
  1   25-30    266,667 bps   53,333      6,667         18       4,608
  0   31-35    250,000 bps   50,000      6,250         17       4,352
                                                      ---      ------
                                              Total:  683     174,848
```

Notes:
- "Bytes/Rev" is the raw capacity including GCR encoding overhead.
- "Data Bytes" is the usable data after subtracting headers, gaps, and sync marks.
- The standard DOS allocates 256 bytes per sector for user data.
- Track 18 is reserved for the directory and BAM (Block Allocation Map), leaving 664
  blocks (169,984 bytes) available for file storage.

The bit rate is controlled by the density select bits at $1C00 bits 5-6:

```
Bits 5-6   Zone   Bit Rate        Clock Divisor
--------   ----   --------        -------------
   00       0     250,000 bps     16 (slowest)
   01       1     266,667 bps     15
   10       2     285,714 bps     14
   11       3     307,692 bps     13 (fastest)
```

### Read/Write Head Signal Level

The 1541 uses a single read/write head that operates in two modes, controlled by
bit 1 of VIA #2's Peripheral Control Register ($1C0C):

**Read mode**: The head detects magnetic flux transitions on the disk surface. Each
transition produces a brief voltage pulse that is amplified and shaped by the analog
circuitry into a clean digital signal. The data separator (PLL on original 1541, digital
on 1541-II) converts the transition timings into a clocked bitstream.

**Write mode**: The head is driven by a write-current circuit that creates flux transitions
at the positions specified by the GCR bitstream. A '1' bit causes a transition; a '0' bit
means no transition at that clock position.

The critical difference between original 1541 and 1541-II:

- **1541 / 1541-C**: Analog phase-locked loop (PLL) data separator. When the incoming
  signal is ambiguous (as with invalid GCR or weak bits), the PLL produces genuinely random
  output because the analog feedback loop oscillates unpredictably.
- **1541-II**: Digital data separator (CSG 5710 or equivalent). When the signal is
  ambiguous, the digital circuit snaps to a deterministic state, producing fixed repeating
  patterns. This broke weak-bit protections that relied on randomness.

### V-MAX! Explained

V-MAX! was created by Harald Seeley and went through at least seven major versions between
1985 and 1993, evolving from a simple fast loader into one of the most formidable
protections ever developed for the C64.

**V-MAX! v1 (1985)**: Essentially a fast loader. The disk was formatted in a custom
streaming format decoded on-the-fly by code uploaded to the drive. Used on early titles
like Star Rank Boxing. The EOR-streaming technique could be copied with tools of the era.

**V-MAX! v2-v3 (1986-1988)**: Non-standard encoding on specific tracks. The drive code
became more complex, with custom GCR tables and interleaved data that the standard DOS
could not parse. Starting with v3, copying required an 8 KB RAMBoard expansion in the
drive because the standard 2 KB RAM could not hold enough of the track to reproduce the
format.

**V-MAX! v3+ and later (1988-1993)**: Combined multiple protection layers:

- Custom GCR encoding with non-standard sync marks
- Track-to-track synchronization checks
- Timing-sensitive authentication sequences
- Drive-speed-dependent verification
- Incompatibility with virtually all fastload cartridges

Seeley himself stated in November 1999: "I honestly don't know of anything that could
copy it with a 1541/71." The later versions of V-MAX! required hardware nibblers with
extra RAM (like the Super Card) or flux-level imaging to preserve.

V-MAX! protected titles include many games published by Thunder Mountain, Cinemaware,
Ocean, US Gold, and others throughout the late 1980s.

### RapidLok Explained

RapidLok was developed for Accolade and used from approximately 1986 through 1992. It
was one of the most technically ambitious protections, reformatting the entire disk in a
proprietary layout.

**How it worked:**

1. The directory track (track 18) was kept in standard Commodore DOS format so the disk
   would boot normally through the standard LOAD command.

2. The boot sector loaded a small stub that sent the RapidLok decoder to the drive's RAM
   via M-W/M-E commands.

3. The decoder took over the drive completely. From this point, all disk I/O used
   RapidLok's custom format, not Commodore DOS.

4. Tracks were reformatted with a custom sector layout:
   - Tracks 1-17: 12 data sectors at bit rate "11" (307,692 bps)
   - Tracks 19-35: 11 data sectors at bit rate "10" (285,714 bps)

5. An authentication key was read from a hidden track beyond track 35. The key's position
   was timing-sensitive -- the loader had to read it at exactly the right moment relative
   to the disk's rotation.

6. RapidLok used $00 bytes between sectors -- an invalid GCR value that caused copiers to
   produce inconsistent results.

7. A track-skew protection checked the angular relationship between tracks, which was
   fixed on a factory-mastered disk but random on a copy.

RapidLok was notoriously sensitive to drive-speed variations. One user recounted that
"GUNSHIP would not load on three different 1541s until I sat there with a small screwdriver
and kept adjusting the drive speed a little at a time." The protection's sensitivity to
motor speed was both its strength (making copies unreliable) and its weakness (causing
legitimate copies to fail).

### The Track-Align Technique

Track alignment (also called track synchronization or angular alignment) exploits the
difference between factory mastering equipment and consumer drives.

**Factory mastering**: Professional equipment uses an index-hole sensor to begin writing
each track at exactly the same angular position. The result is a disk where every track
starts at a known, repeatable angle relative to the index hole.

**Consumer 1541**: Has no index-hole sensor (the sensor was present in the mechanical
design but Commodore left it unconnected to save cost). The drive begins writing at
whatever angular position the disk happens to be at when the write operation starts.

**The protection check**: Measures the time between reading a specific byte on track N and
a specific byte on track N+1. On a factory-aligned disk, this timing is constant. On a
consumer-written copy, this timing varies randomly each time the disk is written.

```
    ; Simplified track-alignment check
    ; (runs as custom drive code)

    ; Position head on track N
    ; Wait for specific sector header
    ; Start CIA timer

    LDA #$FF
    STA $1804        ; Timer low
    STA $1805        ; Timer high, starts counting

    ; Step head to track N+1 (two half-steps)
    JSR step_head
    JSR step_head

    ; Wait for specific sector header on new track
    JSR find_sector

    ; Read timer
    LDA $1804        ; Timer low
    LDX $1805        ; Timer high

    ; Compare against known factory value
    CMP #expected_lo
    BNE fail
    CPX #expected_hi
    BNE fail
    ; ... disk is original ...
```

This check was fundamentally undefeatable by software-only copiers because it tested a
*physical property* of the disk (angular alignment) rather than its *data content*.

### Maximum Storage Per Disk

Summary of achievable storage under various format strategies:

```
Format                                  Tracks  Sectors  KB      % of Std
------                                  ------  -------  ------  --------
Standard Commodore DOS (35 trk)         35      683      170     100%
Standard + extra tracks (41 trk)        41      768      192     113%
Reduced gaps (35 trk)                   35      718      179     106%
Reduced gaps + extra tracks (41 trk)    41      810      202     119%
No headers, streaming (35 trk)          35      ~752     188     110%
All zone 3 + extra tracks (41 trk)     41      ~861     215     127%
All zone 3, minimal gaps (41 trk)       41      ~984     246     144%
Theoretical maximum (all tricks)        41      ~984     246     144%
```

The practical maximum for reliable operation on a standard, well-aligned 1541 is
approximately 200-210 KB, using extra tracks and reduced gaps while staying within the
density specifications for each zone.

### Modern Preservation Tools

Preserving copy-protected C64 disks requires capturing not just the data but the exact
physical characteristics of the magnetic surface. Three generations of tools have been
developed:

**First Generation: Nibblers (1985-2000)**

Software running on the 1541 itself, reading raw GCR data through the standard hardware.

- **nibtools** (Pete Rittwage): The gold standard for GCR-level imaging. Reads raw track
  data and produces NIB or G64 format images. Requires a parallel cable (XP1541 or XM1541)
  for high-speed transfer.
- Limitation: Cannot capture data below the bit level. Weak bits, non-standard bit rates
  within a track, and absolute angular position are lost.

**Second Generation: Flux-Level Imagers (2009-present)**

Hardware devices that bypass the drive's data separator entirely and measure raw magnetic
flux transitions:

- **KryoFlux** (Software Preservation Society, 2009): USB device connecting a standard PC
  floppy drive to a computer. Captures flux transition timings at 41.6 ns resolution
  (24 MHz sampling). Widely adopted by universities, archives, museums, and the US Library
  of Congress. Produces raw stream files that can be converted to any disk image format.

- **SuperCard Pro** (Jim Drew / CBMStuff, 2013): USB device with 25 ns resolution (40 MHz
  sampling) -- nearly twice the resolution of KryoFlux. Primarily designed for disk
  duplication rather than archival. Can both read and write flux-level data, allowing
  physical reproduction of protected disks. Features a powerful analysis utility for
  examining individual flux transitions.

- **GreaseWeazle** (Keir Fraser, 2019): Open-source, low-cost alternative using STM32
  microcontrollers. Comparable resolution to KryoFlux at a fraction of the cost. Has
  become the community standard for hobbyist preservation.

**Capabilities of flux-level imaging:**

- Preserves weak bits as probabilistic timing spreads (reads that return different values
  each time show up as variable transition spacings)
- Records absolute angular position from the index hole (if the drive has an index sensor)
- Captures any encoding scheme (GCR, MFM, FM, or custom)
- Allows perfect physical reconstruction of original disks (with write-capable devices)
- Works with any drive mechanism -- the imaging device does not care about the encoding

**Disk Image Format Hierarchy:**

| Format | Level | Preserves | Limitations |
|--------|-------|-----------|-------------|
| D64 | Sector | Standard 35-track data | No protection, no extra tracks, no GCR |
| G64 | GCR | Raw GCR, half-tracks, 84 positions | No angular position, no true weak bits |
| NIB | GCR | Raw GCR per track (~8 KB/track) | Assumes standard density, no half-tracks |
| SCP | Flux | Raw flux transitions, full resolution | Large files (~20 MB per disk) |
| KF Raw | Flux | Raw flux streams from KryoFlux | Proprietary format, large files |

For preservation-grade archival of protected disks, flux-level capture (SCP or KryoFlux)
is essential. G64 handles most protections adequately for emulation purposes. D64 is
suitable only for unprotected or cracked software.

**Important hardware caveat**: Avoid the 1541-II for preservation of weak-bit protected
disks. Its digital data separator produces consistent output where the original 1541's
analog PLL produces random values. Use an original 1541 or 1541-C for GCR-level nibbling.
For flux-level capture, the drive model is less critical since the flux device bypasses the
data separator entirely.

---

## 8. References

### Primary Technical References

- [All About Commodore 64 Copy Protection Methods](https://www.commodoregames.net/copyprotection/) --
  Comprehensive reference covering every major protection technique, with detailed technical
  explanations of GCR encoding, drive hardware, and named protection systems.

- [Disk Copy Protection Methods](https://www.commodoregames.net/copyprotection/protection-methods.asp) --
  Detailed catalog of individual protection techniques: bad sectors, fat tracks, half-tracks,
  sync manipulation, timing checks, and named commercial systems.

- [The 1541 Drive & GCR Encoding](https://www.commodoregames.net/copyprotection/the-1541-drive.asp) --
  Technical details of the 1541 hardware, speed zones, GCR encoding process, and the
  critical difference between analog and digital data separators.

- [Emulation & Archiving](https://www.commodoregames.net/copyprotection/emulation-archiving.asp) --
  Comparison of disk image formats (D64, G64, NIB, SCP) and their capabilities for
  preserving protected disks.

### Preservation Projects

- [C64 Copy Protection](https://www.c64copyprotection.com/) --
  Documenting the history of the C64 protection scene, including archived protection sheets,
  code wheels, and historical newsletters.

- [Commodore 64 Preservation Project](https://c64preservation.com/dp.php?pg=protection) --
  Pete Rittwage's preservation project, with detailed analysis of individual protection
  schemes including RapidLok and PirateSlayer.

- [nibtools (OpenCBM)](https://github.com/OpenCBM/nibtools) --
  Source code for the standard GCR-level nibbling tool.

### Drive Programming and Hardware

- [Commodore 1541 Drive Memory Map](https://sta.c64.org/cbm1541mem.html) --
  Complete memory map of the 1541 drive, including zero page, VIA registers, and buffer
  locations.

- [How Does the 1541 Drive Work](https://c64os.com/post/howdoes1541work) --
  Detailed explanation of the 1541's internal architecture, motor control, track seeking,
  and sector finding.

- [Commodore 1541 (C64-Wiki)](https://www.c64-wiki.com/wiki/Commodore_1541) --
  Overview of the 1541 drive with technical specifications and programming information.

- [Inside Commodore DOS](https://www.pagetable.com/docs/Inside%20Commodore%20DOS.pdf) --
  The definitive reference book on Commodore disk drive internals (Richard Immers and
  Gerald Neufeld, 1984).

- [1541 ROM Disassembly](https://ist.uwaterloo.ca/~schepers/MJK/ascii/1541map.txt) --
  Complete disassembly and annotation of the 1541 DOS ROM.

### Software Protection Analysis

- [Copy Protection Traps in GEOS](https://www.pagetable.com/?p=865) --
  Detailed analysis of GEOS's serial-number-based protection and its anti-tampering traps.

- [How the Final Cartridge III Freezer Works](https://www.pagetable.com/?p=1810) --
  Technical analysis of the NMI + Ultimax mode mechanism, memory snapshot process, and
  the limitations of freeze-based copying.

- [Fitting 44% More Data on a 1541 Disk](https://www.pagetable.com/?p=1107) --
  Analysis of maximum achievable storage capacity through custom disk formats.

- [A 256 Byte Autostart Fast Loader](https://www.pagetable.com/?p=568) --
  Example of minimal drive code using M-W/M-E for fast loading.

- [Cartridge Detection Methods (Codebase64)](https://codebase64.c64.org/doku.php?id=base:cartridge_detection) --
  Technical reference for detecting Action Replay, Final Cartridge III, and other
  cartridges from software.

### Cracking Scene History

- [Crack Intro (Wikipedia)](https://en.wikipedia.org/wiki/Crack_intro) --
  History of crack intros from Apple II origins through the birth of the demo scene.

- [C64 Crack Intros Collection](https://www.commodoregames.net/c64_crack_intros.asp) --
  Archive of C64 crack intros with historical context.

- [A Pirate's Life for Me, Part 2: The Scene](https://www.filfre.net/2016/01/a-pirates-life-for-me-part-2-the-scene/) --
  Historical account of the C64 piracy scene, cracking groups, and BBS culture.

- [Crack Intros: Piracy, Creativity, and Communication (academic paper)](https://ijoc.org/index.php/ijoc/article/download/3731/1345) --
  Scholarly analysis of crack intros as cultural artifacts.

- [Cracker's Map (Recollection)](https://www.atlantis-prophecy.org/recollection/?load=crackers_map&country=germany) --
  Geographic mapping of the European cracking scene.

### GCR Encoding

- [Group Coded Recording (Wikipedia)](https://en.wikipedia.org/wiki/Group_coded_recording) --
  General reference on GCR encoding with Commodore's specific 4-to-5 bit conversion table.

- [GCR Decoding on the Fly (Linus Akesson)](https://www.linusakesson.net/programming/gcr-decoding/index.php) --
  Technical article on efficient GCR decoding algorithms.

### Modern Preservation Hardware

- [KryoFlux](https://www.kryoflux.com/) --
  Official site for the KryoFlux flux-level imaging device.

- [SuperCard Pro (CBMStuff)](https://www.cbmstuff.com/) --
  Official site for the SuperCard Pro flux imaging and duplication device.

- [GreaseWeazle (GitHub)](https://github.com/keirf/greaseweazle) --
  Open-source flux-level disk imaging tool.

### Copy Tools

- [Copy Tools & Parameters](https://www.commodoregames.net/copyprotection/copy-tools-parameters.asp) --
  Reference for parameter-based copying software and their capabilities.

- [Fast Hack'em (Wikipedia)](https://en.wikipedia.org/wiki/Fast_Hack'em) --
  History of one of the most popular C64 disk copiers.
