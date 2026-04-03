# MOS 6526 CIA (Complex Interface Adapter) -- Comprehensive Reference

## 1. Overview

The Commodore 64 contains two MOS 6526 CIA (Complex Interface Adapter) chips that
provide all general-purpose I/O, timing, and interrupt capabilities outside of the
VIC-II video chip and SID audio chip.

| Chip | Base Address | Decimal       | Primary Role                                      |
|------|-------------|---------------|---------------------------------------------------|
| CIA1 | $DC00-$DC0F | 56320-56335   | Keyboard, joysticks, paddles, datasette, IRQ       |
| CIA2 | $DD00-$DD0F | 56576-56591   | VIC-II bank selection, serial bus, user port, NMI  |

Each CIA provides:
- Two 8-bit bidirectional I/O ports (Port A, Port B) with per-pin direction control
- Two 16-bit programmable interval timers (Timer A, Timer B)
- A 24-bit Time-of-Day (TOD) clock in BCD format
- An 8-bit serial shift register
- An interrupt control/status register

**Key architectural distinction:** CIA1 drives the IRQ line (directly connected to
the 6510 CPU's /IRQ pin), while CIA2 drives the NMI line (connected to /NMI). This
means CIA1 interrupts are maskable (via SEI) but CIA2 interrupts are not -- they
trigger non-maskable interrupts.

### Chip Variants

| Variant | Speed Grade | Notes                                            |
|---------|------------|--------------------------------------------------|
| 6526    | 1 MHz      | Original; has Timer B interrupt bug               |
| 6526A   | 2 MHz      | Timer fires one cycle earlier than 6526           |
| 8521    | 2 MHz      | Used in late C64C boards; functionally similar    |

The C64 always runs the CIA at 1 MHz (system clock), but later board revisions used
the 6526A or 8521 as drop-in replacements. The one-cycle timing difference between
the 6526 and 6526A can affect cycle-exact code.

### System Interrupt Vectors

| Vector         | Address       | Default Points To | Purpose              |
|----------------|--------------|-------------------|----------------------|
| NMI hardware   | $FFFA-$FFFB  | $FE43             | NMI entry point       |
| RESET          | $FFFC-$FFFD  | $FCE2             | Cold start            |
| IRQ/BRK        | $FFFE-$FFFF  | $FF48             | IRQ/BRK entry point   |
| IRQ (KERNAL)   | $0314-$0315  | $EA31             | Software IRQ vector   |
| BRK (KERNAL)   | $0316-$0317  | $FE66             | Software BRK vector   |
| NMI (KERNAL)   | $0318-$0319  | $FE47             | Software NMI vector   |


---

## 2. CIA1 ($DC00-$DC0F) Register Map

### $DC00 -- Port A (PRA)

**Read:** Directly reads the pin states of Port A.
**Write:** Sets the output latch for Port A pins configured as outputs.

Default data direction: $FF (all outputs, for keyboard column selection).

| Bit | Keyboard Function       | Joystick Port 2 | Paddle         |
|-----|------------------------|-----------------|----------------|
| 0   | Select keyboard column 0 | Up              | --             |
| 1   | Select keyboard column 1 | Down            | --             |
| 2   | Select keyboard column 2 | Left            | --             |
| 3   | Select keyboard column 3 | Right           | --             |
| 4   | Select keyboard column 4 | Fire            | --             |
| 5   | Select keyboard column 5 | --              | --             |
| 6   | Select keyboard column 6 | --              | Paddle select  |
| 7   | Select keyboard column 7 | --              | Paddle select  |

When scanning the keyboard, individual column bits are driven low (0) one at a time
while the corresponding row data is read from Port B.

### $DC01 -- Port B (PRB)

**Read:** Reads the pin states of Port B (keyboard rows, joystick port 1).
**Write:** Sets the output latch for Port B pins configured as outputs.

Default data direction: $00 (all inputs, for reading keyboard rows).

| Bit | Keyboard Function     | Joystick Port 1 | Timer Output    |
|-----|-----------------------|-----------------|-----------------|
| 0   | Read keyboard row 0   | Up              | --              |
| 1   | Read keyboard row 1   | Down            | --              |
| 2   | Read keyboard row 2   | Left            | --              |
| 3   | Read keyboard row 3   | Right           | --              |
| 4   | Read keyboard row 4   | Fire            | --              |
| 5   | Read keyboard row 5   | --              | --              |
| 6   | Read keyboard row 6   | --              | Timer A output  |
| 7   | Read keyboard row 7   | --              | Timer B output  |

Bits 6 and 7 can optionally reflect Timer A / Timer B underflow output (pulse or
toggle), controlled by the respective control registers.

### $DC02 -- Data Direction Register A (DDRA)

Each bit controls the direction of the corresponding Port A pin:
- `0` = Input (high-impedance, reads external state)
- `1` = Output (drives the pin from the output latch)

**Default value:** $FF (all outputs -- for keyboard column driving).

### $DC03 -- Data Direction Register B (DDRB)

Same as DDRA but for Port B.

**Default value:** $00 (all inputs -- for keyboard row reading).

### $DC04-$DC05 -- Timer A (TA_LO / TA_HI)

| Address | Read                     | Write                    |
|---------|--------------------------|--------------------------|
| $DC04   | Timer A counter low byte  | Timer A latch low byte   |
| $DC05   | Timer A counter high byte | Timer A latch high byte  |

Reading returns the current countdown value. Writing loads the latch (not the
counter directly). The latch is transferred to the counter on:
- Timer start (setting CRA bit 0)
- Timer underflow (in continuous mode)
- Force load (setting CRA bit 4)

**Default KERNAL value:** $4025 (16421 decimal) -- generates ~60.06 Hz IRQ for
keyboard scanning on NTSC systems. PAL uses $4295 (17045).

### $DC06-$DC07 -- Timer B (TB_LO / TB_HI)

| Address | Read                     | Write                    |
|---------|--------------------------|--------------------------|
| $DC06   | Timer B counter low byte  | Timer B latch low byte   |
| $DC07   | Timer B counter high byte | Timer B latch high byte  |

Identical behavior to Timer A, but Timer B has additional count source options
(see CRB bits 5-6).

### $DC08-$DC0B -- Time of Day Clock (TOD)

All TOD registers use BCD (Binary-Coded Decimal) format.

| Address | Register    | Bits         | Range     | Notes                     |
|---------|------------|-------------|-----------|---------------------------|
| $DC08   | TOD 10ths  | Bits 0-3    | 0-9       | Tenths of seconds          |
| $DC09   | TOD Sec    | Bits 0-3: units, 4-6: tens | 00-59 | Seconds          |
| $DC0A   | TOD Min    | Bits 0-3: units, 4-6: tens | 00-59 | Minutes          |
| $DC0B   | TOD Hours  | Bits 0-3: units, Bit 4: tens, Bit 7: PM | 1-12 | 12-hour format |

**Latching behavior (critical):**
- **Reading $DC0B (hours)** latches all TOD registers, freezing the displayed value
  so you get a consistent time snapshot. The actual clock continues ticking internally.
- **Reading $DC08 (10ths)** releases the latch, allowing displayed values to update again.
- You MUST read 10ths after reading hours, or the TOD display remains frozen.

**Writing behavior:**
- **Writing $DC0B (hours)** halts the TOD clock entirely.
- **Writing $DC08 (10ths)** restarts the TOD clock.
- Always write hours first, then minutes, seconds, and finally 10ths to start.

**Alarm mode:** When CRB bit 7 = 1, writes to TOD registers set the alarm time
instead of the clock time. When the clock matches the alarm, bit 2 of the ICR is set.

### $DC0C -- Serial Shift Register (SDR)

An 8-bit shift register for serial I/O through the SP (Serial Port) pin, clocked by
the CNT (Counter) pin.

- **Output mode** (CRA bit 6 = 1): Timer A provides the clock. On each Timer A
  underflow, CNT toggles and one bit is shifted out on SP (MSB first). After 8 bits
  (16 Timer A underflows, since CNT toggles for both edges), the SDR interrupt flag
  is set in the ICR.
- **Input mode** (CRA bit 6 = 0): External clock on CNT shifts data in from SP.
  After 8 bits received, the SDR interrupt flag is set.

### $DC0D -- Interrupt Control Register (ICR)

This register behaves differently on read vs. write.

**Read -- Interrupt Data Register (clears on read):**

| Bit | Source                          |
|-----|---------------------------------|
| 0   | Timer A underflow               |
| 1   | Timer B underflow               |
| 2   | TOD alarm match                 |
| 3   | SDR full (input) / empty (output) |
| 4   | FLAG pin (active-high-to-low edge on the FLAG pin; directly active on CIA1 from the cassette port read line) |
| 5-6 | Always 0                        |
| 7   | IRQ occurred (logical OR: any enabled source fired) |

**Reading this register clears ALL interrupt flags and releases the IRQ line.**
This is how you acknowledge CIA1 interrupts.

**Write -- Interrupt Mask Register:**

| Bit | Meaning                                                  |
|-----|----------------------------------------------------------|
| 0   | Timer A underflow enable/disable                          |
| 1   | Timer B underflow enable/disable                          |
| 2   | TOD alarm enable/disable                                  |
| 3   | SDR enable/disable                                        |
| 4   | FLAG pin enable/disable                                   |
| 5-6 | Not used                                                  |
| 7   | **SET/CLEAR control:** 1 = set listed bits, 0 = clear them |

Bit 7 determines whether the operation sets or clears the mask bits. For example:
- `LDA #$81 : STA $DC0D` -- Enable Timer A interrupt (set bit 0, with bit 7 = 1)
- `LDA #$01 : STA $DC0D` -- Disable Timer A interrupt (clear bit 0, with bit 7 = 0)
- `LDA #$7F : STA $DC0D` -- Disable ALL interrupt sources

### $DC0E -- Control Register A (CRA)

| Bit | Name     | Values                                              |
|-----|----------|-----------------------------------------------------|
| 0   | START    | 0 = Stop timer, 1 = Start timer                      |
| 1   | PBON     | 0 = PB6 normal, 1 = PB6 shows Timer A output         |
| 2   | OUTMODE  | 0 = Pulse (one cycle high on underflow), 1 = Toggle   |
| 3   | RUNMODE  | 0 = Continuous (reload on underflow), 1 = One-shot (stop on underflow) |
| 4   | LOAD     | 1 = Force load latch into counter (strobe, always reads 0) |
| 5   | INMODE   | 0 = Count system clock (phi2), 1 = Count positive CNT edges |
| 6   | SPMODE   | 0 = Serial port input, 1 = Serial port output         |
| 7   | TODIN    | 0 = 60 Hz TOD input, 1 = 50 Hz TOD input              |

### $DC0F -- Control Register B (CRB)

| Bit   | Name     | Values                                              |
|-------|----------|-----------------------------------------------------|
| 0     | START    | 0 = Stop timer, 1 = Start timer                      |
| 1     | PBON     | 0 = PB7 normal, 1 = PB7 shows Timer B output         |
| 2     | OUTMODE  | 0 = Pulse, 1 = Toggle                                |
| 3     | RUNMODE  | 0 = Continuous, 1 = One-shot                          |
| 4     | LOAD     | 1 = Force load latch into counter (strobe)            |
| 5-6   | INMODE   | See table below                                      |
| 7     | ALARM    | 0 = TOD writes set clock, 1 = TOD writes set alarm   |

**Timer B count source (CRB bits 5-6):**

| Bit 6 | Bit 5 | Count Source                                  |
|-------|-------|-----------------------------------------------|
| 0     | 0     | System clock (phi2)                            |
| 0     | 1     | Positive CNT pin edges                         |
| 1     | 0     | Timer A underflows                             |
| 1     | 1     | Timer A underflows while CNT pin is high       |

The cascaded modes (bits 5-6 = %10 or %11) allow chaining Timer A and Timer B into
an effective 32-bit timer.


---

## 3. CIA2 ($DD00-$DD0F) Register Map

CIA2 has the same internal register structure as CIA1. Only Port A and Port B have
different external connections. Timers, TOD, SDR, ICR, and control registers work
identically -- except that CIA2's interrupt output drives the /NMI line instead of
/IRQ.

### $DD00 -- Port A (PRA)

| Bit | Function                         | Direction | Notes                      |
|-----|----------------------------------|-----------|----------------------------|
| 0   | VIC-II bank select bit 0         | Output    | Active low (inverted)       |
| 1   | VIC-II bank select bit 1         | Output    | Active low (inverted)       |
| 2   | RS-232 TXD (data output)         | Output    |                             |
| 3   | Serial bus ATN OUT               | Output    | 0 = high (inactive), 1 = low (active) |
| 4   | Serial bus CLOCK OUT             | Output    | 0 = high, 1 = low           |
| 5   | Serial bus DATA OUT              | Output    | 0 = high, 1 = low           |
| 6   | Serial bus CLOCK IN              | Input     | 0 = low (active), 1 = high  |
| 7   | Serial bus DATA IN               | Input     | 0 = low (active), 1 = high  |

**VIC-II bank selection (bits 0-1):**

| Bit 1 | Bit 0 | VIC-II Bank | Address Range     |
|-------|-------|-------------|-------------------|
| 1     | 1     | Bank 0      | $0000-$3FFF (default) |
| 1     | 0     | Bank 1      | $4000-$7FFF       |
| 0     | 1     | Bank 2      | $8000-$BFFF       |
| 0     | 0     | Bank 3      | $C000-$FFFF       |

**Note:** The bits are active-low / inverted. Writing %11 selects Bank 0 (the
lowest 16K), not Bank 3. The KERNAL default value for $DD00 is $97, yielding bits
0-1 = %11 = Bank 0.

**Default DDR ($DD02):** $3F (bits 0-5 are outputs, bits 6-7 are inputs).

### $DD01 -- Port B (PRB)

User port and RS-232 signals.

| Bit | Read Function            | Write Function          |
|-----|--------------------------|-------------------------|
| 0   | RS-232 RXD (data input)  | --                       |
| 1   | RS-232 RTS               | RS-232 RTS               |
| 2   | RS-232 DTR               | RS-232 DTR               |
| 3   | RS-232 RI                | RS-232 RI                |
| 4   | RS-232 DCD               | RS-232 DCD               |
| 5   | User port H              | User port H              |
| 6   | RS-232 CTS               | --                       |
| 7   | RS-232 DSR               | --                       |

Also directly accessible as user port pins PB0-PB7. Timer A and Timer B can output
on PB6/PB7 respectively (same as CIA1).

### $DD02 -- Data Direction Register A (DDRA)

**Default value:** $3F (%00111111 -- bits 0-5 output, bits 6-7 input).

### $DD03 -- Data Direction Register B (DDRB)

**Default value:** $00 (all inputs for user port).

### $DD04-$DD07 -- Timer A and Timer B

Identical register structure and behavior as CIA1 ($DC04-$DC07). CIA2's timers are
free for application use since the KERNAL does not use them by default (unlike CIA1
Timer A which generates the system IRQ).

### $DD08-$DD0B -- Time of Day Clock

Identical to CIA1. Same BCD format, same latching behavior.

### $DD0C -- Serial Shift Register

Identical to CIA1.

### $DD0D -- Interrupt Control Register (ICR)

**Identical bit layout to CIA1's ICR**, but drives the /NMI line instead of /IRQ.

| Bit | Read (status)                   | Write (mask)            |
|-----|---------------------------------|-------------------------|
| 0   | Timer A underflow               | Enable/disable          |
| 1   | Timer B underflow               | Enable/disable          |
| 2   | TOD alarm                       | Enable/disable          |
| 3   | SDR complete                    | Enable/disable          |
| 4   | FLAG pin (directly active from the RS-232 / user port) | Enable/disable |
| 7   | NMI occurred (any enabled source) | SET/CLEAR control     |

**Reading $DD0D clears all flags and releases the /NMI line.**

### $DD0E-$DD0F -- Control Registers A and B

Identical bit layout and behavior to CIA1's CRA/CRB ($DC0E/$DC0F).


---

## 4. Keyboard Scanning

### How the Matrix Works

The C64 keyboard is a passive 8x8 matrix of 64 switches. There is no dedicated
keyboard controller -- the CIA1 chip scans the matrix directly.

- **Port A ($DC00)** drives the 8 column lines (active-low outputs)
- **Port B ($DC01)** reads the 8 row lines (active-high inputs with internal pull-ups)

Each key sits at the intersection of one column and one row. When a key is pressed,
it creates an electrical connection between its column and row lines.

### Scanning Technique

1. Set Port A DDR ($DC02) to $FF (all outputs)
2. Set Port B DDR ($DC03) to $00 (all inputs)
3. For each column 0-7:
   a. Write a value to $DC00 with only one bit cleared (the selected column)
   b. Read $DC01 to check all 8 rows
   c. Any bit reading 0 means that key is pressed
4. Repeat ~60 times per second

**Column select values:**

| Column | Value Written to $DC00 | Binary     |
|--------|----------------------|------------|
| 0      | $FE                  | %11111110  |
| 1      | $FD                  | %11111101  |
| 2      | $FB                  | %11111011  |
| 3      | $F7                  | %11110111  |
| 4      | $EF                  | %11101111  |
| 5      | $DF                  | %11011111  |
| 6      | $BF                  | %10111111  |
| 7      | $7F                  | %01111111  |

**Quick-check optimization:** Write $00 to $DC00 (all columns low), then read $DC01.
If the result is $FF, no keys are pressed at all, saving the full scan.

### Assembly Language Example

```asm
; Scan all 8 columns and store results at KEYBUF ($F5-$FC)
            LDA #$FF
            STA $DC02       ; Port A = all outputs
            LDA #$00
            STA $DC03       ; Port B = all inputs

            LDA #$FE        ; Start with column 0
            LDX #$00
scan_loop:
            STA $DC00       ; Select column
            PHA
            LDA $DC01       ; Read row data
            STA KEYBUF,X    ; Store (0 = key pressed)
            PLA
            SEC
            ROL             ; Rotate to select next column
            INX
            CPX #$08
            BNE scan_loop
```

### Complete Keyboard Matrix Table

Columns are selected by driving the corresponding Port A bit low.
Rows are read from Port B; a 0 bit indicates the key is pressed.

```
              PA0       PA1       PA2       PA3       PA4       PA5       PA6       PA7
             ($FE)     ($FD)     ($FB)     ($F7)     ($EF)     ($DF)     ($BF)     ($7F)
            -------   -------   -------   -------   -------   -------   -------   -------
PB0 (bit 0)  DEL      RETURN     C-RIGHT   F7        F1        F3        F5       C-DOWN
PB1 (bit 1)  3         W         A         4         Z         S         E        L-SHIFT
PB2 (bit 2)  5         R         D         6         C         F         T         X
PB3 (bit 3)  7         Y         G         8         B         H         U         V
PB4 (bit 4)  9         I         J         0         M         K         O         N
PB5 (bit 5)  +         P         L         -         .         :         @         ,
PB6 (bit 6)  POUND     *         ;        CLR/HOME  R-SHIFT    =         ^         /
PB7 (bit 7)  1        LEFT-ARR   CTRL      2        SPACE      C=        Q       RUN/STOP
```

**Special keys outside the matrix:**
- **RESTORE** -- Connected directly to the /NMI line (active-low to CIA2). Not part
  of the keyboard matrix at all. Directly triggers NMI when pressed.
- **SHIFT LOCK** -- Mechanically latching switch wired in parallel with LEFT SHIFT.
  Electrically indistinguishable from holding Left Shift.

### Key Ghosting

Key ghosting occurs when three or more simultaneously pressed keys form a
rectangular pattern in the matrix, causing a phantom fourth key to appear pressed.

**Example:** If keys at positions (col2, row1), (col2, row3), and (col5, row3) are
all pressed, the electrical path through the matrix makes (col5, row1) also appear
pressed -- even though it is not.

**Why it happens:** When column 2 is driven low, both rows 1 and 3 read as 0 (both
keys pressed). But since row 3 connects to column 5 through the third key press,
column 5 is also pulled low through the reverse path. When column 5 is then scanned,
row 1 falsely reads as 0.

**Mitigation:** The KERNAL limits detection to 1-2 simultaneous key presses. Custom
scan routines can detect the rectangular pattern and reject ambiguous results.


---

## 5. Joystick Reading

### Hardware Connection

The C64 has two DB-9 control ports. Joystick signals connect directly to CIA1 ports
in parallel with the keyboard matrix:

| Joystick    | CIA1 Register | Port |
|-------------|--------------|------|
| Joystick #1 | $DC01 (PRB)  | Port B (bits 0-4) |
| Joystick #2 | $DC00 (PRA)  | Port A (bits 0-4) |

**Note the swap:** Joystick port 1 is read from Port B and joystick port 2 from
Port A. This is counterintuitive but matches the hardware wiring.

### Bit Assignments (Active-Low)

All joystick bits use active-low logic: 0 = activated, 1 = not activated.

| Bit | Weight | Direction/Action |
|-----|--------|-----------------|
| 0   | $01    | Up               |
| 1   | $02    | Down             |
| 2   | $04    | Left             |
| 3   | $08    | Right            |
| 4   | $10    | Fire button      |

Bits 5-7 are not connected to the joystick and should be masked off.

### Reading Joysticks

**Joystick 2 (simple -- no keyboard conflict):**
```asm
            LDA $DC00       ; Read Port A
            AND #$1F        ; Mask to bits 0-4
            EOR #$1F        ; Invert (now 1 = active)
            ; Bit 0 = up, 1 = down, 2 = left, 3 = right, 4 = fire
```

**Joystick 1 (shares Port B with keyboard):**
```asm
            LDA #$FF
            STA $DC00       ; Deselect all keyboard columns first!
            LDA $DC01       ; Read Port B
            AND #$1F        ; Mask to bits 0-4
            EOR #$1F        ; Invert
```

Before reading joystick 1, you MUST write $FF to $DC00 to deactivate all keyboard
column outputs. Otherwise, pressed keyboard keys will interfere with the joystick
reading (and vice versa).

### Keyboard-Joystick Interference

Since joystick lines run in parallel with the keyboard matrix, moving a joystick
activates the same electrical lines as certain keys:

| Joystick 1 Direction | Equivalent Key(s) Affected |
|---------------------|---------------------------|
| Up                  | Row 0 on all active columns |
| Down                | Row 1                      |
| Left                | Row 2                      |
| Right               | Row 3                      |
| Fire                | Row 4                      |

This is why **most games prefer joystick port 2** -- it connects to Port A (the
column-driving side), which causes fewer phantom keypresses. Port 1 on the row-reading
side more directly conflicts with keyboard scanning.

**Workaround for Port 1 interference:**
```basic
POKE 56322,224    : REM Disable keyboard (set DDR to partial)
... game loop ...
POKE 56322,255    : REM Restore keyboard
```


---

## 6. Timers

### Architecture

Each CIA contains two independent 16-bit countdown timers (Timer A and Timer B).
Each timer consists of:

- A **16-bit counter** (read-only) that decrements toward zero
- A **16-bit latch** (write-only) that holds the reload value
- A **control register** governing behavior

### Starting a Timer

When bit 0 of the control register (CRA/CRB) transitions from 0 to 1:
1. The latch value is loaded into the counter
2. Two phi2 cycles later, the counter begins decrementing

```asm
; Set up CIA1 Timer A for a 1000-cycle delay
            LDA #<1000
            STA $DC04       ; Timer A latch low byte
            LDA #>1000
            STA $DC05       ; Timer A latch high byte
            LDA #$01        ; Bit 0 = start, continuous mode
            STA $DC0E       ; Start Timer A
```

### One-Shot vs. Continuous Mode

**Continuous mode** (CRA/CRB bit 3 = 0):
- Counter decrements to 0, sets the underflow flag in ICR, reloads from latch,
  and continues counting.
- Produces a periodic interrupt at intervals of (latch + 1) phi2 cycles.
- The +1 comes from the reload cycle: the counter spends one cycle at 0 before
  reloading.

**One-shot mode** (CRA/CRB bit 3 = 1):
- Counter decrements to 0, sets the underflow flag, reloads from latch, then
  stops (bit 0 of CRA/CRB is cleared automatically).
- Must be manually restarted by setting bit 0 again.

### Timer Count Sources

**Timer A** can count:
- System clock phi2 (CRA bit 5 = 0) -- decrements every CPU cycle
- Positive edges on the CNT pin (CRA bit 5 = 1)

**Timer B** can count (CRB bits 5-6):
- %00: System clock phi2
- %01: Positive CNT pin edges
- %10: Timer A underflows (cascading for 32-bit timing)
- %11: Timer A underflows while CNT is high (gated cascade)

### Cascading Timers (32-bit Timer)

By setting Timer B to count Timer A underflows, you create an effective 32-bit timer:

```asm
; Set up a 32-bit delay: Timer A = low 16 bits, Timer B = high 16 bits
            LDA #<low_count
            STA $DC04
            LDA #>low_count
            STA $DC05
            LDA #<high_count
            STA $DC06
            LDA #>high_count
            STA $DC07

            LDA #%01000001  ; Timer B: count Timer A underflows, start
            STA $DC0F
            LDA #%00000001  ; Timer A: count phi2, start
            STA $DC0E
```

**Cascade count sequence quirk:** Due to a pipeline delay in recognizing Timer A
underflows, Timer B in cascade mode shows this countdown pattern when Timer A has
latch=2: ...2-2-2-1-1-1-0-0-reload. Consecutive reads may show the same value
multiple times because of the 2-cycle recognition delay.

### Force Load

Setting bit 4 of CRA/CRB forces the latch value into the counter immediately. This
bit is a write-only strobe (always reads as 0). Force-loading also removes the next
pending clock from the internal pipeline, so the first countdown step after a force
load is delayed by one extra cycle.

### Timer Output on Port B

CRA bit 1 enables Timer A output on PB6. CRB bit 1 enables Timer B output on PB7.

- **Pulse mode** (bit 2 = 0): A one-cycle-high pulse appears on the pin at each underflow.
- **Toggle mode** (bit 2 = 1): The output level inverts at each underflow.

The toggle flip-flop is set to high when the timer is started (rising edge on bit 0
of CRA/CRB) and is cleared on system reset.

### Practical Timing Formula

The interval between underflows in phi2 mode:

```
Interval = (latch_value + 1) phi2 cycles
```

For a 1 MHz system clock (985248 Hz PAL, 1022727 Hz NTSC):

```
Frequency = phi2_clock / (latch_value + 1)
```

For example, to generate a 60 Hz interrupt on NTSC:
```
Latch = (1022727 / 60) - 1 = 17044 = $4294
```


---

## 7. Interrupts

### IRQ Generation (CIA1)

CIA1's interrupt output is directly connected to the 6510 CPU's /IRQ pin (active-low,
level-triggered). When any enabled interrupt source fires:

1. The corresponding bit in the ICR is set
2. If the matching bit in the interrupt mask is also set, bit 7 of ICR is set
3. One phi2 cycle later, the /IRQ line is pulled low
4. The CPU finishes the current instruction, then vectors through $FFFE/$FFFF

**Standard CIA1 IRQ flow:**
```asm
irq_handler:
            ; CPU has pushed PC and status register
            ; KERNAL pushes A, X, Y
            LDA $DC0D       ; Read ICR -- acknowledges ALL pending interrupts
            AND #$01        ; Check Timer A underflow
            BNE .timer_a
            ; ... check other sources ...
.timer_a:
            ; Handle Timer A interrupt
            ; ...
            JMP $EA31       ; Return to KERNAL IRQ handler (or RTI)
```

### NMI Generation (CIA2)

CIA2's interrupt output drives the /NMI line (active-low, **edge-triggered**). This
is a critical difference from IRQ:

- /IRQ is **level-triggered**: the line must be held low, and the CPU checks it
  between instructions.
- /NMI is **edge-triggered**: only the HIGH-to-LOW transition triggers the interrupt.
  The CPU latches the edge internally.

**NMI sources:**
1. CIA2 Timer A underflow (if enabled in $DD0D)
2. CIA2 Timer B underflow (if enabled in $DD0D)
3. CIA2 TOD alarm (if enabled in $DD0D)
4. CIA2 SDR (if enabled in $DD0D)
5. CIA2 FLAG pin (directly active from the user port or RS-232)
6. **RESTORE key** (directly connected to the /NMI line, independent of CIA2)

**Acknowledging NMI:**
```asm
nmi_handler:
            ; CPU has pushed PC and status register
            LDA $DD0D       ; Read CIA2 ICR -- clears flags, releases /NMI line
            ; ... handle NMI ...
            RTI
```

**One-byte-shorter NMI acknowledge trick:**
During setup, store $40 (the RTI opcode) in $DD0C (SDR). Then exit NMI handlers with
`JMP $DD0C`, which simultaneously reads $DD0D (as a side effect of the adjacent
address fetch in the 6510's indirect JMP behavior -- but actually this works because
$DD0C contains RTI and reads $DD0D on the way out). In practice this is implemented as:

```asm
; Setup:
            LDA #$40        ; RTI opcode
            STA $DD0C
; NMI handler exit:
            JMP $DD0C       ; Executes RTI; reading $DD0D acknowledges NMI
```

Note: This trick is sometimes described but its reliability depends on the exact
memory access pattern of the 6510. The safe approach is `BIT $DD0D` followed by `RTI`.

### Interrupt Masking

**Enabling specific interrupts:**
```asm
; Enable CIA1 Timer A IRQ only
            LDA #$7F
            STA $DC0D       ; Disable all CIA1 interrupt sources
            LDA #$81        ; Bit 7 = set mode, bit 0 = Timer A
            STA $DC0D       ; Enable Timer A interrupt
```

**Disabling all CIA1 interrupts:**
```asm
            LDA #$7F        ; Bit 7 = clear mode, bits 0-6 = all sources
            STA $DC0D       ; Clear all mask bits
            LDA $DC0D       ; Read to clear any pending flags
```

### IRQ Priority and Sharing

Multiple sources can trigger simultaneously. The IRQ handler should check all bits
of the ICR to determine which sources fired. Reading the ICR clears all flags at
once, so you must test all bits from the single read:

```asm
irq_handler:
            LDA $DC0D       ; Read and clear all flags (do this ONCE)
            STA temp        ; Save for multiple tests
            LSR             ; Bit 0 -> carry
            BCS .timer_a    ; Timer A underflow?
            LSR temp
            LSR temp        ; Bit 2 -> carry
            BCS .tod_alarm  ; TOD alarm?
            ; ... etc ...
```

### KERNAL Default IRQ Setup

The KERNAL configures CIA1 Timer A to generate periodic IRQs:
- **PAL:** Timer A latch = $4025 (16421), generating ~60 Hz IRQ
- **NTSC:** Timer A latch = $4295 (17045), generating ~60 Hz IRQ

This IRQ drives the keyboard scan, cursor blink, tape I/O, and software clock
(`TI` / `TI$` in BASIC, maintained at $A0-$A2).


---

## 8. Hardcore Details

### 8.1 CIA Timer Race Conditions and the One-Cycle Offset

#### 6526 vs. 6526A Timing Difference

The original 6526 and the revised 6526A fire their timer interrupts one phi2 cycle
apart. The 6526A triggers the interrupt one cycle earlier than the 6526.

**Practical impact:** A one-shot timer set to fire at a specific moment will complete
one cycle sooner on the 6526A. Code that relies on exact cycle timing between setting
the timer and the interrupt may behave differently depending on which chip revision
is installed.

**Detection routine:**
```asm
; Detect 6526 vs 6526A
; Set a very short one-shot timer. If the INC executes, it's the old 6526.
            SEI
            LDA #$00
            STA result
            LDA #$01        ; Timer = 1 (will underflow quickly)
            STA $DC04
            LDA #$00
            STA $DC05
            LDA #$81
            STA $DC0D       ; Enable Timer A interrupt
            LDA #$19        ; One-shot, force load, start
            STA $DC0E
            INC result      ; Does this execute before the interrupt?
            ; result = 1: old 6526 (interrupt came one cycle later)
            ; result = 0: new 6526A (interrupt came before INC)
```

#### ICR Read Race Condition

Reading the ICR ($DC0D / $DD0D) at the exact cycle a timer underflows creates a
race condition:

- If the read occurs on the same cycle as the underflow, the interrupt flag may or
  may not be visible in the read result.
- The read clears the register, so the interrupt can be "swallowed" -- neither seen
  in the read result nor generating an IRQ.

This is particularly problematic with **Timer B**, where many 6526 chips have a
documented defect: reading the ICR one or two cycles before Timer B underflow can
prevent the interrupt from being generated at all.

**Workaround:** Avoid polling the ICR in a tight loop when expecting a Timer B
interrupt. Use Timer A where possible, or use the interrupt mechanism rather than
polling.

#### Pipeline Behavior

The CIA timers use an internal pipeline for clock processing:

1. A clock pulse enters the pipeline
2. On the next phi2 cycle, it reaches the counter and causes a decrement
3. When the counter reaches 0 and another clock is pending, the latch reloads

This pipeline means:
- Starting a timer (setting CRA bit 0) takes 2 cycles before counting begins
- Force-loading (setting CRA bit 4) removes the next pending clock from the pipeline
- Consecutive reads of a running timer in phi2 mode show: N, N-1, N-2, ...
  but force-loading produces: N, N, N-1, N-2 (one duplicate due to eaten clock)

### 8.2 TOD Clock Quirks

#### BCD Format

All TOD registers use BCD (Binary-Coded Decimal), not binary. The hours register
uses 12-hour format with an AM/PM flag in bit 7:

```
Hours: $01-$12 (BCD), bit 7 = 0 for AM, 1 for PM
Minutes: $00-$59 (BCD)
Seconds: $00-$59 (BCD)
Tenths: $0-$9 (BCD, single digit)
```

This means you cannot do simple binary arithmetic on TOD values. To increment
seconds, you must handle BCD carry:

```asm
            SED             ; Set decimal mode for BCD arithmetic
            LDA seconds
            CLC
            ADC #$01
            CLD             ; Clear decimal mode
```

#### Latching Behavior (Read Freeze)

Reading the hours register ($DC0B / $DD0B) freezes the output of all four TOD
registers, providing a consistent snapshot. The internal clock continues running.
The freeze is released when the tenths register ($DC08 / $DD08) is read.

**Correct read order:**
```asm
            LDA $DC0B       ; Read hours (freezes output)
            STA hours
            LDA $DC0A       ; Read minutes (still frozen)
            STA minutes
            LDA $DC09       ; Read seconds (still frozen)
            STA seconds
            LDA $DC08       ; Read tenths (unfreezes for next update)
            STA tenths
```

**Bug if you forget:** If you read hours but never read tenths, the TOD display
remains permanently frozen. The clock itself still runs, but you will always read the
same stale values.

#### Write Halt Behavior

Writing to the hours register stops the TOD clock. Writing to the tenths register
restarts it. You must write in order: hours, minutes, seconds, tenths.

```asm
            LDA #$00
            STA $DC0F       ; CRB bit 7 = 0: writes go to clock (not alarm)
            LDA #$12        ; 12:00:00.0 PM (BCD)
            STA $DC0B       ; Write hours -- clock STOPS
            LDA #$00
            STA $DC0A       ; Write minutes
            STA $DC09       ; Write seconds
            STA $DC08       ; Write tenths -- clock STARTS
```

#### Alarm Interrupt Bug

Many 6526 chips have a bug where the TOD alarm interrupt fails to trigger when the
seconds component of the alarm time is exactly $00 (zero). The workaround is to set
the alarm tenths to $01 (0.1 seconds) instead of $00.

#### TOD Input Frequency

The TOD clock requires an external 50 Hz or 60 Hz signal (from the mains power
supply via the C64's power brick). CRA bit 7 selects which frequency to expect:
- Bit 7 = 0: Expect 60 Hz input (North America)
- Bit 7 = 1: Expect 50 Hz input (Europe)

If this is set wrong, the TOD clock runs at the wrong speed (e.g., 20% fast or slow).
The KERNAL sets this during boot based on the system's video standard (PAL/NTSC).

### 8.3 Using CIA Timers for Stable Raster Interrupts

The VIC-II chip can trigger IRQ at a specific raster line, but the exact cycle within
that line when the CPU begins executing the IRQ handler has a jitter of up to 7
cycles (depending on what instruction was executing when the IRQ occurred). Many
graphical effects require cycle-exact timing, so this jitter must be eliminated.

#### The Double-IRQ Technique

This is the classic method for achieving stable raster timing:

**Step 1 -- First IRQ (coarse):**
Set up a VIC-II raster interrupt a few lines before the target line. In the handler:
- Acknowledge the VIC-II interrupt
- Set up CIA1 Timer A for a one-shot interrupt that will fire on the next raster line
- Point the IRQ vector to the second handler
- Execute CLI to allow the CIA interrupt to preempt
- Fill time with NOP instructions (the CIA IRQ will fire during these)

```asm
irq1:       ; First raster IRQ handler (approximate timing)
            STA save_a
            STX save_x
            STY save_y

            LDA #$DC0D      ; Acknowledge CIA1
            LDA $DC0D
            LDA #<irq2
            STA $0314        ; Point to second handler
            LDA #>irq2
            STA $0315

            ; Set CIA Timer A for ~1 raster line delay
            LDA #62          ; 63 cycles per line (PAL) minus 1
            STA $DC04
            LDA #$00
            STA $DC05
            LDA #$19         ; One-shot, force load, start
            STA $DC0E

            LDA #$81
            STA $DC0D        ; Enable CIA Timer A IRQ

            CLI              ; Allow interrupts
            ; Fill with 2-cycle instructions so jitter <= 1 cycle
            NOP
            NOP
            NOP
            NOP
            NOP
            NOP
            NOP
            NOP
            NOP
```

**Step 2 -- Second IRQ (precise):**
Because the first handler only contains 2-byte/2-cycle NOP instructions, the second
IRQ can only arrive with a jitter of 0 or 1 cycle. Detect which case it is:

```asm
irq2:       ; Second IRQ handler (cycle-exact)
            ; At this point, jitter is 0 or 1 cycle
            LDX $D012       ; Read current raster line
            NOP
            NOP
            CPX $D012       ; Has the raster line changed?
            BEQ +2          ; If not, we need one extra cycle delay
            ; We are now cycle-exact!
            ; ... perform cycle-exact raster effect ...
```

The `CPX $D012` trick works because if the raster counter incremented between the
LDX and CPX, we entered the handler 0 cycles late (no extra delay needed). If it
did not increment, we entered 1 cycle early, and the BEQ branch (taken = 3 cycles
vs. not taken = 2 cycles) adds the missing cycle.

#### CIA Timer Value for Raster Lines

The timer value must match the number of phi2 cycles per raster line:

| Video Standard | Cycles/Line | Timer Latch Value |
|---------------|-------------|-------------------|
| PAL (6569)    | 63          | 62 ($3E)          |
| NTSC (6567R8) | 65          | 64 ($40)          |
| NTSC (6567R56A)| 64         | 63 ($3F)          |

### 8.4 Serial Port Tricks

#### Fast Serial Communication via SDR

The CIA's serial shift register can achieve surprisingly fast data rates:
- Timer A clocks the shift register in output mode
- At minimum timer period (latch=1), the SDR can output at phi2/4 rate (~250 kbit/s)
- The 6526A at 2 MHz can reach 500 kbit/s

#### UP9600: 9600 Baud RS-232

Daniel Dallmann's UP9600 interface achieves 9600 bps on the C64 by using the CIA
shift register for both transmit and receive. The clever trick for reception:

1. Configure CIA2 Timer B to pulse on PB7 at the desired baud rate
2. Route PB7 externally (via user port) back to the CNT2 pin
3. The CIA2 SDR uses the CNT2 signal to clock incoming serial data

This creates a self-clocked receive loop using the CIA's own timer output.

#### Clock Generation

The PB6/PB7 timer output feature (CRA/CRB bit 1) provides a hardware-generated
square wave or pulse train without CPU intervention -- useful for generating
clock signals for external hardware.

### 8.5 CIA1/CIA2 Interaction Quirks

#### VIC-II Bank and Keyboard Scanning Conflict

CIA2 Port A bits 0-1 control the VIC-II memory bank. Since the KERNAL keyboard
scan routine modifies CIA1 Port A ($DC00), and CIA2 Port A ($DD00) is a separate
register, there is no direct conflict. However, custom code that manipulates $DD00
for bank switching must preserve bits 2-7 (serial bus lines) to avoid glitching the
IEC bus.

#### NMI During IRQ

An NMI can interrupt an IRQ handler. If both CIA chips fire simultaneously:
1. CIA1 IRQ begins processing
2. CIA2 NMI fires, preempting the IRQ handler
3. NMI handler runs to completion (RTI)
4. IRQ handler resumes

The NMI handler must preserve all registers and not interfere with whatever the
IRQ handler was doing. If the NMI handler reads $DC0D (CIA1 ICR) by mistake, it
will clear the IRQ flags and the IRQ handler will miss its interrupt.

#### Timer Synchronization Between CIAs

The two CIA chips share the same phi2 clock but have no direct hardware connection.
You cannot cascade Timer A of CIA1 into Timer B of CIA2. Cascade mode only works
within a single CIA chip.

### 8.6 NMI Acknowledgment Bug/Behavior

#### Edge-Triggered Nature

The /NMI line is edge-triggered: only the falling edge (HIGH to LOW transition)
triggers the interrupt. While /NMI is held low, no further NMIs can occur, regardless
of activity on other NMI sources.

#### Disabling NMI (Including RESTORE)

Since /NMI is edge-triggered, you can effectively disable all NMI sources by forcing
the line to stay low permanently:

```asm
; Disable NMI by holding the /NMI line low
            LDA #$00
            STA $DD04       ; Timer A = 0 (immediate underflow)
            STA $DD05
            LDA #$81
            STA $DD0D       ; Enable Timer A NMI
            LDA #$01
            STA $DD0E       ; Start Timer A -- triggers NMI immediately

nmi_sink:   ; NMI handler that does NOT acknowledge
            ; Do NOT read $DD0D! This keeps /NMI low.
            RTI             ; Return without clearing the interrupt source
```

After this, the /NMI line is held low by the unacknowledged CIA2 interrupt. The
RESTORE key (which also drives /NMI) cannot produce a new falling edge because the
line is already low. No NMIs will occur until something reads $DD0D to release the
line.

#### The Acknowledge Problem

If your NMI handler reads $DD0D to determine the interrupt source, it simultaneously
clears all flags and releases the /NMI line. If RESTORE is being held down at that
exact moment, a new NMI will trigger immediately -- potentially causing an infinite
NMI loop.

**Safe pattern:** If you use CIA2 NMIs and want to prevent RESTORE from interfering,
acknowledge the CIA2 interrupt and immediately re-arm it. Or, better yet, use the
"hold low" technique above when you want no NMIs at all.

### 8.7 Exact Timer Countdown Behavior

#### Cycle-by-Cycle Timer Operation

The timer uses an internal clock pipeline. Here is the exact sequence after starting
Timer A in phi2 mode (CRA bit 0 written to 1):

| Cycle | Action                                    |
|-------|-------------------------------------------|
| 0     | Write to CRA (bit 0 = 1)                  |
| 1     | Latch loaded into counter, clock enters pipeline |
| 2     | First decrement occurs                     |
| 3     | Second decrement                           |
| ...   | Continues decrementing each cycle          |
| N+1   | Counter reaches 0                          |
| N+2   | Underflow: ICR flag set, counter reloads from latch |
| N+3   | If interrupt enabled, /IRQ or /NMI line goes low |

The total delay from starting the timer to the interrupt is:

```
Delay = latch_value + 2 cycles (pipeline filling) + 1 cycle (interrupt propagation)
```

For a latch value of N, the first underflow occurs N+1 phi2 cycles after counting
begins (the counter counts down from N through 0, inclusive of 0).

#### Continuous Mode Reload

In continuous mode, after underflow:
1. Counter reaches 0
2. Next pending clock causes reload from latch
3. The reload consumes one clock slot
4. Counting continues from the latch value

The effective period is (latch_value + 1) cycles, because the counter passes
through states: latch, latch-1, ..., 1, 0, [reload], latch, latch-1, ...

#### Force Load Side Effects

Setting CRA/CRB bit 4 (force load):
1. Immediately copies the latch into the counter
2. Removes the next pending clock from the pipeline
3. This causes one "skipped" decrement

Observable effect: reading the timer right after a force load may show the same value
twice before it resumes normal decrement.

#### One-Shot Termination

In one-shot mode (CRA/CRB bit 3 = 1):
1. Counter decrements to 0
2. Underflow flag is set in ICR
3. Counter reloads from latch
4. CRA/CRB bit 0 is cleared (timer stops)
5. The bit-0 clear happens on the cycle after underflow

If you set bit 3 (one-shot) in the same write cycle as bit 0 (start), the timer runs
until underflow and stops. If bit 3 is set while the timer is already running, the
stop takes effect on the next underflow.


---

## References

- [CIA - C64-Wiki](https://www.c64-wiki.com/wiki/CIA)
- [Commodore 64 Memory Map (sta.c64.org)](https://sta.c64.org/cbm64mem.html)
- [A Software Model of the CIA6526 (Waterloo)](https://ist.uwaterloo.ca/~schepers/MJK/cia6526.html)
- [CIA 6526 Reference (Waterloo)](https://ist.uwaterloo.ca/~schepers/MJK/cia.html)
- [MOS Technology CIA - Wikipedia](https://en.wikipedia.org/wiki/MOS_Technology_CIA)
- [How the C64 Keyboard Works (C64 OS)](https://c64os.com/post/howthekeyboardworks)
- [Keyboard - C64-Wiki](https://www.c64-wiki.com/wiki/Keyboard)
- [C64 Keyboard Matrix Layout (sta.c64.org)](https://sta.c64.org/cbm64kbdlay.html)
- [Reading the C64 Keyboard Matrix (Elite)](https://elite.bbcelite.com/deep_dives/reading_the_commodore_64_keyboard_matrix.html)
- [Scanning the Keyboard the Correct Way (Codebase64)](https://codebase.c64.org/doku.php?id=base:scanning_the_keyboard_the_correct_and_non_kernal_way)
- [Joystick - C64-Wiki](https://www.c64-wiki.com/wiki/Joystick)
- [Control Port - C64-Wiki](https://www.c64-wiki.com/wiki/Control_Port)
- [Making Stable Raster Routines (Antimon)](https://www.antimon.org/dl/c64/code/stable.txt)
- [Raster Interrupt - C64-Wiki](https://www.c64-wiki.com/wiki/Raster_interrupt)
- [Introduction to Raster IRQs (Codebase64)](http://codebase.c64.org/doku.php?id=base:introduction_to_raster_irqs)
- [Interrupt - C64-Wiki](https://www.c64-wiki.com/wiki/Interrupt)
- [Detecting 6526 vs 6526A (Codebase64)](https://codebase.c64.org/doku.php?id=base:detecting_6526_vs_6526a_cia_chips)
- [Setting Up NMIs on the C64 (Kodiak64)](https://kodiak64.co.uk/blog/setting-up-nmis-on-the-c64)
- [RESTORE Key - C64-Wiki](https://www.c64-wiki.com/wiki/RESTORE_(Key))
- [CIAs -- Timers, Keyboard and More (emudev)](https://emudev.de/q00-c64/cias-timers-keyboard-and-more/)
- [UP9600: 9600 Baud on the C64 (pagetable.com)](https://www.pagetable.com/?p=1656)
- [MOS 6526 CIA Datasheet (Recreated)](http://archive.6502.org/datasheets/mos_6526_cia_recreated.pdf)
- [Memory Map -- Ultimate C64 Reference (pagetable.com)](https://www.pagetable.com/c64ref/c64mem/)
