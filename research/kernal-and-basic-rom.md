# C64 KERNAL and BASIC ROM Routines

## 1. Overview

The Commodore 64 contains two ROMs that provide the system software layer between hardware and user programs:

- **KERNAL ROM** ($E000-$FFFF, 8 KB) -- The operating system kernel handling I/O, file management, memory management, interrupts, the screen editor, keyboard scanning, serial bus communication, tape operations, and system initialization.
- **BASIC ROM** ($A000-$BFFF, 8 KB) -- The BASIC V2 interpreter including tokenizer, expression evaluator, statement executor, floating-point math library, string handling, and variable/array management.

Note the deliberate misspelling "KERNAL" -- this was an accidental typo by Commodore engineer Robert Russell that became the official name.

### When to Use ROM Routines

**Use the KERNAL jump table when:**
- You need portable code that works across different KERNAL revisions
- You need I/O services (screen, keyboard, disk, serial bus, tape)
- You want the system to handle device abstraction
- You need file load/save operations

**Bypass the ROMs when:**
- You need maximum speed (direct hardware access is faster)
- You need all 64 KB of RAM (bank out ROMs via processor port $01)
- You are writing a demo or game with custom IRQ handlers
- You need the memory space occupied by the ROMs
- You are implementing a custom fastloader

### Memory Banking

The processor port at $01 controls ROM/RAM visibility:

| Bit | Name   | Function                                    |
|-----|--------|---------------------------------------------|
| 0   | LORAM  | 1 = BASIC ROM visible at $A000-$BFFF        |
| 1   | HIRAM  | 1 = KERNAL ROM visible at $E000-$FFFF       |
| 2   | CHAREN | 1 = I/O visible at $D000-$DFFF; 0 = Char ROM |

Common configurations of $01 (bits 2-0):

| Value | $A000-$BFFF | $D000-$DFFF | $E000-$FFFF | Notes                       |
|-------|-------------|-------------|-------------|-----------------------------|
| %111 ($37) | BASIC ROM | I/O | KERNAL ROM | Default after power-on       |
| %110 ($36) | RAM       | I/O | KERNAL ROM | BASIC banked out, KERNAL stays |
| %101 ($35) | RAM       | I/O | RAM        | All RAM + I/O                |
| %100 ($34) | RAM       | RAM | RAM        | Full 64 KB RAM               |
| %011 ($33) | BASIC ROM | Char ROM | KERNAL ROM | Character ROM visible       |
| %010 ($32) | RAM       | Char ROM | KERNAL ROM | Char ROM, no BASIC          |

**Important:** If KERNAL is banked out (HIRAM=0), BASIC is also effectively unavailable since BASIC calls KERNAL routines. The default power-on value is $37.


---

## 2. KERNAL Jump Table

The KERNAL provides 39 documented entry points via a jump table at $FF81-$FFF3. Each entry is a 3-byte JMP instruction. Using these addresses (rather than internal ROM addresses) ensures compatibility across KERNAL revisions.

### 2.1 System Initialization Routines

| Address | Name    | Real Addr | Description                                           | Input        | Output   | Clobbers  |
|---------|---------|-----------|-------------------------------------------------------|--------------|----------|-----------|
| $FF81   | CINT    | $FF5B     | Initialize screen editor and VIC-II chip; clear screen; set default I/O to keyboard/screen | -- | -- | A, X, Y |
| $FF84   | IOINIT  | $FDA3     | Initialize CIA chips, SID volume; set up memory configuration; start IRQ timer | -- | -- | A, X |
| $FF87   | RAMTAS  | $FD50     | Clear $0002-$0101 and $0200-$03FF; perform RAM test; set top/bottom of memory pointers | -- | -- | A, X, Y |
| $FF8A   | RESTOR  | $FD15     | Restore default I/O vectors ($0314-$0333) from ROM table | -- | -- | A, Y |
| $FF8D   | VECTOR  | $FD1A     | Read or set I/O vectors ($0314-$0333)                 | C=0: copy from table at X/Y to vectors; C=1: copy vectors to table at X/Y | -- | A, Y |

### 2.2 Memory Management

| Address | Name    | Real Addr | Description                                           | Input        | Output   | Clobbers  |
|---------|---------|-----------|-------------------------------------------------------|--------------|----------|-----------|
| $FF99   | MEMTOP  | $FE25     | Read or set top of user RAM                           | C=0: set from X(lo)/Y(hi); C=1: read | X(lo)/Y(hi) | X, Y |
| $FF9C   | MEMBOT  | $FE34     | Read or set bottom of user RAM                        | C=0: set from X(lo)/Y(hi); C=1: read | X(lo)/Y(hi) | X, Y |
| $FFF3   | IOBASE  | $E500     | Return base address of CIA #1 in X/Y                  | --           | X=$00, Y=$DC | X, Y |

### 2.3 Screen I/O

| Address | Name    | Real Addr | Description                                           | Input        | Output   | Clobbers  |
|---------|---------|-----------|-------------------------------------------------------|--------------|----------|-----------|
| $FFED   | SCREEN  | $E505     | Return number of screen columns (X) and rows (Y)      | --           | X=40, Y=25 | X, Y |
| $FFF0   | PLOT    | $E50A     | Read or set cursor position                           | C=0: set row=X, col=Y; C=1: read | X=row, Y=col | X, Y |
| $FF90   | SETMSG  | $FE18     | Control KERNAL message output                         | A: bit 7=error msgs, bit 6=control msgs | -- | -- |

### 2.4 Keyboard

| Address | Name    | Real Addr | Description                                           | Input        | Output   | Clobbers  |
|---------|---------|-----------|-------------------------------------------------------|--------------|----------|-----------|
| $FF9F   | SCNKEY  | $EA87     | Scan keyboard matrix; decode key and place in buffer   | --           | --       | A, X, Y   |
| $FFE1   | STOP    | $F6ED     | Check if STOP key was pressed (via $91)               | --           | Z=1 if pressed; C from column | A, X |
| $FFE4   | GETIN   | $F13E     | Get one character from keyboard buffer                 | --           | A=char (0 if empty) | A, X, Y |

### 2.5 Character I/O

| Address | Name    | Real Addr | Description                                           | Input        | Output   | Clobbers  |
|---------|---------|-----------|-------------------------------------------------------|--------------|----------|-----------|
| $FFCF   | CHRIN   | $F157     | Read byte from current input channel                   | --           | A=byte   | A, Y      |
| $FFD2   | CHROUT  | $F1CA     | Write byte to current output channel                   | A=byte       | --       | --        |

CHRIN reads from whatever device is the current input (keyboard by default). When reading from the keyboard, the screen editor handles full-line input with cursor editing. CHROUT sends to the current output device (screen by default). When outputting to the screen, PETSCII control codes are interpreted for cursor movement, color changes, etc.

### 2.6 File I/O

| Address | Name    | Real Addr | Description                                           | Input        | Output   | Clobbers  |
|---------|---------|-----------|-------------------------------------------------------|--------------|----------|-----------|
| $FFBA   | SETLFS  | $FE00     | Set logical file parameters                           | A=logical file#, X=device#, Y=secondary addr | -- | -- |
| $FFBD   | SETNAM  | $FDF9     | Set file name                                         | A=name length, X/Y=pointer to name (lo/hi) | -- | -- |
| $FFC0   | OPEN    | $F34A     | Open a logical file                                   | (uses SETLFS/SETNAM params) | C=1 on error, A=error code | A, X, Y |
| $FFC3   | CLOSE   | $F291     | Close a logical file                                  | A=logical file# | C=1 on error | A, X, Y |
| $FFC6   | CHKIN   | $F20E     | Set input channel to a logical file                   | X=logical file# | C=1 on error | A, X |
| $FFC9   | CHKOUT  | $F250     | Set output channel to a logical file                  | X=logical file# | C=1 on error | A, X |
| $FFCC   | CLRCHN  | $F333     | Reset input to keyboard and output to screen           | --           | --       | A, X      |
| $FFE7   | CLALL   | $F32F     | Close all files and call CLRCHN                        | --           | --       | A, X      |
| $FFB7   | READST  | $FE07     | Read I/O status byte (ST variable)                    | --           | A=status | A         |
| $FFD5   | LOAD    | $F49E     | Load or verify file from device                       | A: 0=load, 1=verify; X/Y=load address (if SA=0) | C=1 on error, A=error; X/Y=end addr+1 | A, X, Y |
| $FFD8   | SAVE    | $F5DD     | Save memory to device                                 | A=ZP pointer to start addr; X/Y=end addr+1 | C=1 on error, A=error | A, X, Y |

**Typical file I/O sequence:**
```
SETLFS  -->  SETNAM  -->  OPEN  -->  CHKIN/CHKOUT  -->  read/write loop  -->  CLRCHN  -->  CLOSE
```

### 2.7 Serial Bus / IEC

| Address | Name    | Real Addr | Description                                           | Input        | Output   | Clobbers  |
|---------|---------|-----------|-------------------------------------------------------|--------------|----------|-----------|
| $FFB1   | LISTEN  | $ED0C     | Send LISTEN command (device# + $20) on serial bus     | A=device#    | --       | A         |
| $FFB4   | TALK    | $ED09     | Send TALK command (device# + $40) on serial bus       | A=device#    | --       | A         |
| $FF93   | SECOND  | $EDB9     | Send secondary address after LISTEN                   | A=secondary addr (OR'd with $60) | -- | A |
| $FF96   | TKSA    | $EDC7     | Send secondary address after TALK                     | A=secondary addr (OR'd with $60) | -- | A |
| $FFA5   | ACPTR   | $EE13     | Read byte from serial bus                             | --           | A=byte   | A         |
| $FFA8   | CIOUT   | $EDDD     | Write byte to serial bus                              | A=byte       | --       | --        |
| $FFAB   | UNTLK   | $EDEF     | Send UNTALK command on serial bus                     | --           | --       | A         |
| $FFAE   | UNLSN   | $EDFE     | Send UNLISTEN command on serial bus                   | --           | --       | A         |
| $FFA2   | SETTMO  | $FE21     | Set serial bus timeout flag (for IEEE-488 card)       | A=timeout flag | --     | --        |

### 2.8 Time

| Address | Name    | Real Addr | Description                                           | Input        | Output   | Clobbers  |
|---------|---------|-----------|-------------------------------------------------------|--------------|----------|-----------|
| $FFDB   | SETTIM  | $F6E4     | Set jiffy clock (1/60 sec ticks)                      | A=MSB, X=middle, Y=LSB (big-endian) | -- | -- |
| $FFDE   | RDTIM   | $F6DD     | Read jiffy clock                                      | --           | A=MSB, X=middle, Y=LSB | A, X, Y |
| $FFEA   | UDTIM   | $F69B     | Increment jiffy clock by 1 tick; also checks STOP key | --           | --       | A, X      |

The jiffy clock is a 3-byte big-endian counter at $A0-$A2, incremented 60 times per second by the default IRQ handler. It wraps to zero every 24 hours (5,184,000 jiffies). There is a known minor bug: one jiffy is lost per 24-hour cycle.


---

## 3. Useful Undocumented KERNAL Routines

These internal routines are not in the official jump table but are widely used by assembly programmers. **Warning:** These addresses are specific to the C64 KERNAL and may differ in other Commodore machines.

### 3.1 Screen Routines

| Address | Name     | Description                                            | Input / Output |
|---------|----------|--------------------------------------------------------|----------------|
| $E544   | CLSR     | Clear the screen and home the cursor                   | Clobbers A, X, Y |
| $E566   | NXTD/HOME | Home the cursor (move to row 0, column 0)            | Clobbers A, X, Y |
| $E716   | SCROUT   | Output one character to screen (main screen editor entry) | A = PETSCII character code |
| $E8EA   | SCRLUP   | Scroll the screen up one line                          | Clobbers A, X, Y |
| $E9FF   | SCRDWN   | Scroll the screen down one line                        | Clobbers A, X, Y |
| $E505   | SCRORG   | Return screen dimensions (same as SCREEN jump table)   | X=40, Y=25 |
| $EA24   | COLPTR   | Calculate color RAM pointer from screen line pointer   | Uses $D1/$D2 |

### 3.2 BASIC ROM Utility Routines (Callable from ML)

| Address | Name     | Description                                            | Input / Output |
|---------|----------|--------------------------------------------------------|----------------|
| $AB1E   | STROUT   | Print a null-terminated string to current output       | A/Y = pointer (lo/hi) to string |
| $BDCD   | PRTFIX   | Print 16-bit unsigned integer in A(hi)/X(lo) as decimal | A=high byte, X=low byte |
| $B391   | GIVAYF   | Convert signed 16-bit integer to FAC float             | A=high byte, Y=low byte |
| $B1AA   | FACINX   | Convert FAC to 16-bit signed integer                   | Result in Y(lo), A(hi) |
| $BCF3   | FIN      | Convert PETSCII string to float in FAC                 | String pointer in $7A/$7B (CHRGET pointer) |
| $BDDD   | FOUT     | Convert FAC to PETSCII string                          | Result string at $0100 (stack area) |
| $AAD7   | CRDO     | Output a carriage return / newline                     | -- |
| $A43A   | ERROR    | Display BASIC error and do warm start                  | X = error number * 2 |
| $A437   | ERRMSG   | Print error message from error number                  | X = error number * 2 |
| $A533   | LNKPRG   | Relink BASIC program (rebuild line link pointers)      | -- |
| $A560   | INLIN    | Input a line from keyboard into buffer at $0200        | -- |
| $A57C   | CRUNCH   | Tokenize a line of BASIC text                          | Line in input buffer |
| $A7AE   | NEWSTT   | Execute next BASIC statement (interpreter main loop)   | -- |
| $AD9E   | FRMEVL   | Evaluate an expression; result in FAC or string descriptor | -- |
| $B7F7   | FRESTR   | Free a temporary string and return pointer             | A = string descriptor pointer |

### 3.3 Low-Level Serial Bus (Internal)

These routines manipulate the CIA #2 port lines for the IEC serial bus directly:

| Address | Description                                           |
|---------|-------------------------------------------------------|
| $E9A5   | Set DATA line low                                     |
| $E9B2   | Release DATA line (set high)                          |
| $E9C0   | Set CLK line low                                      |
| $E9AE   | Release CLK line (set high)                           |
| $E9C9   | Read DATA line state                                  |
| $E9CD   | Read CLK line state                                   |

### 3.4 Alternative IRQ Entry Point

| Address | Description |
|---------|-------------|
| $EA81   | IRQ handler entry that skips keyboard scanning -- useful when you want the jiffy clock to keep running but don't need keyboard input, saving significant CPU time. Chain to this from a custom IRQ handler instead of $EA31. |


---

## 4. BASIC ROM

### 4.1 Memory Layout ($A000-$BFFF)

```
$A000-$A001  Cold start vector (points to $E394)
$A002-$A003  Warm start vector (points to $E37B, BASIC NMI entry)
$A004-$A00B  "cbmbasic" identification string

$A00C-$A051  Statement dispatch address table (35 two-byte entries)
$A052-$A07F  Function dispatch address table (23 two-byte entries)
$A080-$A09D  Operator hierarchy and dispatch table

$A09E-$A128  Statement keyword text (tokens $80-$A2, bit 7 set on last char)
$A129-$A13F  Secondary keyword text (TAB(, TO, FN, SPC(, THEN, NOT, STEP)
$A140-$A14C  Operator keyword text (+, -, *, /, ^, AND, OR, >, =, <)
$A14D-$A19D  Function keyword text (SGN through GO)

$A19E-$A327  Error message text (29 messages, bit 7 on last char)
$A328-$A363  Error message pointer table
$A364-$A389  Status messages: "OK", "ERROR IN ", "READY.", "BREAK"

$A38A-$A3B7  Stack search for FOR/GOSUB
$A3B8-$A3FA  Memory space allocation (make room for new lines)
$A3FB-$A437  Memory check and error handlers
$A43A-$A49B  Error display and warm start

$A49C-$A532  Insert/remove BASIC lines
$A533-$A55F  Relink program lines
$A560-$A578  Input line from keyboard
$A57C-$A612  Tokenizer (CRUNCH)
$A613-$A641  Search for BASIC line by number

$A642-$A65D  NEW command
$A65E-$A69B  CLR command
$A69C-$A741  LIST command
$A742-$A7AD  FOR command
$A7AE-$A882  Interpreter main loop (NEWSTT)
$A883-$A8D2  GOSUB / RETURN
$A8A0-$A8D2  GOTO
$A928-$A9A4  IF / THEN
$A9A5-$AA79  LET (variable assignment)

$AA80-$AAA0  PRINT# and PRINT
$AAA0-$AB17  PRINT formatting
$AB1E-$AB46  String output routine
$AB47-$ABF8  GET / INPUT / READ

$ABF9-$AD8F  Expression evaluator setup
$AD9E-$AEF7  FRMEVL (main expression evaluator)
$AEA8         Constant: PI (3.14159265)
$AEF8-$AF27  Operator evaluation

$AF28-$B080  Variable lookup and creation
$B081-$B194  DIM and array handling
$B194-$B34C  Array element address calculation

$B34D-$B465  FRE, POS, DEF FN, FN evaluation
$B465-$B526  STR$, string descriptor management
$B526-$B63C  Garbage collection
$B63D-$B6EB  String concatenation
$B6EC-$B7AD  CHR$, LEFT$, RIGHT$, MID$, LEN, ASC, VAL

$B7AE-$B849  Numeric/string type checking
$B849-$B8D1  Addition, subtraction setup
$B8D2-$B9E9  Integer and floating-point arithmetic
$B9EA-$BA27  LOG function
$BA28-$BAE1  Multiply FAC by memory
$BAE2-$BAFD  MUL10 (multiply FAC by 10)
$BAFE-$BB0E  DIV10 (divide FAC by 10)
$BB0F-$BBA1  Division (memory / FAC)
$BBA2-$BBD3  MOVFM (memory to FAC)
$BBD4-$BBFB  MOVMF (FAC to memory)
$BBFC-$BC0E  MOVEF/MOVFA (copy between FAC and ARG)

$BC0F-$BC57  Normalization and rounding
$BC58-$BC9A  ABS, comparison
$BC9B-$BCCC  QINT (FAC to 32-bit integer)
$BCCC-$BCF2  INT function
$BCF3-$BDCC  FIN (string to float conversion)
$BDCD-$BDDC  Print unsigned integer
$BDDD-$BE67  FOUT (float to string conversion)
$BE68-$BF70  TI/TI$ and SQR setup

$BF71-$BFEC  SQR, FPWR (power), NEGOP (negate)
$BFED         EXP function (extends into KERNAL area)
```

Note: The BASIC ROM also uses routines in the KERNAL address space at $E000-$E4D2 for additional math functions (SIN, COS, TAN, ATN) and the BASIC warm/cold start code.

### 4.2 Tokenized Program Storage

BASIC programs are stored as a linked list of tokenized lines starting at $0801 (default). The byte at $0800 must be $00 or a SYNTAX ERROR results.

**Line format:**

```
Byte 0-1:  Line link pointer (little-endian address of NEXT line's first byte)
Byte 2-3:  Line number (little-endian, 0-63999)
Byte 4+:   Tokenized BASIC text
Last byte: $00 (line terminator)
```

**Program terminator:** A line link with high byte = $00 marks end of program (two zero bytes after the last line's terminator).

**Tokenization rules:**
- BASIC keywords are replaced with single-byte tokens ($80-$CB)
- Variable names, string literals, numeric constants, and line numbers in GOTO/GOSUB remain as PETSCII text
- Text inside quote marks and after REM is not tokenized
- Tokens always have bit 7 set (values $80+)

### 4.3 Complete Token Table

| Token | Keyword | Token | Keyword | Token | Keyword |
|-------|---------|-------|---------|-------|---------|
| $80   | END     | $90   | STOP    | $A0   | CLOSE   |
| $81   | FOR     | $91   | ON      | $A1   | GET     |
| $82   | NEXT    | $92   | WAIT    | $A2   | NEW     |
| $83   | DATA    | $93   | LOAD    | $A3   | TAB(    |
| $84   | INPUT#  | $94   | SAVE    | $A4   | TO      |
| $85   | INPUT   | $95   | VERIFY  | $A5   | FN      |
| $86   | DIM     | $96   | DEF     | $A6   | SPC(    |
| $87   | READ    | $97   | POKE    | $A7   | THEN    |
| $88   | LET     | $98   | PRINT#  | $A8   | NOT     |
| $89   | GOTO    | $99   | PRINT   | $A9   | STEP    |
| $8A   | RUN     | $9A   | CONT    | $AA   | + (add) |
| $8B   | IF      | $9B   | LIST    | $AB   | - (sub) |
| $8C   | RESTORE | $9C   | CLR     | $AC   | * (mul) |
| $8D   | GOSUB   | $9D   | CMD     | $AD   | / (div) |
| $8E   | RETURN  | $9E   | SYS     | $AE   | ^ (pow) |
| $8F   | REM     | $9F   | OPEN    | $AF   | AND     |
|       |         |       |         | $B0   | OR      |

| Token | Function | Token | Function |
|-------|----------|-------|----------|
| $B1   | >        | $BC   | LOG      |
| $B2   | =        | $BD   | EXP      |
| $B3   | <        | $BE   | COS      |
| $B4   | SGN      | $BF   | SIN      |
| $B5   | INT      | $C0   | TAN      |
| $B6   | ABS      | $C1   | ATN      |
| $B7   | USR      | $C2   | PEEK     |
| $B8   | FRE      | $C3   | LEN      |
| $B9   | POS      | $C4   | STR$     |
| $BA   | SQR      | $C5   | VAL      |
| $BB   | RND      | $C6   | ASC      |
|       |          | $C7   | CHR$     |
|       |          | $C8   | LEFT$    |
|       |          | $C9   | RIGHT$   |
|       |          | $CA   | MID$     |
|       |          | $CB   | GO       |

Total: 76 tokens (68 keywords + 8 operators).

### 4.4 Important Zero-Page Variables (BASIC)

| Address   | Name    | Description                                            |
|-----------|---------|--------------------------------------------------------|
| $02B-$2C  | TXTTAB  | Pointer to start of BASIC program text (default: $0801) |
| $2D-$2E   | VARTAB  | Pointer to start of BASIC variables                    |
| $2F-$30   | ARYTAB  | Pointer to start of BASIC arrays                       |
| $31-$32   | STREND  | Pointer to end of BASIC arrays                         |
| $33-$34   | FRETOP  | Pointer to bottom of string storage                    |
| $35-$36   | FRESPC  | Current string allocation pointer                      |
| $37-$38   | MEMSIZ  | Pointer to highest BASIC RAM + 1 (default: $A000)      |
| $39-$3A   | CURLIN  | Current BASIC line number ($FF** = direct mode)        |
| $3B-$3C   | OLDLIN  | Line number saved by STOP/END for CONT                 |
| $3D-$3E   | OLDTXT  | Instruction pointer saved for CONT                     |
| $3F-$40   | DATLIN  | Line number of current DATA statement                  |
| $41-$42   | DATPTR  | Pointer to next DATA item                              |
| $43-$44   | INPPTR  | Input result pointer (GET/INPUT/READ)                  |
| $45-$46   | VARNAM  | Current variable name (2 bytes)                        |
| $47-$48   | VARPNT  | Pointer to current variable value                      |
| $61-$66   | FAC     | Floating-point accumulator #1 (5-byte mantissa + sign) |
| $69-$6E   | ARG     | Floating-point accumulator #2 (5-byte mantissa + sign) |
| $73-$8A   | CHRGET  | Self-modifying CHRGET/CHRGOT routine (18 bytes)        |
| $7A-$7B   | TXTPTR  | Current BASIC text pointer (within CHRGET code)        |

### 4.5 Floating-Point Format

The C64 uses a 5-byte floating-point format (40-bit):

```
Byte 0:     Exponent (excess-128 notation; $00 = number is zero)
Bytes 1-4:  Mantissa (MSB first, normalized with implicit leading 1)
            Bit 7 of byte 1 holds the sign (0=positive, 1=negative)
```

**Range:** approximately +/-2.94 x 10^-39 to +/-1.70 x 10^38

**FAC layout (zero page):**

| Address | Purpose                            |
|---------|------------------------------------|
| $61     | Exponent                           |
| $62-$65 | Mantissa (4 bytes, MSB first)      |
| $66     | Sign ($00 = positive, $FF = negative) |

**ARG layout (zero page):**

| Address | Purpose                            |
|---------|------------------------------------|
| $69     | Exponent                           |
| $6A-$6D | Mantissa (4 bytes, MSB first)      |
| $6E     | Sign ($00 = positive, $FF = negative) |

### 4.6 BASIC ROM Math Routines (Callable from ML)

Before calling FAC/ARG arithmetic routines, load the accumulator with the FAC exponent: `LDA $61`.

#### Moving / Copying

| Address | Name   | Description                                            |
|---------|--------|--------------------------------------------------------|
| $BA8C   | CONUPK | Load ARG from 5-byte float at address A(lo)/Y(hi)     |
| $BBA2   | MOVFM  | Load FAC from 5-byte float at address A(lo)/Y(hi)     |
| $BBD4   | MOVMF  | Store FAC to 5-byte float at address X(lo)/Y(hi)      |
| $BBFC   | MOVEF  | Copy ARG to FAC                                        |
| $BC0F   | MOVFA  | Copy FAC to ARG                                        |

#### Conversion

| Address | Name   | Description                                            |
|---------|--------|--------------------------------------------------------|
| $B391   | GIVAYF | Convert 16-bit signed integer (A=hi, Y=lo) to FAC     |
| $B1AA   | FACINX | Convert FAC to 16-bit signed integer (Y=lo, A=hi)     |
| $BC9B   | QINT   | Convert FAC to 32-bit signed integer at $62-$65        |
| $BCF3   | FIN    | Convert PETSCII string to float in FAC                 |
| $BDDD   | FOUT   | Convert FAC to PETSCII string at $0100                 |
| $B7B5   | STRVAL | Convert string to float (address in $22/$23)           |

#### Arithmetic

| Address | Name   | Description                                            |
|---------|--------|--------------------------------------------------------|
| $B850   | FSUB   | FAC = mem(A/Y) - FAC                                   |
| $B853   | FSUBT  | FAC = ARG - FAC                                        |
| $B867   | FADD   | FAC = FAC + mem(A/Y)                                   |
| $B86A   | FADDT  | FAC = FAC + ARG                                        |
| $BA28   | FMULT  | FAC = FAC * mem(A/Y)                                   |
| $BB0F   | FDIV   | FAC = mem(A/Y) / FAC                                   |
| $BB12   | FDIVT  | FAC = ARG / FAC                                        |
| $BAE2   | MUL10  | FAC = FAC * 10                                         |
| $BAFE   | DIV10  | FAC = FAC / 10                                         |
| $BF78   | FPWR   | FAC = mem(A/Y) ^ FAC                                   |
| $BF7B   | FPWRT  | FAC = ARG ^ FAC                                        |

#### Functions

| Address | Name   | Description                                            |
|---------|--------|--------------------------------------------------------|
| $BC58   | ABS    | FAC = |FAC|                                            |
| $BFB4   | NEGOP  | FAC = -FAC (toggle sign)                               |
| $BC39   | SGN    | FAC = SGN(FAC): -1, 0, or +1                          |
| $BCCC   | INT    | FAC = INT(FAC) (floor)                                 |
| $BF71   | SQR    | FAC = SQR(FAC) (square root)                           |
| $B9EA   | LOG    | FAC = LOG(FAC) (natural logarithm)                     |
| $BFED   | EXP    | FAC = EXP(FAC) (e^FAC)                                 |
| $E26B   | SIN    | FAC = SIN(FAC) (radians)                               |
| $E264   | COS    | FAC = COS(FAC) (radians)                               |
| $E2B4   | TAN    | FAC = TAN(FAC) (radians)                               |
| $E30E   | ATN    | FAC = ATN(FAC) (arctangent, radians)                   |
| $E043   | POLY1  | Evaluate odd-power polynomial on FAC                   |
| $E059   | POLY2  | Evaluate full polynomial on FAC                        |

#### Comparison

| Address | Name   | Description                                            |
|---------|--------|--------------------------------------------------------|
| $BC5B   | FCOMP  | Compare FAC to 5-byte float at A(lo)/Y(hi); A = 0 (equal), 1 (FAC greater), $FF (FAC less) |
| $BC2B   | SIGN   | Set processor flags (N, Z) based on FAC value          |

### 4.7 The CHRGET / CHRGOT Routine

The CHRGET routine at $73-$8A is the heartbeat of the BASIC interpreter. During cold start, the KERNAL copies 24 bytes from $E3A2 into zero page. This is self-modifying code: the text pointer at $7A-$7B is embedded as an operand within the routine itself.

```
$0073  CHRGET:  INC $7A        ; Increment low byte of text pointer
$0075           BNE $0079      ; Skip high byte increment if no wrap
$0077           INC $7B        ; Increment high byte
$0079  CHRGOT:  LDA $0801      ; $7A/$7B is self-modified address
$007C           CMP #$3A       ; Compare with ':'
$007E           BCS $008A      ; If >= ':', return (not digit, not space)
$0080           CMP #$20       ; Compare with space
$0082           BEQ $0073      ; If space, skip and get next char
$0084           SEC            ;
$0085           SBC #$30       ; Subtract '0'
$0087           SEC            ;
$0088           SBC #$D0       ; Add '0' back (restores A, sets C if digit)
$008A           RTS
```

**Key behavior:**
- CHRGET advances the pointer, then reads the byte
- CHRGOT re-reads the byte at the current position (entry at $79)
- Spaces are automatically skipped
- On return: Z=1 if byte is $00 (end of line) or ':' (statement separator); C=0 if byte is a digit '0'-'9'
- Placing CHRGET in zero page saves 1-2 cycles per access -- critical since it runs thousands of times per second during BASIC execution


---

## 5. Interrupt Vectors

### 5.1 Hardware Vectors (ROM, $FFFA-$FFFF)

These are hardwired 6510 CPU vectors in the last 6 bytes of address space. Since they are in ROM, they cannot be changed directly.

| Address     | Vector  | Default Target | Description                            |
|-------------|---------|----------------|----------------------------------------|
| $FFFA-$FFFB | NMI     | $FE43          | Non-Maskable Interrupt handler         |
| $FFFC-$FFFD | RESET   | $FCE2          | Cold start / power-on reset            |
| $FFFE-$FFFF | IRQ/BRK | $FF48          | Maskable interrupt and BRK instruction |

The ROM handlers at these addresses then indirect through the RAM vectors below, allowing user programs to intercept interrupts.

### 5.2 RAM Vectors ($0314-$0333)

These are the redirectable vectors that the ROM interrupt handlers JMP through. Changing these is the standard way to hook interrupts and I/O.

| Address     | Name   | Default Value | Description                            |
|-------------|--------|---------------|----------------------------------------|
| $0314-$0315 | CINV   | $EA31         | IRQ handler vector                     |
| $0316-$0317 | CBINV  | $FE66         | BRK instruction handler vector         |
| $0318-$0319 | NMINV  | $FE47         | NMI handler vector                     |
| $031A-$031B | IOPEN  | $F34A         | KERNAL OPEN vector                     |
| $031C-$031D | ICLOSE | $F291         | KERNAL CLOSE vector                    |
| $031E-$031F | ICHKIN | $F20E         | KERNAL CHKIN vector                    |
| $0320-$0321 | ICKOUT | $F250         | KERNAL CHKOUT vector                   |
| $0322-$0323 | ICLRCH | $F333         | KERNAL CLRCHN vector                   |
| $0324-$0325 | IBASIN | $F157         | KERNAL CHRIN vector                    |
| $0326-$0327 | IBSOUT | $F1CA         | KERNAL CHROUT vector                   |
| $0328-$0329 | ISTOP  | $F6ED         | KERNAL STOP vector                     |
| $032A-$032B | IGETIN | $F13E         | KERNAL GETIN vector                    |
| $032C-$032D | ICLALL | $F32F         | KERNAL CLALL vector                    |
| $032E-$032F | USRCMD | $FE66         | User-defined vector (unused by KERNAL) |
| $0330-$0331 | ILOAD  | $F4A5         | KERNAL LOAD vector                     |
| $0332-$0333 | ISAVE  | $F5ED         | KERNAL SAVE vector                     |

### 5.3 How to Safely Hook an IRQ

The standard technique is to redirect the RAM vector at $0314/$0315:

```asm
; Install custom IRQ handler
        SEI                     ; Disable interrupts
        LDA #<my_irq           ; Low byte of handler address
        STA $0314
        LDA #>my_irq           ; High byte of handler address
        STA $0315
        CLI                     ; Re-enable interrupts
        RTS

; Custom IRQ handler
my_irq:
        ; ... your code here ...
        JMP $EA31               ; Chain to default handler (keyboard, clock, etc.)
        ; OR:
        JMP $EA81               ; Chain but skip keyboard scanning

; If NOT chaining to default handler, you must:
;   1. Acknowledge the interrupt (LDA $DC0D)
;   2. Restore registers from stack
;   3. Execute RTI
my_irq_standalone:
        ; ... your code here ...
        ASL $D019               ; Acknowledge VIC-II raster interrupt (if using raster IRQ)
        LDA $DC0D               ; Acknowledge CIA #1 timer interrupt
        PLA                     ; Restore Y
        TAY
        PLA                     ; Restore X
        TAX
        PLA                     ; Restore A
        RTI
```

**Important notes:**
- Always use SEI/CLI around vector changes to prevent interrupts firing with a half-written vector
- If you chain to $EA31, it handles register restoration and RTI for you
- If you only need a raster interrupt (not the CIA timer), set up VIC-II raster interrupts and either disable the CIA timer or handle both sources
- Saving/restoring the old vector allows clean removal of your handler

### 5.4 Raster Interrupt Setup

Many programs use the VIC-II raster interrupt instead of (or in addition to) the CIA timer:

```asm
        SEI
        LDA #$7F
        STA $DC0D               ; Disable CIA #1 timer interrupts
        LDA $DC0D               ; Acknowledge any pending CIA interrupt
        LDA #$01
        STA $D01A               ; Enable VIC-II raster interrupt
        LDA #<raster_line
        STA $D012               ; Set raster line (low 8 bits)
        LDA $D011
        AND #$7F                ; Clear bit 7 of $D011 (raster line bit 8)
        STA $D011
        LDA #<my_handler
        STA $0314
        LDA #>my_handler
        STA $0315
        CLI
```

### 5.5 Default IRQ Handler Chain ($FF48 -> $EA31)

When the CPU receives an IRQ or BRK:

1. **$FF48 (ROM IRQ entry):** Pushes A, X, Y onto stack. Checks bit 4 of status register (saved on stack by CPU) to distinguish IRQ from BRK
2. If **BRK:** JMP ($0316) -- the BRK vector (default: $FE66, which enters the machine language monitor or returns to BASIC)
3. If **IRQ:** JMP ($0314) -- the IRQ vector (default: $EA31)
4. **$EA31 (default IRQ handler):**
   - Calls UDTIM ($FFEA) to increment the jiffy clock at $A0-$A2
   - Checks if cursor blinking is enabled ($CC); if so, decrements the blink counter ($CD); on expiry, XORs bit 7 of the character under the cursor to toggle its appearance
   - Checks the cassette motor interlock
   - Calls SCNKEY ($EA87) to scan the keyboard matrix and decode keys into the keyboard buffer ($0277-$0280)
   - Acknowledges the CIA #1 timer interrupt by reading $DC0D
   - Pulls Y, X, A from stack and executes RTI

### 5.6 NMI Handler Chain ($FE43)

When the CPU receives an NMI (usually the RESTORE key):

1. **$FE43 (ROM NMI entry):** Pushes A, X, Y. Sets the interrupt disable flag (SEI)
2. **JMP ($0318)** -- the NMI vector (default: $FE47)
3. **$FE47 (default NMI handler):**
   - Checks for an autostart cartridge at $8000 (looks for "CBM80" signature); if found, jumps through the cartridge NMI vector at $8002
   - Checks if the RUN/STOP key is held down (reads keyboard column at $DC01)
   - If RUN/STOP **is pressed**: Performs a warm start -- restores default I/O, resets VIC-II, silences SID, closes files, and jumps to BASIC warm start ($E37B)
   - If RUN/STOP **is not pressed**: Pulls Y, X, A from stack and executes RTI (effectively ignoring the RESTORE key press)

**The RESTORE key** is wired directly to the NMI line of the CPU through a debouncing circuit. It is NOT part of the keyboard matrix and cannot be detected by SCNKEY. It can only be detected via the NMI mechanism.


---

## 6. I/O System

### 6.1 Device Numbers

| Device # | Name               | Connection                | Notes                                 |
|----------|--------------------|---------------------------|---------------------------------------|
| 0        | Keyboard           | Internal (KERNAL special) | Input only; uses screen editor for line input |
| 1        | Datasette          | Internal (KERNAL special) | Tape cassette player/recorder         |
| 2        | RS-232 / Modem     | Internal (KERNAL special) | Uses CIA #2 and NMI; buffers in $F7-$FA |
| 3        | Screen             | Internal (KERNAL special) | Output only; characters to screen RAM |
| 4-5      | Printer            | IEC serial bus            | Typically 4 = first printer           |
| 6-7      | Plotter / other    | IEC serial bus            | Assigned by user                      |
| 8-30     | Disk drives        | IEC serial bus            | 8 = first drive (default); configurable via DIP switches |

Devices 0-3 are handled entirely within the KERNAL with dedicated code paths. Devices 4-30 all use the IEC serial bus protocol and are addressed by their device number.

### 6.2 Logical File Numbers

Logical file numbers (1-127) are user-assigned identifiers that map to a (device, secondary address) pair via the KERNAL's internal file table. The KERNAL supports up to 10 simultaneously open files. The table is stored in three 10-byte arrays:

| Address     | Contents                                  |
|-------------|-------------------------------------------|
| $0259-$0262 | Logical file numbers (LAT)                |
| $0263-$026C | Device numbers (FAT)                      |
| $026D-$0276 | Secondary addresses (SAT)                 |
| $0098       | Number of currently open files (0-10)     |

### 6.3 Secondary Addresses

Secondary addresses (0-15) select the communication channel within a device:

| SA  | Disk Drive Meaning                         | Printer Meaning                |
|-----|--------------------------------------------|-------------------------------|
| 0   | Load (PRG, first two bytes = load address) | Graphics/uppercase mode        |
| 1   | Save (PRG)                                 | Graphics/uppercase mode        |
| 2-14| Data channels (user files)                 | Various modes (device-specific) |
| 15  | Command channel (DOS commands/status)      | --                             |

For LOAD: If SA=0, the file loads to the address embedded in the first two bytes of the file. If SA=1 (or any nonzero), the file loads to the address specified in X/Y of the LOAD call.

### 6.4 How LOAD Works

1. Call SETLFS with logical file#, device# (e.g., 8), secondary address (0 or 1)
2. Call SETNAM with filename
3. Call LOAD with A=0 (load, not verify), X/Y=address (used only if SA != 0)
4. KERNAL opens a channel to the device, sends the filename, then reads bytes via the serial bus and stores them in RAM
5. On success: C=0, X/Y = end address + 1
6. On error: C=1, A = error code (see READST)

```asm
; Load "GAME" from device 8 to its embedded address
        LDA #$04           ; Filename length
        LDX #<filename
        LDY #>filename
        JSR $FFBD          ; SETNAM
        LDA #$01           ; Logical file #1
        LDX #$08           ; Device 8
        LDY #$00           ; SA=0 (use file's load address)
        JSR $FFBA          ; SETLFS
        LDA #$00           ; 0 = LOAD (not verify)
        JSR $FFD5          ; LOAD
        BCS error           ; C=1 means error
        ; X/Y = end address + 1
        RTS
filename: .text "GAME"
```

### 6.5 How SAVE Works

```asm
; Save $C000-$CFFF to disk as "DATA"
        LDA #$04
        LDX #<filename
        LDY #>filename
        JSR $FFBD          ; SETNAM
        LDA #$01
        LDX #$08
        LDY #$01           ; SA=1
        JSR $FFBA          ; SETLFS
        LDA #$FB           ; ZP pointer to start address (put $00/$C0 at $FB/$FC)
        LDX #$00
        LDY #$D0           ; End address $D000 (exclusive)
        JSR $FFD8          ; SAVE
        BCS error
        RTS
```

### 6.6 Disk Command Channel

The command channel (secondary address 15) is used to send DOS commands to the disk drive and read status/error messages.

**Sending a command:**
```basic
OPEN 15,8,15,"S:OLDFILE"    : REM Scratch (delete) a file
CLOSE 15
```

**Reading the error channel:**
```basic
OPEN 15,8,15
INPUT#15, EN, EM$, TR, SC   : REM Error#, Message, Track, Sector
PRINT EN; EM$; TR; SC
CLOSE 15
```

**Common error channel responses:**

| Code | Message           | Meaning                                    |
|------|-------------------|--------------------------------------------|
| 00   | OK                | No error                                   |
| 01   | FILES SCRATCHED   | File(s) deleted (not an error)             |
| 20   | READ ERROR        | Block header not found                     |
| 21   | READ ERROR        | No sync mark found                         |
| 22   | READ ERROR        | Data block not found                       |
| 23   | READ ERROR        | Checksum error in data block               |
| 25   | WRITE ERROR       | Verify error after write                   |
| 26   | WRITE PROTECT ON  | Disk is write-protected                    |
| 29   | DISK ID MISMATCH  | Wrong disk inserted                        |
| 30   | SYNTAX ERROR      | General DOS syntax error                   |
| 33   | SYNTAX ERROR      | Invalid file name                          |
| 39   | FILE NOT FOUND    | Requested file does not exist              |
| 62   | FILE NOT FOUND    | Requested file does not exist              |
| 63   | FILE EXISTS       | Cannot create; file already exists         |
| 66   | ILLEGAL TRACK OR SECTOR | Access out of disk bounds             |
| 70   | NO CHANNEL        | All 5 drive buffers in use                 |
| 72   | DISK FULL         | No free blocks remaining                   |
| 73   | DOS VERSION       | Returned on drive reset (CBM DOS V2.6 1541) |
| 74   | DRIVE NOT READY   | No disk in drive                           |

### 6.7 Complete 1541 DOS Commands

**File Management:**

| Command    | Syntax                                | Description                          |
|------------|---------------------------------------|--------------------------------------|
| NEW (N)    | `N:<diskname>,<id>`                   | Format disk; omit ID for quick-format (BAM only) |
| SCRATCH (S)| `S:<filename>`                       | Delete files; wildcards (* ?) allowed |
| RENAME (R) | `R:<newname>=<oldname>`              | Rename a file                        |
| COPY (C)   | `C:<dest>=<source>`                  | Copy/concatenate files               |
| VALIDATE (V)| `V`                                 | Rebuild BAM from directory; fixes orphaned blocks |
| INITIALIZE (I)| `I`                               | Re-read BAM from disk into drive memory |

**Block-Level Access:**

| Command    | Syntax                                | Description                          |
|------------|---------------------------------------|--------------------------------------|
| U1 (B-R)   | `U1:<channel>,<drive>,<track>,<sector>` | Read 256-byte block into buffer   |
| U2 (B-W)   | `U2:<channel>,<drive>,<track>,<sector>` | Write 256-byte buffer to block    |
| B-P        | `B-P:<channel>,<position>`           | Set buffer pointer position          |
| B-A        | `B-A:<drive>,<track>,<sector>`       | Allocate a block in BAM              |
| B-F        | `B-F:<drive>,<track>,<sector>`       | Free a block in BAM                  |
| B-E        | `B-E:<channel>,<drive>,<track>,<sector>` | Load block and execute as code    |

**Drive Memory Access:**

| Command    | Syntax                                | Description                          |
|------------|---------------------------------------|--------------------------------------|
| M-R        | `M-R:<addrlo><addrhi>[<count>]`      | Read drive RAM (binary, not ASCII)   |
| M-W        | `M-W:<addrlo><addrhi><count><data...>` | Write to drive RAM                 |
| M-E        | `M-E:<addrlo><addrhi>`               | Execute code in drive RAM            |

**User Commands:**

| Command    | Description                                                  |
|------------|--------------------------------------------------------------|
| UI (U3-U8) | Jump to user-defined routines in drive RAM                   |
| UI         | Soft reset (re-read BAM)                                     |
| UJ         | Hard reset (reset drive controller, same as power-cycle)     |

Note: The original B-R and B-W commands have bugs in the 1541 firmware. U1 and U2 are the corrected replacements and should always be used instead.


---

## 7. Hardcore Details

### 7.1 KERNAL Initialization Sequence (Cold Start)

When the C64 is powered on or reset, the 6510 CPU reads the RESET vector at $FFFC-$FFFD (which contains $FCE2) and begins execution:

```
$FCE2:  LDX #$FF          ; Initialize stack pointer
$FCE4:  SEI                ; Disable interrupts
$FCE5:  TXS                ; Set stack pointer to $01FF
$FCE6:  CLD                ; Clear decimal mode flag
$FCE7:  JSR $FD02          ; Check for autostart cartridge
                            ;   Looks for "CBM80" at $8004-$8008
                            ;   If found: JMP ($8000) -- cartridge cold start
$FCEA:  STX $D016          ; X=$FF from above; sets VIC-II to default
$FCED:  JSR $FDA3          ; IOINIT -- Initialize CIAs, SID, memory config
                            ;   Sets CIA #1 Timer A to ~60 Hz for system IRQ
                            ;   Sets processor port $01 to $37 (default banking)
                            ;   Initializes SID volume to 0
$FCF0:  JSR $FD50          ; RAMTAS -- RAM test and initialization
                            ;   Clears $0002-$0101 (zero page and stack)
                            ;   Clears $0200-$03FF (input buffer, screen line table)
                            ;   Tests RAM to find top of memory
                            ;   Sets MEMTOP/MEMBOT pointers
$FCF3:  JSR $FD15          ; RESTOR -- Copy default vectors to $0314-$0333
$FCF6:  JSR $FF5B          ; CINT -- Initialize VIC-II and screen editor
                            ;   Sets up 40x25 text screen at $0400
                            ;   Sets colors, cursor position
                            ;   Detects PAL/NTSC for timing
$FCF9:  CLI                ; Enable interrupts
$FCFA:  JMP $E394          ; Jump to BASIC cold start
                            ;   Prints "*** COMMODORE 64 BASIC V2 ***"
                            ;   Sets BASIC pointers (start $0801, end $A000)
                            ;   Enters BASIC READY prompt loop
```

### 7.2 Warm Start

A warm start (RUN/STOP + RESTORE, or `SYS 64738`) differs from cold start:

- **RUN/STOP + RESTORE:** The NMI handler detects STOP key held, restores default screen settings, silences SID, closes open files, clears keyboard buffer, and jumps to BASIC warm start at $E37B. BASIC programs in memory are preserved.
- **SYS 64738 ($FCE2):** Executes the full cold start sequence. RAM test will clear important areas, and BASIC memory pointers are reset. Effectively a full reboot (but RAM content outside cleared areas may survive).

### 7.3 Screen Editor Internals

The screen editor occupies a significant portion of the KERNAL ROM (roughly $E000-$EA00). It manages:

#### Screen Memory Organization

- Screen RAM: 1000 bytes at $0400 (default) -- one byte per character (40 x 25)
- Color RAM: 1000 bytes at $D800 -- always at this fixed address (4-bit color nybbles)
- The screen editor maintains a table of 25 screen line start addresses at zero-page $D9-$F1

#### Logical Lines

The editor works with 80-column "logical lines" -- each logical line can span two physical 40-column screen lines. The high bit of each entry in the line pointer table ($D9-$F1) indicates whether that physical line is the continuation of the line above.

#### Key Zero-Page Screen Variables

| Address | Name    | Description                                        |
|---------|---------|----------------------------------------------------|
| $C5     | LSTX    | Matrix code of last key pressed                    |
| $C6     | NDX     | Number of characters in keyboard buffer (0-10)     |
| $C7     | RVS     | Reverse mode flag (0=off, nonzero=on)              |
| $C8     | INDX    | Length of line during screen input                  |
| $C9     | LXSP    | Cursor row at start of input                       |
| $CA     | LXSP+1  | Cursor column at start of input                    |
| $CB     | SFDX    | Current key matrix code (64 = no key)              |
| $CC     | BLNSW   | Cursor blink enable (0=blink, nonzero=no blink)    |
| $CD     | BLNCT   | Cursor blink countdown timer                       |
| $CE     | GDBLN   | Character code under cursor (saved for restore)    |
| $CF     | BLNON   | Cursor blink phase (0=character visible, 1=cursor visible) |
| $D0     | CRSW    | Input source flag (0=keyboard, 3=screen)           |
| $D1-$D2 | PNT     | Pointer to current screen line start address        |
| $D3     | PNTR    | Current cursor column (0-39)                       |
| $D4     | QTSW    | Quotation mode flag (0=off, nonzero=on)            |
| $D5     | LNMX    | Maximum column for current logical line (39 or 79) |
| $D6     | TBLX    | Current cursor row (0-24)                          |
| $D7     | DATA    | Temp: last character code entered                  |
| $D8     | INSRT   | Number of insert characters remaining              |
| $F3-$F4 | USER    | Pointer to current color RAM location              |
| $F5-$F6 | KEYTAB  | Pointer to current keyboard decode table           |

#### Keyboard Buffer

| Address     | Description                                        |
|-------------|----------------------------------------------------|
| $0277-$0280 | Keyboard buffer (10 bytes, FIFO)                   |
| $C6         | Number of characters waiting in buffer              |
| $0289       | Maximum keyboard buffer size (default: 10)         |
| $028A       | Key repeat mode (0=cursors only, $40=no repeat, $80=all keys) |

#### Character Output Path ($FFD2 CHROUT -> $E716)

When CHROUT is called with a character in A:

1. If the current output device is the screen (device 3, the default):
2. Enter screen editor at $E716
3. Check for PETSCII control codes:
   - **$0D** (RETURN): Move cursor to column 0 of next line; signal end-of-line for input
   - **$11** (cursor down), **$1D** (cursor right), **$91** (cursor up), **$9D** (cursor left): Move cursor
   - **$13** (HOME): Move cursor to row 0, column 0
   - **$93** (CLR): Clear screen and home cursor
   - **$14** (DEL): Delete character left of cursor
   - **$94** (INSERT): Insert space at cursor, shift right
   - **$12** (RVS ON): Enable reverse video mode
   - **$92** (RVS OFF): Disable reverse video mode
   - **$0E** (lowercase charset), **$8E** (uppercase charset): Switch character sets
   - Color codes ($05, $1C-$1F, $81, $90, $95-$9F): Change text color
4. If printable character: Write screen code to screen RAM at ($D1/$D2)+$D3; write color to color RAM at ($F3/$F4)+$D3; advance cursor

### 7.4 Complete Zero-Page Map

**Processor Port ($00-$01):**

| Addr | Name  | Description                                          |
|------|-------|------------------------------------------------------|
| $00  | D6510 | 6510 data direction register (bits: 1=output, 0=input) |
| $01  | R6510 | 6510 I/O port: bits 0-2=memory config, bit 3=cassette output, bit 4=cassette sense, bit 5=cassette motor |

**BASIC Working Storage ($02-$72):**

| Addr    | Name   | Description                                        |
|---------|--------|----------------------------------------------------|
| $02     | --     | Unused                                             |
| $03-$04 | ADRAY1 | USR function jump vector (default: $B1AA = float-to-int) |
| $05-$06 | ADRAY2 | USR function jump vector (default: $B391 = int-to-float) |
| $07     | CHARONE| Search character / digit buffer                    |
| $08     | CHARTWO| Scan quote / token buffer                         |
| $09     | ENDCHR | Column counter (TAB/SPC)                           |
| $0A     | TRMPOS | LOAD/VERIFY flag (0=LOAD, 1=VERIFY)               |
| $0B     | VERCK  | Token / dimension count                            |
| $0C     | COUNT  | Array operation flag                               |
| $0D     | DIMFLG | Expression type (0=numeric, $FF=string)            |
| $0E     | VALTYP | Numeric type (0=float, $80=integer)                |
| $0F     | INTFLG | Quotation mode / garbage collection flag           |
| $10     | GTEFLAG| Variable name fetch flag                           |
| $11     | SUBFLG | GET/INPUT/READ indicator (0=INPUT, $40=GET, $98=READ) |
| $12     | INPFLG | SIN/TAN sign flag                                  |
| $13     | DOMESSION| Current I/O device number for PRINT/INPUT        |
| $14-$15 | LINNUM | Integer line number (GOTO, GOSUB, etc.)            |
| $16     | TEMPPT | String stack pointer (0, 3, 6, or 9)               |
| $17-$18 | LASTPT | Previous string stack entry pointer                |
| $19-$21 | TEMPST | String descriptor stack (3 entries x 3 bytes)      |
| $22-$25 | INDEX  | Temporary pointer / scratch area (4 bytes)         |
| $26-$29 | RESHO  | Multiplication/division working area (4 bytes)     |
| $2A     | --     | Unused                                             |
| $2B-$2C | TXTTAB | Start of BASIC text (default: $0801)               |
| $2D-$2E | VARTAB | Start of BASIC variables                           |
| $2F-$30 | ARYTAB | Start of BASIC arrays                              |
| $31-$32 | STREND | End of BASIC arrays (bottom of string area)        |
| $33-$34 | FRETOP | Bottom of string storage                           |
| $35-$36 | FRESPC | Current string allocation pointer                  |
| $37-$38 | MEMSIZ | Top of BASIC memory (default: $A000)               |
| $39-$3A | CURLIN | Current BASIC line number (MSB=$FF: direct mode)   |
| $3B-$3C | OLDLIN | Previous line (STOP/END) for CONT                  |
| $3D-$3E | OLDTXT | Previous text pointer for CONT                     |
| $3F-$40 | DATLIN | DATA line number for READ                          |
| $41-$42 | DATPTR | Pointer to next DATA item                          |
| $43-$44 | INPPTR | Pointer for GET/INPUT/READ result                  |
| $45-$46 | VARNAM | Current variable name (2 bytes encode name+type)   |
| $47-$48 | VARPNT | Pointer to current variable value                  |
| $49-$4A | FORPNT | Variable pointer for FOR/NEXT; WAIT params         |
| $4B-$4C | OPPTR  | Temporary saved text pointer                       |
| $4D     | OPMASK | Comparison operator flag (bit 1=>, bit 2==, bit 3=<) |
| $4E-$4F | DEFPNT | Pointer to current DEF FN                          |
| $50-$51 | DESSION| Temporary string pointer                           |
| $53     | GESSION| Garbage collection step size                       |
| $54-$56 | JMPER  | JMP instruction for functions ($4C, addr-lo, addr-hi) |
| $57-$5B | --     | Arithmetic register #3 (5 bytes)                   |
| $5C-$60 | --     | Arithmetic register #4 (5 bytes)                   |
| $61-$66 | FAC    | Floating-point accumulator #1 ($61=exp, $62-$65=mantissa, $66=sign) |
| $67     | SESSION| Polynomial degree counter                          |
| $68     | ARIESSION| Temp overflow area                                |
| $69-$6E | ARG    | Floating-point accumulator #2 ($69=exp, $6A-$6D=mantissa, $6E=sign) |
| $6F-$70 | --     | String comparison pointer                          |
| $71-$72 | --     | Polynomial / array / VAL pointer                   |

**CHRGET Subroutine ($73-$8A):**

| Addr    | Description                                          |
|---------|------------------------------------------------------|
| $73-$78 | CHRGET: INC $7A / BNE $79 / INC $7B                 |
| $79-$7B | CHRGOT: LDA $xxxx (self-modified text pointer)       |
| $7A-$7B | TXTPTR: Current BASIC text pointer (embedded in LDA) |
| $7C-$8A | Compare/skip logic and RTS                           |

**RND Seed ($8B-$8F):**

| Addr    | Description                                          |
|---------|------------------------------------------------------|
| $8B-$8F | Previous RND result (5-byte float, seeded at startup) |

**KERNAL Working Storage ($90-$FF):**

| Addr    | Name   | Description                                        |
|---------|--------|----------------------------------------------------|
| $90     | STATUS | I/O status byte (ST variable)                      |
| $91     | STKEY  | STOP key flag ($7F=pressed during last scan)       |
| $92     | SVXT   | Tape timing constant                               |
| $93     | VERCK  | LOAD/VERIFY switch (0=LOAD, 1=VERIFY)             |
| $94     | C3PO   | Serial bus output cache flag (0=empty, nonzero=full) |
| $95     | BSOUR  | Serial bus cached output byte                      |
| $96     | SYESSION | Tape end-of-tape flag                             |
| $97     | XSAV   | Temp register save (X or Y)                        |
| $98     | LDTND  | Number of open logical files                       |
| $99     | DESSION| Default input device (0=keyboard)                  |
| $9A     | DFLTO  | Default output device (3=screen)                   |
| $9B     | PRESSION | Tape parity byte                                  |
| $9C     | BYESSION| Tape byte-received flag                           |
| $9D     | MSGFLG | Error message mode (bit 7=errors, bit 6=control)  |
| $9E     | ESSION | Tape/RS-232 error count / byte buffer              |
| $9F     | --     | File name counter / scratch                        |
| $A0-$A2 | TIME   | Jiffy clock (3 bytes, big-endian, 1/60 sec)        |
| $A3     | --     | EOI flag / tape bit counter                        |
| $A4     | --     | Serial/tape byte buffer                            |
| $A5     | --     | Serial/tape bit counter                            |
| $A6     | --     | Tape buffer byte offset                            |
| $A7-$AB | --     | RS-232 working variables                           |
| $AC-$AD | TAPE1  | Tape/SAVE pointer (current address)                |
| $AE-$AF | --     | LOAD end address / next block start                |
| $B0-$B1 | --     | Unused                                             |
| $B2-$B3 | TAPE2  | Tape buffer pointer (default: $033C)               |
| $B4-$B6 | --     | RS-232 bit buffer / counter                        |
| $B7     | FNLEN  | File name length                                   |
| $B8     | LA     | Current logical file number                        |
| $B9     | SA     | Current secondary address                          |
| $BA     | FA     | Current device number                              |
| $BB-$BC | FNADR  | Pointer to file name string                        |
| $BD-$BE | --     | RS-232 / tape byte buffer                          |
| $BF     | --     | Unused                                             |
| $C0     | STESSION| Tape motor interlock flag                         |
| $C1-$C2 | STAL   | LOAD/SAVE start address / I/O scratch              |
| $C3-$C4 | MEMUSS | Secondary LOAD address / tape pointer              |
| $C5     | LSTX   | Last key matrix code pressed                       |
| $C6     | NDX    | Keyboard buffer count (0-10)                       |
| $C7     | RVS    | Reverse mode flag                                  |
| $C8-$CA | --     | Screen input working (line length, cursor pos)     |
| $CB     | SFDX   | Current key matrix code                            |
| $CC-$CF | --     | Cursor blink variables (see screen editor section) |
| $D0-$D8 | --     | Screen editor variables (see screen editor section)|
| $D9-$F1 | --     | Screen line start address table (25 entries)       |
| $F2     | --     | Screen scroll temp                                 |
| $F3-$F4 | --     | Color RAM pointer                                  |
| $F5-$F6 | --     | Keyboard decode table pointer                      |
| $F7-$F8 | --     | RS-232 input buffer pointer                        |
| $F9-$FA | --     | RS-232 output buffer pointer                       |
| **$FB-$FE** | **FREE** | **4 bytes free for user programs**           |
| $FF     | BESSION| Float-to-string buffer start                       |

**Free zero-page locations for ML programs:** $02, $FB-$FE (6 bytes total). The range $57-$60 (arithmetic registers 3-4) may also be safely used if not calling BASIC math routines. Some BASIC variables ($07-$12, $22-$2A, $4B-$53) can be used if BASIC is not running.

### 7.5 IRQ Handler Internals

The default CIA #1 Timer A fires approximately 60 times per second on both PAL and NTSC systems. The full IRQ processing path:

```
1. CPU detects IRQ line low, finishes current instruction
2. CPU pushes PC(hi), PC(lo), P (status) onto stack (7 cycles)
3. CPU loads PC from $FFFE/$FFFF -> $FF48
4. $FF48: PHA / TXA / PHA / TYA / PHA        ; Save A, X, Y
5. $FF48: TSX / LDA $0104,X                   ; Read status from stack
6.         AND #$10                             ; Test BRK flag
7.         BNE -> JMP ($0316)                   ; If BRK: jump through BRK vector
8.         JMP ($0314)                          ; Else: jump through IRQ vector
                                                ; Default: $EA31

9. $EA31: JSR $FFEA (UDTIM)                    ; Increment jiffy clock
10.        LDA $CC                              ; Check cursor blink enable
11.        BNE skip_blink
12.        DEC $CD                              ; Decrement blink counter
13.        BNE skip_blink
14.        LDA #$14                             ; Reset counter (20 frames)
15.        STA $CD
16.        ; Toggle cursor character (XOR bit 7)
17. skip_blink:
18.        ; Check cassette motor interlock
19.        JSR $EA87 (SCNKEY)                   ; Scan keyboard matrix
20.        ; Scan 8 rows x 8 columns via CIA #1 ports A/B
21.        ; Decode key using table at ($F5/$F6)
22.        ; Handle shift/CTRL/C= key combinations
23.        ; Store PETSCII code in buffer at $0277 + $C6
24.        LDA $DC0D                            ; Read CIA #1 ICR (acknowledge interrupt)
25.        PLA / TAY / PLA / TAX / PLA          ; Restore Y, X, A
26.        RTI
```

**Cycle cost:** The default IRQ handler takes approximately 300-400 cycles when no key is pressed, and 800+ cycles when a key is held down (due to keyboard matrix scanning and decoding). On a ~1 MHz CPU with ~60 Hz IRQs, this represents roughly 1.5-4% of total CPU time.

### 7.6 NMI Handler Internals

The NMI is edge-triggered (not level-triggered like IRQ), so it fires once per RESTORE key press:

```
1. CPU detects NMI edge
2. CPU pushes PC(hi), PC(lo), P onto stack
3. CPU loads PC from $FFFA/$FFFB -> $FE43

$FE43: SEI                          ; Disable IRQ
       JMP ($0318)                  ; Default: $FE47

$FE47: PHA / TXA / PHA / TYA / PHA ; Save registers
       ; Check for autostart cartridge:
       LDA $8004                    ; Look for "CBM80" signature
       CMP #$C3                     ; 'C'
       BNE not_cart
       ; ... check remaining bytes ...
       ; If cartridge found: JMP ($8002) -- cartridge NMI handler

not_cart:
       ; Check STOP key:
       LDA #$7F
       STA $DC00                   ; Select keyboard row 7
       LDA $DC01                   ; Read column
       AND #$10                    ; Check bit 4 (STOP key)
       BEQ stop_pressed            ; If 0, STOP is pressed

       ; STOP not pressed: ignore NMI
       PLA / TAY / PLA / TAX / PLA
       RTI

stop_pressed:
       ; Warm start sequence:
       JSR $FD15                   ; RESTOR: restore default vectors
       JSR $FDA3                   ; IOINIT: reinitialize CIAs, SID
       JSR $E518                   ; Initialize screen editor
       JMP $E37B                   ; BASIC warm start (READY prompt)
```

### 7.7 Serial Bus Protocol

The C64 communicates with disk drives, printers, and other peripherals through a Commodore-proprietary serial bus based on IEEE-488 but using only three signal lines (active-low, open collector):

| Pin | Signal | Description                                   |
|-----|--------|-----------------------------------------------|
| 1   | SRQ    | Service Request (active low, unused in practice) |
| 2   | GND    | Ground                                        |
| 3   | ATN    | Attention -- computer pulls low to send commands |
| 4   | CLK    | Clock -- controlled by sender (talker)        |
| 5   | DATA   | Data -- carries one bit per clock cycle       |
| 6   | RESET  | Reset all devices                             |

**Protocol overview:**

The serial bus operates as a master/slave architecture. The C64 is always the controller (master). Devices are addressed by number (4-30) and have one or more channels (secondary addresses 0-15).

**Byte Transfer Sequence (Standard Protocol):**

1. **Sender holds CLK low** (busy signal)
2. **Receiver(s) hold DATA low** (not ready)
3. **Sender releases CLK** (ready to send)
4. **All receivers release DATA** (all ready)
5. **For each of 8 bits (LSB first):**
   - Sender pulls CLK low, places bit on DATA
   - After ~60 us, sender releases CLK (data valid)
   - Receiver reads DATA when CLK goes high
   - After ~60 us, sender pulls CLK low again
6. **After 8th bit:** Sender releases CLK and DATA; receiver pulls DATA low within 1 ms to acknowledge

**EOI (End Or Identify) Signaling:**

To indicate the last byte in a transmission, the sender delays step 3 by at least 200 us. The receiver detects this timeout and acknowledges by pulling DATA low for 60 us before releasing it, then the byte transfer proceeds normally.

**Key Timing Specifications:**

| Parameter | Symbol | Time         | Description                     |
|-----------|--------|--------------|---------------------------------|
| ATN Response | Tat | 1000 us max  | Device must respond after ATN   |
| Bit Setup | Ts     | 20-70 us     | Data stable before CLK release  |
| Data Valid | Tv    | 20 us min    | CLK high (data readable)        |
| Frame Handshake | Tf | 0-1000 us  | Receiver ack after byte         |
| Between Bytes | Tbb | 100 us min | Minimum gap between bytes       |
| EOI Timeout | Tye  | 200-250 us   | Delay indicating last byte      |
| EOI Ack | Tei    | 60 us        | Receiver holds DATA for ack     |
| Non-EOI Response | Tne | 40 us typ, 200 us max | Normal data availability |

**Command Sequences:**

The controller (C64) uses ATN to send command bytes:

| Command   | Byte Format     | Description                            |
|-----------|-----------------|----------------------------------------|
| LISTEN    | Device# + $20   | Tell device to receive data            |
| TALK      | Device# + $40   | Tell device to send data               |
| UNLISTEN  | $3F             | Release all listeners                  |
| UNTALK    | $5F             | Release current talker                 |
| Secondary | SA OR'd with $60 | Select channel after LISTEN/TALK      |
| CLOSE     | SA OR'd with $E0 | Close channel (with LISTEN)           |
| OPEN      | SA OR'd with $F0 | Open channel (with LISTEN)            |

**Typical LOAD sequence on the serial bus:**

```
1. LISTEN device 8                    ; ATN, send $28
2. SECOND (SA=$F0 | channel)          ; Open channel: send $F0
3. Send filename bytes via CIOUT      ; ATN released, send filename
4. UNLISTEN                           ; ATN, send $3F
5. TALK device 8                      ; ATN, send $48
6. TKSA (SA=$60 | channel)            ; Select channel: send $60
7. Loop: ACPTR to read bytes          ; ATN released, device sends data
8.   (Check READST for EOI)
9. UNTALK                             ; ATN, send $5F
10. LISTEN device 8                   ; ATN, send $28
11. SECOND (SA=$E0 | channel)         ; Close channel: send $E0
12. UNLISTEN                          ; ATN, send $3F
```

### 7.8 Tape Routines and Datasette Format

The KERNAL contains extensive tape handling code ($F72C-$FC93). Tape I/O is interrupt-driven and uses CIA #1 timers for precise timing.

**Tape Data Encoding:**

Each bit is encoded as a square wave pulse pair:
- **Short pulse:** ~352 us (represents a '0' bit on TAP level)
- **Medium pulse:** ~512 us (represents a '1' bit on TAP level)
- **Long pulse:** ~672 us (sync mark)

Bytes are transmitted LSB first with an extra parity bit, giving 9 pulses per byte.

**File Structure on Tape:**

Each file consists of four blocks:
1. Header (1st copy) -- preceded by ~10 second leader of short pulses
2. Header (2nd copy) -- preceded by ~2 second leader (for error recovery)
3. Data (1st copy) -- preceded by ~2 second leader
4. Data (2nd copy) -- preceded by ~2 second leader

**Header Block Format (192 bytes):**

| Offset | Length | Description                              |
|--------|--------|------------------------------------------|
| 0      | 1      | File type: $01=relocatable PRG, $03=non-relocatable PRG, $04=SEQ, $05=end-of-tape |
| 1-2    | 2      | Start address (little-endian)            |
| 3-4    | 2      | End address (little-endian)              |
| 5-20   | 16     | Filename (padded with spaces)            |
| 21-191 | 171    | Unused (filled with $20)                 |

**Block Synchronization:**

Before each block, countdown bytes are sent:
- 1st copy: $89, $88, $87, ... $81 (countdown with bit 7 set)
- 2nd copy: $09, $08, $07, ... $01 (countdown with bit 7 clear)

Each block ends with a 1-byte XOR checksum of all payload bytes.

**Tape Buffer:** Located at $033C-$03FB (192 bytes). The pointer at $B2/$B3 points to this buffer.

### 7.9 Key Addresses Quick Reference

**KERNAL Warm/Cold Entry Points:**

| Address | Description                                    |
|---------|------------------------------------------------|
| $FCE2   | Cold start (hardware reset entry)              |
| $E394   | BASIC cold start (print banner, NEW, READY)    |
| $E37B   | BASIC warm start (READY prompt, no clear)      |
| $A474   | BASIC warm start (print READY, wait for input) |
| $A480   | BASIC main loop (parse and execute a line)     |
| $A7AE   | BASIC interpreter inner loop (NEWSTT)          |

**Screen Memory (Defaults):**

| Address     | Description                                  |
|-------------|----------------------------------------------|
| $0400-$07E7 | Screen RAM (1000 bytes, 40x25)              |
| $07E8-$07FF | Unused screen RAM (24 bytes, sprite pointers) |
| $D800-$DBE7 | Color RAM (1000 nybbles, fixed address)     |

**Important I/O Chip Registers (for context):**

| Address | Chip    | Description                              |
|---------|---------|------------------------------------------|
| $D000-$D02E | VIC-II | Video controller                      |
| $D400-$D41C | SID    | Sound Interface Device                |
| $D800-$DBFF | Color  | Color RAM (4-bit nybbles)             |
| $DC00-$DC0F | CIA #1 | Keyboard, joystick, IRQ timer         |
| $DD00-$DD0F | CIA #2 | Serial bus, RS-232, NMI timer, VIC bank |

**Input Buffer and Work Areas:**

| Address     | Description                                  |
|-------------|----------------------------------------------|
| $0200-$0258 | BASIC input buffer (89 bytes)               |
| $0259-$0276 | File table (LFN, Device, SA -- 10 each)     |
| $0277-$0280 | Keyboard buffer (10 bytes)                  |
| $0281-$0288 | Various KERNAL settings                     |
| $0289       | Max keyboard buffer size                    |
| $028A       | Key repeat mode                             |
| $0314-$0333 | RAM vectors (16 two-byte entries)           |
| $033C-$03FB | Tape I/O buffer (192 bytes)                 |


---

## References

### Primary Sources

- [Commodore 64 Programmer's Reference Guide](https://www.devili.iki.fi/Computers/Commodore/C64/Programmers_Reference/) -- Commodore's official programming reference
- [KERNAL API -- Ultimate C64 Reference (pagetable.com)](https://www.pagetable.com/c64ref/kernal/) -- Michael Steil's comprehensive KERNAL API reference
- [C64 BASIC & KERNAL ROM Disassembly (pagetable.com)](https://www.pagetable.com/c64ref/c64disasm/) -- Complete annotated disassembly
- [Commodore 64 Standard KERNAL Functions (sta.c64.org)](https://sta.c64.org/cbm64krnfunc.html) -- Jump table with real addresses and register details
- [Commodore 64 Memory Map (sta.c64.org)](https://sta.c64.org/cbm64mem.html) -- Complete memory map including zero page
- [C64 ROM Routines (skoolkid)](https://skoolkid.github.io/sk6502/c64rom/maps/routines.html) -- Annotated ROM routine map

### C64-Wiki Articles

- [Kernal](https://www.c64-wiki.com/wiki/Kernal)
- [BASIC-ROM](https://www.c64-wiki.com/wiki/BASIC-ROM)
- [BASIC Token](https://www.c64-wiki.com/wiki/BASIC_token)
- [Floating Point Arithmetic](https://www.c64-wiki.com/wiki/Floating_point_arithmetic)
- [Interrupt](https://www.c64-wiki.com/wiki/Interrupt)
- [Bank Switching](https://www.c64-wiki.com/wiki/Bank_Switching)
- [Device Number](https://www.c64-wiki.com/wiki/Device_number)
- [Serial Port](https://www.c64-wiki.com/wiki/Serial_Port)
- [Drive Command](https://www.c64-wiki.com/wiki/Drive_command)
- [Datassette Encoding](https://www.c64-wiki.com/wiki/Datassette_Encoding)
- [Reset (Process)](https://www.c64-wiki.com/wiki/Reset_(Process))
- [RESTORE (Key)](https://www.c64-wiki.com/wiki/RESTORE_(Key))
- [CHRGET](https://www.c64-wiki.com/wiki/CHRGET)
- [Memory Map](https://www.c64-wiki.com/wiki/Memory_Map)
- [Control Character](https://www.c64-wiki.com/wiki/control_character)
- [OPEN](https://www.c64-wiki.com/wiki/OPEN)

### C64 OS Technical Articles

- [C64 KERNAL ROM: Making Sense](https://c64os.com/post/c64kernalrom) -- KERNAL module organization and design
- [Floating Point Math from BASIC](https://c64os.com/post/floatingpointmath) -- Detailed FAC/ARG usage
- [Memory Management in BASIC](https://c64os.com/post/basicmemorymanagement) -- BASIC memory layout
- [The 6510 Processor Port](https://www.c64os.com/post/6510procport) -- Memory banking details

### Serial Bus Protocol

- [Commodore Peripheral Bus: Part 4: Standard Serial (pagetable.com)](https://www.pagetable.com/?p=1135) -- Comprehensive protocol documentation
- [Commodore IEC Serial Bus Manual](https://www.commodore.ca/wp-content/uploads/2018/11/Commodore-IEC-Serial-Bus-Manual-C64-Plus4.txt)

### Source Code

- [Original Commodore Source Code (GitHub/mist64)](https://github.com/mist64/cbmsrc) -- Includes KERNAL C64 source
- [Commented C64 KERNAL (GitHub Gist)](https://gist.github.com/cbmeeks/4287745eab43e246ddc6bcbe96a48c19) -- Community-annotated disassembly

### Books

- "Mapping the Commodore 64" by Sheldon Leemon -- Comprehensive memory map reference
- "The Complete Commodore Inner Space Anthology" -- Quick-reference for all C64 internals
- "COMPUTE!'s Machine Language Routines for the Commodore 64 and 128" -- Practical ML programming with ROM calls
