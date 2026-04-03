# Mathematical Routines and Algorithmic Techniques for C64 Programming

Comprehensive reference covering multiplication, division, trigonometry, random numbers,
graphics algorithms, 3D mathematics, sorting, and advanced optimization techniques for the
MOS 6510 CPU (a 6502 variant) on the Commodore 64.


---

## 1. Overview -- Math on the 6510

The MOS 6510 is a 1 MHz 8-bit processor with no multiply instruction, no divide
instruction, no floating-point unit, and only three 8-bit registers (A, X, Y). All
arithmetic beyond addition and subtraction must be constructed in software. This is the
central constraint that shapes every algorithm described in this document.

### What the 6510 provides

| Instruction | Operation                     | Cycles |
|-------------|-------------------------------|--------|
| ADC         | A = A + operand + carry       | 2-6    |
| SBC         | A = A - operand - !carry      | 2-6    |
| ASL         | Shift left (multiply by 2)    | 2-6    |
| LSR         | Shift right (divide by 2)     | 2-6    |
| ROL         | Rotate left through carry     | 2-6    |
| ROR         | Rotate right through carry    | 2-6    |
| AND/ORA/EOR | Bitwise logic                 | 2-6    |
| CMP         | Compare (subtract, set flags) | 2-6    |

That is it. No MUL, no DIV, no barrel shifter, no 16-bit operations. Every multiply,
divide, square root, sine, cosine, and matrix operation must be built from sequences of
these primitives -- or avoided entirely through lookup tables.

### How programmers work around the limitations

1. **Lookup tables** -- Pre-compute results and store them in RAM or ROM. A 256-byte
   table accessed by LDA table,X takes only 4 cycles regardless of how complex the
   function is.

2. **Shift-and-add / shift-and-subtract** -- Binary long multiplication and long
   division using ASL, ROL, LSR, ROR, ADC, and SBC in loops.

3. **Fixed-point arithmetic** -- Represent fractional values as integers scaled by a
   power of two. All operations remain integer addition/subtraction, but the programmer
   tracks the implicit binary point.

4. **Strength reduction** -- Replace multiplies with shifts and adds for constants.
   Multiplying by 10 becomes (x << 3) + (x << 1) = 8x + 2x.

5. **Self-modifying code** -- Patch instruction operands at runtime to avoid indirect
   addressing overhead, eliminate loop counters, or parameterize routines.

6. **Precalculation** -- Compute expensive values during initialization or loading, not
   during the frame loop.


---

## 2. Fixed-Point Arithmetic

### 2.1 Why Fixed-Point?

The C64 BASIC ROM includes a 5-byte floating-point library (the Woz/Microsoft FAC
routines), but a single multiply takes thousands of cycles -- far too slow for real-time
graphics or games. Fixed-point arithmetic provides fractional precision using only the
integer instructions the CPU already has, at speeds comparable to plain integer math.

### 2.2 Common Formats

**8.8 fixed-point (16-bit total)**

The most widely used format on the C64. One byte holds the integer part, one byte holds
the fractional part. The implicit binary point sits between the two bytes.

```
  High byte (integer)    Low byte (fraction)
  [IIIIIIII]             [FFFFFFFF]
       ^                      ^
    integer part         fractional part (1/256 resolution)
```

Range: 0.0 to 255.99609375 (unsigned) or -128.0 to 127.99609375 (signed)
Resolution: 1/256 = 0.00390625

Example: The value 3.5 is stored as $0380 (high byte $03 = 3, low byte $80 = 128/256 =
0.5).

**4.4 fixed-point (8-bit total)**

Fits in a single byte. Four bits integer, four bits fraction.

Range: 0.0 to 15.9375 (unsigned)
Resolution: 1/16 = 0.0625

Useful for small delta values (velocities, accelerations) where memory is tight.

**1.7 signed fixed-point**

Used for sine/cosine tables where values range from -1.0 to +0.9921875. One sign/integer
bit plus seven fractional bits. Stored as signed bytes where $7F = +0.9921875, $00 = 0.0,
$81 = -0.9921875.

**Other formats**

Some 3D engines use 8.8 for coordinates and 1.15 (16-bit) for matrix entries, or 4.12
for higher fractional precision in rotation calculations.

### 2.3 Addition and Subtraction

Fixed-point addition and subtraction are identical to integer addition and subtraction
-- the binary point is implicit and does not need to be managed:

```asm
; 8.8 addition: result = value1 + value2
    CLC
    LDA value1_lo       ; fractional parts
    ADC value2_lo
    STA result_lo
    LDA value1_hi       ; integer parts
    ADC value2_hi       ; carry propagates automatically
    STA result_hi
```

This is exactly the same as a 16-bit integer add. The carry flag bridges the fractional
byte to the integer byte, which is precisely the behavior needed.

### 2.4 Multiplication

Fixed-point multiplication requires post-shifting. If two 8.8 numbers are multiplied,
the raw 32-bit result has the format 16.16 -- the binary point is at bit 16, not bit 8.
To get an 8.8 result, you must shift right by 8 (discard the lowest byte of the 32-bit
product and keep the middle two bytes).

### 2.5 Why Fixed-Point is Essential for Smooth Movement

Without fractional precision, the smallest velocity is 1 pixel per frame. At 50 fps
(PAL), that is 50 pixels per second -- far too coarse for many game mechanics. With 8.8
fixed-point, the smallest velocity is 1/256 pixel per frame, giving a minimum speed of
about 0.2 pixels per second. This enables slow, smooth scrolling, gentle arcs,
deceleration curves, and analog-feeling controls.

The fractional part accumulates across frames. A horizontal velocity of $0040 (0.25
pixels/frame) means the object moves one full pixel every four frames -- perfectly smooth
to the eye.


---

## 3. Multiplication

### 3.1 Shift-and-Add (Binary Long Multiplication)

The fundamental software multiply. It mirrors pencil-and-paper multiplication in binary:
for each bit of the multiplier, if the bit is 1, add the multiplicand (shifted to the
appropriate position) to the running total.

**8x8 -> 16-bit unsigned multiply:**

```asm
; Input:  NUM1, NUM2 (zero page bytes)
; Output: 16-bit result in A (high) and RESULT (low)
    LDA #0
    LDX #8              ; 8 bits to process
loop:
    LSR NUM2            ; shift multiplier right, bit 0 -> carry
    BCC skip            ; if bit was 0, skip addition
    CLC
    ADC NUM1            ; add multiplicand to accumulator
skip:
    ROR A               ; rotate product right (high byte)
    ROR RESULT          ; rotate product right (low byte)
    DEX
    BNE loop
    STA RESULT+1        ; store high byte
```

Performance: approximately 130-153 cycles depending on implementation. The loop always
executes 8 iterations regardless of operand values.

**Optimization -- sentinel bit:** Instead of using X as a loop counter, preload the
result register with a sentinel bit (bit 7 set). When the sentinel bit rotates out into
the carry after 8 iterations, the loop terminates. This eliminates DEX (2 cycles) from
each iteration:

```asm
    LDA #0
    STA RESULT
    LDA #$80            ; sentinel bit
    STA RESULT          ; will be shifted out after 8 iterations
    LDA #0
    ; ... (loop omits DEX/BNE, instead checks carry after ROR RESULT)
```

### 3.2 Multiply by Constants (Strength Reduction)

For multiplication by known constants, decompose the constant into powers of two and
combine with shifts and adds. This is dramatically faster than a general multiply.

| Constant | Decomposition      | Implementation           | Approx. cycles |
|----------|--------------------|--------------------------|-----------------|
| x * 2   | x << 1             | ASL                      | 2               |
| x * 3   | (x << 1) + x       | ASL, then ADC original   | ~8              |
| x * 5   | (x << 2) + x       | ASL ASL, then ADC        | ~10             |
| x * 7   | (x << 3) - x       | ASL ASL ASL, then SBC    | ~12             |
| x * 10  | (x << 3) + (x << 1)| Two shifts, add          | ~14             |
| x * 40  | (x << 5) + (x << 3)| For bitmap row offset    | ~18             |
| x * 320 | 256x + 64x         | Shift tricks on 16-bit   | ~25             |

These are far cheaper than a generic multiply routine and should always be preferred when
one operand is a compile-time constant.

### 3.3 Quarter-Square Multiplication (Table Lookup)

The fastest general-purpose multiply for the 6502. Based on the algebraic identity:

    a * b = f(a + b) - f(a - b)

where f(x) = floor(x^2 / 4)

Alternatively expressed as:

    a * b = ((a + b)^2 - (a - b)^2) / 4

This transforms multiplication into two table lookups and a subtraction.

**Table layout:**

Two tables are needed, each 512 bytes (to handle the range 0..510 for a+b when both
operands are 0..255):

- `sqr_lo[i]` -- low byte of floor(i^2 / 4), for i = 0..511
- `sqr_hi[i]` -- high byte of floor(i^2 / 4), for i = 0..511

Total memory: 2 KB (four 256-byte pages) when split into four page-aligned tables for
lo/hi bytes of the two lookups.

**How it works, step by step:**

1. Compute `sum = a + b` and `diff = |a - b|`
2. Look up `f(sum)` = `sqr[sum]` (16-bit value from two table pages)
3. Look up `f(diff)` = `sqr[diff]` (16-bit value from two table pages)
4. Subtract: result = `f(sum) - f(diff)` (16-bit subtraction)

**Performance:** 38-83 cycles depending on the implementation variant, compared to
130-153 for shift-and-add. The fastest unrolled versions achieve approximately 38 cycles
worst-case for an 8x8 -> 16-bit unsigned multiply.

**For signed multiplication:** The quarter-square method naturally handles unsigned
operands. For signed operands, compute the result sign from bit 7 of both operands (EOR
the sign bits), convert both operands to positive, perform the unsigned multiply, then
conditionally negate the result.

**16x16 -> 32-bit multiplication:** Chain four 8x8 multiplies using the same tables:

    (A_hi * 256 + A_lo) * (B_hi * 256 + B_lo)
    = A_hi * B_hi * 65536
    + (A_hi * B_lo + A_lo * B_hi) * 256
    + A_lo * B_lo

Each partial product uses the quarter-square tables, and the four 16-bit results are
accumulated with appropriate byte alignment.

**Table generation (in BASIC or cross-assembler):**

```basic
10 FOR I = 0 TO 511
20 V = INT(I * I / 4)
30 POKE TABLE_LO + I, V AND 255
40 POKE TABLE_HI + I, INT(V / 256)
50 NEXT I
```

### 3.4 Special Cases and Optimizations

**When one operand is constant across multiple multiplies:** In routines like matrix
multiplication, one factor (e.g., a sine value) is used with many different second
factors. The quarter-square method benefits here because the table base address for the
constant operand can be set once (via self-modifying code patching the table pointers)
and only the variable operand changes between calls.

**Squaring (a * a):** Since a + a = 2a and a - a = 0, squaring reduces to a single
table lookup: f(2a) - f(0) = f(2a). Only one table access is needed.

**Multiply by reciprocal for division:** See Section 4.


---

## 4. Division

### 4.1 Shift-and-Subtract (Binary Long Division)

The mirror image of shift-and-add multiplication. For each bit position, attempt to
subtract the divisor from the running remainder. If the subtraction succeeds (no
borrow), record a 1 in the quotient; otherwise, restore the remainder and record a 0.

**16-bit / 16-bit unsigned division:**

```asm
; Input:  NUM1 (16-bit dividend), NUM2 (16-bit divisor)
; Output: NUM1 (16-bit quotient), REM (16-bit remainder)
    LDA #0
    STA REM
    STA REM+1
    LDX #16              ; 16 bits to process
loop:
    ASL NUM1             ; shift dividend left
    ROL NUM1+1           ; high bit -> carry
    ROL REM              ; carry -> remainder
    ROL REM+1
    LDA REM
    SEC
    SBC NUM2             ; try subtracting divisor
    TAY
    LDA REM+1
    SBC NUM2+1
    BCC skip             ; if borrow, don't update
    STA REM+1            ; subtraction succeeded
    STY REM
    INC NUM1             ; set quotient bit
skip:
    DEX
    BNE loop
```

Performance: approximately 300-400 cycles for 16/16 division. The loop always runs 16
iterations.

**16-bit / 8-bit division** is simpler and faster (~200 cycles), since the remainder
fits in a single byte and the inner subtraction is 8-bit.

### 4.2 Division by Constants

Division by constants is far more common than general division and can almost always be
optimized:

**Powers of two:** Use LSR (divide by 2) and ROR chains. Dividing a 16-bit value by 8
requires three LSR/ROR pairs (~18 cycles total).

**Multiply by reciprocal:** Instead of dividing by N, multiply by 256/N (or 65536/N for
higher precision) and take the high byte(s) of the result. This converts a slow division
into a fast table-based multiply.

Example: Division by 3 becomes multiplication by 85 (since 256/3 ~ 85.33). The error
is small and acceptable for many applications. Division by 5 becomes multiplication
by 51 (256/5 ~ 51.2).

**Division by 10 (common for decimal output):** Multiply by 26 and shift right by 8,
or use a specialized routine combining shifts and subtracts.

### 4.3 Reciprocal Tables

For perspective projection and other contexts requiring division by a variable, a
precomputed reciprocal table is invaluable:

```
recip_table[z] = 256 * d / z    (or 65536 * d / z for higher precision)
```

Where `d` is the viewer distance. Indexed by the Z coordinate, this converts a division
into a single table lookup. The table consumes 256 or 512 bytes depending on precision
and range, but perspective division drops from ~300 cycles to ~4 cycles (a single indexed
load).

### 4.4 Lookup Tables for Common Divisions

For bitmap address calculations, dividing X by 8 and computing X mod 8 are needed
constantly. These are trivially replaced by:

- `X / 8` = `X >> 3` (three LSR instructions)
- `X mod 8` = `X AND #$07`

For converting Y coordinates to character rows (divide by 8, multiply by 40 for screen
offset), precomputed row-address tables completely eliminate the division and
multiplication.


---

## 5. Trigonometry

### 5.1 Sine and Cosine Lookup Tables

On the C64, trigonometric functions are always implemented as lookup tables. Computing
sine or cosine in real-time (via Taylor series, CORDIC, or the BASIC ROM SIN function)
is far too slow for frame-rate graphics.

**Binary angle representation (brads):**

A full circle is represented as 256 steps (one byte), where:

| Brads | Degrees | Radians    |
|-------|---------|------------|
| 0     | 0       | 0          |
| 64    | 90      | pi/2       |
| 128   | 180     | pi         |
| 192   | 270     | 3*pi/2    |
| 256(0)| 360     | 2*pi       |

This means a full rotation fits exactly in one byte with natural wraparound -- no range
checking needed. Incrementing the angle byte by 1 rotates by 1.40625 degrees.

### 5.2 Table Formats

**Unsigned format (0..255 representing -1.0 to +1.0):**

```
table[angle] = round(sin(angle * 2 * pi / 256) * 127) + 128
```

Where $80 (128) represents zero, $FF (255) represents approximately +1.0, and $01 (1)
represents approximately -1.0. This is convenient for direct use as screen coordinates
(e.g., for sprite sine-wave movement).

**Signed format (-128..+127):**

```
table[angle] = round(sin(angle * 2 * pi / 256) * 127)
```

Stored as signed bytes (two's complement). Value $7F = +0.9921875, $00 = 0.0, $81 =
-0.9921875. This is the preferred format for multiplication in rotation routines, since
the multiply naturally handles the signed range.

**Scaled signed format (x64 or x32):**

For 3D rotation, values are often scaled by a power of two (e.g., 64) rather than 127:

```
table[angle] = round(sin(angle * 2 * pi / 256) * 64)
```

The divisor of 64 (a power of two) makes post-multiplication correction trivial -- just
shift right by 6 bits. Using 127 as the scale would require a division by 127, which is
expensive.

### 5.3 Quarter-Wave Symmetry Optimization

A full 256-entry table consumes 256 bytes. Using the symmetry of sine, only the first
quadrant (64 entries, angles 0-63) needs to be stored. The other three quadrants are
derived at lookup time:

- Quadrant 2 (angles 64-127): `sin(a) = sin(128 - a)` -- read the table backwards
- Quadrant 3 (angles 128-191): `sin(a) = -sin(a - 128)` -- negate the first quadrant
- Quadrant 4 (angles 192-255): `sin(a) = -sin(256 - a)` -- negate, read backwards

This reduces table size from 256 bytes to 64 bytes at the cost of a few extra
instructions per lookup (testing bits 6 and 7 of the angle, conditional index
reversal, conditional negation). In practice, most C64 programs use the full 256-byte
table because 192 extra bytes are cheap and the per-lookup overhead matters in tight
inner loops.

**Cosine from sine:** `cos(a) = sin(a + 64)`. No separate cosine table is needed -- just
add 64 to the angle index before looking up the sine table. Since the angle is a byte,
this wraps automatically.

### 5.4 Generating Tables

**From BASIC (on the C64 itself):**

```basic
10 FOR I = 0 TO 255
20 V = INT(SIN(I * 6.28318530718 / 256) * 127 + 0.5)
30 IF V < 0 THEN V = V + 256
40 POKE 49152 + I, V
50 NEXT I
```

**From a cross-assembler (e.g., KickAssembler):**

```
.for (var i = 0; i < 256; i++) {
    .byte round(sin(toRadians(i * 360.0 / 256)) * 127)
}
```

**From C (generating a .bin file):**

```c
for (int i = 0; i < 256; i++) {
    int v = (int)round(sin(i * 2.0 * M_PI / 256.0) * 127.0);
    fputc(v & 0xFF, f);
}
```

### 5.5 CORDIC on the 6502

CORDIC (COordinate Rotation DIgital Computer) computes trigonometric functions using only
shifts, additions, and subtractions -- no multiplies. The algorithm iteratively rotates a
vector toward the desired angle using a table of arctangent values for decreasing power-
of-two angles.

**The algorithm (rotation mode):**

```
Initialize: x = K (CORDIC gain constant), y = 0, z = target_angle
For i = 0 to n-1:
    If z >= 0:
        x_new = x - (y >> i)
        y_new = y + (x >> i)
        z_new = z - atan(2^-i)
    Else:
        x_new = x + (y >> i)
        y_new = y - (x >> i)
        z_new = z + atan(2^-i)
    x, y, z = x_new, y_new, z_new
Result: x = cos(target_angle) * K, y = sin(target_angle) * K
```

The atan table is precomputed: `[45.0, 26.565, 14.036, 7.125, 3.576, 1.790, 0.895, ...]`

**Practical 6502 considerations:**

- CORDIC requires multi-byte shift-right operations at each iteration, which are slow on
  the 6502 (no barrel shifter).
- For 8-bit precision, 8 iterations are needed. Each iteration involves two shifts, two
  additions/subtractions, and one subtraction -- roughly 40-60 cycles per iteration,
  totaling 320-480 cycles.
- This is slower than a single table lookup (4 cycles) but faster than Taylor series.
- CORDIC was used by some early microcomputer ROMs (including Applesoft BASIC) for
  computing transcendental functions, but for real-time C64 graphics, lookup tables are
  universally preferred.
- CORDIC's main advantage is simultaneous computation of both sine and cosine, and it
  requires minimal memory (just the small arctangent table).

### 5.6 Using the BASIC ROM SIN/COS

The C64 Kernal provides floating-point sine and cosine:

- SIN: Store angle (in radians) in FAC#1, then JSR $E26B
- COS: Store angle in FAC#1, then JSR $E264
- TAN: Store angle in FAC#1, then JSR $E2B4

These routines work but are extremely slow (~3000-5000 cycles per call) and use the
5-byte floating-point accumulator format. They are suitable only for one-time table
generation during initialization, never for real-time computation.


---

## 6. Random Number Generation

### 6.1 Linear Feedback Shift Register (LFSR)

The LFSR is the workhorse PRNG for 6502 systems. It uses only shifts and XOR operations,
requires minimal state, and produces long non-repeating sequences.

**Fibonacci LFSR:**

Bits at specific "tap" positions are XORed together and fed back into the shift register.
The choice of tap positions determines the sequence length. A maximal-length 16-bit LFSR
cycles through all 65,535 non-zero states before repeating.

**Galois LFSR (preferred on 6502):**

Instead of XORing selected bits to produce a feedback bit, the Galois form conditionally
XORs a feedback polynomial into the entire register. This is more efficient on the 6502
because it can be implemented with fewer instructions per iteration.

**8-bit output from a 16-bit Galois LFSR:**

The LFSR is iterated 8 times to produce each 8-bit random number:

```asm
; 16-bit Galois LFSR
; State: seed (2 bytes, zero page, must be non-zero)
; Output: A = 8-bit random number
; Clobbers: Y
prng:
    LDY #8              ; iterate 8 times
    LDA seed+0
loop:
    ASL A               ; shift the register
    ROL seed+1
    BCC skip
    EOR #$2D            ; apply XOR feedback (polynomial taps)
skip:
    DEY
    BNE loop
    STA seed+0
    ; A now contains the random byte
    RTS
```

### 6.2 LFSR Performance Comparison

| Width  | State | Period         | Simple cycles | Overlapped cycles | Code bytes |
|--------|-------|----------------|---------------|-------------------|------------|
| 16-bit | 2     | 65,535         | ~137 avg      | ~69               | 19-35      |
| 24-bit | 3     | 16,777,215     | ~177 avg      | ~73               | 21-38      |
| 32-bit | 4     | 4,294,967,295  | ~217 avg      | ~83               | 23-44      |

"Simple" implementations have minimal code size. "Overlapped" versions unroll and
interleave iterations, running more than twice as fast at the cost of larger code.

**Seed requirements:** The state must be initialized to any non-zero value. A zero state
will produce zero forever (the LFSR is stuck).

### 6.3 SID Voice 3 Hardware Noise

The SID chip's noise waveform generator contains a 23-bit LFSR that runs continuously in
hardware. The upper 8 bits of voice 3's oscillator output are readable at register
$D41B, providing a hardware random byte that requires zero CPU time to generate.

**Initialization:**

```asm
    LDA #$FF
    STA $D40E            ; voice 3 frequency low = $FF
    STA $D40F            ; voice 3 frequency high = $FF (maximum rate)
    LDA #$80
    STA $D412            ; voice 3 control: noise waveform, gate off
```

**Reading a random byte:**

```asm
    LDA $D41B            ; read voice 3 oscillator output
```

**Characteristics:**

- At maximum frequency, the LFSR produces ~65,535 values per second
- Reads must be spaced at least 16 cycles apart for the value to change
- The output is not cryptographically random -- it is a deterministic LFSR sequence
- Using voice 3 for random numbers prevents using it for audible sound
- Voice 3's output can optionally be disconnected from the audio mix using bit 7 of
  $D418 (filter/volume register), allowing silent random number generation while still
  producing audio on voices 1 and 2
- The 23-bit LFSR has a period of 8,388,607 before repeating

**Practical use:** Many C64 games use SID noise for quick random numbers (enemy
behavior, particle effects) while using a software LFSR seeded from the SID value for
reproducible sequences (level generation, replay systems).

### 6.4 Seeding Strategies

- **SID noise + timing:** Read $D41B at the moment of a keypress. Human timing provides
  true entropy.
- **TOD clock:** The CIA TOD (Time of Day) clock registers at $DC08-$DC0B provide BCD
  time values that make reasonable seeds.
- **Raster counter:** $D012 (VIC-II raster line) changes every ~63 cycles. Reading it at
  user-input time provides a varying seed.
- **Combining sources:** XOR multiple entropy sources together for better randomness.

### 6.5 Beyond Simple LFSR

For applications requiring better statistical properties:

- **Combined generators:** XOR two LFSRs with different periods. The combined period is
  the LCM of the two individual periods.
- **Output scrambling:** Apply a lookup table or bit permutation to the LFSR output to
  break the linear correlation between successive values.
- **Linear congruential generator (LCG):** `seed = seed * a + c`. Requires a multiply
  routine, making it slower than LFSR on the 6502, but produces output with different
  statistical properties.


---

## 7. Graphics Algorithms

### 7.1 Pixel Plotting (Address Calculation)

The C64's high-resolution bitmap mode (320 x 200, 1 bit per pixel) has a non-linear
memory layout. The 8000 bytes of bitmap data are organized as a 40 x 25 grid of 8x8
character cells:

```
Byte 0:    Cell(0,0) row 0     (pixels 0-7 of screen row 0)
Byte 1:    Cell(0,0) row 1     (pixels 0-7 of screen row 1)
...
Byte 7:    Cell(0,0) row 7     (pixels 0-7 of screen row 7)
Byte 8:    Cell(1,0) row 0     (pixels 8-15 of screen row 0)
Byte 9:    Cell(1,0) row 1     (pixels 8-15 of screen row 1)
...
Byte 319:  Cell(39,0) row 7    (pixels 312-319 of screen row 7)
Byte 320:  Cell(0,1) row 0     (pixels 0-7 of screen row 8)
...
```

**Address formula for pixel at (X, Y):**

```
byte_address = bitmap_base + (Y AND %11111000) * 40 + (X AND %11111000) + (Y AND 7)
```

More precisely:

```
byte_address = bitmap_base + INT(Y/8) * 320 + INT(X/8) * 8 + (Y AND 7)
bit_mask     = $80 >> (X AND 7)
```

**The multiplication by 320 problem:** INT(Y/8) * 320 is expensive. The standard
optimization is to precompute a 200-entry (or 25-entry) row address table:

```asm
; Precomputed: row_lo[y], row_hi[y] for y = 0..199
; Each entry = bitmap_base + INT(y/8) * 320 + (y AND 7)
;
; Pixel plot at (X, Y):
    LDY ycoord
    LDA row_lo,Y         ; low byte of row base address
    STA ptr
    LDA row_hi,Y
    STA ptr+1
    LDA xcoord
    LSR                   ; divide X by 8 to get column
    LSR
    LSR
    TAY                   ; Y = column * 1 (will be * 8 via table)
    ; ... (add column*8 offset to ptr)
    LDA bitmask_table,X   ; X = xcoord AND 7
    ORA (ptr),Y
    STA (ptr),Y
```

The row address table costs about 400 bytes (200 entries x 2 bytes) but converts the
most expensive part of pixel plotting into a single indexed load. This is standard
practice in virtually all C64 bitmap routines.

**Bit mask table (8 bytes):**

```
bitmask: .byte $80, $40, $20, $10, $08, $04, $02, $01
```

### 7.2 Bresenham's Line Drawing Algorithm

Bresenham's algorithm draws lines using only integer addition and comparison -- no
multiply or divide in the inner loop. It steps along the major axis (the axis with the
larger delta) one pixel at a time, accumulating an error term that determines when to
step along the minor axis.

**Algorithm:**

```
dx = |x1 - x0|
dy = |y1 - y0|
error = 0

If dx >= dy (shallow line, step along X):
    For each x from x0 to x1:
        plot(x, y)
        error = error + dy
        if error * 2 >= dx:
            y = y + sign_y
            error = error - dx

If dy > dx (steep line, step along Y):
    For each y from y0 to y1:
        plot(x, y)
        error = error + dx
        if error * 2 >= dy:
            x = x + sign_x
            error = error - dy
```

**6502 implementation considerations:**

- The algorithm requires two implementations: one for lines where |dx| >= |dy| (X-major)
  and one for |dy| > |dx| (Y-major), each with positive and negative direction variants.
  The BBC Micro game Elite implements seven variants of the inner loop to handle all
  octants.

- The error accumulation uses a single-byte addition and carry check. When the
  accumulated error overflows a byte boundary, the minor axis increments. This avoids
  explicit comparison: just check the carry flag after ADC.

- On the C64's non-linear bitmap, stepping along Y requires recalculating the byte
  address (incrementing by 1 within a cell, jumping by 312 bytes between cells), which
  complicates the inner loop compared to linear framebuffers.

- **Self-modifying code** is often used to patch INC/DEC for the direction and to patch
  the plot routine's address calculations, avoiding branches in the inner loop.

- Elite specifically does not plot the first pixel of any line, to prevent corner pixels
  from being XORed twice (which would erase them) when drawing connected wireframes.

**Typical inner-loop performance:** 17-26 cycles per pixel depending on implementation.
The fastest unrolled versions achieve ~17-18 cycles per pixel.

### 7.3 Circle Drawing (Midpoint Algorithm)

The midpoint circle algorithm (a variant of Bresenham's for circles) draws circles using
only addition and subtraction in the inner loop:

**Algorithm:**

```
x = radius
y = 0
d = 1 - radius    ; decision variable

While x >= y:
    Plot 8 symmetric points: (cx+x,cy+y), (cx-x,cy+y), (cx+x,cy-y), (cx-x,cy-y),
                              (cx+y,cy+x), (cx-y,cy+x), (cx+y,cy-x), (cx-y,cy-x)
    y = y + 1
    if d < 0:
        d = d + 2*y + 1
    else:
        x = x - 1
        d = d + 2*(y - x) + 1
```

**6502 considerations:**

- Eight-fold symmetry means only 1/8 of the circle needs to be computed. Each computed
  point generates 8 plotted pixels.
- The decision variable update uses only addition -- `2*y` is maintained as a running
  variable incremented by 2 each step, avoiding multiplication.
- The eight plot calls per iteration dominate the cost. Address calculation for each point
  is the bottleneck, not the circle math itself.
- For filled circles, horizontal lines are drawn between symmetric pairs instead of
  individual points.

### 7.4 Flood Fill

Flood fill on the C64 is challenging because:

1. The 6502 hardware stack is only 256 bytes (page 1), far too small for recursive
   fill algorithms on a 320x200 bitmap.
2. X coordinates require 9 bits (0-319), so each stack entry for a seed point needs
   at least 3 bytes (X low, X high, Y).

**Practical approaches:**

**Scanline fill (preferred):** Start at the seed point and fill horizontally in both
directions until boundaries are hit. Then scan the line above and below for unfilled
segments and add them to a queue. This uses a software stack (a region of RAM, typically
extending to $D000) rather than the hardware stack.

**Edge-list approach:** For filled vector graphics (as used in demos), the shape
boundaries are tracked in an edge list during line drawing. Fill is then a simple matter
of drawing horizontal spans between left and right edges for each scanline -- no flood
fill recursion needed.

```asm
; Simplified scanline fill between edge_left[y] and edge_right[y]:
    LDY #0                ; start at top of shape
fill_row:
    LDA edge_left,Y
    ; ... calculate start byte and mask
    LDA edge_right,Y
    ; ... calculate end byte and mask
    ; ... fill bytes between start and end with ORA/STA
    INY
    CPY #height
    BNE fill_row
```

Typical fill performance: 6-8 cycles per byte for horizontal span filling.


---

## 8. 3D Mathematics

### 8.1 The 3D Pipeline on the C64

All C64 3D engines follow essentially the same pipeline:

```
1. Define object (vertices + edge list)
2. Apply rotation (matrix multiply each vertex)
3. Apply perspective projection (divide by Z)
4. Draw wireframe or filled polygons
5. Double-buffer to prevent flicker
```

The bottleneck is step 2 (rotation) for many vertices and step 4 (drawing) for many
edges. Steps 2 and 3 are pure math; step 4 is the line-drawing/fill routine.

### 8.2 Rotation Matrices

A 3D rotation is represented as a 3x3 matrix. For rotation around each individual axis:

**X-axis rotation (pitch):**

```
| 1       0        0    |
| 0    cos(a)   -sin(a) |
| 0    sin(a)    cos(a) |
```

**Y-axis rotation (yaw):**

```
|  cos(b)   0   sin(b) |
|    0       1     0    |
| -sin(b)   0   cos(b) |
```

**Z-axis rotation (roll):**

```
| cos(c)  -sin(c)   0 |
| sin(c)   cos(c)   0 |
|   0        0       1 |
```

A combined rotation requires multiplying these matrices together. The resulting 3x3
matrix has 9 entries, each of which is a combination of sines and cosines of the three
rotation angles.

**Transforming a vertex:**

```
x' = m00*x + m01*y + m02*z
y' = m10*x + m11*y + m12*z
z' = m20*x + m21*y + m22*z
```

Each transformed coordinate requires 3 multiplications and 2 additions. For N vertices,
this is 9N multiplications and 6N additions. With 8 vertices (a cube), that is 72
multiplications per frame.

### 8.3 Optimization: Pre-combine the Matrix

**Trigonometric identity optimization:** Rather than performing three sequential
rotations (9 multiplies each = 27 multiplies to combine matrices), the combined matrix
entries are computed directly using trigonometric identities:

For example, if the rotation uses angles t1 (X-axis) and t2 (Y-axis):

```
m00 = (cos(t1) + cos(t2)) / 2      ; = cos(t1)*cos(t2) via identity
m01 = (cos(t1) - cos(t2)) / 2      ; related identity
```

This transforms the 9 matrix entries into expressions involving only additions and
subtractions of pre-tabulated sine/cosine values -- no multiplications needed to build
the matrix itself.

### 8.4 Scaling Sine/Cosine for 8-bit Math

Sine and cosine values range from -1.0 to +1.0. For 8-bit signed fixed-point, they are
scaled by a power of two:

**Scale factor 64 (common choice):**

- sin table values range from -64 to +64
- After multiplying vertex * sin_value (using the quarter-square tables), the result must
  be divided by 64 (shift right 6 bits) to get the correct coordinate
- Scale factor 64 is chosen because it is large enough for reasonable precision while
  small enough that intermediate products (coordinate * 64 max) do not overflow 16 bits
  for typical vertex coordinate ranges (-64 to +63)

**Scale factor 127 (maximum precision):**

- Uses the full signed byte range
- Requires division by 127 after multiplication, which is expensive
- Rarely used in practice due to the division cost

### 8.5 Perspective Projection

Once a vertex is rotated to world coordinates (x', y', z'), it must be projected onto
the 2D screen. The pinhole camera model gives:

```
screen_x = d * x' / (z' + z0) + center_x
screen_y = d * y' / (z' + z0) + center_y
```

Where `d` is the viewer distance (controls field of view) and `z0` is a translation
offset to keep the object in front of the camera.

**Implementation with reciprocal table:**

```asm
; z_recip[z] = 256 * d / (z + z0), precomputed for all valid z values
    LDX z_rotated        ; Z coordinate after rotation
    LDA z_recip,X        ; look up 256*d/(z+z0)
    ; Now multiply x_rotated * A to get screen_x * 256
    ; Take the high byte as the final screen coordinate
```

This converts the expensive division into a table lookup and a single multiplication.

### 8.6 The Complete Vertex Transform

For each vertex, the full pipeline is:

1. **Load vertex** (3 bytes: x, y, z)
2. **Multiply by rotation matrix** (9 multiplications, 6 additions) using quarter-square
   tables and fixed-point sine/cosine values
3. **Look up perspective reciprocal** from z_recip table (1 table lookup)
4. **Project** (2 multiplications: x' * recip, y' * recip)
5. **Add screen center offset**

Total: 11 multiplications per vertex. Using 38-cycle quarter-square multiplies, that is
~418 cycles per vertex for the math alone. A cube (8 vertices) requires ~3,344 cycles for
rotation and projection -- comfortably within a single frame.

### 8.7 Dot Product and Cross Product

**Dot product** (used for backface culling):

```
a . b = ax*bx + ay*by + az*bz
```

Three multiplications and two additions. The sign of the dot product between a face
normal and the view vector determines whether the face points toward or away from the
camera. Faces pointing away are skipped (not drawn), saving significant line-drawing
time.

**Cross product** (used to compute face normals):

```
(a x b).x = ay*bz - az*by
(a x b).y = az*bx - ax*bz
(a x b).z = ax*by - ay*bx
```

Six multiplications and three subtractions. Typically precomputed or computed once per
face per frame.

### 8.8 Common Optimization Tricks

- **Symmetric objects:** A cube's 8 vertices can be derived from 1 vertex by negation,
  reducing the transform to 1 full vertex transform + 7 sign flips.

- **Incremental rotation:** For small per-frame angle changes, approximate the rotation
  as the previous rotation plus a small delta, avoiding full matrix recomputation.

- **Reduced matrix:** If only rotating around one or two axes, several matrix entries are
  zero or one, eliminating those multiplications.

- **Coordinate range limiting:** Keep vertex coordinates in the range -64 to +63 so that
  all intermediate products fit in 16 bits with room for accumulation.

- **Unrolled per-vertex code:** The 9-multiply inner loop is often fully unrolled and
  uses self-modifying code to patch the sine/cosine operands, eliminating all loop
  overhead.


---

## 9. Sorting and Searching

### 9.1 Bubble Sort

The simplest sort, and often the first one implemented on the 6502. Elements are compared
pairwise and swapped if out of order. Passes repeat until no swaps occur.

```asm
; 8-bit bubble sort (ascending)
; Array at 'data', length in 'count'
sort:
    LDA #0
    STA swapped          ; flag: any swaps this pass?
    LDX #0
loop:
    LDA data,X
    CMP data+1,X
    BCC no_swap          ; if data[x] < data[x+1], no swap needed
    BEQ no_swap
    TAY                  ; save data[x]
    LDA data+1,X
    STA data,X           ; data[x] = data[x+1]
    TYA
    STA data+1,X         ; data[x+1] = old data[x]
    INC swapped
no_swap:
    INX
    CPX count_minus_1
    BNE loop
    LDA swapped
    BNE sort             ; repeat if any swaps occurred
    RTS
```

**Performance:** O(n^2) worst case, O(n) best case (already sorted). For the C64,
sorting 32 sprite Y-coordinates typically takes 1000-4000 cycles with bubble sort
depending on disorder.

**Key advantage:** Very simple, very small code, and fast on nearly-sorted data. Since
sprite Y-positions change only slightly between frames, the data is typically almost
sorted, making bubble sort surprisingly effective for sprite multiplexing.

### 9.2 Insertion Sort

Elements are inserted into their correct position in the already-sorted portion of the
array. Efficient for small arrays and nearly-sorted data.

```asm
; Insertion sort (ascending)
    LDX #1               ; start with second element
outer:
    LDA data,X           ; element to insert
    STA temp
    TAY
    DEX
inner:
    CPX #$FF             ; reached beginning?
    BEQ insert
    CMP data,X           ; compare with sorted element
    BCS insert           ; if temp >= data[x], position found
    LDA data,X
    STA data+1,X         ; shift element right
    DEX
    LDA temp
    BCC inner            ; (unconditional, since BCS was not taken)
insert:
    INX
    STY data,X           ; insert element
    INX
    CPX count
    BNE outer
    RTS
```

**Performance:** O(n^2) worst case, O(n) best case. Typically slightly faster than
bubble sort because it performs fewer swaps (each element is moved directly to its
correct position).

### 9.3 Bucket Sort for Sprite Multiplexing

The C64 has only 8 hardware sprites. Sprite multiplexing displays more by reusing sprite
hardware as the raster beam moves down the screen. This requires sprites to be sorted by
Y-coordinate so the multiplexer knows which sprite to display next.

**Basic bucket sort:**

```
1. Create 128 buckets (one per 2 scanlines, or one per visible Y-position)
2. For each sprite, drop it into the bucket corresponding to its Y-coordinate
3. Walk the buckets from top to bottom, collecting sprites in sorted order
```

**Implementation details:**

- Each bucket is a linked list. A 128-byte array `bucket_head[y]` stores the first
  sprite ID for each Y position. A separate `next[sprite]` array stores the next sprite
  in the same bucket.
- Filling: For each sprite, read its Y-coordinate, use it to index `bucket_head`, chain
  the old head into `next[sprite]`, and store the new sprite ID.
- Emptying: Walk from Y=0 to Y=127, following the linked list for each non-empty bucket.

**Performance:** O(n + k) where n = number of sprites, k = number of buckets. For
typical game scenarios (16-32 sprites), bucket sort takes 1000-2000 cycles -- comparable
to or faster than an optimized bubble sort.

### 9.4 Field Sort (Linus Akesson's Speedcode Technique)

An advanced sprite sorting technique that converts bucket traversal into executable code.
The "field" is a 220-byte array of INY instructions (opcode $C8), one per visible
Y-position. When a sprite occupies a Y-position, its INY instruction is replaced with a
JMP to a bucket-emptying routine.

**Execution model:**

1. **Fill phase:** For each sprite, patch its Y-position's byte in the field from INY
   ($C8) to JMP ($4C), and chain the sprite ID into a linked list at that position.
2. **Traverse phase:** Execute the field as code (JSR to the field start). Empty
   positions execute INY in 2 cycles. Occupied positions jump to a handler that pushes
   sprite IDs to the stack, then returns to the field.
3. **Cleanup:** Restore patched bytes back to INY.

**Performance:** For 32 sprites, worst-case ~2208 cycles in approximately 2 KB of RAM.
Uses the undocumented `LAX` opcode (loads both A and X simultaneously) for efficiency.

**Comparison:**

| Method          | Typical cycles (32 sprites) | Worst case | Memory  |
|-----------------|-----------------------------|------------|---------|
| Bubble sort     | ~1500                       | ~4000      | ~50 B   |
| Bucket sort     | ~1200                       | ~2000      | ~400 B  |
| Field sort      | ~1200                       | ~2208      | ~2 KB   |
| Ocean sort      | ~800                        | ~4000+     | ~100 B  |

Ocean sort (a bubble sort variant) is fastest for nearly-sorted data but has terrible
worst-case behavior. Bucket sort and field sort have stable, predictable timing.

### 9.5 Binary Search

For sorted tables (e.g., looking up which screen row corresponds to a sprite Y-
coordinate, or finding a value in a translation table):

```asm
; Binary search for 'target' in sorted array 'table' of 'count' entries
; Returns: index in X, carry set if found
    LDA #0
    STA lo
    LDA count
    STA hi
search:
    LDA lo
    CLC
    ADC hi
    ROR A                ; mid = (lo + hi) / 2
    TAX
    LDA table,X
    CMP target
    BEQ found
    BCS too_high
    INX                  ; lo = mid + 1
    STX lo
    JMP check
too_high:
    DEX                  ; hi = mid - 1
    STX hi
check:
    LDA lo
    CMP hi
    BCC search           ; lo < hi, keep searching
    BEQ search           ; lo = hi, one more check
not_found:
    CLC
    RTS
found:
    SEC
    RTS
```

**Performance:** O(log n) -- for a 256-element table, at most 8 iterations. However,
for small tables (< 16 elements), a linear search is often faster due to lower overhead
per iteration on the 6502.


---

## 10. Hardcore Details

### 10.1 Cycle-Counted Multiply Routines

The most performance-critical multiply in C64 programming is the 8x8 -> 16-bit unsigned
multiply used in rotation matrix calculations and line drawing. Over 120 different
routines have been compared in the multiply_test project by TobyLobster.

**Categories and performance ranges:**

| Method                  | Cycles (avg) | Cycles (worst) | Table memory | Code size |
|-------------------------|--------------|----------------|--------------|-----------|
| Shift-and-add (basic)   | 130-153      | 153            | 0            | 20-30 B   |
| Shift-and-add (opt.)    | 100-120      | 130            | 0            | 30-50 B   |
| Quarter-square (basic)  | 70-83        | 83             | 2 KB         | 40-60 B   |
| Quarter-square (fast)   | 38-45        | 45             | 2 KB         | 80-120 B  |
| Log/antilog             | 40-50        | 50             | 768 B-1 KB   | 20-30 B   |
| 4-bit multiply table    | 55-65        | 65             | 512 B        | 40-60 B   |
| Full 8x8 table          | 8-12         | 12             | 64 KB        | 10 B      |

The full 8x8 lookup table (64 KB for all 256x256 products) is impossibly large for the
C64's 64 KB address space. The quarter-square method at 2 KB is the practical optimum.

**The fastest known general 8x8 -> 16 multiply** uses the quarter-square method with
page-aligned tables and achieves approximately 38 cycles worst-case. The inner loop is
fully unrolled:

```asm
; Assumes tables sqr_lo, sqr_hi are page-aligned
; Input: A = factor1, X = factor2 (or vice versa)
; Setup: self-modify table pointers with factor1
;
; Core sequence (simplified):
    SEC
    SBC factor2          ; A = factor1 - factor2
    BCS pos
    EOR #$FF             ; negate to get |factor1 - factor2|
    ADC #1
pos:
    TAY                  ; Y = |diff|
    LDA (sum_sqr_lo),Y   ; f(|diff|) low
    SEC
    SBC (sum_sqr_lo+2),Y ; f(sum) low - f(diff) low (table pointers pre-set)
    STA result_lo
    LDA (sum_sqr_hi),Y
    SBC (sum_sqr_hi+2),Y
    STA result_hi
```

(Actual implementations vary in how they set up the table pointers; the fastest use
self-modifying code to patch LDA absolute,Y instructions directly.)

### 10.2 The Log/Exp Multiplication Method

Used in the game Elite across several 6502 platforms (BBC Micro, Apple II, C64). Instead
of algebraic manipulation, this method exploits logarithmic identities:

```
a * b = 2^(log2(a) + log2(b))
```

**Implementation:**

1. Build a 256-byte log table: `log_table[x] = round(32 * log2(x) * 256)` for x = 1..255
2. Build a 512-byte antilog table: `antilog[r] = round(2^(r/32 + 8) / 256)` for
   r = 0..511

**The FMLTU routine (from Elite):**

```
Given A and Q (unsigned 8-bit):
1. If A = 0 or Q = 0, return 0
2. Look up La = log_table[A]
3. Look up Lq = log_table[Q]
4. sum = La + Lq
5. If sum < 256 (no carry), return 0  (product underflows)
6. Return antilog[sum AND $FF]        (low byte indexes the antilog table)
```

**Performance:** approximately 46-50 cycles for an 8x8 -> 8-bit multiply (high byte
only). This is comparable to the quarter-square method but produces only the high 8 bits
of the result -- suitable for Elite's perspective calculations where full 16-bit precision
is not needed.

**Advantage:** Only 768 bytes of tables (vs. 2 KB for quarter-square). The trade-off is
reduced precision (only the high byte of the product).

**Division via logs:** Subtraction of logarithms gives division:

```
a / b = 2^(log2(a) - log2(b))
```

This converts division into a table lookup, a subtraction, and another table lookup --
dramatically faster than shift-and-subtract division.

### 10.3 Fixed-Point Error Analysis

When performing chains of fixed-point operations, rounding errors accumulate. Key
considerations:

**Truncation vs. rounding:** Simple bit-shifting (LSR/ROR) truncates toward zero. This
introduces a systematic negative bias. Adding $80 (0.5 in 8.8 format) before shifting
provides proper rounding but costs extra cycles.

**Multiplication precision loss:** Multiplying two 8.8 values and keeping only the middle
16 bits of the 32-bit result discards the lowest 8 bits. This introduces up to
1/256 error per multiplication. In a rotation pipeline with 9-11 multiplications per
vertex, errors can accumulate to 9-11/256 = 0.035-0.043 -- visible as 1-2 pixel jitter.

**Mitigation strategies:**

- Use 16.16 or 8.16 intermediate precision during critical calculations, reducing to 8.8
  only for final output.
- Recompute vertex positions from original coordinates each frame rather than
  incrementally updating (which accumulates error over time).
- Choose power-of-two scale factors for sine/cosine tables (64 or 128) so that the
  de-scaling shift is exact with no rounding.
- Test with extreme vertex coordinates to verify that intermediate products do not
  overflow. An 8.8 coordinate times a 1.7 sine value produces a 9.15 result -- the
  integer part must fit in the available bits.

**Overflow detection:** The 6502 overflow flag (V) is set by ADC/SBC when the signed
result does not fit in the destination. However, checking V after every operation is too
expensive in tight loops. Instead, programmers constrain input ranges to guarantee that
overflow cannot occur.

### 10.4 Arbitrary Precision Arithmetic

For applications requiring more than 16 bits (music sequencer timing, large-number
calculations, cryptographic challenges):

**Multi-byte addition:**

```asm
; 32-bit addition: result = value1 + value2
    CLC
    LDA value1+0        ; byte 0 (least significant)
    ADC value2+0
    STA result+0
    LDA value1+1        ; byte 1
    ADC value2+1
    STA result+1
    LDA value1+2        ; byte 2
    ADC value2+2
    STA result+2
    LDA value1+3        ; byte 3 (most significant)
    ADC value2+3
    STA result+3
```

The carry flag chains all four bytes automatically. The pattern extends to any number of
bytes.

**Multi-byte subtraction:**

```asm
; 32-bit subtraction: result = value1 - value2
    SEC                  ; note: SEC, not CLC
    LDA value1+0
    SBC value2+0
    STA result+0
    LDA value1+1
    SBC value2+1
    STA result+1
    LDA value1+2
    SBC value2+2
    STA result+2
    LDA value1+3
    SBC value2+3
    STA result+3
```

**Multi-byte shifting:** Shift operations chain through carry:

```asm
; 32-bit left shift (multiply by 2):
    ASL result+0         ; shift byte 0, bit 7 -> carry
    ROL result+1         ; carry -> byte 1 bit 0, bit 7 -> carry
    ROL result+2
    ROL result+3

; 32-bit right shift (divide by 2):
    LSR result+3         ; shift byte 3, bit 0 -> carry
    ROR result+2         ; carry -> byte 2 bit 7, bit 0 -> carry
    ROR result+1
    ROR result+0
```

**Multi-byte multiplication:** Uses the same shift-and-add or quarter-square algorithms,
but with multi-byte operands. A 32x32 -> 64-bit multiply decomposes into 16 partial
products of 8x8, each using the fast quarter-square tables, with careful carry propagation
between partial sums.

### 10.5 Fast Square Root

**Subtraction method:** The square root of N can be found by subtracting successive odd
numbers (1, 3, 5, 7, ...) from N. The count of successful subtractions is the integer
square root:

```
sqrt(25): 25-1=24, 24-3=21, 21-5=16, 16-7=9, 9-9=0 -> 5 subtractions -> sqrt = 5
```

This is slow (O(sqrt(N)) iterations) but extremely compact (under 20 bytes of code).

**Digit-by-digit method (faster):** Processes two bits of the input per iteration:

```asm
; 16-bit integer -> 8-bit square root
; Input: NUM (16-bit), Output: ROOT (8-bit), REM (9-bit remainder)
    LDA #0
    STA ROOT
    STA REM
    STA REM+1
    LDX #8               ; 8 iterations (2 bits per iteration = 16 bits)
loop:
    ASL NUM              ; shift top 2 bits of input
    ROL NUM+1            ; into remainder
    ROL REM
    ROL REM+1
    ASL NUM
    ROL NUM+1
    ROL REM
    ROL REM+1
    ; trial subtraction: remainder - (4*root + 1)
    LDA ROOT
    ASL A                ; 2 * root
    ASL A                ; 4 * root
    ORA #1               ; 4 * root + 1
    ; ... (16-bit subtraction from REM)
    ; if subtraction succeeds: REM = REM - trial, ROOT = ROOT*2 + 1
    ; if subtraction fails: ROOT = ROOT * 2
    DEX
    BNE loop
```

Performance: 8 iterations for a 16-bit input, approximately 200-250 cycles.

**Table lookup:** For 8-bit inputs, a 256-byte table gives instant square roots.
For 16-bit inputs, use the high byte to index a 256-entry table for an initial
approximation, then refine with one Newton-Raphson iteration:

```
approx = sqrt_table[N >> 8]        ; initial guess from table
refined = (approx + N/approx) / 2   ; one Newton-Raphson step
```

This gives a good 8-bit square root of a 16-bit number in approximately 50-80 cycles
(table lookup + one division + one addition + one shift).

### 10.6 How the Best 3D Demos Achieve Speed

The most impressive C64 3D demos (spinning filled-polygon objects at 50fps) combine
every optimization described in this document, plus several additional techniques:

**Pre-rotation:** For objects that rotate continuously in a predictable pattern, all
vertex positions for every frame of the animation are precomputed and stored in memory.
The frame loop simply copies the appropriate vertex set. This trades potentially tens of
kilobytes of RAM for zero runtime math.

**Screencode 3D:** Instead of bitmap graphics (8000 bytes, slow pixel addressing), some
engines use a custom character set (2048 bytes) with linear Y-addressing. Each character
is treated as an 8x8 pixel block. Column boundaries are tracked with pointer arithmetic
rather than per-pixel address calculation, making line drawing significantly faster.

**Double buffering with character sets:** Two character sets are allocated. While one is
displayed, the other is drawn into. Frame swap is achieved by changing a single VIC-II
register ($D018) -- instantaneous, no data copying needed.

**Backface culling:** Using the dot product of face normals with the view direction to
skip back-facing polygons. For a cube, this typically eliminates 3 of 6 faces, nearly
halving draw time.

**Edge caching / dirty rectangle:** Only redraw edges that have changed since the last
frame. Combined with XOR drawing, the previous frame's wireframe can be erased by
redrawing it (XOR cancels out), then drawing the new frame's wireframe.

**Raster-split rendering:** Use the top portion of the screen for the 3D view and the
bottom for a text panel. The 3D area can be smaller (e.g., 160x128), reducing draw time
while still looking impressive.

**Undocumented opcodes:** Illegal opcodes like LAX (load A and X simultaneously), SAX
(store A AND X), and DCP (decrement and compare) save cycles in critical inner loops.
LAX alone saves 2 cycles every time you need the same value in both A and X.

**Self-modifying multiply:** The fastest multiply routines patch their own LDA operands
with one of the factors before the multiply loop, avoiding indirection overhead.
Combined with page-aligned tables, this eliminates all 16-bit address arithmetic from
the inner loop.

**Cycle-exact frame budgeting:** Developers count every cycle of every routine and fit
them into the ~19,000 available cycles per PAL frame (after accounting for VIC-II badline
and sprite DMA cycle stealing). The budget typically looks like:

| Phase                        | Cycles (approx)  |
|------------------------------|-------------------|
| Matrix setup (sine lookups)  | 200-400           |
| Vertex rotation (8 verts)    | 3000-4000         |
| Perspective projection       | 500-1000          |
| Backface culling             | 200-400           |
| Erase previous frame (XOR)   | 2000-4000         |
| Draw new wireframe           | 3000-6000         |
| Double-buffer swap           | 10-20             |
| Sprite/music/input           | 1000-2000         |
| **Total**                    | **~10000-18000**  |

That leaves almost no headroom. Every cycle saved in the multiply routine or line drawer
directly enables more vertices or edges per frame.


---

## References

### Multiplication

- [Multiplication on 6502 (Lysator)](https://www.lysator.liu.se/~nisse/misc/6502-mul.html) -- Quarter-square method analysis, shift-and-add survey, cycle counts
- [Multiplying and Dividing on the 6502 (Neil Parker)](https://llx.com/Neil/a2/mult.html) -- Thorough treatment of shift-and-add, quarter-square, division, signed arithmetic
- [TobyLobster multiply_test (GitHub)](https://github.com/TobyLobster/multiply_test) -- Comparison of 120+ 6502 multiply routines with benchmarks
- [Codebase64: Seriously Fast Multiplication](https://codebase64.org/doku.php?id=base:seriously_fast_multiplication) -- Quarter-square implementation for C64
- [6502.org: Fast Multiplication](http://www.6502.org/source/integers/fastmult.htm) -- Classic fast multiply source code
- [Retro64: 6502 8-bit Fast Multiply](https://retro64.altervista.org/blog/assembly-math-6502-8-bit-fast-multiply-routine-16-bit-multiply-without-bit-shifting/) -- Table-based multiply without bit shifting

### Division

- [6502.org: Source: Division (32-bit)](http://6502.org/source/integers/ummodfix/ummodfix.htm) -- 32-bit unsigned division
- [NESDev: 16-bit Division](https://forums.nesdev.org/viewtopic.php?t=143) -- Division routines for NES/6502
- [NESDev: Unsigned Integer Division Routines](https://forums.nesdev.org/viewtopic.php?t=11336) -- Division routine comparison

### Logarithmic Multiplication

- [Elite: Multiplication and Division Using Logarithms](https://elite.bbcelite.com/deep_dives/multiplication_and_division_using_logarithms.html) -- Log/antilog method as used in Elite

### Trigonometry and Tables

- [Codebase64: Trigonometric Functions](https://codebase64.org/doku.php?id=base:trigonometric_functions) -- Sine/cosine table implementations
- [Wilson Mines Co.: 16-bit Math Look-up Tables](https://wilsonminesco.com/16bitMathTables/) -- Fixed-point trig and log tables for 6502
- [NESdev: Sine and Cosine Lookup Tables](https://forums.nesdev.org/viewtopic.php?t=1529) -- Sine table discussion and implementations
- [Lemon64: 6502 ASM Sin, Cos and Tan Routine](https://www.lemon64.com/forum/viewtopic.php?t=10492) -- C64-specific trig routines

### Random Number Generation

- [prng_6502 (GitHub)](https://github.com/bbbradsmith/prng_6502) -- Galois LFSR implementations with benchmarks
- [NESdev Wiki: Random Number Generator](https://www.nesdev.org/wiki/Random_number_generator) -- LFSR theory and implementations
- [SID - C64-Wiki](https://www.c64-wiki.com/wiki/SID) -- SID chip noise waveform and $D41B register
- [Electric Druid: Practical LFSR Random Number Generators](https://electricdruid.net/practical-lfsr-random-number-generators/) -- LFSR theory and polynomial selection

### Line Drawing and Graphics

- [Elite: Bresenham's Line Algorithm](https://elite.bbcelite.com/deep_dives/bresenhams_line_algorithm.html) -- Detailed Bresenham implementation on 6502
- [EgonOlsen71/bresenham (GitHub)](https://github.com/EgonOlsen71/bresenham) -- C64 line drawing, pixel plotting, and flood fill
- [Retro64: Line Drawing Routines](https://retro64.altervista.org/blog/line-drawing-routines-programming-different-approach-6502-assembly-implementation/) -- Alternative line drawing implementation
- [Bumbershoot Software: Building a Faster C64 Bitmap Library](https://bumbershootsoft.wordpress.com/2020/11/09/building-a-faster-c64-bitmap-library/) -- Optimized pixel plotting with lookup tables

### 3D Mathematics

- [Creators C64 Wiki: 3D GFX on C64](http://www.creators.no/dokuwiki/doku.php?id=c_hack_8_3d_gfx_on_c64) -- Complete 3D pipeline tutorial
- [Codebase64: 3D Rotation](https://codebase64.org/doku.php?id=base%3A3d_rotation) -- Rotation matrix implementation
- [mahnke.tech: 3D Graphics on the C64](http://www.mahnke.tech/blog/2019-04-12-3d-graphics-on-the-commodore-64-part-one.html) -- 3D engine tutorial
- [Retro64: Vector-Based Graphics on C64](https://retro64.altervista.org/blog/an-introduction-to-vector-based-graphics-the-commodore-64-rotating-simple-3d-objects/) -- Rotation and wireframe rendering

### Sorting

- [Linus Akesson: Field Sort](https://www.linusakesson.net/programming/fieldsort/index.php) -- Advanced bucket sort with speedcode
- [Covert Bitops: Sorting for Sprite Multiplexing](https://cadaver.github.io/rants/sorting.html) -- Seven sorting methods compared
- [C64 OS: QuickSort6502](https://c64os.com/post/quicksort6502) -- QuickSort implementation for 6502
- [6502.org: Bubble Sort](http://6502.org/source/sorting/bubble8.htm) -- Classic 8-bit bubble sort
- [6502.org: Combination Sort](http://6502.org/source/sorting/combo.htm) -- Shell sort variant, 3.6x faster than bubble

### Square Root

- [6502.org Wiki: Software Math - Square Root](http://6502org.wikidot.com/software-math-sqrt) -- Multiple sqrt algorithms compared
- [TobyLobster/sqrt_test (GitHub)](https://github.com/TobyLobster/sqrt_test) -- Benchmark of 6502 square root routines
- [BeebWiki: Square Roots in 6502 Machine Code](https://beebwiki.mdfs.net/Square_roots_in_6502_machine_code) -- Multiple implementation approaches

### General 6502 Math

- [Codebase64: 6502/6510 Maths](https://codebase64.org/doku.php?id=base%3A6502_6510_maths) -- Comprehensive math routine collection
- [6502.org: Source Code Library](http://www.6502.org/source/) -- Curated collection of 6502 routines
- [6502.org Algorithms (Obelisk)](http://www.6502.org/users/obelisk/6502/algorithms.html) -- Multi-byte arithmetic and algorithms
- [Atari Archives: Assembly Language Math](https://www.atariarchives.org/roots/chapter_10.php) -- Introduction to 6502 arithmetic

### Demo Programming and Optimization

- [Antimon: An Introduction to Programming C-64 Demos](http://www.antimon.org/code/Linus/) -- Comprehensive demo coding tutorial
- [nurpax: Dirty Tricks 6502 Programmers Use](https://nurpax.github.io/posts/2019-08-18-dirty-tricks-6502-programmers-use.html) -- Self-modifying code and optimization techniques
- [Kick-3D (GitHub)](https://github.com/Isenbeck/Kick-3D) -- C64 3D engine demo code

### Fixed-Point and Floating-Point

- [6502-Arithmetic (GitHub)](https://github.com/rmsk2/6502-Arithmetic) -- 32-bit signed integer/fixed-point library
- [C64-Wiki: Floating Point Arithmetic](https://www.c64-wiki.com/wiki/Floating_point_arithmetic) -- C64 BASIC floating-point format
- [6502.org: Woz Floating Point](http://6502.org/source/floats/wozfp1.txt) -- Steve Wozniak's original floating-point routines
