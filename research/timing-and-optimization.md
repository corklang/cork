# Cycle-Exact Timing, Performance Optimization, and Advanced Coding Techniques

Comprehensive reference for the Commodore 64 covering cycle counting, self-modifying code,
lookup tables, multiplication, loop optimization, and low-level tricks that squeeze every
last cycle out of the MOS 6510 CPU.


---

## 1. Overview

The Commodore 64 runs its MOS 6510 CPU (a 6502 variant with an on-chip I/O port) at a
clock speed determined by the video standard:

| Parameter        | PAL (6569)      | NTSC (6567R8)     |
|------------------|-----------------|-------------------|
| CPU clock        | 985,248 Hz      | 1,022,727 Hz      |
| Cycles per line  | 63              | 65                |
| Raster lines     | 312             | 263               |
| Cycles per frame | 19,656          | 17,095            |
| Frame rate       | ~50.125 Hz      | ~59.826 Hz        |

Every single cycle matters because there is no instruction cache, no pipeline overlap
between instructions, and no way to burst above the clock speed. The VIC-II graphics chip
shares the same data bus and actively steals cycles from the CPU on badlines and during
sprite DMA. After accounting for those stolen cycles, the CPU often has far fewer than
19,656 (PAL) or 17,095 (NTSC) cycles per frame for actual computation.

In demoscene and game programming, being off by even one cycle can mean a visible glitch
in a raster effect, a missed register write, or a frame-rate drop. Cycle-exact coding is
not an academic exercise -- it is the fundamental discipline of C64 programming.


---

## 2. Cycle Counting Fundamentals

### 2.1 How to Count Cycles

Every 6502/6510 instruction has a fixed base cycle cost determined by its opcode and
addressing mode. The 6510 has no caches and no speculative execution -- each instruction
completes fully before the next one begins. To count cycles, you add up the cost of each
instruction in your code path.

Key rules:

- **Each memory access takes one clock cycle.** An instruction's cycle count equals the
  number of bus accesses it performs (opcode fetch, operand fetch, dummy reads, effective
  address calculation, data read/write, etc.).
- **Instruction fetch is included.** The opcode byte fetch counts as cycle 1.
- **Read-modify-write instructions** perform an extra dummy write of the unmodified value
  before writing the result (this is why INC abs takes 6 cycles, not 5).

### 2.2 Instruction Cycle Costs by Addressing Mode

| Addressing Mode        | Read (LDA etc.) | Write (STA etc.) | RMW (INC etc.) |
|------------------------|------------------|-------------------|----------------|
| Implied / Accumulator  | 2                | --                | 2              |
| Immediate              | 2                | --                | --             |
| Zero Page              | 3                | 3                 | 5              |
| Zero Page,X / Y        | 4                | 4                 | 6              |
| Absolute               | 4                | 4                 | 6              |
| Absolute,X / Y         | 4(+1)            | 5                 | 7              |
| (Indirect,X)           | 6                | 6                 | 8              |
| (Indirect),Y           | 5(+1)            | 6                 | 8              |
| JMP absolute           | 3                | --                | --             |
| JMP (indirect)         | 5                | --                | --             |
| JSR                    | 6                | --                | --             |
| RTS                    | 6                | --                | --             |
| RTI                    | 6                | --                | --             |
| BRK                    | 7                | --                | --             |
| Branch (not taken)     | 2                | --                | --             |
| Branch (taken, same pg)| 3                | --                | --             |
| Branch (taken, cross)  | 4                | --                | --             |
| PHA / PHP              | 3                | --                | --             |
| PLA / PLP              | 4                | --                | --             |

**(+1)** = add one cycle if indexing crosses a page boundary (high byte of effective
address differs from base address).

### 2.3 Page-Crossing Penalties

A page boundary is crossed when adding an index register to a base address causes the low
byte to wrap from $FF to $00 (incrementing the high byte). Only **read** instructions with
Absolute,X / Absolute,Y / (Indirect),Y addressing modes incur the penalty. Write and
read-modify-write instructions always take the worst-case time because they must calculate
the correct address before writing.

Example:
```
LDA $10F0,X    ; X = $20 -> effective = $1110, no page cross -> 4 cycles
LDA $10F0,X    ; X = $30 -> effective = $1120, crosses $10FF -> 5 cycles
STA $10F0,X    ; always 5 cycles regardless of page crossing
```

### 2.4 Branch Penalties

Conditional branches (BEQ, BNE, BCC, BCS, BMI, BPL, BVC, BVS) cost:
- **2 cycles** if the branch is NOT taken
- **3 cycles** if taken, target on the same page
- **4 cycles** if taken, target on a different page

This means tight loops should avoid branches that cross page boundaries. Aligning loop
bodies so the branch target stays within the same 256-byte page saves one cycle per
iteration.

### 2.5 Total Cycles Per Frame

**PAL (6569):**
- 63 cycles/line x 312 lines = **19,656 cycles/frame**
- At ~50.125 Hz, this gives ~985,248 cycles/second

**NTSC (6567R8):**
- 65 cycles/line x 263 lines = **17,095 cycles/frame**
- At ~59.826 Hz, this gives ~1,022,727 cycles/second

**NTSC (6567R56A):**
- 64 cycles/line x 262 lines = **16,768 cycles/frame** (early NTSC revision)

### 2.6 VIC-II Cycle Stealing

The VIC-II shares the system bus with the CPU using a two-phase clock:
- **Phase 1 (phi1):** VIC-II accesses the bus (character pointers, bitmap data, sprites)
- **Phase 2 (phi2):** CPU accesses the bus

This interleaving means the CPU and VIC-II normally coexist without conflict. However, the
VIC-II sometimes needs phase-2 cycles as well, and it takes them by force.

#### Badlines

A **badline** occurs when all three conditions are true:
1. The raster counter (RASTER) is between $30 and $F7 (the display window)
2. The lower 3 bits of RASTER match YSCROLL ($D011 bits 0-2)
3. The DEN (Display Enable) bit in $D011 was set during raster line $30

On a badline, the VIC-II must fetch 40 bytes of character pointers (c-accesses) from
screen RAM. This happens during cycles 15-54 of the raster line (PAL), stealing
approximately **40 cycles** from the CPU. The CPU retains only about **23 usable cycles**
on a badline (20 cycles before cycle 15 and 3 cycles after cycle 54, minus overhead).

In standard text/bitmap mode, badlines occur every 8th raster line within the display
window, giving 25 badlines per frame.

#### How BA/AEC Works

Three cycles before the VIC-II needs the phase-2 bus, it pulls the **BA (Bus Available)**
line low. BA is connected to the CPU's RDY pin. The RDY pin only halts the CPU on **read**
cycles -- the CPU can still complete up to 3 consecutive write cycles while BA is low.
After 3 cycles with BA low, the VIC-II also takes AEC (Address Enable Control) low during
phase 2, fully locking out the CPU.

This means:
- A 2-cycle instruction that reads on its last cycle will be halted immediately
- A write instruction (like STA) can complete even after BA goes low
- The CPU never performs more than 3 consecutive writes, so the VIC-II is guaranteed access

#### Sprite DMA

Each active sprite costs additional CPU cycles. The VIC-II must fetch the sprite pointer
(1 p-access) and 3 bytes of sprite data (3 s-accesses) for each sprite on each raster
line it spans. Two of the s-accesses occur during phase 2, stealing those cycles from the
CPU.

Approximate cycle costs per raster line:

| Active Sprites | Stolen Cycles | CPU Cycles Remaining (normal line) |
|----------------|---------------|------------------------------------|
| 0              | 0             | 63 (PAL)                           |
| 1              | ~2            | ~61                                |
| 2              | ~4            | ~59                                |
| 3              | ~6            | ~57                                |
| 8 (all)        | ~17           | ~46                                |

On a **badline with 8 sprites**, only about **4-7 cycles** remain for CPU execution.

#### Effective CPU Time Per Frame (PAL)

Rough calculation for a typical game screen (25 badlines, 8 sprites in display area):

- 312 total lines
- 200 lines in display window, of which:
  - 25 are badlines: 25 x 23 = 575 cycles
  - 175 are normal display lines with sprites: 175 x 46 = 8,050 cycles
- 112 border/blanking lines (no stealing): 112 x 63 = 7,056 cycles
- **Total: ~15,681 cycles** (vs 19,656 theoretical maximum)

With no sprites and careful scheduling, you can recover many of those stolen cycles. The
border and vertical blanking lines are particularly valuable because they are never
interrupted by the VIC-II.


---

## 3. Self-Modifying Code

### 3.1 What It Is

Self-modifying code (SMC) is code that writes to its own instruction bytes at runtime,
changing opcodes or operand addresses. On the 6510 this works perfectly because:

- The CPU has no instruction cache -- it reads each opcode byte directly from RAM every
  time it executes
- All program code lives in RAM (no hardware write protection)
- The modification takes effect on the very next execution of the modified instruction

SMC is so fundamental to C64 programming that virtually every demo, game, and performance-
sensitive utility uses it extensively. On architectures with instruction caches, SMC causes
cache invalidation penalties; on the 6510 there is zero penalty.

### 3.2 Common Patterns

#### Modifying Load/Store Addresses

The most common pattern: overwrite the address bytes of an LDA or STA instruction to
redirect where it reads from or writes to.

```asm
        ; Copy a byte from a dynamically computed source
        LDA source_addr     ; <- this address gets modified
        STA destination
        ; ...
        ; Elsewhere, update the source:
        LDA new_addr_lo
        STA load_instr+1    ; modify low byte of operand
        LDA new_addr_hi
        STA load_instr+2    ; modify high byte of operand
load_instr:
        LDA $0000           ; will read from new_addr at runtime
```

**Why:** This avoids using (indirect),Y addressing (5-6 cycles) and replaces it with
absolute addressing (4 cycles), saving 1-2 cycles per access. It also frees up the Y
register and a zero-page pointer pair.

#### Modifying Immediate Values

Store a computed value directly into an instruction's immediate operand:

```asm
        ; Instead of: LDA variable / CMP #threshold
        ; We modify the CMP operand directly:
        LDA new_threshold
        STA cmp_instr+1
        ; ...
cmp_instr:
        CMP #$00            ; operand gets patched to new_threshold
```

#### Modifying Branch Targets

Change a branch offset to redirect control flow:

```asm
        LDA #offset
        STA branch_instr+1
branch_instr:
        BNE target          ; target changes based on stored offset
```

#### Switching Instructions (Opcode Modification)

Replace an entire opcode to change behavior:

```asm
        ; Toggle between enabled (BIT = skip next 2 bytes) and disabled (JMP)
enable:
        LDA #$2C            ; BIT absolute opcode
        STA toggle
        RTS
disable:
        LDA #$4C            ; JMP absolute opcode
        STA toggle
        RTS
        ; ...
toggle: BIT skip_target     ; gets changed to JMP when disabled
        ; normal code path continues here
```

#### Self-Modifying Register Save/Restore

Used in interrupt handlers to avoid stack operations (which cost extra cycles):

```asm
irq_handler:
        STA restore_a+1     ; save A into upcoming LDA # operand
        STX restore_x+1     ; save X into upcoming LDX # operand
        STY restore_y+1     ; save Y into upcoming LDY # operand
        ; ... do work ...
restore_a:
        LDA #$00            ; patched value
restore_x:
        LDX #$00            ; patched value
restore_y:
        LDY #$00            ; patched value
        RTI
```

This saves 3 cycles vs PHA/PHX/PHY + PLA/PLX/PLY and avoids touching the stack.

### 3.3 Unrolled Loops with Modified Addresses

A powerful pattern combines loop unrolling with address modification. Instead of a loop
counter and indexed addressing, you write straight-line code where each instruction's
address was patched at setup time:

```asm
        ; Clear 40 screen columns -- generated at runtime
        LDA #$20            ; space character
        STA $0400           ; these addresses are patched
        STA $0401           ; to point at the correct
        STA $0402           ; screen locations
        ; ... 37 more STA instructions ...
```

The setup routine writes the correct addresses into each STA operand. This eliminates all
loop overhead (DEX, BNE, index calculation) and replaces indexed addressing (5 cycles for
STA abs,X) with absolute addressing (4 cycles for STA abs), saving one cycle per store.

### 3.4 Speed Tables

A "speed table" is a block of self-modifying code used as a computed-goto dispatch table.
Each entry is an instruction sequence whose operands have been pre-patched. This is
commonly used in music players, sprite multiplexers, and raster effects where different
actions must be taken on different raster lines.

### 3.5 Why SMC Is Essential on the C64

- **No indirect addressing mode for X register.** The 6502 has (zp,X) and (zp),Y but no
  (zp),X or (zp,Y). SMC fills these gaps.
- **Limited registers.** With only A, X, Y, you quickly run out. SMC lets you embed
  "variables" directly in instruction operands.
- **Faster than indirection.** LDA abs (4 cycles) beats LDA (zp),Y (5+ cycles).
- **Zero-cost switching.** Changing an opcode (3 cycles to STA) beats a runtime flag check
  (LDA flag + BEQ/BNE = 4-5 cycles) every time the switched instruction executes.
- **No cache penalty.** Unlike modern CPUs, there is literally no downside to modifying
  code on the 6510.


---

## 4. Lookup Tables

### 4.1 Pre-Computed Tables vs Runtime Calculation

On the 6510, multiplication takes dozens to hundreds of cycles, division even more, and
transcendental functions are out of the question at runtime. The standard solution is to
pre-compute results into lookup tables and replace calculation with a single indexed load
(4 cycles for LDA table,X).

The tradeoff is memory for speed. A 256-byte table costs 256 bytes of RAM but replaces an
arbitrarily expensive calculation with a 4-cycle table lookup. On a machine with 64KB of
RAM and a ~1 MHz clock, this tradeoff almost always favors tables.

### 4.2 Common Lookup Tables

#### Sine/Cosine Tables

Sine tables are nearly universal in C64 demos and games. A typical implementation:

- 256 entries (one full period maps to indices 0-255, wrapping naturally with 8-bit math)
- Values scaled to a useful range (e.g., 0-127 for unsigned, or -128 to +127 for signed)
- Generated at startup using BASIC or an assembler scripting language:

```
; KickAssembler syntax
.align $100
sine_table:
    .fill 256, round(127.5 + 127.5 * sin(toRadians(i * 360 / 256)))
```

Having 256 entries means the index wraps naturally with 8-bit arithmetic -- incrementing
past 255 wraps to 0, which is exactly one full period.

#### Multiply Tables

See section 5 (Fast Multiplication) for detailed coverage of quarter-square multiply
tables.

#### Screen Address Tables

Computing `row * 40 + column` for screen or bitmap addressing is expensive at runtime.
Split low/high byte tables eliminate this entirely:

```asm
; Pre-computed row address tables for screen at $0400
screen_lo:
    .byte <$0400, <$0428, <$0450, <$0478, <$04A0  ; rows 0-4
    .byte <$04C8, <$04F0, <$0518, <$0540, <$0568  ; rows 5-9
    ; ... etc for all 25 rows
screen_hi:
    .byte >$0400, >$0428, >$0450, >$0478, >$04A0
    .byte >$04C8, >$04F0, >$0518, >$0540, >$0568
    ; ... etc for all 25 rows

; Usage: set pointer to row Y, then index by column with X
    LDA screen_lo,Y
    STA ptr
    LDA screen_hi,Y
    STA ptr+1
    LDA (ptr),X         ; or use self-modifying code instead
```

#### Bit Mask Tables

For bitmap graphics, a table of single-bit masks indexed by bit position:

```asm
bit_mask:   .byte $80, $40, $20, $10, $08, $04, $02, $01
```

This replaces shift loops that would cost 2 cycles per shift x up to 7 shifts = 14 cycles
with a single 4-cycle table lookup.

#### Power-of-Two Tables

```asm
pow2:       .byte 1, 2, 4, 8, 16, 32, 64, 128
```

#### Log/Exp Tables

For approximate multiplication via logarithms: a*b = exp(log(a) + log(b)). Requires two
256-byte tables (log and exp). Less accurate than quarter-square but useful when
approximate results are acceptable.

### 4.3 256-Byte Aligned Tables

Placing a table at a page-aligned address (low byte = $00, e.g., $xx00) provides several
advantages:

- **No page-crossing penalty.** If the table starts at a page boundary and is <= 256
  bytes, LDA table,X will never cross a page boundary, guaranteeing 4 cycles.
- **High byte is constant.** When using self-modifying code, you only need to update the
  low byte of the table address, saving one STA instruction.
- **Index IS the offset.** The index register directly gives you the byte offset within the
  table, simplifying address arithmetic.
- **Split-table optimization.** With two 256-byte tables at $xx00 and $yy00, you can
  switch between them by changing only the high byte of the address.

Assembler directives for alignment:
```asm
    .align $100         ; KickAssembler
    * = (* + $FF) & $FF00  ; generic: round up to next page
```

### 4.4 Memory vs Speed Tradeoff

| Approach               | Memory Cost | Access Time | When to Use              |
|------------------------|-------------|-------------|--------------------------|
| Runtime calculation    | 0 bytes     | 20-200+ cyc | Rarely; only if RAM-bound|
| 256-byte table         | 256 bytes   | 4 cycles    | Default choice           |
| 512-byte table         | 512 bytes   | 4 cycles    | Square tables, multiply  |
| 2 KB table (4x256)     | 2048 bytes  | 4 cycles    | Fast multiply, sprites   |
| Interleaved tables     | Varies      | 4 cycles    | Multiple related lookups |

On the C64, memory is usually more available than cycles. A 64KB address space with ROM
banking provides substantial room for tables. The general rule: **if a calculation is
performed inside a loop or per-frame, replace it with a table.**


---

## 5. Fast Multiplication

### 5.1 The Problem

The 6510 has no multiply instruction. A naive shift-and-add loop for 8x8-bit
multiplication costs approximately 150-200 cycles on average. For real-time graphics and
audio, this is unacceptable -- a single frame might require hundreds of multiplications.

### 5.2 Quarter-Square Method

The mathematical identity behind fast table-based multiplication:

```
a * b = ((a + b)^2 - (a - b)^2) / 4
```

This is the "quarter-square" formula. Since squaring can be done by table lookup, the
entire multiplication reduces to:

1. Compute `a + b` and `a - b`
2. Look up `(a+b)^2 / 4` and `(a-b)^2 / 4` in a table
3. Subtract the two results

#### Table Construction

The quarter-square table stores `floor(n^2 / 4)` for n = 0 to 511:

```
f(n) = floor(n * n / 4)

n:    0  1  2  3  4  5  6  7  8  9  ...
f(n): 0  0  1  2  4  6  9  12 16 20 ...
```

This requires a 512-entry table (since a+b can be as large as 510 for unsigned 8-bit
inputs). Each entry is 16 bits, so the full table is 1024 bytes. However, by splitting
into low and high byte tables and aligning them on page boundaries, lookup is efficient.

#### Implementation (8x8 -> 16-bit unsigned)

The standard approach uses four 256-byte page-aligned tables:

```asm
; Tables: sqr_lo, sqr_hi contain f(n) = floor(n^2/4) for n=0..511
; Each split into two 256-byte pages for low/high byte access

; Input: A = multiplicand, X = multiplier
; Output: result_lo, result_hi

multiply:
        STA sm1+1           ; self-modify: store A as index base
        STA sm3+1
        EOR #$FF            ; negate A for (b - a) calculation
        STA sm2+1
        STA sm4+1
        ; Now compute f(a+b) - f(a-b):
sm1:    LDA sqr_lo,X        ; f(a+b) low byte    (a stored in operand)
sm2:    SBC sqr_lo,X        ; - f(b-a) low byte
        STA result_lo
sm3:    LDA sqr_hi,X        ; f(a+b) high byte
sm4:    SBC sqr_hi,X        ; - f(b-a) high byte
        STA result_hi
        RTS
```

**Performance:** Approximately 25-45 cycles depending on implementation (excluding
JSR/RTS overhead), using 2 KB of tables (four 512-byte halves, page-aligned).

Compare this to ~150 cycles average for a shift-and-add loop. The table-based approach
is roughly **3-6x faster**.

### 5.3 Alternative Quarter-Square Formulation

An alternative formula avoids the sign issue with (a - b):

```
a * b = f(a + b) - f(a) - f(b)
    where f(n) = floor(n^2 / 2)
```

This uses a 512-byte pre-shifted squaring table and three lookups instead of dealing with
signed subtraction. Implementation uses ~79-83 cycles with a 512-byte table.

### 5.4 8x8 -> 16-bit Performance Comparison

Based on extensive benchmarking (TobyLobster/multiply_test repository, 120+ routines):

| Method                     | Avg Cycles | Table Size | Code Size |
|----------------------------|------------|------------|-----------|
| Shift-and-add (basic)      | ~166       | 0 bytes    | 27 bytes  |
| Shift-and-add (unrolled)   | ~148       | 0 bytes    | 144 bytes |
| Optimized shift-and-add    | ~130       | 0 bytes    | ~50 bytes |
| Quarter-square (512B table)| ~68        | 512 bytes  | ~40 bytes |
| Quarter-square (2KB table) | ~42        | 2048 bytes | ~30 bytes |
| Fully optimized (2KB table)| ~25        | 2048 bytes | ~25 bytes |

### 5.5 16x16 -> 32-bit Multiplication

A 16x16 multiply decomposes into four 8x8 multiplies using the identity:

```
(A*256 + B) * (C*256 + D) = A*C*65536 + (A*D + B*C)*256 + B*D
```

Each of the four partial products (A*C, A*D, B*C, B*D) uses the fast 8x8 table multiply,
then results are shifted and accumulated. Total cost: ~150-250 cycles depending on
implementation, vs 500+ cycles for shift-and-add.

### 5.6 Special-Case Multiplies

When one factor is a constant, specialized code is much faster:

```asm
; Multiply A by 10:   A*10 = A*8 + A*2
        ASL             ; *2  (2 cycles)
        STA temp        ;     (3 cycles)
        ASL             ; *4  (2 cycles)
        ASL             ; *8  (2 cycles)
        CLC             ;     (2 cycles)
        ADC temp        ; *10 (3 cycles)
        ; Total: 14 cycles

; Multiply A by 40:   A*40 = A*32 + A*8
        ASL             ; *2
        ASL             ; *4
        ASL             ; *8
        STA temp
        ASL             ; *16
        ASL             ; *32
        CLC
        ADC temp        ; *40
        ; Total: 17 cycles (but only works for inputs 0-6 in 8 bits)

; Multiply A by 5:    A*5 = A*4 + A
        STA temp
        ASL             ; *2
        ASL             ; *4
        CLC
        ADC temp        ; *5
        ; Total: 12 cycles
```

For multiplication by powers of two, simple ASL chains suffice (2 cycles each).


---

## 6. Loop Optimization

### 6.1 Unrolled Loops (Speedcode)

Loop unrolling -- called "speedcode" in the C64 community -- is the most important single
optimization technique for time-critical code. It eliminates the per-iteration overhead of
loop control (counter decrement, branch, index increment).

#### Basic Example

A loop that copies 40 bytes:

```asm
; Looped version: 40 * (4+5+2+3) = 40 * 14 = 560 cycles + setup
        LDX #39
loop:   LDA source,X        ; 4 cycles (+1 if page cross)
        STA dest,X          ; 5 cycles
        DEX                 ; 2 cycles
        BPL loop            ; 3 cycles (taken)
        ; Per iteration: 14 cycles
        ; Total: ~564 cycles (40 iterations, last BPL = 2)

; Unrolled version: 40 * (4+4) = 320 cycles
        LDA source+0        ; 4 cycles (absolute, not indexed!)
        STA dest+0           ; 4 cycles
        LDA source+1
        STA dest+1
        ; ... 38 more pairs ...
        ; Per "iteration": 8 cycles
        ; Total: 320 cycles -- 43% faster
```

The unrolled version is faster for two reasons:
1. No DEX/BPL overhead (5 cycles saved per iteration)
2. Absolute addressing (4 cycles) instead of absolute,X (4-5 cycles for read, 5 for write)

The cost: 40 * 6 = 240 bytes of code vs ~10 bytes for the loop.

#### When Speedcode Is Generated at Runtime

For large unrolled sections, writing the code manually is impractical. C64 programmers
routinely generate speedcode at runtime:

```asm
; Generator: create "STA $xxxx" sequence for 1000 locations
        LDX #0
gen_loop:
        LDA #$8D            ; STA absolute opcode
        STA code_buf,X
        INX
        LDA addr_lo_table,Y ; low byte of target address
        STA code_buf,X
        INX
        LDA addr_hi_table,Y ; high byte of target address
        STA code_buf,X
        INX
        INY
        CPY #200            ; number of entries
        BNE gen_loop
```

This "speedcode generator" approach is standard in demos: a compact generator (~30 bytes)
produces kilobytes of optimized straight-line code at startup. The generator runs once;
the generated code runs every frame.

### 6.2 DEX/BNE: Counting Down to Zero

The 6502's zero flag is set automatically by DEX, DEY, INX, INY, and all load/transfer
instructions. This means counting down to zero costs only 2 instructions (DEX + BNE)
with no explicit CMP needed:

```asm
        LDX #40
loop:   ; ... do work ...
        DEX                 ; 2 cycles, sets Z when X reaches 0
        BNE loop            ; 3 cycles if taken, 2 if not
```

Counting UP requires a CMP instruction (adding 2-3 cycles per iteration):

```asm
        LDX #0
loop:   ; ... do work ...
        INX                 ; 2 cycles
        CPX #40             ; 2 cycles  <-- extra cost
        BNE loop            ; 3 cycles
```

**Always count down when possible.** If the loop body uses X as an index and needs
ascending order, restructure the data or offset the base address:

```asm
; Instead of: LDA source,X with X going 0..39
; Use: LDA source+39,X with X going 0 downto -39 (i.e., X = 40 downto 1)
        LDX #40
loop:   LDA source-1,X      ; offset base address by -1
        STA dest-1,X
        DEX
        BNE loop
```

### 6.3 Page-Aligned Loop Bodies

If a loop's branch instruction crosses a page boundary (the branch target is on a
different page than the instruction following the branch), each taken branch costs an extra
cycle. For a loop that executes 256 times, that is 256 wasted cycles.

Solution: align the loop body so the branch target is on the same page:

```asm
        .align $100         ; start on page boundary
loop:   ; ... loop body ...
        DEX
        BNE loop            ; target is on same page = 3 cycles, not 4
```

Alternatively, rearrange code so the branch instruction and its target are within the
same 256-byte page without full page alignment.

### 6.4 Double-Buffered Updates

For screen updates that must not be visible during drawing, double buffering is used:

1. Display screen buffer A while drawing to screen buffer B
2. At VBlank, switch VIC-II to display buffer B
3. Next frame: draw to buffer A while displaying B
4. Repeat

On the C64, this is done by changing the screen memory pointer in $D018:

```asm
        ; Switch between screen at $0400 and $0800
        LDA $D018
        EOR #$10            ; toggle bit 4 (screen address)
        STA $D018
```

**Important limitation:** Color RAM ($D800-$DBFF) cannot be double-buffered because it
exists only at a fixed hardware address. Color RAM updates must be synchronized with the
raster beam, typically copying during VBlank or line-by-line using raster interrupts.


---

## 7. Memory Access Optimization

### 7.1 Zero Page as Register File

The 6510 has only three general-purpose registers (A, X, Y). The 256-byte zero page
($0000-$00FF) effectively extends this to a much larger register file:

- **Zero page instructions are 1 byte shorter** (2 bytes vs 3 bytes for absolute)
- **Zero page instructions are 1 cycle faster** (3 cycles vs 4 for load/store)
- **Indirect addressing requires zero page.** Both (zp,X) and (zp),Y modes only work with
  zero-page pointers.

| Operation       | Zero Page  | Absolute   | Savings |
|-----------------|------------|------------|---------|
| LDA zp          | 3 cyc, 2B | LDA abs: 4 cyc, 3B | 1 cyc, 1B |
| STA zp          | 3 cyc, 2B | STA abs: 4 cyc, 3B | 1 cyc, 1B |
| INC zp          | 5 cyc, 2B | INC abs: 6 cyc, 3B | 1 cyc, 1B |
| LDA (zp),Y      | 5 cyc, 2B | (not available)     | --         |
| ASL zp          | 5 cyc, 2B | ASL abs: 6 cyc, 3B | 1 cyc, 1B |

#### Free Zero-Page Locations

After disabling BASIC and KERNAL ROMs, most zero-page addresses become available. With
ROMs enabled, the safest free locations are:

- **$02:** Unused by BASIC or KERNAL
- **$FB-$FE:** Documented as free for user programs (4 bytes = 2 pointers)
- **$22-$25:** Used by BASIC but safe if BASIC is not running

In practice, demo and game code typically disables ROMs entirely and treats all of
$02-$FF as a 254-byte register file.

#### Optimization Strategy

Move your most frequently accessed variables to zero page. In a typical game loop:
- Frame counter, scroll position, player coordinates -> zero page
- Temporary pointers for copy loops -> zero page
- Rarely-used configuration flags -> regular RAM

A real-world measurement showed that converting a routine from absolute to zero-page
addressing reduced code size by 13% (46 to 40 bytes) and cycle count by 10.5% (319 to
285 cycles).

### 7.2 Page Boundary Alignment

Beyond tables (section 4.3) and loops (section 6.3), page alignment matters for:

- **Code placement:** Ensuring that critical code paths do not have branches crossing page
  boundaries.
- **Stack page:** The hardware stack is fixed at $0100-$01FF. Accessing stack data with
  `LDA $0100,X` after TSX never crosses a page boundary.
- **I/O registers:** VIC-II registers at $D000-$D3FF and SID at $D400-$D7FF are always at
  fixed addresses. Common patterns like `STA $D020` (border color) compile to known,
  fixed cycle counts.

### 7.3 Data Interleaving and Structure of Arrays

When processing multiple attributes of many objects (e.g., sprite X, Y, color for 8
sprites), store attributes in separate arrays rather than interleaved structs:

```asm
; BAD: Array of Structures (interleaved)
; sprite_data: x0, y0, c0, x1, y1, c1, x2, y2, c2, ...
; Accessing X requires: LDA sprite_data,X with X = i*3  (messy multiplication!)

; GOOD: Structure of Arrays (split)
sprite_x:  .byte x0, x1, x2, x3, x4, x5, x6, x7
sprite_y:  .byte y0, y1, y2, y3, y4, y5, y6, y7
sprite_col: .byte c0, c1, c2, c3, c4, c5, c6, c7
; Accessing X: LDA sprite_x,X with X = i  (simple!)
```

Split arrays allow natural indexing with X or Y registers, eliminate stride multiplication,
and keep related data within a single page for cache-free (page-crossing-free) access.

### 7.4 Split High/Low Byte Tables

For 16-bit address tables, split into separate low-byte and high-byte arrays:

```asm
; Instead of a single table of 16-bit addresses:
;   addr_table: .word $0400, $0428, $0450, ...  (requires ASL to index)

; Use split tables:
addr_lo: .byte <$0400, <$0428, <$0450, ...
addr_hi: .byte >$0400, >$0428, >$0450, ...

; Access with simple index:
        LDY row
        LDA addr_lo,Y       ; 4 cycles
        STA ptr
        LDA addr_hi,Y       ; 4 cycles
        STA ptr+1
```

This avoids the need to multiply the index by 2 (ASL, etc.) and saves 2 bytes and 4
cycles over the word-table approach.


---

## 8. Common Optimization Patterns

### 8.1 Tail Call Optimization: JSR/RTS -> JMP

When the last thing a subroutine does is call another subroutine, replace JSR + RTS with
JMP:

```asm
; SLOW: 6 + 6 + 6 = 18 cycles for call chain
my_routine:
        ; ... do work ...
        JSR other_routine   ; 6 cycles
        RTS                 ; 6 cycles

; FAST: 6 + 3 = 9 cycles -- saves 9 cycles and 1 byte
my_routine:
        ; ... do work ...
        JMP other_routine   ; 3 cycles (other_routine's RTS returns to our caller)
```

This is safe as long as my_routine does not need to do anything after other_routine
returns. The RTS inside other_routine will return directly to whatever called my_routine.

### 8.2 Stack for Fast Data Access

The hardware stack ($0100-$01FF) can be used as a fast data buffer:

```asm
; Push data, then access it directly via stack pointer
        TSX                 ; save stack pointer
        STX saved_sp
        ; ... push data with PHA ...
        ; Access top of stack without popping:
        TSX
        LDA $0101,X         ; read top of stack (4 cycles, no pop)
        LDA $0102,X         ; read second item
        ; Restore stack when done:
        LDX saved_sp
        TXS
```

The key insight: `LDA $0101,X` (after TSX) reads the top stack byte without removing it,
at the same cost as PLA (4 cycles) but without the destructive side effect. This is used
extensively in Exile and Prince of Persia for fast sprite data access.

### 8.3 Decimal Mode

The 6510 supports BCD (Binary Coded Decimal) arithmetic via the SED instruction. While
rarely used for optimization, it has specific uses:

- **Score display:** A BCD score counter can be printed directly without binary-to-decimal
  conversion (which is expensive on the 6502).
- **CLC/SED/ADC sequence:** Adding in decimal mode automatically handles carry between
  decimal digits.

```asm
        SED                 ; enable decimal mode
        CLC
        LDA score_lo
        ADC #$01            ; increment score by 1 (BCD)
        STA score_lo
        LDA score_hi
        ADC #$00            ; propagate carry
        STA score_hi
        CLD                 ; ALWAYS clear decimal mode when done
```

**Warning:** Always CLD before returning from an interrupt handler or entering time-
critical code. BCD mode affects the cycle count of ADC and SBC (1 extra cycle on CMOS
65C02, but not on NMOS 6502/6510 -- same cycle count, different results).

### 8.4 Illegal Opcodes for Combined Operations

The NMOS 6510 has 151 official opcodes and 105 "illegal" (undocumented) opcodes. Many of
these perform two operations in the time of one, making them valuable for optimization.

#### Stable, Widely-Used Illegal Opcodes

| Mnemonic | Hex (example) | Operation               | Cycles | Equivalent        |
|----------|---------------|-------------------------|--------|-------------------|
| LAX      | $AF           | LDA + LDX (same value)  | 4      | LDA abs + TAX     |
| SAX      | $8F           | STA (A AND X)           | 4      | (no equivalent)   |
| DCP      | $CF           | DEC memory + CMP A      | 6      | DEC abs + CMP abs |
| ISC      | $EF           | INC memory + SBC A      | 6      | INC abs + SBC abs |
| SLO      | $0F           | ASL memory + ORA A      | 6      | ASL abs + ORA abs |
| SRE      | $4F           | LSR memory + EOR A      | 6      | LSR abs + EOR abs |
| RLA      | $2F           | ROL memory + AND A      | 6      | ROL abs + AND abs |
| RRA      | $6F           | ROR memory + ADC A      | 6      | ROR abs + ADC abs |
| ANC      | $0B           | AND # + copy bit 7 to C | 2      | AND # + (partial) |
| ALR      | $4B           | AND # + LSR A           | 2      | AND # + LSR       |
| ARR      | $6B           | AND # + ROR A (special) | 2      | AND # + ROR       |
| SBX      | $CB           | (A AND X) - # -> X      | 2      | CMP + DEX combo   |

#### Practical Uses

**LAX (Load A and X simultaneously):**
```asm
        LAX $FB             ; load zero-page value into both A and X (3 cycles)
        ; vs: LDA $FB + TAX  (3 + 2 = 5 cycles) -- saves 2 cycles
```

**SAX (Store A AND X):**
```asm
        ; Useful when A and X together encode a value you want to store
        LDA #$0F
        LDX #$F3
        SAX $D020           ; stores $0F AND $F3 = $03 (border color)
```

**DCP (Decrement and Compare in one instruction):**
```asm
        ; Countdown timer with automatic comparison
        DCP counter         ; decrement counter, compare with A
        BEQ timer_expired   ; branch if counter reached A's value
```

**SLO (Shift Left and OR):**
```asm
        ; Combine a shift operation with accumulation
        SLO bitmap_byte     ; shift bitmap left, OR result into A
```

#### Multi-Byte NOP Variants

Illegal NOPs with different addressing modes consume different numbers of bytes and cycles,
useful for precise timing adjustment:

| Opcode | Bytes | Cycles | Notes                    |
|--------|-------|--------|--------------------------|
| $EA    | 1     | 2      | Official NOP             |
| $1A etc| 1     | 2      | Implied NOP (unofficial) |
| $80    | 2     | 2      | Immediate NOP (skip 1 byte) |
| $04    | 2     | 3      | Zero-page NOP            |
| $0C    | 3     | 4      | Absolute NOP             |

These are invaluable for padding code to exact cycle counts without affecting registers
or flags.

#### Unstable Opcodes (Use with Caution)

Some opcodes (ANE/$8B, LXA/$AB, SHA, SHX, SHY, TAS) have behavior that varies between
individual chips, temperature, and even voltage. These should generally be avoided in
production code, though some demos use them when targeting specific hardware.

### 8.5 The BIT Skip-Byte Trick

The BIT instruction reads a byte from memory and sets flags but does not modify A, X, or
Y. This property makes it useful for "hiding" instructions:

```asm
; Two entry points sharing the same code:
entry_with_flag:
        LDA #$01            ; A = 1 (flag set)
        .byte $2C           ; BIT absolute -- "eats" the next 2 bytes as its operand
entry_without_flag:
        LDA #$00            ; A = 0 (flag clear) -- skipped if entering from above
        ; common code continues here with A = 0 or 1
        STA flag_value
```

When execution enters at `entry_with_flag`, the sequence is:
1. LDA #$01 (A = 1)
2. BIT $00A9 (reads from $00A9, sets flags, but A is still 1)
3. STA flag_value

When execution enters at `entry_without_flag`:
1. LDA #$00 (A = 0)
2. STA flag_value

The `$2C` byte (BIT absolute) consumes the `$A9 $00` bytes of the LDA #$00 instruction
as its operand, effectively skipping it. This saves a JMP or branch instruction (2-3
bytes, 3 cycles).

**Variants:**
- `$2C` (BIT abs): skips 2 bytes, costs 4 cycles, clobbers N/V/Z flags
- `$24` (BIT zp): skips 1 byte, costs 3 cycles, clobbers N/V/Z flags
- `$C9` (CMP #imm): skips 1 byte, costs 2 cycles, clobbers N/Z/C flags
- `$89` (BIT #imm, 65C02 only): skips 1 byte, costs 2 cycles, clobbers Z flag only

### 8.6 RTS Jump Table

The RTS instruction pops a 16-bit address from the stack, adds 1, and jumps there. This
can be exploited to implement fast jump tables:

```asm
; Jump table using RTS trick
; Table contains (address - 1) for each target
jump_table_lo: .byte <(routine_0-1), <(routine_1-1), <(routine_2-1)
jump_table_hi: .byte >(routine_0-1), >(routine_1-1), >(routine_2-1)

dispatch:
        ; X = index of routine to call
        LDA jump_table_hi,X ; 4 cycles
        PHA                 ; 3 cycles
        LDA jump_table_lo,X ; 4 cycles
        PHA                 ; 3 cycles
        RTS                 ; 6 cycles -- jumps to routine_X
        ; Total: 20 cycles
```

Compare to JMP (indirect): `JMP ($xxxx)` takes 5 cycles but requires the address in
consecutive RAM bytes (and has the infamous JMP indirect page-boundary bug on the NMOS
6502). The RTS trick uses split tables (more cache-friendly indexing) and costs 20 cycles
for a variable dispatch, but allows any number of targets with simple byte indexing.


---

## 9. Hardcore Details

### 9.1 Cycle-by-Cycle Instruction Execution

Every 6510 instruction executes as a fixed sequence of bus operations, one per clock cycle.
There is no overlap between instructions -- the last cycle of one instruction is always
followed by the opcode fetch of the next.

Example: `LDA $1234,X` (Absolute,X addressing, 4 or 5 cycles)

```
Cycle 1: Fetch opcode ($BD) from PC. Increment PC.
Cycle 2: Fetch low byte of address ($34) from PC. Increment PC.
Cycle 3: Fetch high byte of address ($12) from PC. Increment PC.
          Add X to low byte internally: effective_lo = $34 + X.
Cycle 4: Read from ($12 * 256 + effective_lo).
          If no carry from addition (no page cross): this is the correct data. Done.
          If carry: this read is from the wrong page (a "dummy read"). Continue.
Cycle 5: Read from correct address (high byte incremented). Done.
```

This explains the page-crossing penalty: the CPU optimistically reads from the
uncorrected address on cycle 4. If there was no page cross, the data is correct and the
instruction completes. If there was a page cross, cycle 4 was wasted and a corrected read
happens on cycle 5.

**Read-modify-write** instructions (INC, DEC, ASL, LSR, ROL, ROR) always include a dummy
write of the original value before writing the modified value:

```
INC $1234 (6 cycles):
  Cycle 1: Fetch opcode
  Cycle 2: Fetch address low
  Cycle 3: Fetch address high
  Cycle 4: Read value from $1234
  Cycle 5: Write ORIGINAL value back to $1234 (dummy write)
  Cycle 6: Write MODIFIED value to $1234
```

This dummy write on cycle 5 is significant: if used on a hardware register (e.g., INC
$D019 to acknowledge a VIC-II interrupt), the dummy write briefly writes the old value
back before writing the new one. This can cause unintended side effects on I/O registers.

### 9.2 VIC-II CPU Halting: BA/AEC in Detail

The VIC-II and CPU share the bus using a precise protocol:

#### Normal Operation (no stealing)
```
Phase 1 (phi2 low):  VIC-II reads (character data, bitmap data, refresh)
Phase 2 (phi2 high): CPU reads/writes (instruction execution)
```

The VIC-II always gets phase 1. The CPU always gets phase 2. They interleave perfectly.

#### When VIC-II Needs Phase 2 (Cycle Stealing)

Three cycles before the VIC-II needs a phase-2 access:
1. **Cycle T-3:** BA goes low. If the CPU is about to do a READ, the RDY line halts it.
   If the CPU is doing a WRITE, it continues (RDY is ignored on writes).
2. **Cycle T-2:** BA still low. CPU halted on reads, writes still proceed.
3. **Cycle T-1:** BA still low. CPU halted on reads, writes still proceed.
4. **Cycle T:** AEC stays low during phase 2. VIC-II takes the bus. CPU is fully locked
   out.

The 3-cycle warning via BA is sufficient because **the 6510 never performs more than 3
consecutive write cycles.** The longest write sequence is the 3 stack pushes during an
interrupt (push PCH, push PCL, push status). After those 3 writes, the next cycle is
always a read (fetching the interrupt vector), at which point RDY halts the CPU.

#### Badline Timing (PAL, 6569)

On a badline, the VIC-II steals cycles 15-54 (40 cycles). BA goes low at cycle 12 (3
cycles early warning):

```
Cycle:  12   13   14   15   16   17  ...  53   54   55   56
BA:     LOW  LOW  LOW  LOW  LOW  LOW ...  LOW  LOW  HIGH HIGH
AEC(φ2): hi   hi   hi   LOW  LOW  LOW ... LOW  LOW  HIGH HIGH
CPU:    stun stun stun halt halt halt ... halt halt  run  run
```

"Stun" means the CPU cannot start a new read but can complete an in-progress write.
"Halt" means the CPU is completely frozen.

#### Sprite DMA Timing

Each sprite requires 2 phase-2 accesses for DMA. BA goes low 3 cycles before the first
access. The exact cycles depend on the sprite number. For sprite 0 on the 6569:

- BA drops at cycle 55 of the PREVIOUS line
- DMA occurs at cycles 58-60 of the previous line and cycle 1 of the current line

When multiple sprites are active, their DMA windows can overlap or abut, creating longer
continuous stall periods.

### 9.3 Instruction Timing Reference

Complete cycle counts for all official 6502/6510 instructions:

#### Load/Store/Transfer

| Instruction | Imm | ZP  | ZP,X | ZP,Y | Abs | Abs,X | Abs,Y | (ZP,X) | (ZP),Y |
|-------------|-----|-----|------|------|-----|-------|-------|---------|--------|
| LDA         | 2   | 3   | 4    | --   | 4   | 4+    | 4+    | 6       | 5+     |
| LDX         | 2   | 3   | --   | 4    | 4   | --    | 4+    | --      | --     |
| LDY         | 2   | 3   | 4    | --   | 4   | 4+    | --    | --      | --     |
| STA         | --  | 3   | 4    | --   | 4   | 5     | 5     | 6       | 6      |
| STX         | --  | 3   | --   | 4    | 4   | --    | --    | --      | --     |
| STY         | --  | 3   | 4    | --   | 4   | --    | --    | --      | --     |

`+` = add 1 cycle if page boundary crossed

| Transfer | Cycles |
|----------|--------|
| TAX      | 2      |
| TAY      | 2      |
| TXA      | 2      |
| TYA      | 2      |
| TSX      | 2      |
| TXS      | 2      |

#### Arithmetic and Logic

| Instruction | Imm | ZP  | ZP,X | Abs | Abs,X | Abs,Y | (ZP,X) | (ZP),Y |
|-------------|-----|-----|------|-----|-------|-------|---------|--------|
| ADC         | 2   | 3   | 4    | 4   | 4+    | 4+    | 6       | 5+     |
| SBC         | 2   | 3   | 4    | 4   | 4+    | 4+    | 6       | 5+     |
| AND         | 2   | 3   | 4    | 4   | 4+    | 4+    | 6       | 5+     |
| ORA         | 2   | 3   | 4    | 4   | 4+    | 4+    | 6       | 5+     |
| EOR         | 2   | 3   | 4    | 4   | 4+    | 4+    | 6       | 5+     |
| CMP         | 2   | 3   | 4    | 4   | 4+    | 4+    | 6       | 5+     |
| CPX         | 2   | 3   | --   | 4   | --    | --    | --      | --     |
| CPY         | 2   | 3   | --   | 4   | --    | --    | --      | --     |
| BIT         | --  | 3   | --   | 4   | --    | --    | --      | --     |

#### Read-Modify-Write

| Instruction | Acc | ZP  | ZP,X | Abs | Abs,X |
|-------------|-----|-----|------|-----|-------|
| ASL         | 2   | 5   | 6    | 6   | 7     |
| LSR         | 2   | 5   | 6    | 6   | 7     |
| ROL         | 2   | 5   | 6    | 6   | 7     |
| ROR         | 2   | 5   | 6    | 6   | 7     |
| INC         | --  | 5   | 6    | 6   | 7     |
| DEC         | --  | 5   | 6    | 6   | 7     |

Note: RMW Abs,X ALWAYS takes 7 cycles (no page-cross variability; dummy read always
occurs).

#### Increment/Decrement Registers

| Instruction | Cycles |
|-------------|--------|
| INX         | 2      |
| INY         | 2      |
| DEX         | 2      |
| DEY         | 2      |

#### Stack Operations

| Instruction | Cycles |
|-------------|--------|
| PHA         | 3      |
| PHP         | 3      |
| PLA         | 4      |
| PLP         | 4      |

#### Control Flow

| Instruction     | Cycles                         |
|-----------------|--------------------------------|
| JMP abs         | 3                              |
| JMP (ind)       | 5                              |
| JSR             | 6                              |
| RTS             | 6                              |
| RTI             | 6                              |
| BRK             | 7                              |
| Bxx (not taken) | 2                              |
| Bxx (taken)     | 3 (+1 if page cross)           |

#### Flag Operations

| Instruction | Cycles |
|-------------|--------|
| CLC         | 2      |
| SEC         | 2      |
| CLD         | 2      |
| SED         | 2      |
| CLI         | 2      |
| SEI         | 2      |
| CLV         | 2      |
| NOP         | 2      |

### 9.4 IRQ Latency

When an interrupt occurs (IRQ line goes low), the CPU does not respond instantly:

1. **Finish current instruction:** 0-7 cycles depending on which instruction is executing.
   The CPU checks the IRQ line during the last cycle of each instruction.
2. **Interrupt dispatch:** 7 cycles (push PCH, push PCL, push status, read vector low,
   read vector high, load PC).
3. **Total latency:** 7-14 cycles from IRQ assertion to first instruction of handler.

If the KERNAL IRQ handler is used (vector at $FFFE pointing to the KERNAL routine), it
adds approximately 29 cycles of overhead (register saves, checking interrupt source)
before reaching user code at the vector in $0314/$0315. This gives a total latency of
**38-44 cycles** from IRQ to user code.

For cycle-exact work, bypass the KERNAL entirely by pointing the hardware IRQ vector
($FFFE/$FFFF, mapped through $0314/$0315 or by banking out ROMs) directly at your handler.

#### The Double-IRQ Technique for Raster Stabilization

To achieve cycle-exact raster timing, the "double IRQ" technique eliminates the variable
latency:

1. **First IRQ:** Fires on the target raster line. The handler:
   - Saves registers (using SMC, not stack -- saves cycles)
   - Sets up a second raster IRQ for the NEXT line
   - Re-enables interrupts (CLI)
   - Executes a NOP slide (many consecutive NOP instructions)

2. **Second IRQ:** Fires during the NOP slide. Since all NOPs are exactly 2 cycles, the
   entry point into the second handler has at most 1 cycle of jitter (you either just
   finished a NOP or are 1 cycle into one).

3. **Remove final cycle:** Use a CIA timer trick or carefully timed branches to eliminate
   the remaining 1-cycle jitter:
   ```asm
   ; In second IRQ handler:
           LDA #$08         ; cycle count modulo target
           STA $D012        ; (example -- actual technique varies)
           ; ... NOP padding to align ...
           ; Check CIA timer or use DEC $D019 trick
   ```

After stabilization, all subsequent writes to VIC-II registers happen at a known,
deterministic cycle position on the raster line.

### 9.5 CIA Timer Measurement

The CIA (Complex Interface Adapter) chips contain precision timers that can measure CPU
cycle consumption:

```asm
; Measure cycles consumed by a code block:
        LDA #$FF
        STA $DC04           ; Timer A low byte
        STA $DC05           ; Timer A high byte (starts counting from $FFFF)
        LDA #%00001001      ; bit 0: start, bit 3: one-shot
        STA $DC0E           ; start Timer A

        ; === code being measured ===
        NOP
        NOP
        NOP
        ; === end of measured code ===

        LDA $DC04           ; read timer low byte
        ; cycles_elapsed = $FFFF - timer_value - overhead
```

**Quirk:** The CIA timer has a 2-cycle startup delay after writing to the control register.
The timer actually starts counting 2 cycles after the STA $DC0E instruction completes.
Account for this when calculating precise cycle counts.

**Another quirk:** On the old CIA (6526), the timer reads differently than on the new CIA
(6526A). The old CIA latches the timer value on the cycle the read occurs, while the new
CIA may return a value that is 1 cycle off. This can affect cycle measurement accuracy.

### 9.6 Speedcode Generators

Runtime speedcode generation is a cornerstone of C64 demo technique. Instead of storing
massive blocks of unrolled code on disk, a compact generator creates them in memory:

#### Basic Generator Pattern

```asm
; Generate N copies of "LDA #value / STA $addr" with incrementing addresses
generate:
        LDX #0              ; output pointer
        LDY #0              ; entry counter
gen_loop:
        LDA #$A9            ; LDA # opcode
        STA code_buf,X
        INX
        LDA values,Y        ; the immediate value
        STA code_buf,X
        INX
        LDA #$8D            ; STA abs opcode
        STA code_buf,X
        INX
        LDA target_lo,Y     ; address low byte
        STA code_buf,X
        INX
        LDA target_hi,Y     ; address high byte
        STA code_buf,X
        INX
        INY
        CPY #count
        BNE gen_loop
        LDA #$60            ; RTS opcode
        STA code_buf,X
        RTS
```

#### Advanced Generator Techniques

- **Variant generation:** A single generator can produce multiple speedcode variants
  (e.g., scroll left vs scroll right) by parameterizing the opcode or address increment
  direction.
- **Compression:** The generator itself is small (~30-50 bytes), while the generated code
  can be 5-10 KB. This dramatically improves load times.
- **Memory reuse:** Generated code can be overwritten when no longer needed, reclaiming
  RAM for the next effect.
- **Self-modifying generators:** The generator itself can use SMC for maximum compactness.

#### Practical Memory Budget

On a PAL C64 with ROMs banked out:
- ~58 KB usable RAM (excluding I/O area and zero page/stack)
- A 10 KB speedcode block is ~17% of available RAM
- Multiple effects can share the same memory region if they are not active simultaneously


---

## References

### Timing and Architecture
- [Raster Time - C64-Wiki](https://www.c64-wiki.com/wiki/raster_time)
- [The MOS 6567/6569 VIC-II and Its Application in the C64 (Christian Bauer)](https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt)
- [Hardware Basics: Tick Tock, Know Your Clock - Dustlayer](https://dustlayer.com/c64-architecture/2013/5/7/hardware-basics-part-1-tick-tock-know-your-clock)
- [VIC-II for Beginners: Rasters and Cycles - Dustlayer](https://dustlayer.com/vic-ii/2013/4/25/vic-ii-for-beginners-beyond-the-screen-rasters-cycle)
- [Missing Cycles by Pasi Ojala](http://www.antimon.org/dl/c64/code/missing.txt)
- [VIC-II Interrupt Timing - Bumbershoot Software](https://bumbershootsoft.wordpress.com/2015/07/26/vic-ii-interrupt-timing-or-how-i-learned-to-stop-worrying-and-love-unstable-rasters/)
- [Stabilizing the VIC-II Raster - Bumbershoot Software](https://bumbershootsoft.wordpress.com/2015/12/29/stabilizing-the-vic-ii-raster/)
- [Sprites and Raster Timing on the C64 - Bumbershoot Software](https://bumbershootsoft.wordpress.com/2016/02/05/sprites-and-raster-timing-on-the-c64/)

### Instruction Set and Cycle Counts
- [6502 Instruction Set - masswerk.at](https://www.masswerk.at/6502/6502_instruction_set.html)
- [6502 Cycle Times - NESdev Wiki](https://www.nesdev.org/wiki/6502_cycle_times)
- [6502/6510 Instruction Set - C64 OS](https://c64os.com/post/6502instructions)
- [6502 Opcodes - 6502.org](http://www.6502.org/tutorials/6502opcodes.html)
- [Ultimate C64 Reference: 6502](https://www.pagetable.com/c64ref/6502/?tab=2)

### Illegal Opcodes
- [6502 Illegal Opcodes Demystified - masswerk.at](https://www.masswerk.at/nowgobang/2021/6502-illegal-opcodes)
- [6502/6510/8500/8502 Opcodes - oxyron.de](https://www.oxyron.de/html/opcodes02.html)
- [NMOS 6510 Unintended Opcodes - No More Secrets](https://hitmen.c02.at/files/docs/c64/NoMoreSecrets-NMOS6510UnintendedOpcodes-20162412.pdf)
- [CPU Unofficial Opcodes - NESdev Wiki](https://www.nesdev.org/wiki/CPU_unofficial_opcodes)

### Optimization Techniques
- [Dirty Tricks 6502 Programmers Use - nurpax](https://nurpax.github.io/posts/2019-08-18-dirty-tricks-6502-programmers-use.html)
- [A Grimoire of 8-Bit Implementation Patterns - Bumbershoot Software](https://bumbershootsoft.wordpress.com/2021/01/02/a-grimoire-of-8-bit-implementation-patterns/)
- [6502 Assembly Optimisations - NESdev Wiki](https://www.nesdev.org/wiki/6502_assembly_optimisations)
- [Speedcode / Loop Unrolling - Codebase64](https://codebase64.pokefinder.org/doku.php?id=base:speedcode)
- [6502/6510 Coding - Codebase64](https://codebase64.net/doku.php?id=base:6502_6510_coding)

### Multiplication
- [Multiplication on 6502 - lysator.liu.se](https://www.lysator.liu.se/~nisse/misc/6502-mul.html)
- [Comparing 6502 Multiply Routines - TobyLobster/multiply_test](https://github.com/TobyLobster/multiply_test)
- [6502/6510 Maths - Codebase64](https://codebase64.org/doku.php?id=base:6502_6510_maths)
- [Fast 8-bit Multiply Routine - Retro64](https://retro64.altervista.org/blog/optimizing-the-assembly-program-sphere-more-tips-fixed-point-math-fast-8-bit-multiply-routine/)

### Zero Page and Memory
- [Zeropage - C64-Wiki](https://www.c64-wiki.com/wiki/Zeropage)
- [Stack Manipulation, Some Gotchas - C64 OS](https://c64os.com/post/stackmanipulation)
- [Synthesizing Instructions with RTS, RTI, and JSR - Wilson Mines](http://wilsonminesco.com/stacks/RTSsynth.html)

### Interrupts and Raster Stabilization
- [Double IRQ Explained - Codebase64](https://codebase.c64.org/doku.php?id=base:double_irq_explained)
- [Making Stable Raster Routines - Antimon](https://www.antimon.org/dl/c64/code/stable.txt)
- [Raster Interrupt - C64-Wiki](https://www.c64-wiki.com/wiki/Raster_interrupt)
- [CIA - C64-Wiki](https://www.c64-wiki.com/wiki/CIA)

### General Reference
- [6502 Decimal Mode - 6502.org](http://6502.org/tutorials/decimal_mode.html)
- [Beginning Programming Tips - Wilson Mines](http://wilsonminesco.com/6502primer/PgmTips.html)
- [Self-Modifying Code on 65xx - Wilson Mines](https://wilsonminesco.com/SelfModCode/)
- [ca65 Macros for Self Modifying Code - CC65](https://cc65.github.io/doc/smc.html)
