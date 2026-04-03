# C64 Input Devices and Control Methods -- Comprehensive Reference

## 1. Overview

The Commodore 64 supports a variety of input devices, all routed through a small
number of hardware interfaces. Understanding how each device connects to the system
and how the relevant chips process input is essential for writing responsive,
conflict-free input handling code.

### Input Devices at a Glance

| Device       | Interface       | Registers                  | Max Devices | Key Limitation                           |
|--------------|----------------|----------------------------|-------------|------------------------------------------|
| Keyboard     | CIA1 matrix     | $DC00/$DC01                | 1           | 2-key rollover; ghost keys               |
| Joystick     | CIA1 ports      | $DC00 (port 2), $DC01 (port 1) | 2      | Port 1 conflicts with keyboard matrix    |
| Paddle       | SID A/D + CIA1  | $D419/$D41A + $DC00        | 4 (2/port)  | 512-cycle sampling; jitter               |
| Mouse (1351) | SID pot + CIA1  | $D419/$D41A + $DC00/$DC01  | 1           | Requires driver; keyboard scan interferes|
| Light Pen    | VIC-II + port 1 | $D013/$D014                | 1           | 2-pixel X resolution; CRT only           |

### Hardware Interconnections

All input devices share remarkably few hardware resources:

- **CIA1** ($DC00-$DC0F): Handles the keyboard matrix, joystick digital lines,
  paddle button lines, and the analog multiplexer select bits. This single chip is
  the nexus of almost all input conflicts on the C64.

- **SID** ($D400-$D41C): Contains the two analog-to-digital converters used for
  paddle position and mouse proportional data ($D419/$D41A).

- **VIC-II** ($D000-$D03F): Latches light pen X/Y coordinates into $D013/$D014.

- **Control Ports**: Two DE-9 connectors on the right side of the machine. Each
  carries 5 digital lines (up/down/left/right/fire), 2 analog lines (POTX/POTY),
  and +5V/GND.

### Control Port Pin Assignments

| Pin | Signal     | Joystick       | Paddle         | Mouse (1351)      | Light Pen    |
|-----|-----------|----------------|----------------|-------------------|--------------|
| 1   | Joy0      | Up             | --             | Right button      | --           |
| 2   | Joy1      | Down           | --             | --                | --           |
| 3   | Joy2      | Left           | Paddle X fire  | --                | --           |
| 4   | Joy3      | Right          | Paddle Y fire  | --                | --           |
| 5   | POTY      | (Button 3)     | Paddle Y pos   | Y movement        | --           |
| 6   | Button/LP | Fire           | --             | Left button       | Trigger      |
| 7   | +5V       | --             | --             | --                | --           |
| 8   | GND       | Ground         | Ground         | Ground            | Ground       |
| 9   | POTX      | (Button 2)     | Paddle X pos   | X movement        | --           |


---

## 2. Keyboard

### 2.1 Matrix Architecture

The C64 keyboard is an 8x8 matrix of 64 switch positions, scanned through CIA1's
two I/O ports:

- **$DC00 (Port A)**: Column select (directly active -- directly drives the matrix
  columns). Default DDR = $FF (all outputs).
- **$DC01 (Port B)**: Row read (reads which keys in the selected column are pressed).
  Default DDR = $00 (all inputs).

When Port A drives a column low (bit = 0) and a key in that column is pressed, the
corresponding Port B bit reads as 0. Unpressed keys read as 1 (pulled high
internally).

### 2.2 Complete Keyboard Matrix

The matrix maps each of the C64's 66 keys to a column/row intersection. Two keys
are special: RESTORE is wired directly to the NMI line (not in the matrix), and
SHIFT LOCK is mechanically latched and electrically identical to Left Shift.

**Column = $DC00 bit driven low; Row = $DC01 bit read**

| $DC00 bit | 0 ($FE) | 1 ($FD) | 2 ($FB) | 3 ($F7) | 4 ($EF) | 5 ($DF) | 6 ($BF) | 7 ($7F) |
|-----------|---------|---------|---------|---------|---------|---------|---------|---------|
| **$DC01 bit 0** | DEL     | RETURN  | CRSR LR | F7      | F1      | F3      | F5      | CRSR UD |
| **$DC01 bit 1** | 3       | W       | A       | 4       | Z       | S       | E       | L.SHIFT |
| **$DC01 bit 2** | 5       | R       | D       | 6       | C       | F       | T       | X       |
| **$DC01 bit 3** | 7       | Y       | G       | 8       | B       | H       | U       | V       |
| **$DC01 bit 4** | 9       | I       | J       | 0       | M       | K       | O       | N       |
| **$DC01 bit 5** | +       | P       | L       | -       | .       | :       | @       | ,       |
| **$DC01 bit 6** | POUND   | *       | ;       | CLR/HOME| R.SHIFT | =       | UP-ARROW| /       |
| **$DC01 bit 7** | 1       | LEFT-ARR| CTRL    | 2       | SPACE   | C=      | Q       | RUN/STOP|

**Reading convention**: To scan column N, write a value to $DC00 with bit N cleared
(0) and all other bits set (1). Then read $DC01 -- any bit that reads 0 indicates a
pressed key in that column at the corresponding row.

### 2.3 Column Select Values

| Column | $DC00 value | Binary       |
|--------|------------|--------------|
| 0      | $FE        | %11111110    |
| 1      | $FD        | %11111101    |
| 2      | $FB        | %11111011    |
| 3      | $F7        | %11110111    |
| 4      | $EF        | %11101111    |
| 5      | $DF        | %11011111    |
| 6      | $BF        | %10111111    |
| 7      | $7F        | %01111111    |

### 2.4 Scanning Technique

**Basic single-column scan:**

```asm
    ; Check if SPACE is pressed (column 4, row 7)
    lda #%11101111      ; Select column 4
    sta $DC00
    lda $DC01
    and #%10000000      ; Test row 7 (bit 7)
    beq space_pressed   ; Branch if bit is 0 (pressed)
```

**Full matrix scan (unrolled for speed):**

```asm
    ; Scan all 8 columns, store results in 8 bytes
    lda #%11111110      ; Column 0
    sta $DC00
    lda $DC01
    sta matrix_buf+0

    lda #%11111101      ; Column 1
    sta $DC00
    lda $DC01
    sta matrix_buf+1

    ; ... repeat for columns 2-6 ...

    lda #%01111111      ; Column 7
    sta $DC00
    lda $DC01
    sta matrix_buf+7

    lda #%11111111      ; Deselect all columns when done
    sta $DC00
```

**Important**: Always deselect all columns ($DC00 = $FF) after scanning to avoid
interfering with joystick reads on Port 1 and to prevent unintended electrical paths
through the matrix.

### 2.5 Debouncing

The KERNAL's built-in keyboard scan (SCNKEY at $FF9F) performs basic debouncing by
comparing the current scan against the previous frame's result. For custom routines,
a common approach:

```asm
    ; Compare new scan result against previous
    lda matrix_buf+N     ; Current column N result
    cmp old_matrix+N     ; Previous frame's result
    beq .no_new_press    ; Same = no new key event
    sta old_matrix+N     ; Update previous state
    ; Process new key press/release
.no_new_press:
```

More robust debouncing requires the key to be in the same state for two consecutive
scans (two frames = ~33ms at 60 Hz) before accepting it as a genuine press or
release. This eliminates switch bounce, which typically lasts 5-20ms.

### 2.6 Multiple Key Detection and Ghost Keys

**Ghost key problem**: When three keys are pressed that form three corners of a
rectangle in the matrix, a phantom fourth key appears to be pressed at the fourth
corner. This occurs because current flows backward through the pressed keys, creating
an unintended electrical path.

**Example**: Pressing A (col 2, row 1), S (col 5, row 1), and X (col 2, row 2)
simultaneously causes a ghost press of F (col 5, row 2). The matrix cannot
distinguish this from four genuinely pressed keys.

**Detection strategy**: After detecting a key press, also scan the other columns. If
a second key in the same row is pressed, and either of those keys' columns also shows
activity in another row, the combination is suspect and should be rejected.

```asm
    ; Ghost key detection: if key detected at (col, row),
    ; check if any other column in the same row is also active.
    ; If yes, check if the other column also has activity in
    ; a different row. If both conditions true, likely a ghost.
```

**Hardware solution**: Placing a diode in series with each key switch eliminates
ghosting entirely, but this is not present on stock C64 hardware.

**2-key rollover**: The C64 keyboard reliably supports detecting up to 2 simultaneous
keys without ghosting. 3-key combinations may work if they do not form a rectangle
in the matrix.

### 2.7 Modifier Key Detection

The modifier keys occupy specific matrix positions and can be scanned directly:

| Key        | Column | Row | $DC00 | $DC01 mask |
|------------|--------|-----|-------|------------|
| Left Shift | 7      | 1   | $7F   | $02        |
| Right Shift| 4      | 6   | $EF   | $40        |
| CTRL       | 2      | 7   | $FB   | $80        |
| C= (Commodore) | 5  | 7   | $DF   | $80        |
| RUN/STOP   | 7      | 7   | $7F   | $80        |

**Scanning modifiers separately:**

```asm
    ; Check Left Shift
    lda #$7F            ; Column 7
    sta $DC00
    lda $DC01
    and #$02            ; Row 1
    beq lshift_down

    ; Check CTRL
    lda #$FB            ; Column 2
    sta $DC00
    lda $DC01
    and #$80            ; Row 7
    beq ctrl_down

    ; Check C= key
    lda #$DF            ; Column 5
    sta $DC00
    lda $DC01
    and #$80            ; Row 7
    beq commodore_down
```

**SHIFT LOCK**: Mechanically latches to the same matrix position as Left Shift
(column 7, row 1). The computer cannot distinguish SHIFT LOCK from Left Shift being
held down.

### 2.8 RUN/STOP Key

The RUN/STOP key is at column 7, row 7 of the matrix. The KERNAL scans it specially:

- **$91 (145 decimal)**: Zero-page location where the KERNAL stores the current
  state of column 7 (the result of reading $DC01 when $DC00 = $7F).
- **STOP KERNAL routine ($FFE1)**: Checks location $91 for value $7F (127). If
  RUN/STOP is pressed, bit 7 of $91 will be 0, making $91 = $7F. The routine then
  calls CLRCHN and clears the keyboard buffer.

```asm
    ; Manual STOP key check
    lda #$7F
    sta $DC00
    lda $DC01
    cmp #$7F            ; All other keys in col 7 up, only STOP down?
    ; More robust: just test bit 7
    and #$80
    beq stop_pressed    ; Bit 7 = 0 means STOP is pressed
```

### 2.9 RESTORE Key

The RESTORE key is unique -- it is **not** part of the keyboard matrix. Instead, it
connects directly to the CPU's NMI (Non-Maskable Interrupt) line through a simple
debounce circuit on the motherboard.

- Pressing RESTORE triggers an NMI (falling edge on the /NMI pin).
- The KERNAL NMI handler at $FE43 checks if RUN/STOP is simultaneously held. If so,
  it performs a warm reset (RUN/STOP+RESTORE).
- The NMI software vector at $0318-$0319 (default: $FE47) can be redirected to a
  custom handler.

**Disabling RESTORE/NMI**: Since NMI is edge-triggered (not level-triggered), you
can lock it out by triggering a CIA2 NMI and then leaving CIA2's interrupt output
in the active-low state without acknowledging it. No further falling edges can occur,
so neither CIA2 nor RESTORE can trigger additional NMIs.

### 2.10 KERNAL Keyboard Handling

The KERNAL's default IRQ handler (triggered 60 times per second by CIA1 Timer A)
includes a keyboard scan routine:

| Component                | Address       | Purpose                               |
|--------------------------|--------------|---------------------------------------|
| SCNKEY                   | $FF9F        | Scan matrix, decode to PETSCII        |
| Keyboard buffer          | $0277-$0280  | 10-character FIFO queue               |
| Buffer count             | $C6 (198)    | Number of characters in buffer        |
| Current key              | $CB (203)    | Matrix code of currently pressed key   |
| Previous key             | $C5 (197)    | Matrix code of previous key           |
| Shift flag               | $028D (653)  | Bit 0=Shift, Bit 1=C=, Bit 2=CTRL    |
| Repeat mode              | $028A (650)  | 0=cursor/DEL, $80=all, $40=none       |
| GETIN                    | $FFE4        | Read one character from buffer         |

**Clearing the keyboard buffer:**

```asm
    lda #$00
    sta $C6         ; Set buffer count to 0
```

**Reading via KERNAL:**

```asm
    jsr $FFE4       ; GETIN -- character in A, or 0 if empty
    beq no_key
    ; Process character in A
```


---

## 3. Joysticks

### 3.1 Register Mapping

The C64's two joystick ports connect through CIA1, but with a perhaps surprising
assignment:

| Port          | CIA1 Register | Address | Bits Used  |
|---------------|--------------|---------|------------|
| Control Port 2| Port A (PRA) | $DC00   | Bits 0-4   |
| Control Port 1| Port B (PRB) | $DC01   | Bits 0-4   |

Note the reversal: Port 2 is on Port A ($DC00) and Port 1 is on Port B ($DC01).

### 3.2 Bit Layout

Both ports use the same 5-bit layout in the lower bits of their respective registers.
A bit reads 0 when the corresponding switch is closed (active low logic).

| Bit | Weight | Direction/Action | Bitmask |
|-----|--------|------------------|---------|
| 0   | $01    | Up               | %00000001 |
| 1   | $02    | Down             | %00000010 |
| 2   | $04    | Left             | %00000100 |
| 3   | $08    | Right            | %00001000 |
| 4   | $10    | Fire             | %00010000 |

**Diagonal directions** are represented by two direction bits being active
simultaneously (e.g., Up+Right = bits 0 and 3 both cleared).

### 3.3 Reading a Joystick

```asm
    ; Read joystick in port 2
    lda $DC00
    eor #$FF            ; Invert so 1=active (optional but clearer)
    sta joy2_state

    ; Test individual directions
    lda joy2_state
    and #$01            ; Up?
    bne .moving_up

    lda joy2_state
    and #$02            ; Down?
    bne .moving_down

    lda joy2_state
    and #$10            ; Fire?
    bne .fire_pressed
```

**Without inversion (testing active-low directly):**

```asm
    lda $DC00           ; Read port 2
    lsr                 ; Bit 0 -> Carry
    bcc .up             ; Carry clear = Up pressed
    lsr                 ; Bit 1 -> Carry
    bcc .down
    lsr                 ; Bit 2 -> Carry
    bcc .left
    lsr                 ; Bit 3 -> Carry
    bcc .right
    lsr                 ; Bit 4 -> Carry
    bcc .fire
```

### 3.4 Why Port 2 Is Preferred

Port 1 shares CIA1 Port B ($DC01) with the keyboard matrix row readout. When a
joystick is plugged into Port 1, moving the stick or pressing fire pulls specific
$DC01 bits low. The KERNAL keyboard scan interprets these as key presses:

| Joystick Action | $DC01 Bit | Ghost Key Effect         |
|----------------|-----------|--------------------------|
| Up             | 0         | Affects row 0 of matrix  |
| Down           | 1         | Affects row 1            |
| Left           | 2         | Affects row 2            |
| Right          | 3         | Affects row 3            |
| Fire           | 4         | Affects row 4            |

The result is random characters appearing on screen when using a Port 1 joystick.
Port 2 connects to Port A ($DC00), which drives column selection. A joystick on
Port 2 at worst selects wrong columns during scanning, but since the scan reads
rows (Port B), the interference is minimal and usually invisible.

**Workaround for Port 1 use**: Disable keyboard scanning before reading the joystick:

```asm
    lda #$FF
    sta $DC00           ; Drive all columns high (deselect)
    lda $DC01           ; Now read port 1 without keyboard interference
    and #$1F            ; Mask to joystick bits only
```

Or via the DDR:

```asm
    lda #$00
    sta $DC02           ; Set Port A DDR to all inputs (disable column drive)
    lda $DC01           ; Read port 1 cleanly
    ; ... process joystick ...
    lda #$FF
    sta $DC02           ; Restore Port A DDR for keyboard scanning
```

### 3.5 8-Direction Handling

A standard digital joystick produces 9 possible states (8 directions + centered).
The lower 4 bits of the joystick register encode this:

| State       | Bits 3-0 (RLDU) | Value (inverted) | Angle   |
|-------------|------------------|------------------|---------|
| Centered    | 1111             | $00              | --      |
| Up          | 1110             | $01              | 0       |
| Up+Right    | 0110             | $09              | 45      |
| Right       | 0111             | $08              | 90      |
| Down+Right  | 0101             | $0A              | 135     |
| Down        | 1101             | $02              | 180     |
| Down+Left   | 1001             | $06              | 225     |
| Left        | 1011             | $04              | 270     |
| Up+Left     | 1010             | $05              | 315     |

**Lookup table approach** for converting joystick state to movement deltas:

```asm
    lda $DC00
    and #$0F            ; Isolate direction bits
    eor #$0F            ; Invert (1=active)
    tax
    lda dx_table,x      ; X-axis delta
    clc
    adc player_x
    sta player_x
    lda dy_table,x
    clc
    adc player_y
    sta player_y

dx_table:
    ;     0   U   D  UD   L  UL  DL UDL   R  UR  DR UDR  LR ULR DLR UDLR
    .byte 0,  0,  0,  0, -1, -1, -1,  0, +1, +1, +1,  0,  0,  0,  0,  0
dy_table:
    .byte 0, -1, +1,  0,  0, -1, +1,  0,  0, -1, +1,  0,  0,  0,  0,  0
```

### 3.6 Two-Button and Three-Button Joysticks

Standard joystick ports only have one fire line (pin 6), but additional buttons can
be wired to the POTX (pin 9) and POTY (pin 5) analog lines:

| Button   | Connection     | Detection Register | Detection Method          |
|----------|---------------|-------------------|---------------------------|
| Fire 1   | Pin 6 -> Joy4 | $DC00/$DC01 bit 4 | Standard digital read      |
| Fire 2   | Pin 9 -> POTX | $D419             | Read MSB: $00=pressed     |
| Fire 3   | Pin 5 -> POTY | $D41A             | Read MSB: $00=pressed     |

**How it works**: The extra button connects +5V (pin 7) to the POT line through a
270-330 ohm safety resistor. When unpressed, the POT line floats (reads $FF from the
SID). When pressed, it connects to +5V with minimal resistance (reads $00).

**Reading extra buttons:**

```asm
    ; Read fire 2 (POTX)
    bit $D419           ; Test MSB of Paddle X register
    bmi .fire2_not_pressed  ; MSB=1 means ~$FF (not pressed)
    ; Fire 2 is pressed

    ; Read fire 3 (POTY)
    bit $D41A           ; Test MSB of Paddle Y register
    bmi .fire3_not_pressed
    ; Fire 3 is pressed
```

**Combined joystick register** (building a virtual 7-bit state):

```asm
read_joystick:
    lda $DC00           ; Read port 2 directions + fire1
    and #$1F            ; Bits 0-4: directions + fire
    sta joy_state

    bit $D419           ; POTX (fire 2)
    bmi .no_fire2
    lda joy_state
    ora #$20            ; Set bit 5 for fire 2
    sta joy_state
.no_fire2:
    bit $D41A           ; POTY (fire 3)
    bmi .no_fire3
    lda joy_state
    ora #$40            ; Set bit 6 for fire 3
    sta joy_state
.no_fire3:
    rts
```

**Warning**: Using extra buttons via POT lines conflicts with paddle and mouse input.
A joystick with extra buttons on POT lines cannot be used simultaneously with paddles
or a 1351 mouse on the same port.

**Safety note**: Joysticks modified with POT-line buttons should only be used on C64,
C128, C64GS, or Amiga. Other systems (e.g., Atari, Sega Master System) may be
damaged by voltage on the POT pins.


---

## 4. Paddles

### 4.1 Hardware Description

A paddle controller is a rotary knob (potentiometer) that produces a variable
resistance, connected to one of the analog POT lines on a control port. Each port
supports two paddles (X and Y), for a total of four paddles across both ports.

Each paddle also has a fire button connected to the digital joystick lines:

| Paddle | Port | Analog Line | Fire Button  | Fire Register     |
|--------|------|-------------|-------------- |-------------------|
| 1 (X)  | 1    | Pin 9 POTX  | Pin 3 (Joy2)  | $DC01 bit 2       |
| 2 (Y)  | 1    | Pin 5 POTY  | Pin 4 (Joy3)  | $DC01 bit 3       |
| 3 (X)  | 2    | Pin 9 POTX  | Pin 3 (Joy2)  | $DC00 bit 2       |
| 4 (Y)  | 2    | Pin 5 POTY  | Pin 4 (Joy3)  | $DC00 bit 3       |

### 4.2 SID Pot Registers

The SID chip contains two analog-to-digital converters that measure the charge time
of capacitors connected to the POT lines:

| Register | Address | Description                               |
|----------|---------|-------------------------------------------|
| POTX     | $D419   | A/D converter result for X-axis (0-255)   |
| POTY     | $D41A   | A/D converter result for Y-axis (0-255)   |

**Measurement principle**: The SID periodically discharges the POT line capacitors,
then times how long they take to recharge through the connected resistance. A low
resistance (knob turned one way) produces a low value; high resistance produces a
high value. The range is 0-255.

### 4.3 Analog Multiplexer

The SID has only two A/D inputs, but the C64 has four POT lines (two per port).
A 4066 analog multiplexer switch selects which port's POT lines are routed to the
SID, controlled by CIA1 Port A bits 6 and 7:

| $DC00 Bits 7:6 | Selected Port |
|-----------------|---------------|
| %01xxxxxx       | Control Port 1|
| %10xxxxxx       | Control Port 2|

**Critical interaction**: These are the same bits used during keyboard column
scanning! Every time the KERNAL scans a keyboard column, bits 6-7 of $DC00 change,
briefly switching the multiplexer. This is why paddle reads must disable keyboard
scanning first.

### 4.4 Reading Paddles

The correct procedure for reading paddles:

1. **Disable keyboard scanning** (prevent the IRQ handler from changing $DC00).
2. **Select the desired port** by setting bits 7:6 of $DC00.
3. **Wait at least 512 machine cycles** for the SID to complete a fresh measurement.
4. **Read $D419 and $D41A**.
5. **Re-enable keyboard scanning**.

```asm
read_paddles_port1:
    sei                     ; Disable interrupts (prevent KERNAL scan)

    lda $DC00
    and #$3F                ; Clear bits 7:6
    ora #$40                ; Set bit 6 = select port 1
    sta $DC00

    ; Wait 512+ cycles for SID to complete measurement
    ; A simple delay loop:
    ldx #$80                ; 128 iterations
.wait:
    nop                     ; 2 cycles
    dex                     ; 2 cycles
    bne .wait               ; 3 cycles (taken) = ~7 cycles * 128 = 896 cycles

    lda $D419               ; Read paddle X position (0-255)
    sta paddle_x
    lda $D41A               ; Read paddle Y position (0-255)
    sta paddle_y

    ; Read paddle fire buttons
    lda $DC01               ; Port 1 buttons
    and #$04                ; Bit 2 = paddle X fire
    sta paddle_x_fire
    lda $DC01
    and #$08                ; Bit 3 = paddle Y fire
    sta paddle_y_fire

    cli                     ; Re-enable interrupts
    rts
```

### 4.5 Timing Details

The SID samples the POT lines every **512 phi2 clock cycles**, which equals:

| System | Clock     | Sample Rate    | Sample Period |
|--------|-----------|----------------|---------------|
| PAL    | 985248 Hz | ~1924 Hz       | ~519.7 us     |
| NTSC   | 1022727 Hz| ~1997 Hz       | ~500.7 us     |

This sampling is completely asynchronous to the CPU -- it happens continuously
regardless of what the processor is doing. The $D419/$D41A registers simply hold the
most recent measurement result.

### 4.6 Jitter and Filtering

Paddle readings exhibit noticeable jitter (values fluctuating by several counts even
when the knob is stationary). Sources include:

- Electrical noise on the analog lines
- Aging potentiometers with dirty/worn tracks
- Asynchronous sampling relative to the CPU read

**Software filtering techniques:**

**Moving average:**
```asm
    ; Simple 4-sample average (shift right by 2)
    clc
    lda sample_0
    adc sample_1
    sta temp_lo
    lda #$00
    adc #$00
    sta temp_hi

    clc
    lda temp_lo
    adc sample_2
    sta temp_lo
    lda temp_hi
    adc #$00
    sta temp_hi

    clc
    lda temp_lo
    adc sample_3
    sta temp_lo
    lda temp_hi
    adc #$00
    sta temp_hi

    lsr temp_hi
    ror temp_lo
    lsr temp_hi
    ror temp_lo     ; temp_lo = average
```

**Exponential weighted average (IIR filter):**
```asm
    ; smoothed = smoothed + (raw - smoothed) / 4
    lda raw_value
    sec
    sbc smoothed
    cmp #$80
    bcc .positive
    ; Negative: arithmetic shift right twice
    sec
    ror
    sec
    ror
    jmp .apply
.positive:
    lsr
    lsr
.apply:
    clc
    adc smoothed
    sta smoothed
```

**Median filter** (best for eliminating outlier spikes): Take 3 or 5 readings, sort
them, and use the middle value. More expensive computationally but excellent at
rejecting single-sample noise spikes.


---

## 5. Mouse (Commodore 1351)

### 5.1 Overview

The Commodore 1351 is a two-button mouse that communicates position data through the
SID pot registers. Unlike paddles (which send absolute position), the 1351 sends
relative movement encoded as a 6-bit counter modulo 64.

**Modes:**
- **Proportional mode** (default): True mouse operation via SID pot lines. Requires
  a software driver.
- **Joystick emulation mode**: Hold right button during power-on. Emulates the
  earlier 1350 mouse (digital joystick-style, no driver needed).

### 5.2 How Proportional Mode Works

The 1351 contains a MOS 5717 controller IC with a quadrature encoder for each axis.
Each axis maintains a 6-bit counter (0-63) that increments or decrements with
movement. Every 512 microseconds, the mouse outputs this counter value as an analog
resistance on the POTX/POTY lines, which the SID's A/D converter reads.

**Bit layout in SID pot registers:**

```
Bit:    7   6   5   4   3   2   1   0
        X   P5  P4  P3  P2  P1  P0  N

P5-P0 = 6-bit position counter (modulo 64)
X     = Noise/unused (unreliable)
N     = Noise/unused (unreliable)
```

The position bits are middle-aligned (bits 6-1) to minimize the impact of noise on
the least and most significant bits.

### 5.3 Button Connections

| Button | Connection      | Register        | Bit  |
|--------|----------------|-----------------|------|
| Left   | Pin 6 (Fire)   | $DC00 bit 4 (port 2) or $DC01 bit 4 (port 1) | 4 |
| Right  | Pin 1 (Joy Up) | $DC00 bit 0 (port 2) or $DC01 bit 0 (port 1) | 0 |

**Important**: When reading buttons, temporarily set CIA1 DDR to inputs to avoid
keyboard matrix crosstalk:

```asm
    lda #$00
    sta $DC03           ; Port B DDR = all inputs
    sta $DC02           ; Port A DDR = all inputs
    lda $DC01           ; Read buttons (port 1)
    eor #$FF            ; Invert (1=pressed)
    sta mouse_buttons
    ; Restore DDRs after reading
    lda #$FF
    sta $DC02           ; Port A back to outputs
    lda #$00
    sta $DC03           ; Port B stays inputs
```

### 5.4 Delta Calculation

The driver must compute how much the mouse has moved since the last read. Since the
counter wraps around at 64, the difference is computed modulo 64, then interpreted
as a signed value (-32 to +31):

```asm
; MOVCHK -- Calculate movement delta
; Input:  A = new pot value, Y = old pot value
; Output: A (lo) / X (hi) = 16-bit signed delta
;
movchk:
    sty .old_val+1      ; Self-modifying: store old value
    sec
.old_val:
    sbc #$00             ; A = new - old (self-modified)
    and #%01111110       ; Mask to bits 6-1 (the 6-bit counter)
    cmp #%01000000       ; Test sign (bit 6)
    bcc .positive        ; < $40 = positive movement

    ; Negative movement
    ora #%11000000       ; Sign-extend to 8 bits
    ldx #$FF             ; High byte = -1
    rts

.positive:
    ldx #$00             ; High byte = 0
    rts
```

**With acceleration:**

```asm
    ; After computing delta in A:
    ; If |delta| > threshold, double the movement
    pha
    and #$7F             ; Absolute value
    cmp #10              ; Cutoff threshold
    pla
    bcc .no_accel        ; Below threshold, no acceleration

    asl                  ; Double the delta
.no_accel:
```

### 5.5 Complete Mouse Driver Skeleton

```asm
; Called from IRQ handler, BEFORE the KERNAL keyboard scan
; Mouse in control port 1
;
mouse_irq:
    ; --- Select port 1 for POT reading ---
    lda $DC00
    and #$3F
    ora #$40             ; Bit 6 set, bit 7 clear = port 1
    sta $DC00

    ; --- Read pot registers ---
    lda $D419            ; Current POTX
    ldy old_potx         ; Previous POTX
    jsr movchk           ; Returns delta in A/X (16-bit signed)
    sty old_potx         ; movchk stored new value via self-mod
    ; Add delta to X coordinate (16-bit)
    clc
    adc mouse_x          ; Low byte
    sta mouse_x
    txa
    adc mouse_x+1        ; High byte
    sta mouse_x+1

    lda $D41A            ; Current POTY
    ldy old_poty
    jsr movchk
    sty old_poty
    clc
    adc mouse_y
    sta mouse_y
    txa
    adc mouse_y+1
    sta mouse_y+1

    ; --- Clamp coordinates to screen bounds ---
    ; (check mouse_x against 0..319, mouse_y against 0..199)
    jsr clamp_coords

    ; --- Read buttons ---
    lda #$00
    sta $DC03            ; DDR B = inputs
    sta $DC02            ; DDR A = inputs
    lda $DC01            ; Port 1 buttons
    eor #$FF
    sta mouse_buttons
    lda #$FF
    sta $DC02            ; Restore DDR A

    rts
```

### 5.6 Keyboard Scan Interference

The KERNAL keyboard scan manipulates $DC00 to select matrix columns, which also
switches the 4066 analog multiplexer. During the scan, the POT lines briefly connect
to the wrong port, corrupting the SID's measurement cycle.

**Solution**: Install the mouse driver as part of the IRQ handler, executing it
**before** the KERNAL keyboard scan runs. The pot lines must be connected to the
correct port for at least 512 cycles (~0.5ms on PAL) before reading the registers.
Since the standard IRQ fires every ~16,422 cycles (PAL), there is ample time.

**Best practice**: Place the mouse read at the very beginning of the IRQ, then allow
the KERNAL scan to execute afterward. This ensures the POT lines have been stable
since the end of the previous frame's keyboard scan.


---

## 6. Light Pen

### 6.1 Mechanism

A light pen contains a photodiode that detects the electron beam of a CRT as it
sweeps past the pen's tip. When light is detected, the pen generates a signal on
the control port's fire line (pin 6 of Control Port 1 only). The VIC-II chip
latches its current beam position into dedicated registers.

**Requirements:**
- Must use **Control Port 1** only (the LP input is hardwired to port 1's fire line).
- Requires a **CRT display** -- does not work with LCD or other flat panel monitors.
- The display must be showing sufficiently bright pixels at the aim point.

### 6.2 VIC-II Light Pen Registers

| Register | Address | Description                                        |
|----------|---------|---------------------------------------------------|
| LPX      | $D013   | Latched X coordinate / 2 (8 bits, read-only)      |
| LPY      | $D014   | Latched Y coordinate (8 bits, read-only)           |

**X coordinate**: The value in $D013 represents half the actual X position. To get
the true X coordinate, shift left (ASL or multiply by 2). The coordinate system
matches sprite coordinates -- the first visible text column pixel is at X=24 (PAL).

**Y coordinate**: $D014 gives the raster line directly, again in the sprite
coordinate system. The first visible text row begins at Y=50 (PAL).

### 6.3 Latching Behavior

- The VIC-II latches coordinates on a **negative (falling) edge** of the LP input.
- The latch triggers **only once per frame**. Subsequent triggers within the same
  frame are ignored. This means you get at most one coordinate reading per frame
  (~50 Hz PAL / ~60 Hz NTSC).
- The latch is reset at the start of each new frame (when the raster counter wraps).

### 6.4 Interrupt Support

The VIC-II can generate an IRQ when a light pen event occurs:

| Register | Address | Bit | Name  | Purpose                    |
|----------|---------|-----|-------|----------------------------|
| $D019    | IRQ flags    | 3   | ILPIRQ| Light pen IRQ occurred     |
| $D01A    | IRQ enable   | 3   | MLPI  | Enable light pen interrupt |

```asm
    ; Enable light pen interrupt
    lda $D01A
    ora #$08            ; Set bit 3
    sta $D01A

    ; In IRQ handler, check for light pen event:
    lda $D019
    and #$08
    beq .not_lightpen
    ; Read coordinates
    lda $D013
    asl                 ; X = LPX * 2
    sta lp_x
    lda $D014
    sta lp_y
    ; Acknowledge interrupt
    lda #$08
    sta $D019           ; Write 1 to bit 3 to clear
.not_lightpen:
```

### 6.5 Accuracy Limitations

The light pen on the C64 suffers from several accuracy problems:

**X resolution**: Only 2-pixel resolution due to the division by 2 in $D013. In
practice, electrical noise and timing jitter reduce effective accuracy to 8-16
pixels horizontally.

**Jitter**: Even with the pen held perfectly still, the reported X coordinate
fluctuates by several pixels due to noise in the cables, non-ideal analog signal
characteristics, and the imprecise nature of detecting the beam edge.

**Dark area problem**: The pen requires sufficient brightness to trigger. On dark
graphics, the aim point "slips off" dark regions, making it impossible to point
accurately at dark-colored screen elements.

**Practical mitigation**: Read coordinates multiple times per frame (before and after
the main latch) and average or median-filter the results. Some software uses
crosshair cursors that are always bright, giving the pen a reliable target.

### 6.6 Using the Light Pen Programmatically

Since the light pen fires once per frame and has limited precision, it is best
suited for coarse pointing (like aiming a crosshair) rather than precise drawing:

```asm
; Read light pen with simple averaging (called once per frame)
read_lightpen:
    lda $D013
    asl                 ; X * 2
    sta lp_raw_x

    lda $D014
    sta lp_raw_y

    ; Simple exponential smoothing
    lda lp_raw_x
    sec
    sbc lp_smooth_x
    ; Divide difference by 4
    cmp #$80
    bcc .pos_x
    sec
    ror
    sec
    ror
    jmp .apply_x
.pos_x:
    lsr
    lsr
.apply_x:
    clc
    adc lp_smooth_x
    sta lp_smooth_x

    ; Repeat for Y axis...
    rts
```


---

## 7. Advanced Techniques

### 7.1 Combining Keyboard and Joystick

Many games need to read both keyboard and joystick. The challenge is that both share
CIA1. The proper sequence:

```asm
game_input:
    ; 1. Read joystick port 2 FIRST (it's on Port A, minimal conflict)
    lda $DC00
    and #$1F
    eor #$1F            ; Invert to 1=active
    sta joy2

    ; 2. Disconnect keyboard matrix before reading port 1
    lda #$FF
    sta $DC00           ; All columns high (no key scanning interference)
    lda $DC01
    and #$1F
    eor #$1F
    sta joy1

    ; 3. Now scan keyboard columns
    lda #$00
    sta $DC00           ; All columns low = quick "any key?" check
    lda $DC01
    cmp #$FF
    beq .no_keys        ; No keys pressed, skip full scan

    ; Full column-by-column scan
    ldx #$07
.scan_loop:
    lda col_select,x
    sta $DC00
    lda $DC01
    sta key_matrix,x
    dex
    bpl .scan_loop

.no_keys:
    lda #$FF
    sta $DC00           ; Clean up
    rts

col_select:
    .byte $FE,$FD,$FB,$F7,$EF,$DF,$BF,$7F
```

### 7.2 Low-Latency Input

For the most responsive input possible:

**Read input as late as possible** in the frame, ideally just before the game logic
uses it. This minimizes the gap between the physical input event and the software
response.

**Avoid the KERNAL scan entirely** in performance-critical games. The KERNAL scan
runs during the IRQ handler and buffers results through multiple layers of
indirection. Direct hardware reads save time and reduce latency.

**Poll during vertical blank**: Reading input during the vertical blanking interval
ensures consistent timing and avoids VIC-II bus contention (badlines), which can
steal cycles from the CPU during the visible area.

### 7.3 Input Buffering

**Keyboard buffering**: The KERNAL maintains a 10-character FIFO at $0277-$0280,
which is fine for text input but inappropriate for games. Games should read the
matrix directly for instantaneous state.

**Joystick edge detection** (detecting press/release events rather than held state):

```asm
    lda $DC00
    and #$1F
    eor #$1F            ; Now 1=active
    sta joy_current

    ; Detect newly pressed buttons (were 0 last frame, are 1 now)
    lda joy_current
    eor joy_previous    ; Changed bits
    and joy_current     ; Only newly pressed
    sta joy_pressed

    ; Detect newly released buttons
    lda joy_current
    eor joy_previous
    and joy_previous    ; Only newly released
    sta joy_released

    lda joy_current
    sta joy_previous    ; Save for next frame
```

This pattern is essential for menu navigation, weapon switching, or any action that
should happen once per press rather than repeating while held.

### 7.4 8-Direction Handling with Speed Normalization

Diagonal movement on a digital joystick covers sqrt(2) times the distance of
cardinal movement. To normalize speed:

```asm
    ; Use different speed for diagonal vs cardinal
    lda joy_current
    and #$0F            ; Direction bits only
    tax
    lda speed_table,x   ; Pixels to move this frame
    sta move_speed

speed_table:
    ;     0    U    D   UD    L   UL   DL  UDL    R   UR   DR  UDR   LR  ULR  DLR UDLR
    .byte 0,   2,   2,   0,   2,   1,   1,   0,   2,   1,   1,   0,   0,   0,   0,   0
    ; Cardinal = 2 pixels, Diagonal = 1 pixel (approximate sqrt(2) ratio)
```

For smoother results, use subpixel positioning (16-bit coordinates with the high
byte as the screen pixel):

```asm
    ; Diagonal speed = cardinal * 181/256 (approximation of 1/sqrt(2))
    lda #181            ; $B5
    ; Used as fractional step for diagonal movement
```

### 7.5 Multiplayer Input

**Two players**: Use both joystick ports. Accept the Port 1 keyboard conflict or
mitigate it as described in section 3.4.

**Four players** (via user port adapter): The Protovision/Classical Games 4-Player
Adapter connects two additional joystick ports through CIA2's user port ($DD01).
Reading the extra joysticks:

```asm
; Read joysticks 3 and 4 via Protovision adapter on user port
; CIA2 Port B ($DD01), DDR ($DD03)

read_joy3_joy4:
    lda #$80
    sta $DD03           ; Bit 7 = output, bits 6-0 = inputs

    ; Read joystick 3
    lda #$00            ; PB7 = 0, select joy 3
    sta $DD01
    lda $DD01
    and #$1F            ; Bits 0-4 = joy 3 directions + fire
    eor #$1F
    sta joy3

    ; Read joystick 4
    lda #$80            ; PB7 = 1, select joy 4
    sta $DD01
    lda $DD01
    and #$1F            ; Bits 0-4 = joy 4 directions + fire
    eor #$1F
    sta joy4

    rts
```

**Keyboard + joystick multiplayer**: One player on joystick, another on keyboard
keys. Must scan both without conflicts (see section 7.1).


---

## 8. Hardcore Details

### 8.1 CIA Timing for Keyboard Scanning

**IRQ frequency**: CIA1 Timer A is configured by the KERNAL to fire at 60 Hz on both
PAL and NTSC systems:

| System | CPU Clock    | Timer Value | Cycles/IRQ | Actual Rate |
|--------|-------------|-------------|------------|-------------|
| PAL    | 985,248 Hz  | $4025       | 16,421     | ~60.0 Hz    |
| NTSC   | 1,022,727 Hz| $4295       | 17,045     | ~60.0 Hz    |

**Register access timing**: Writing to $DC00 and reading $DC01 each takes the
standard memory access time (1 cycle for the 6510). However, there is no propagation
delay concern for the keyboard matrix itself -- the passive switches and pull-up
resistors settle effectively instantly at the CIA's clock rate.

**CIA startup quirk**: The 6526 has a 2-cycle delay after writing the timer control
register before the timer actually starts counting. The 6526A (later revision) starts
one cycle earlier. This difference can affect cycle-exact code but is irrelevant for
keyboard scanning at normal rates.

**Column settling**: After writing a new column select value to $DC00, the matrix
needs time for the electrical signals to propagate and settle. In practice at 1 MHz,
reading $DC01 in the very next instruction (2-3 cycles later minimum due to the write
instruction's execution time) is sufficient. The unrolled scan code in section 2.4
naturally provides adequate settling time.

### 8.2 Keyboard Ghosting Analysis

Ghost keys arise from the passive matrix topology. Consider the circuit:

```
          Column A        Column B
            |                |
Row 1  ----[K1]----+---[K2]----
            |       |        |
Row 2  ----[K3]----+---[K4]----
            |                |
```

If K1, K2, and K3 are all pressed simultaneously:
- Scanning Column A: Row 1 and Row 2 both read as pressed (correct: K1, K3).
- Scanning Column B: Row 1 reads as pressed (correct: K2). BUT current also flows
  backward: Column B -> K2 -> Row 1 -> K1 -> Column A -> K3 -> Row 2. So Row 2
  also appears pressed, creating a phantom K4.

**Ghost-free key groups for games**: Choose keys that do not share both a column and
a row with any other used key. For two keys that need simultaneous pressing, ensure
they are in different columns AND different rows. Common game layouts are designed
with this constraint in mind.

**Detection algorithm:**

```asm
; After scanning the full matrix into key_matrix[0..7]:
; For each detected key, verify it's not a ghost
;
; A key at (col, row) is genuine if:
;   - No other column has a pressed key in the same row, OR
;   - The other column with a same-row key does not share any
;     other active row with our column
;
check_ghost:
    ldx #$07            ; For each column
.col_loop:
    lda key_matrix,x
    eor #$FF            ; Invert: 1 = pressed
    beq .next_col       ; No keys in this column
    sta temp_col

    ; Check every other column for same-row overlap
    ldy #$07
.other_col:
    cpy x_reg           ; Skip same column
    beq .skip
    lda key_matrix,y
    eor #$FF
    and temp_col        ; Same row pressed in both columns?
    beq .skip           ; No overlap

    ; Overlap found -- check if these columns share 2+ rows
    ; (which would indicate potential ghosting)
    ; Count bits: if >= 2, mark as suspicious
    ; ...
.skip:
    dey
    bpl .other_col
.next_col:
    dex
    bpl .col_loop
```

### 8.3 SID Paddle Update Timing (512-Cycle Window)

The SID's A/D conversion process:

1. **Discharge phase**: The SID pulls the POT line capacitor to ground, discharging it
   completely.
2. **Charge phase**: The SID releases the line. The capacitor charges through the
   connected resistance (paddle pot or mouse output).
3. **Measurement**: An internal counter increments each cycle during charging. When
   the voltage crosses the threshold, the counter value is latched into $D419/$D41A.
4. **Cycle repeats**: The entire process takes exactly 512 phi2 cycles, then restarts.

**Timing diagram (512 cycles):**

```
Cycle 0     : Discharge begins
Cycle ~16   : Charge phase begins (capacitor starts charging)
Cycle 16-511: Counter runs; latches when threshold crossed
Cycle 512   : New measurement begins; registers updated with result
```

**Asynchronous nature**: The 512-cycle measurement runs on a free-running internal
counter that is not synchronized to the CPU's instruction stream. A CPU read of
$D419/$D41A always returns the result of the most recently completed measurement.
The programmer cannot predict or synchronize to the exact moment a new value becomes
available.

**Multiplexer switching penalty**: After switching the 4066 multiplexer (by changing
$DC00 bits 7:6), the SID must complete at least one full 512-cycle measurement of
the new port before the registers contain valid data. In the worst case, you switch
just after a measurement began, requiring nearly 1024 cycles of waiting (the current
measurement completes with stale data, then a fresh one runs).

### 8.4 Mouse Movement Calculation -- Full Analysis

The 1351 mouse's MOS 5717 controller maintains a 6-bit bidirectional counter per
axis. The counter value appears in bits 6-1 of the SID pot register, middle-aligned
to reduce noise impact:

```
SID register:  [X] [P5] [P4] [P3] [P2] [P1] [P0] [N]
                ^                                    ^
              noise                                noise
              (ignore)                           (ignore)
```

**Delta extraction algorithm:**

```asm
; Input:  A = new pot register value
;         Y = old pot register value (from previous read)
; Output: A = signed 8-bit delta, X = sign extension ($00 or $FF)

movchk:
    sta new_pot
    tya
    sec
    sbc new_pot         ; old - new (we'll negate conceptually)
    ; Actually: new - old by subtracting in the other direction
    ; Let's redo: A = new, Y = old
    lda new_pot
    sty temp
    sec
    sbc temp            ; A = new - old
    and #%01111110      ; Mask to 6-bit counter in bits 6-1

    ; Now A contains the unsigned difference modulo 64 (in bits 6-1)
    ; If A >= $40 (bit 6 set), movement was negative
    cmp #%01000000
    bcs .negative

.positive:
    ; A is 0..$3E in steps of 2 (bits 6-1)
    lsr                 ; Shift right to get actual count (0..31)
    ldx #$00            ; Positive: high byte = 0
    rts

.negative:
    ; A is $40..$7E, representing -32..-1
    ora #%10000000      ; Set bit 7 for sign extension
    sec
    ror                 ; Arithmetic shift right (preserves sign)
    ldx #$FF            ; Negative: high byte = -1
    rts
```

**Wraparound handling**: Because the counter is modulo 64, if the mouse moves more
than 31 units between reads, the direction will be misinterpreted (a large positive
movement wraps to appear negative, and vice versa). At the standard 60 Hz polling
rate and typical mouse speeds, this limit is rarely reached. However, very fast mouse
movements or dropped frames can cause glitches.

**Maximum reliable speed**: 31 units per frame at 60 Hz = 1,860 units per second.
With a standard 1351 encoder resolution, this corresponds to rapid but not extreme
mouse movement.

### 8.5 Light Pen Latching Internals

The VIC-II latches the light pen coordinates as follows:

1. The LP input pin is active-low. A falling edge triggers the latch.
2. The VIC-II's internal X position counter is divided by 2 and stored in $D013.
3. The current raster line is stored in $D014.
4. A flag is set internally, preventing any further latching until the next frame
   (after the raster counter resets past line 0).
5. If light pen interrupts are enabled (bit 3 of $D01A), the VIC-II asserts IRQ.

**Coordinate system relationship to sprites:**

| Parameter             | Value (PAL)  | Value (NTSC) |
|-----------------------|-------------|--------------|
| First visible X pixel | 24          | 24           |
| Last visible X pixel  | 343         | 343          |
| $D013 range (visible) | 12-171      | 12-171       |
| First visible Y line  | 50          | 50           |
| Last visible Y line   | 249         | 249          |

**Undocumented behavior**: The LP input on the VIC-II is active on **any** falling
edge of pin 6, not just from actual light pens. This means software can trigger a
light pen latch by briefly pulling the fire line low (e.g., via a joystick fire
button in port 1). Some programs exploit this for timing measurements by connecting
the IRQ output to the LP input to determine the VIC-II's internal X counter position
at known times.

### 8.6 CIA Port Direction Register Interactions

The CIA DDR (Data Direction Register) bits determine whether each port pin is an
input (0) or output (1). This has critical implications for reading input devices:

**$DC02 (Port A DDR)** -- Default $FF (all outputs):
- Must be $FF for keyboard column selection to work.
- Joystick port 2 directions (bits 0-4) are readable even when configured as outputs
  because the CIA allows reading the pin state regardless of DDR setting. However,
  output drivers can overpower weak joystick switches, so some routines briefly
  change DDR to $00 for clean joystick reads.

**$DC03 (Port B DDR)** -- Default $00 (all inputs):
- Required for keyboard row reading and joystick port 1 reading.
- Must remain $00 during normal operation.
- When reading mouse buttons, temporarily setting both DDRs to $00 prevents
  keyboard column signals from interfering with the button state.

**Timing consideration**: After changing a DDR, the pin state is available
immediately on the next read. There is no settling delay beyond the CIA's standard
access time.


---

## 9. References

### Official Documentation
- Commodore 64 Programmer's Reference Guide, Commodore Business Machines, 1982
- MOS 6526 Complex Interface Adapter (CIA) Datasheet
- MOS 6581/8580 SID Datasheet
- MOS 6567/6569 VIC-II Datasheet
- Commodore 1351 Mouse User's Manual

### Online References
- [C64 Keyboard Matrix Layout (sta.c64.org)](https://sta.c64.org/cbm64kbdlay.html)
- [How the C64 Keyboard Works (C64 OS)](https://c64os.com/post/howthekeyboardworks)
- [Reading the C64 Keyboard Matrix (Elite on the 6502)](https://elite.bbcelite.com/deep_dives/reading_the_commodore_64_keyboard_matrix.html)
- [Reading the Keyboard (Codebase64)](https://codebase.c64.org/doku.php?id=base:reading_the_keyboard)
- [Scanning the Keyboard the Correct and Non-KERNAL Way (Codebase64)](https://codebase.c64.org/doku.php?id=base:scanning_the_keyboard_the_correct_and_non_kernal_way)
- [Keyboard (C64-Wiki)](https://www.c64-wiki.com/wiki/Keyboard)
- [Joystick (C64-Wiki)](https://www.c64-wiki.com/wiki/Joystick)
- [Control Port (C64-Wiki)](https://www.c64-wiki.com/wiki/Control_Port)
- [How to Program Joystick Input Handling on a C64 (Retro-Programming)](https://retro-programming.com/how-to-program-joystick-input-handling-on-a-c64/)
- [Reading C64 Joysticks and Analog Signals (Retro Game Coders)](https://retrogamecoders.com/c64-joysticks-analog-sensors/)
- [DE-9 Joystick Standard (Individual Computers Wiki)](https://wiki.icomp.de/wiki/DE-9_Joystick)
- [Paddle (C64-Wiki)](https://www.c64-wiki.com/wiki/Paddle)
- [Mouse 1351 (C64-Wiki)](https://www.c64-wiki.com/wiki/Mouse_1351)
- [1351 Mouse and Mouse Driver (C64 OS)](https://c64os.com/post/1351mousedriver)
- [cc65 C64 1351 Mouse Driver Source (GitHub)](https://github.com/cc65/cc65/blob/master/libsrc/c64/mou/c64-1351.s)
- [Light Pen (C64-Wiki)](https://www.c64-wiki.com/wiki/Light_pen)
- [VIC-II Technical Reference (zimmers.net)](https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt)
- [CIA (C64-Wiki)](https://www.c64-wiki.com/wiki/CIA)
- [CIAs -- Timers, Keyboard and More (emudev.de)](https://emudev.de/q00-c64/cias-timers-keyboard-and-more/)
- [STOP KERNAL Routine (C64-Wiki)](https://www.c64-wiki.com/wiki/STOP_(Kernal))
- [RESTORE Key (C64-Wiki)](https://www.c64-wiki.com/wiki/RESTORE_(Key))
- [Multiplayer Interface (C64-Wiki)](https://www.c64-wiki.com/wiki/Multiplayer_Interface)
- [4 Player Adapter (Individual Computers Wiki)](https://wiki.icomp.de/wiki/4_Player_Adapter)
- [Protovision 4 Player Interface](https://www.protovision.games/hardw/4_player.php?language=en)
- [KERNAL Functions Reference (sta.c64.org)](https://sta.c64.org/cbm64krnfunc.html)
- [Commodore 64 Keyboard Matrix Codes (sta.c64.org)](https://sta.c64.org/cbm64kbdcode1.html)
- [SID (C64-Wiki)](https://www.c64-wiki.com/wiki/SID)
