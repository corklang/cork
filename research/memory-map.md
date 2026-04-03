# Commodore 64 Memory Map -- Comprehensive Reference

## 1. Overview

The Commodore 64 has a 16-bit address bus giving a 64KB ($0000-$FFFF) address
space. However, the system contains more than 64KB of addressable memory: 64KB
of DRAM, 20KB of ROM (8KB BASIC + 8KB KERNAL + 4KB Character Generator), and
approximately 4KB of I/O registers all compete for the same address ranges.

The C64 resolves this overlap through **bank switching** controlled by:

- **The 6510 processor port** at addresses $0000-$0001 (bits LORAM, HIRAM,
  CHAREN)
- **The PLA (Programmable Logic Array)** chip, which decodes the processor port
  bits along with cartridge port signals (GAME, EXROM) to determine what is
  visible at each address
- **CIA #2 Port A** bits 0-1, which select one of four 16KB banks for the
  VIC-II chip

Key architectural facts:

- **Writes always go to RAM.** Even when ROM is banked in, writing to an address
  in the ROM range stores the byte in the underlying RAM. Only reads are
  affected by bank switching.
- **The VIC-II has its own memory view.** It sees a 16KB bank of RAM (selected
  via CIA #2) and never sees BASIC ROM, KERNAL ROM, or I/O registers. It does
  see Character ROM at $1000-$1FFF within banks 0 and 2.
- **Color RAM is special.** It is a dedicated 1Kx4-bit SRAM chip wired directly
  to VIC-II data lines D8-D11. It always appears at $D800-$DBFF for the CPU
  regardless of banking, and VIC-II accesses it directly in parallel.
- **$0000-$0001 are hardwired.** The 6510's on-chip I/O port always occupies
  these two addresses; they cannot be remapped.

### Default Memory Layout (Mode 31: EXROM=1, GAME=1, $0001=$37)

```
$0000 +-----------+
      | RAM       | Zero page, stack, system workspace
$0400 +-----------+
      | RAM       | Default screen memory (40x25)
$0800 +-----------+
      | RAM       | BASIC program storage area (38911 bytes free)
$A000 +-----------+
      | BASIC ROM | 8KB BASIC interpreter
$C000 +-----------+
      | RAM       | 4KB free RAM
$D000 +-----------+
      | I/O       | VIC-II, SID, Color RAM, CIA 1, CIA 2
$E000 +-----------+
      | KERNAL ROM| 8KB operating system
$FFFF +-----------+
```

---

## 2. Detailed Memory Map

### 2.1 Processor Port ($0000-$0001)

These two addresses are hardwired into the 6510 CPU. They are not RAM; they are
on-chip I/O port registers.

| Addr | Label | Description | Default |
|------|-------|-------------|---------|
| $0000 | D6510 | Data Direction Register for processor port. Each bit: 0=input, 1=output. | $2F |
| $0001 | R6510 | Processor I/O port. Controls ROM/RAM banking and datasette. | $37 |

**$0001 bit definitions:**

| Bit | Name | Function |
|-----|------|----------|
| 0 | LORAM | 1=BASIC ROM visible at $A000-$BFFF, 0=RAM |
| 1 | HIRAM | 1=KERNAL ROM visible at $E000-$FFFF, 0=RAM |
| 2 | CHAREN | 1=I/O visible at $D000-$DFFF, 0=Character ROM visible |
| 3 | Cassette write | Datasette motor control output |
| 4 | Cassette sense | Datasette button status input (active low) |
| 5 | Cassette motor | Datasette motor control (0=motor on, 1=motor off) |
| 6 | N/C | Not connected |
| 7 | N/C | Not connected |

Note: The CHAREN bit only has effect when at least one of LORAM or HIRAM is set
to 1. When both are 0 (mode 28/24), $D000-$DFFF is always plain RAM regardless
of CHAREN.

### 2.2 Zero Page ($0000-$00FF)

The zero page is critical for 6502/6510 performance: it supports unique
addressing modes (zero-page direct, zero-page indirect indexed) that are faster
and produce shorter opcodes. Almost all zero-page locations are used by BASIC
and KERNAL.

#### CPU Port Registers ($00-$01)

| Addr | Label | Description |
|------|-------|-------------|
| $00 | D6510 | Data direction register (see above) |
| $01 | R6510 | Processor I/O port (see above) |

#### Unused ($02)

| Addr | Label | Description |
|------|-------|-------------|
| $02 | -- | Unused. Safe for user programs. |

#### BASIC Working Registers ($03-$0072)

| Addr | Label | Description |
|------|-------|-------------|
| $03-$04 | ADRAY1 | Vector: float-to-integer routine (default: $B1AA) |
| $05-$06 | ADRAY2 | Vector: integer-to-float routine (default: $B391) |
| $07 | CHARAC | Search character for scanning BASIC text |
| $08 | ENDCHR | Search character for statement termination or quote |
| $09 | TRMPOS | Column position before last TAB or SPC |
| $0A | VERCK | Flag: 0=LOAD, 1-255=VERIFY |
| $0B | COUNT | Index into text input buffer / number of array subscripts |
| $0C | DIMFLG | Flag for array locate/build routines |
| $0D | VALTYP | Data type flag: $00=numeric, $FF=string |
| $0E | INTFLG | Numeric type: $00=float, $80=integer |
| $0F | GARBFL | Flag for LIST, garbage collection, tokenization |
| $10 | SUBFLG | Flag: subscript reference / user function call |
| $11 | INPFLG | Input source: $00=INPUT, $40=GET, $98=READ |
| $12 | TANSGN | Sign result of TAN/SIN function |
| $13 | CHANNL | Current I/O channel (CMD logical file) number |
| $14-$15 | LINNUM | Integer line number value (GOTO, GOSUB, LIST) |
| $16 | TEMPPT | Pointer to next free slot in temporary string stack (default: $19) |
| $17-$18 | LASTPT | Pointer to last string in temporary string stack |
| $19-$21 | TEMPST | Temporary string descriptor stack (3 entries x 3 bytes) |
| $22-$25 | INDEX | Miscellaneous temporary pointers (4 bytes) |
| $26-$2A | RES | Floating-point multiplication work area (5 bytes) |
| $2B-$2C | TXTTAB | Pointer: start of BASIC program text (default: $0801) |
| $2D-$2E | VARTAB | Pointer: start of BASIC variable storage |
| $2F-$30 | ARYTAB | Pointer: start of BASIC array storage |
| $31-$32 | STREND | Pointer: end of BASIC array storage (+1) |
| $33-$34 | FRETOP | Pointer: bottom of string storage area |
| $35-$36 | FRESPC | Temporary string pointer |
| $37-$38 | MEMSIZ | Pointer: highest address used by BASIC (default: $A000) |
| $39-$3A | CURLIN | Current BASIC line number being executed |
| $3B-$3C | OLDLIN | Previous BASIC line number (used by CONT) |
| $3D-$3E | OLDTXT | Pointer: BASIC statement for CONT |
| $3F-$40 | DATLIN | Current DATA line number |
| $41-$42 | DATPTR | Pointer: current DATA item address (for READ) |
| $43-$44 | INPPTR | Temporary pointer storage during INPUT |
| $45-$46 | VARNAM | Name of variable being searched for |
| $47-$48 | VARPNT | Pointer: to value of current variable |
| $49-$4A | FORPNT | Pointer: index variable for FOR/NEXT loop |
| $4B-$4C | VARTXT | Temporary TXTPTR storage during READ |
| $4D | OPMASK | Comparison operator mask for FRMEVL |
| $4E-$52 | TEMPF3 | Temporary floating-point storage (5 bytes) |
| $53 | FOUR6 | String length during garbage collection |
| $54-$56 | JMPER | Jump vector for function evaluation ($54=$4C=JMP opcode) |
| $57-$5B | TEMPF1 | Temporary floating-point storage (5 bytes) |
| $5C-$60 | TEMPF2 | Temporary floating-point storage (5 bytes) |
| $61-$66 | FAC | Floating-point accumulator #1 (6 bytes: exponent + 4 mantissa + sign) |
| $67 | SGNFLG | Pointer: series evaluation constant / sign flag |
| $68 | BITS | Bit overflow area during normalization |
| $69-$6E | AFAC | Floating-point accumulator #2 (6 bytes) |
| $6F | ARISGN | Sign of result of arithmetic evaluation |
| $70 | FACOV | FAC #1 low-order rounding byte |
| $71-$72 | FBUFPT | Pointer used during tokenization/ASCII conversion |

#### CHRGET Subroutine ($73-$8A)

| Addr | Label | Description |
|------|-------|-------------|
| $73-$8A | CHRGET | Self-modifying subroutine in RAM: fetches next byte of BASIC text. The pointer at $7A-$7B (TXTPTR) is the current position in the BASIC text. CHRGET increments the pointer, CHRGOT does not. |
| $7A-$7B | TXTPTR | Pointer: current byte of BASIC text (embedded within CHRGET code) |

This is the single most important zero-page routine for BASIC. It is copied
from ROM into RAM during initialization so it runs faster from zero page.

#### Random Number Seed ($8B-$8F)

| Addr | Label | Description |
|------|-------|-------------|
| $8B-$8F | RNDX | Floating-point RND seed value (5 bytes) |

#### KERNAL I/O and Status ($90-$A2)

| Addr | Label | Description |
|------|-------|-------------|
| $90 | STATUS | KERNAL I/O status word (ST variable) |
| $91 | STKEY | Stop key flag: $7F=STOP pressed, $FF=not pressed |
| $92 | SVXT | Timing constant for tape operations |
| $93 | VERCKK | Flag: 0=LOAD, 1=VERIFY (KERNAL copy) |
| $94 | C3PO | Flag: serial bus output char buffered ($00=no, $FF=yes) |
| $95 | BSOUR | Buffered character for serial bus |
| $96 | SYNO | Cassette sync number |
| $97 | TEMPX | Temporary X register storage during I/O |
| $98 | LDTND | Number of open files / index to file table (0-10) |
| $99 | DFLTN | Default input device (default: $00 = keyboard) |
| $9A | DFLTO | Default output device (default: $03 = screen) |
| $9B | PRTY | Tape byte output parity |
| $9C | DPSW | Flag: byte received from tape |
| $9D | MSGFLG | KERNAL message control: bit 6=error msgs, bit 7=control msgs |
| $9E | FNMIDX | Index to cassette file name / tape error log pass 1 |
| $9F | PTR2 | Tape error log pass 2 |
| $A0-$A2 | TIME | Software jiffy clock (3 bytes, big-endian). Updated every 1/60 second by IRQ handler. Read by TI/TI$ in BASIC. |

#### Tape and Serial I/O ($A3-$B6)

| Addr | Label | Description |
|------|-------|-------------|
| $A3 | TSFCNT | Bit counter for tape read/write; serial bus EOI flag |
| $A4 | TBTCNT | Pulse counter tape / serial bus shift counter |
| $A5 | CNTDN | Tape sync countdown |
| $A6 | BUFPNT | Pointer into tape I/O buffer |
| $A7 | INBIT | RS-232 received bit / tape temporary |
| $A8 | BITC1 | RS-232 input bit count / tape temporary |
| $A9 | RINONE | RS-232 start bit check flag / tape temporary |
| $AA | RIDATA | RS-232 input byte buffer / tape temporary |
| $AB | RIPRTY | RS-232 input parity / tape temporary |
| $AC-$AD | SAL | Tape buffer pointer / screen scrolling pointer |
| $AE-$AF | EAL | Tape end address / end of program |
| $B0-$B1 | CMPO | Tape timing constants |
| $B2-$B3 | TAPE1 | Pointer: start of tape buffer (default: $033C) |
| $B4 | BITTS | RS-232 write bit count / tape read timing flag |
| $B5 | NXTBIT | RS-232 next bit to send / tape end-of-tape flag |
| $B6 | RODATA | RS-232 output byte buffer / tape read error flag |

#### File Parameters ($B7-$C4)

| Addr | Label | Description |
|------|-------|-------------|
| $B7 | FNLEN | Length of current filename |
| $B8 | LA | Current logical file number |
| $B9 | SA | Current secondary address |
| $BA | FA | Current device number |
| $BB-$BC | FNADR | Pointer: current filename address |
| $BD | ROPRTY | RS-232 output parity / tape byte to be I/O'd |
| $BE | FSBLK | Tape I/O block count |
| $BF | MYCH | Serial word buffer |
| $C0 | CAS1 | Tape motor switch |
| $C1-$C2 | STAL | Start address for LOAD / cassette write |
| $C3-$C4 | MEMUSS | Type 3 tape LOAD pointer / general use pointer |

#### Keyboard and Screen Editor ($C5-$D8)

| Addr | Label | Description |
|------|-------|-------------|
| $C5 | LSTX | Matrix code of last key pressed ($40=none) |
| $C6 | NDX | Number of characters in keyboard buffer (0-10) |
| $C7 | RVS | Reverse mode flag: $00=normal, $12=reverse |
| $C8 | INDX | Pointer: end of logical line for input |
| $C9-$CA | LXSP | Cursor position at start of input (line, column) |
| $CB | SFDX | Current key matrix code |
| $CC | BLNSW | Cursor blink enable: $00=blink on, $01+=blink off |
| $CD | BLNCT | Cursor blink countdown timer |
| $CE | GDBLN | Character code under cursor (saved during blink) |
| $CF | BLNON | Cursor phase: $00=character showing, $01=cursor showing |
| $D0 | CRSW | Input source: $00=keyboard, $03=screen |
| $D1-$D2 | PNT | Pointer: current screen line address |
| $D3 | PNTR | Cursor column on current line (0-39) |
| $D4 | QTSW | Quote mode flag: $00=not in quotes |
| $D5 | LNMX | Current logical line length (39 or 79) |
| $D6 | TBLX | Current screen line number of cursor (0-24) |
| $D7 | SCHAR | Screen code of current input character |
| $D8 | INSRT | Count of insert characters outstanding |

#### Screen Line Link Table ($D9-$F2)

| Addr | Label | Description |
|------|-------|-------------|
| $D9-$F2 | LDTB1 | Screen line link table (26 bytes). High byte of each screen line address. Bit 7 of each entry indicates whether the line is a continuation of the previous logical line (0=continuation, 1=new line). |

#### Color and Keyboard Pointers ($F3-$FA)

| Addr | Label | Description |
|------|-------|-------------|
| $F3-$F4 | USER | Pointer: current color RAM location |
| $F5-$F6 | KEYTAB | Pointer: current keyboard decode table (default: $EB81) |
| $F7-$F8 | RIBUF | RS-232 input buffer pointer |
| $F9-$FA | ROBUF | RS-232 output buffer pointer |

#### Free for User Programs ($FB-$FE)

| Addr | Label | Description |
|------|-------|-------------|
| $FB | FREKZP | Free zero-page byte for user programs |
| $FC | FREKZP+1 | Free zero-page byte for user programs |
| $FD | FREKZP+2 | Free zero-page byte for user programs |
| $FE | FREKZP+3 | Free zero-page byte for user programs |

These four bytes are the **only** zero-page locations guaranteed safe for
machine-language programs while BASIC and KERNAL are active. If BASIC is not
running, $02 and $22-$25 (INDEX), $39-$46 (BASIC execution state), and the
floating-point accumulators ($57-$70) are also usable.

#### BASIC Temporary ($FF)

| Addr | Label | Description |
|------|-------|-------------|
| $FF | BASZPT | BASIC temporary data area |


### 2.3 Processor Stack ($0100-$01FF)

The 6510 hardware stack grows downward from $01FF. The stack pointer register
(S) is an 8-bit offset into this page. On RESET, S is initialized to $FF,
pointing to $01FF.

| Addr | Description |
|------|-------------|
| $0100-$013E | Also used as tape input error log (BAD) during tape operations |
| $013F-$01FF | BASIC stack area (BSTACK) -- FOR/NEXT, GOSUB return addresses |

The stack is shared between hardware (JSR/RTS, interrupts push/pull return
addresses and status register) and BASIC (FOR/NEXT loops store 18 bytes each,
GOSUB stores 5 bytes). Deep nesting can overflow the stack.

### 2.4 BASIC/OS Working Storage ($0200-$03FF)

#### Input Buffer ($0200-$0258)

| Addr | Label | Description |
|------|-------|-------------|
| $0200-$0258 | BUF | BASIC input buffer (89 bytes). Lines typed at keyboard are stored here before tokenization. |

#### File Tables ($0259-$0276)

| Addr | Label | Description |
|------|-------|-------------|
| $0259-$0262 | LAT | Table of active logical file numbers (10 entries) |
| $0263-$026C | FAT | Table of active device numbers (10 entries) |
| $026D-$0276 | SAT | Table of active secondary addresses (10 entries) |

#### Keyboard Buffer ($0277-$0280)

| Addr | Label | Description |
|------|-------|-------------|
| $0277-$0280 | KEYD | Keyboard buffer queue (FIFO, 10 bytes max) |

#### System Configuration ($0281-$02A6)

| Addr | Label | Description | Default |
|------|-------|-------------|---------|
| $0281-$0282 | MEMSTR | OS bottom-of-memory pointer | $0800 |
| $0283-$0284 | MEMSIZ | OS top-of-memory pointer | $A000 |
| $0285 | TIMOUT | Serial bus timeout defeat flag | $00 |
| $0286 | COLOR | Current character color code | $0E (light blue) |
| $0287 | GDCOL | Background color under cursor | $06 (blue) |
| $0288 | HIBASE | High byte of screen memory address | $04 (screen at $0400) |
| $0289 | XMAX | Max keyboard buffer size | $0A (10) |
| $028A | RPTFLG | Key repeat flag: $00=only cursor/DEL, $40=no repeat, $80=all repeat | $00 |
| $028B | KOUNT | Key repeat speed counter | $04 |
| $028C | DELAY | Key repeat initial delay counter | $10 |
| $028D | SHFLAG | Shift key flag: bit 0=SHIFT, bit 1=CBM, bit 2=CTRL | $00 |
| $028E | LSTSHF | Previous shift key state (for debounce) | $00 |
| $028F-$0290 | KEYLOG | Vector: keyboard decode table selection routine (default: $EB48) |
| $0291 | MODE | Charset toggle flag: $00=disabled, $80=C=-SHIFT toggles upper/lower | $00 |
| $0292 | AUTODN | Auto-scroll-down flag | $00 |

#### RS-232 Working Storage ($0293-$02A6)

| Addr | Label | Description |
|------|-------|-------------|
| $0293 | M51CTR | RS-232 pseudo-6551 control register image |
| $0294 | M51CDR | RS-232 pseudo-6551 command register image |
| $0295-$0296 | M51AJB | RS-232 nonstandard baud rate (timer value) |
| $0297 | RSSTAT | RS-232 pseudo-6551 status register image |
| $0298 | BITNUM | RS-232 bits remaining to send |
| $0299-$029A | BAUDOF | RS-232 baud rate timing value |
| $029B | RIDBE | RS-232 index to end of input buffer |
| $029C | RIDBS | RS-232 input buffer page address |
| $029D | RODBS | RS-232 output buffer page address |
| $029E | RODBE | RS-232 index to end of output buffer |
| $029F-$02A0 | IRQTMP | Temporary IRQ vector storage during tape operations |
| $02A1 | ENABL | RS-232 enable flags |
| $02A2 | TODSNS | TOD sense during tape I/O |
| $02A3 | TRDTMP | Temporary storage during tape READ |
| $02A4 | TD1IRQ | Temporary D1IRQ indicator during tape READ |
| $02A5 | TLNIDX | Temporary line index |
| $02A6 | TVSFLG | TV standard flag: $00=NTSC (6567), $01=PAL (6569) |

#### Unused Space ($02A7-$02FF)

| Addr | Description |
|------|-------------|
| $02A7-$02FF | Unused (89 bytes, available for ML programs) |

### 2.5 System Vectors ($0300-$033B)

These RAM vectors are the primary interception points for extending or replacing
BASIC and KERNAL functionality. They are initialized from a ROM table by the
RESTOR ($FF8A) KERNAL routine.

#### BASIC Vectors ($0300-$030B)

| Addr | Label | Description | Default |
|------|-------|-------------|---------|
| $0300-$0301 | IERROR | Error message handler | $E38B |
| $0302-$0303 | IMAIN | BASIC warm start / main loop | $A483 |
| $0304-$0305 | ICRNCH | Tokenize BASIC text | $A57C |
| $0306-$0307 | IQPLOP | LIST a BASIC token | $A71A |
| $0308-$0309 | IGONE | Execute BASIC token | $A7E4 |
| $030A-$030B | IEVAL | Evaluate expression | $AE86 |

#### SYS Register Storage ($030C-$030F)

| Addr | Label | Description |
|------|-------|-------------|
| $030C | SAREG | Accumulator value for SYS command |
| $030D | SXREG | X register value for SYS command |
| $030E | SYREG | Y register value for SYS command |
| $030F | SPREG | Status register value for SYS command |

#### USR Function ($0310-$0312)

| Addr | Label | Description |
|------|-------|-------------|
| $0310 | USRPOK | JMP opcode ($4C) for USR() function |
| $0311-$0312 | USRADD | Address for USR() function jump |

#### Hardware Interrupt Vectors ($0314-$0319)

| Addr | Label | Description | Default |
|------|-------|-------------|---------|
| $0314-$0315 | CINV | IRQ/BRK interrupt vector | $EA31 |
| $0316-$0317 | CNBINV | BRK instruction vector | $FE66 |
| $0318-$0319 | NMINV | NMI interrupt vector | $FE47 |

#### KERNAL I/O Vectors ($031A-$0333)

| Addr | Label | Description | Default |
|------|-------|-------------|---------|
| $031A-$031B | IOPEN | OPEN routine | $F34A |
| $031C-$031D | ICLOSE | CLOSE routine | $F291 |
| $031E-$031F | ICHKIN | CHKIN (set input channel) | $F20E |
| $0320-$0321 | ICKOUT | CHKOUT (set output channel) | $F250 |
| $0322-$0323 | ICLRCH | CLRCHN (restore default I/O) | $F333 |
| $0324-$0325 | IBASIN | CHRIN (character input) | $F157 |
| $0326-$0327 | IBSOUT | CHROUT (character output) | $F1CA |
| $0328-$0329 | ISTOP | STOP key check | $F6ED |
| $032A-$032B | IGETIN | GETIN (get character) | $F13E |
| $032C-$032D | ICLALL | CLALL (close all files) | $F32F |
| $032E-$032F | USRCMD | User-defined vector | $FE66 |
| $0330-$0331 | ILOAD | LOAD routine | $F4A5 |
| $0332-$0333 | ISAVE | SAVE routine | $F5ED |

#### Tape Buffer ($033C-$03FB)

| Addr | Label | Description |
|------|-------|-------------|
| $033C-$03FB | TBUFFR | Tape I/O buffer (192 bytes). Also used for sprite data in some programs. |

### 2.6 Default Screen RAM ($0400-$07FF)

| Addr | Description |
|------|-------------|
| $0400-$07E7 | Screen character matrix: 1000 bytes (25 rows x 40 columns). Each byte is a screen code (not PETSCII). Row 0 starts at $0400, row 1 at $0428, etc. Stride = 40 ($28). |
| $07E8-$07F7 | Unused (16 bytes, but overwritten by scroll operations) |
| $07F8-$07FF | Sprite data pointers (8 bytes). Sprite 0 pointer at $07F8, sprite 7 at $07FF. Each pointer value N means sprite data is fetched from N*64 within the current VIC bank. |

The screen location can be moved by changing VIC register $D018 bits 4-7 and
the screen memory high byte at $0288 (HIBASE).

### 2.7 BASIC Program Area ($0800-$9FFF)

| Addr | Description |
|------|-------------|
| $0800 | Must contain $00 for the RUN command to work (end-of-line marker for a "line 0" that doesn't exist) |
| $0801 onwards | BASIC program text in tokenized form. Each line: 2 bytes next-line pointer (lo/hi), 2 bytes line number (lo/hi), tokenized text, $00 terminator. Program ends with $00 $00 (null next-line pointer). |
| After program | BASIC variables (simple), then array variables, then free memory, then strings growing downward from MEMSIZ ($37-$38). |

With the default configuration, BASIC has 38911 bytes free ($0801-$9FFF). The
key pointers are:

- $2B-$2C (TXTTAB): Start of program (default $0801)
- $2D-$2E (VARTAB): Start of simple variables (end of program + 1)
- $2F-$30 (ARYTAB): Start of array variables
- $31-$32 (STREND): End of array variables
- $33-$34 (FRETOP): Bottom of string storage (grows downward)
- $37-$38 (MEMSIZ): Top of BASIC memory (default $A000)

### 2.8 Cartridge ROM Area ($8000-$9FFF)

| Addr | Description |
|------|-------------|
| $8000-$9FFF | 8KB cartridge ROM low bank (active when EXROM=0 and GAME=1, or EXROM=0 and GAME=0). Contains auto-start signature when present. |

**Cartridge auto-start signature (at $8000):**

| Addr | Content | Description |
|------|---------|-------------|
| $8000-$8001 | Cold start vector | Pointer to cartridge cold-start routine |
| $8002-$8003 | Warm start vector | Pointer to cartridge warm-start (RESTORE key) routine |
| $8004-$8008 | $C3 $C2 $CD $38 $30 | Magic bytes "CBM80" -- KERNAL checks for this |

### 2.9 BASIC ROM ($A000-$BFFF)

8KB BASIC V2 interpreter. Visible when LORAM=1 and HIRAM=1 (and no cartridge
ROM override).

Key BASIC ROM entry points:

| Addr | Description |
|------|-------------|
| $A000 | Cold start: restart BASIC |
| $A00C | BASIC keyword token table |
| $A052 | BASIC keyword action vector table |
| $A080 | BASIC function vector table |
| $A483 | Main BASIC loop (IMAIN default) |
| $A57C | Tokenize line (ICRNCH default) |
| $A71A | List tokens (IQPLOP default) |
| $A7E4 | Execute statement (IGONE default) |
| $AE86 | Evaluate expression (IEVAL default) |
| $B248 | BASIC error messages |
| $BF52-$BFFF | Unused area in BASIC ROM |

### 2.10 Upper RAM ($C000-$CFFF)

4KB of RAM that is **always** RAM for the CPU (never overlaid with ROM or I/O).
This is the only portion of upper memory that is never bank-switched. Commonly
used for machine-language programs, custom character sets, or data tables.

### 2.11 I/O Area / Character ROM / RAM ($D000-$DFFF)

This 4KB region is the most complex in the C64 memory map. Three different
things can appear here depending on bank switching:

1. **I/O Registers** (default: CHAREN=1 with LORAM or HIRAM=1)
2. **Character Generator ROM** (CHAREN=0 with LORAM or HIRAM=1)
3. **RAM** (when both LORAM=0 and HIRAM=0, regardless of CHAREN)

See Section 4 for the full I/O register map.

**Character ROM layout (when banked in at $D000-$DFFF):**

| Addr | Description |
|------|-------------|
| $D000-$D7FF | Upper-case / graphics character set (256 chars x 8 bytes) |
| $D800-$DFFF | Lower-case / upper-case character set (256 chars x 8 bytes) |

### 2.12 KERNAL ROM ($E000-$FFFF)

8KB KERNAL operating system ROM. Visible when HIRAM=1.

#### KERNAL Jump Table ($FF81-$FFF3)

The KERNAL provides a stable API through 39 jump table entries. These addresses
are guaranteed not to change across ROM revisions.

| Addr | Name | Description |
|------|------|-------------|
| $FF81 | SCINIT | Initialize VIC; restore default I/O to keyboard/screen |
| $FF84 | IOINIT | Initialize CIAs, SID volume; setup memory configuration |
| $FF87 | RAMTAS | Clear memory; test RAM; set BASIC work area pointers |
| $FF8A | RESTOR | Restore default I/O vectors ($0314-$0333) |
| $FF8D | VECTOR | Copy vector table to/from user table (C=0: read, C=1: write) |
| $FF90 | SETMSG | Set KERNAL message control flag ($009D) |
| $FF93 | LSTNSA | Send LISTEN secondary address to serial bus |
| $FF96 | TALKSA | Send TALK secondary address to serial bus |
| $FF99 | MEMTOP | Get/set top of memory (C=0: set, C=1: get) |
| $FF9C | MEMBOT | Get/set bottom of memory (C=0: set, C=1: get) |
| $FF9F | SCNKEY | Scan keyboard matrix and place key in buffer |
| $FFA2 | SETTMO | Set serial bus timeout (unused on C64) |
| $FFA5 | IECIN | Read byte from serial bus (returned in A) |
| $FFA8 | IECOUT | Write byte (in A) to serial bus |
| $FFAB | UNTALK | Send UNTALK command to serial bus |
| $FFAE | UNLSTN | Send UNLISTEN command to serial bus |
| $FFB1 | LISTEN | Send LISTEN command (device in A) to serial bus |
| $FFB4 | TALK | Send TALK command (device in A) to serial bus |
| $FFB7 | READST | Read I/O status word (ST) into A |
| $FFBA | SETLFS | Set logical file parameters (A=logical#, X=device#, Y=secondary) |
| $FFBD | SETNAM | Set filename (A=length, X/Y=pointer lo/hi) |
| $FFC0 | OPEN | Open a logical file |
| $FFC3 | CLOSE | Close a logical file (A=logical#) |
| $FFC6 | CHKIN | Set input channel (X=logical file#) |
| $FFC9 | CHKOUT | Set output channel (X=logical file#) |
| $FFCC | CLRCHN | Restore default input/output channels |
| $FFCF | CHRIN | Read byte from current input channel |
| $FFD2 | CHROUT | Write byte (in A) to current output channel |
| $FFD5 | LOAD | Load or verify file (A=0: load, A=1: verify; X/Y=address) |
| $FFD8 | SAVE | Save memory to file (A=zp pointer to start, X/Y=end address) |
| $FFDB | SETTIM | Set jiffy clock (A=hours, X=minutes, Y=seconds -- actually $A0-$A2) |
| $FFDE | RDTIM | Read jiffy clock into A/X/Y |
| $FFE1 | STOP | Check STOP key; Z=1 if pressed |
| $FFE4 | GETIN | Get one character from input device |
| $FFE7 | CLALL | Close all files; restore default I/O |
| $FFEA | UDTIM | Update jiffy clock and check STOP key |
| $FFED | SCREEN | Return screen size in X (columns=40) and Y (rows=25) |
| $FFF0 | PLOT | Get/set cursor position (C=0: set X=row Y=col, C=1: get) |
| $FFF3 | IOBASE | Return CIA #1 base address in X/Y ($DC00) |

#### Hardware Vectors ($FFFA-$FFFF)

| Addr | Description | Default |
|------|-------------|---------|
| $FFFA-$FFFB | NMI vector | $FE43 |
| $FFFC-$FFFD | RESET vector | $FCE2 |
| $FFFE-$FFFF | IRQ/BRK vector | $FF48 |

These are in ROM and cannot be changed (they are read from whatever is banked
in at those addresses). The KERNAL's IRQ and NMI handlers use the RAM vectors
at $0314 and $0318 for indirection, which is how user code hooks interrupts.

---

## 3. Bank Switching

### 3.1 Control Signals

Five signals control the C64's memory map, decoded by the PLA chip:

| Signal | Source | Active meaning |
|--------|--------|----------------|
| LORAM | $0001 bit 0 | 1 = BASIC ROM visible at $A000-$BFFF |
| HIRAM | $0001 bit 1 | 1 = KERNAL ROM visible at $E000-$FFFF |
| CHAREN | $0001 bit 2 | 1 = I/O at $D000-$DFFF; 0 = Character ROM |
| EXROM | Expansion port pin 9 | Active low; directly connected to PLA |
| GAME | Expansion port pin 8 | Active low; directly connected to PLA |

The three processor port bits give the CPU 8 configurations. The GAME and EXROM
lines (active low on the hardware, but represented as active-high in the mode
table) multiply this by 4, yielding 32 modes. Many produce identical results.

### 3.2 Complete Mode Table (All 32 Configurations)

The mode number equals the 5-bit value EXROM:GAME:CHAREN:HIRAM:LORAM. The
$0000-$0FFF region is always RAM. The $0000-$0001 range is always the CPU I/O
port.

| Mode | EXROM | GAME | CHAREN | HIRAM | LORAM | $1000-$7FFF | $8000-$9FFF | $A000-$BFFF | $C000-$CFFF | $D000-$DFFF | $E000-$FFFF |
|------|-------|------|--------|-------|-------|-------------|-------------|-------------|-------------|-------------|-------------|
| 31 | 1 | 1 | 1 | 1 | 1 | RAM | RAM | BASIC | RAM | I/O | KERNAL |
| 30 | 1 | 1 | 1 | 1 | 0 | RAM | RAM | RAM | RAM | I/O | KERNAL |
| 29 | 1 | 1 | 1 | 0 | 1 | RAM | RAM | RAM | RAM | I/O | RAM |
| 28 | 1 | 1 | 1 | 0 | 0 | RAM | RAM | RAM | RAM | RAM | RAM |
| 27 | 1 | 1 | 0 | 1 | 1 | RAM | RAM | BASIC | RAM | CHAR ROM | KERNAL |
| 26 | 1 | 1 | 0 | 1 | 0 | RAM | RAM | RAM | RAM | CHAR ROM | KERNAL |
| 25 | 1 | 1 | 0 | 0 | 1 | RAM | RAM | RAM | RAM | RAM | RAM |
| 24 | 1 | 1 | 0 | 0 | 0 | RAM | RAM | RAM | RAM | RAM | RAM |
| 23 | 1 | 0 | 1 | 1 | 1 | -- | CART LO | -- | -- | I/O | CART HI |
| 22 | 1 | 0 | 1 | 1 | 0 | -- | CART LO | -- | -- | I/O | CART HI |
| 21 | 1 | 0 | 1 | 0 | 1 | -- | CART LO | -- | -- | I/O | CART HI |
| 20 | 1 | 0 | 1 | 0 | 0 | -- | CART LO | -- | -- | I/O | CART HI |
| 19 | 1 | 0 | 0 | 1 | 1 | -- | CART LO | -- | -- | I/O | CART HI |
| 18 | 1 | 0 | 0 | 1 | 0 | -- | CART LO | -- | -- | I/O | CART HI |
| 17 | 1 | 0 | 0 | 0 | 1 | -- | CART LO | -- | -- | I/O | CART HI |
| 16 | 1 | 0 | 0 | 0 | 0 | -- | CART LO | -- | -- | I/O | CART HI |
| 15 | 0 | 1 | 1 | 1 | 1 | RAM | CART LO | BASIC | RAM | I/O | KERNAL |
| 14 | 0 | 1 | 1 | 1 | 0 | RAM | RAM | RAM | RAM | I/O | KERNAL |
| 13 | 0 | 1 | 1 | 0 | 1 | RAM | RAM | RAM | RAM | I/O | RAM |
| 12 | 0 | 1 | 1 | 0 | 0 | RAM | RAM | RAM | RAM | RAM | RAM |
| 11 | 0 | 1 | 0 | 1 | 1 | RAM | CART LO | BASIC | RAM | CHAR ROM | KERNAL |
| 10 | 0 | 1 | 0 | 1 | 0 | RAM | RAM | RAM | RAM | CHAR ROM | KERNAL |
| 9 | 0 | 1 | 0 | 0 | 1 | RAM | RAM | RAM | RAM | RAM | RAM |
| 8 | 0 | 1 | 0 | 0 | 0 | RAM | RAM | RAM | RAM | RAM | RAM |
| 7 | 0 | 0 | 1 | 1 | 1 | RAM | CART LO | CART HI | RAM | I/O | KERNAL |
| 6 | 0 | 0 | 1 | 1 | 0 | RAM | RAM | CART HI | RAM | I/O | KERNAL |
| 5 | 0 | 0 | 1 | 0 | 1 | RAM | RAM | RAM | RAM | I/O | RAM |
| 4 | 0 | 0 | 1 | 0 | 0 | RAM | RAM | RAM | RAM | RAM | RAM |
| 3 | 0 | 0 | 0 | 1 | 1 | RAM | CART LO | CART HI | RAM | CHAR ROM | KERNAL |
| 2 | 0 | 0 | 0 | 1 | 0 | RAM | RAM | CART HI | RAM | CHAR ROM | KERNAL |
| 1 | 0 | 0 | 0 | 0 | 1 | RAM | RAM | RAM | RAM | RAM | RAM |
| 0 | 0 | 0 | 0 | 0 | 0 | RAM | RAM | RAM | RAM | RAM | RAM |

`--` = Unmapped (open bus). These are **Ultimax** mode configurations (modes
16-23), where GAME=0 and EXROM=1. In Ultimax mode, the $1000-$7FFF, $A000-$BFFF,
and $C000-$CFFF regions are not mapped -- reads return open bus garbage and
writes go nowhere. This mode was designed for the MAX Machine.

### 3.3 Common Configurations for Programmers

**All RAM ($0001 = $34, modes 28/24):**
```
LDA #$34
STA $01        ; HIRAM=0, LORAM=0, CHAREN=1 (but moot)
               ; Full 64KB RAM visible, I/O NOT accessible
               ; Interrupts MUST be disabled first (SEI)
```

**RAM + I/O ($0001 = $35, mode 29):**
```
LDA #$35
STA $01        ; HIRAM=0, LORAM=1, CHAREN=1
               ; RAM everywhere except I/O at $D000-$DFFF
               ; KERNAL not available -- handle IRQs yourself
```

**RAM + KERNAL + I/O ($0001 = $36, mode 30):**
```
LDA #$36
STA $01        ; HIRAM=1, LORAM=0, CHAREN=1
               ; BASIC ROM replaced by RAM at $A000-$BFFF
               ; KERNAL and I/O still visible
```

**Default ($0001 = $37, mode 31):**
```
; BASIC + KERNAL + I/O -- the power-on default
```

**Character ROM visible ($0001 = $33, mode 27):**
```
SEI
LDA #$33
STA $01        ; CHAREN=0: Character ROM at $D000-$DFFF
               ; I/O NOT accessible -- must disable interrupts
; ... read character data ...
LDA #$37
STA $01        ; Restore default
CLI
```

### 3.4 VIC-II Bank Selection

The VIC-II has a 14-bit address bus, allowing it to address 16KB at a time.
CIA #2 Port A ($DD00) bits 0-1 select which 16KB bank the VIC sees. The bits
are **inverted**: the bank number = value XOR 3.

| $DD00 bits 1-0 | Bank | VIC address range | Notes |
|-----------------|------|-------------------|-------|
| %11 (3) | 0 | $0000-$3FFF | Default. Character ROM visible at VIC addr $1000-$1FFF |
| %10 (2) | 1 | $4000-$7FFF | No character ROM substitute |
| %01 (1) | 2 | $8000-$BFFF | Character ROM visible at VIC addr $1000-$1FFF |
| %00 (0) | 3 | $C000-$FFFF | No character ROM substitute |

**Important:** The VIC-II **never sees** BASIC ROM, KERNAL ROM, or I/O
registers. It sees only RAM, except in banks 0 and 2 where Character ROM
appears at the $1000-$1FFF offset within the bank. To use custom characters
in banks 1 or 3, you must copy character data into RAM.

To switch VIC banks:
```
LDA $DD02
ORA #$03       ; Set PA0, PA1 as output
STA $DD02
LDA $DD00
AND #$FC       ; Clear bank bits
ORA #$01       ; Bank 2: $8000-$BFFF
STA $DD00
```

---

## 4. I/O Register Map ($D000-$DFFF)

When I/O is visible (CHAREN=1 with HIRAM or LORAM=1), this 4KB region contains
the hardware registers for all C64 I/O chips.

### 4.1 VIC-II Video Controller ($D000-$D3FF)

The VIC-II (MOS 6567 NTSC / 6569 PAL) has 47 registers. They are mirrored
every 64 bytes throughout $D000-$D3FF (i.e., register 0 appears at $D000,
$D040, $D080, ... $D3C0).

#### Sprite Position Registers ($D000-$D010)

| Addr | Register | Bits | Description |
|------|----------|------|-------------|
| $D000 | SP0X | 7-0 | Sprite 0 X position (low 8 bits) |
| $D001 | SP0Y | 7-0 | Sprite 0 Y position |
| $D002 | SP1X | 7-0 | Sprite 1 X position (low 8 bits) |
| $D003 | SP1Y | 7-0 | Sprite 1 Y position |
| $D004 | SP2X | 7-0 | Sprite 2 X position (low 8 bits) |
| $D005 | SP2Y | 7-0 | Sprite 2 Y position |
| $D006 | SP3X | 7-0 | Sprite 3 X position (low 8 bits) |
| $D007 | SP3Y | 7-0 | Sprite 3 Y position |
| $D008 | SP4X | 7-0 | Sprite 4 X position (low 8 bits) |
| $D009 | SP4Y | 7-0 | Sprite 4 Y position |
| $D00A | SP5X | 7-0 | Sprite 5 X position (low 8 bits) |
| $D00B | SP5Y | 7-0 | Sprite 5 Y position |
| $D00C | SP6X | 7-0 | Sprite 6 X position (low 8 bits) |
| $D00D | SP6Y | 7-0 | Sprite 6 Y position |
| $D00E | SP7X | 7-0 | Sprite 7 X position (low 8 bits) |
| $D00F | SP7Y | 7-0 | Sprite 7 Y position |
| $D010 | MSIGX | 7-0 | MSB of X coordinate for sprites 0-7 (bit N = sprite N) |

Sprite X range: 0-511 (9 bits); Y range: 0-255 (8 bits). Visible screen area
is approximately X: 24-343, Y: 50-249 (PAL) or Y: 50-229 (NTSC).

#### Control Registers ($D011-$D016)

| Addr | Register | Bit | Name | Description |
|------|----------|-----|------|-------------|
| $D011 | CR1 | 7 | RST8 | Raster compare bit 8 (bit 9 of raster counter) |
| | | 6 | ECM | Extended Color Mode (1=enable) |
| | | 5 | BMM | Bitmap Mode (1=enable) |
| | | 4 | DEN | Display Enable (1=screen on, 0=border only) |
| | | 3 | RSEL | Row Select: 1=25 rows, 0=24 rows (trims top/bottom border) |
| | | 2-0 | YSCROLL | Vertical fine scroll (0-7 pixels) |
| $D012 | RASTER | 7-0 | | Read: current raster line (bits 0-7). Write: raster compare value (bits 0-7). |
| $D013 | LPX | 7-0 | | Light pen X coordinate (read only) |
| $D014 | LPY | 7-0 | | Light pen Y coordinate (read only) |
| $D016 | CR2 | 7-6 | | Unused (read as 1) |
| | | 5 | RES | Reset bit (normally 0; setting to 1 halts VIC) |
| | | 4 | MCM | Multi-Color Mode (1=enable) |
| | | 3 | CSEL | Column Select: 1=40 columns, 0=38 columns (trims left/right border) |
| | | 2-0 | XSCROLL | Horizontal fine scroll (0-7 pixels) |

**Display modes** (combinations of ECM, BMM, MCM):

| ECM | BMM | MCM | Mode |
|-----|-----|-----|------|
| 0 | 0 | 0 | Standard character mode (default) |
| 0 | 0 | 1 | Multi-color character mode |
| 0 | 1 | 0 | Standard bitmap mode (hires) |
| 0 | 1 | 1 | Multi-color bitmap mode |
| 1 | 0 | 0 | Extended background color mode |
| 1 | 0 | 1 | Invalid -- displays black (ECM+MCM) |
| 1 | 1 | 0 | Invalid -- displays black (ECM+BMM) |
| 1 | 1 | 1 | Invalid -- displays black (ECM+BMM+MCM) |

#### Sprite Enable and Expansion ($D015, $D017, $D01D)

| Addr | Register | Description |
|------|----------|-------------|
| $D015 | SPENA | Sprite enable: bit N enables sprite N |
| $D017 | YXPAND | Sprite Y expansion (double height): bit N = sprite N |
| $D01D | XXPAND | Sprite X expansion (double width): bit N = sprite N |

#### Memory Pointers ($D018)

| Addr | Register | Bit | Description |
|------|----------|-----|-------------|
| $D018 | VMCSB | 7-4 | Video matrix base address. Multiply by $0400 to get offset within VIC bank. E.g., %0001 = $0400 (default). |
| | | 3 | Bitmap mode: bitmap base address. 0 = $0000, 1 = $2000 within VIC bank. |
| | | 3-1 | Character mode: character generator base address. Multiply by $0800 to get offset within VIC bank. Default: %010 = $1000 (points to Character ROM in bank 0). |
| | | 0 | Unused |

Default value: $15 = %00010101.
- Bits 7-4 = %0001 -> screen at $0400 in bank 0
- Bits 3-1 = %010 -> characters at $1000 in bank 0 (= Character ROM)

#### Interrupt Registers ($D019-$D01A)

| Addr | Register | Bit | Name | Description |
|------|----------|-----|------|-------------|
| $D019 | IRQST | 7 | IRQ | Latched: 1 if any enabled interrupt source is active |
| | | 3 | ILP | Light pen triggered interrupt |
| | | 2 | IMMC | Sprite-sprite collision interrupt |
| | | 1 | IMBC | Sprite-background collision interrupt |
| | | 0 | IRST | Raster line match interrupt |
| $D01A | IRQEN | 3 | ELP | Enable light pen interrupt |
| | | 2 | EMMC | Enable sprite-sprite collision interrupt |
| | | 1 | EMBC | Enable sprite-background collision interrupt |
| | | 0 | ERST | Enable raster interrupt |

**$D019 is read-to-acknowledge**: reading returns the interrupt status; writing
a 1 to any bit clears (acknowledges) that interrupt source. You must acknowledge
by writing 1s to the bits you want to clear.

#### Sprite Properties ($D01B-$D01C)

| Addr | Register | Description |
|------|----------|-------------|
| $D01B | SPBGPR | Sprite-to-background priority: 0=sprite in front, 1=background in front |
| $D01C | SPMC | Sprite multicolor mode: 1=multicolor, 0=hires |

#### Collision Detection ($D01E-$D01F)

| Addr | Register | Description |
|------|----------|-------------|
| $D01E | SPSPCL | Sprite-sprite collision (read only). Bit set for each sprite involved. **Cleared on read.** |
| $D01F | SPBGCL | Sprite-background collision (read only). Bit set for each sprite that collided with background data. **Cleared on read.** |

#### Color Registers ($D020-$D02E)

| Addr | Register | Description |
|------|----------|-------------|
| $D020 | EXTCOL | Border color (bits 3-0, bits 7-4 unused) |
| $D021 | BGCOL0 | Background color 0 |
| $D022 | BGCOL1 | Background color 1 (multicolor/ECM) |
| $D023 | BGCOL2 | Background color 2 (ECM) |
| $D024 | BGCOL3 | Background color 3 (ECM) |
| $D025 | SPMC0 | Sprite multicolor 0 (shared by all sprites) |
| $D026 | SPMC1 | Sprite multicolor 1 (shared by all sprites) |
| $D027 | SP0COL | Sprite 0 color |
| $D028 | SP1COL | Sprite 1 color |
| $D029 | SP2COL | Sprite 2 color |
| $D02A | SP3COL | Sprite 3 color |
| $D02B | SP4COL | Sprite 4 color |
| $D02C | SP5COL | Sprite 5 color |
| $D02D | SP6COL | Sprite 6 color |
| $D02E | SP7COL | Sprite 7 color |

Registers $D02F-$D03F are unused and read as $FF.

### 4.2 SID Sound Interface Device ($D400-$D7FF)

The SID (MOS 6581/8580) has 29 registers. They are mirrored every 32 bytes
throughout $D400-$D7FF (in the original C64; the C128 does not mirror).

Most SID registers are **write-only**. Only $D419-$D41C are readable.

#### Voice 1 ($D400-$D406)

| Addr | Register | R/W | Description |
|------|----------|-----|-------------|
| $D400 | FRELO1 | W | Frequency low byte |
| $D401 | FREHI1 | W | Frequency high byte |
| $D402 | PWLO1 | W | Pulse width low byte (bits 7-0) |
| $D403 | PWHI1 | W | Pulse width high byte (bits 3-0 only; 12-bit total) |
| $D404 | VCREG1 | W | Voice 1 control register (see below) |
| $D405 | ATDCY1 | W | Attack (bits 7-4) / Decay (bits 3-0) |
| $D406 | SUREL1 | W | Sustain level (bits 7-4) / Release (bits 3-0) |

**Control register ($D404) bits:**

| Bit | Name | Description |
|-----|------|-------------|
| 7 | NOISE | Noise waveform |
| 6 | PULSE | Pulse waveform |
| 5 | SAW | Sawtooth waveform |
| 4 | TRI | Triangle waveform |
| 3 | TEST | Test bit: 1=reset oscillator, lock at zero |
| 2 | RING | Ring modulation with voice 3's oscillator |
| 1 | SYNC | Hard sync with voice 3's oscillator |
| 0 | GATE | Gate: 1=start attack/decay/sustain, 0=start release |

Multiple waveform bits can be set simultaneously (combined waveforms produce
AND-like results of the individual waveform outputs; commonly exploited for
sound effects).

#### Voice 2 ($D407-$D40D)

Identical structure to Voice 1. Ring modulation and sync reference Voice 1.

| Addr | Register | R/W | Description |
|------|----------|-----|-------------|
| $D407 | FRELO2 | W | Frequency low byte |
| $D408 | FREHI2 | W | Frequency high byte |
| $D409 | PWLO2 | W | Pulse width low byte |
| $D40A | PWHI2 | W | Pulse width high byte (bits 3-0) |
| $D40B | VCREG2 | W | Voice 2 control register |
| $D40C | ATDCY2 | W | Attack/Decay |
| $D40D | SUREL2 | W | Sustain/Release |

#### Voice 3 ($D40E-$D414)

Identical structure to Voice 1. Ring modulation and sync reference Voice 2.

| Addr | Register | R/W | Description |
|------|----------|-----|-------------|
| $D40E | FRELO3 | W | Frequency low byte |
| $D40F | FREHI3 | W | Frequency high byte |
| $D410 | PWLO3 | W | Pulse width low byte |
| $D411 | PWHI3 | W | Pulse width high byte (bits 3-0) |
| $D412 | VCREG3 | W | Voice 3 control register |
| $D413 | ATDCY3 | W | Attack/Decay |
| $D414 | SUREL3 | W | Sustain/Release |

#### Filter and Volume ($D415-$D418)

| Addr | Register | R/W | Bit | Description |
|------|----------|-----|-----|-------------|
| $D415 | CUTLO | W | 2-0 | Filter cutoff frequency low 3 bits |
| $D416 | CUTHI | W | 7-0 | Filter cutoff frequency high 8 bits (11-bit total) |
| $D417 | RESSION | W | 7-4 | Filter resonance (0-15) |
| | | | 3 | FILTEX: Filter external input |
| | | | 2 | FILT3: Filter voice 3 |
| | | | 1 | FILT2: Filter voice 2 |
| | | | 0 | FILT1: Filter voice 1 |
| $D418 | SIGVOL | W | 7 | Mute voice 3 output (1=disconnected from output) |
| | | | 6 | HP: High-pass filter |
| | | | 5 | BP: Band-pass filter |
| | | | 4 | LP: Low-pass filter |
| | | | 3-0 | Master volume (0-15) |

#### Read-Only Registers ($D419-$D41C)

| Addr | Register | R/W | Description |
|------|----------|-----|-------------|
| $D419 | POTX | R | Paddle X value (active paddle selected by CIA1 PA6-PA7) |
| $D41A | POTY | R | Paddle Y value |
| $D41B | RANDOM | R | Voice 3 oscillator output (upper 8 bits). Often used as random number source. |
| $D41C | ENV3 | R | Voice 3 envelope generator output (8 bits) |

### 4.3 Color RAM ($D800-$DBFF)

| Addr | Description |
|------|-------------|
| $D800-$DBE7 | Color data for screen matrix (1000 nybbles). Each byte corresponds to a character position on screen. Only bits 0-3 are significant (4-bit color value 0-15). Bits 4-7 are undefined on reads (random/noise on 6510 bus). |
| $DBE8-$DBFF | Additional color RAM (24 bytes, not normally displayed) |

Color RAM is a dedicated 1Kx4-bit static RAM chip (2114 type). It is **not part
of the main 64KB DRAM**. It is wired directly to VIC-II data pins D8-D11 via a
private 4-bit bus. The CPU accesses it through the main data bus during I/O
mode, but only the low 4 bits are connected.

Color RAM is **always** at $D800-$DBFF for the CPU when I/O is mapped in. It
does not move with VIC bank changes. The VIC reads it directly by asserting its
address lines.

### 4.4 CIA #1 -- Keyboard, Joystick, IRQ ($DC00-$DCFF)

The CIA (MOS 6526 Complex Interface Adapter) has 16 registers. CIA #1 is
mirrored every 16 bytes throughout $DC00-$DCFF.

CIA #1 interrupt output is connected to the CPU's IRQ line.

| Addr | Register | R/W | Description |
|------|----------|-----|-------------|
| $DC00 | PRA | R/W | Port A: keyboard column select (directly selecting keyboard matrix column); joystick port 2 input; paddle select |
| $DC01 | PRB | R/W | Port B: keyboard row read; joystick port 1 input; timer A/B output |
| $DC02 | DDRA | R/W | Data Direction Register A (1=output, 0=input). Default: $FF |
| $DC03 | DDRB | R/W | Data Direction Register B. Default: $00 |
| $DC04 | TALO | R/W | Timer A low byte (read=counter, write=latch) |
| $DC05 | TAHI | R/W | Timer A high byte (read=counter, write=latch; writing also transfers latch to counter if timer is stopped) |
| $DC06 | TBLO | R/W | Timer B low byte |
| $DC07 | TBHI | R/W | Timer B high byte |
| $DC08 | TOD10 | R/W | TOD clock 1/10 seconds (BCD, bits 3-0). Read latches TOD; write sets TOD or alarm. |
| $DC09 | TODSEC | R/W | TOD seconds (BCD: tens in bits 6-4, units in bits 3-0) |
| $DC0A | TODMIN | R/W | TOD minutes (BCD format) |
| $DC0B | TODHR | R/W | TOD hours (BCD: bit 7=PM flag, bits 4-0=hours). Reading unlatches TOD. |
| $DC0C | SDR | R/W | Serial Data Register (directly shifts 8 bits in/out on SP/CNT pins) |
| $DC0D | ICR | R/W | Interrupt Control Register (see below) |
| $DC0E | CRA | R/W | Control Register A (see below) |
| $DC0F | CRB | R/W | Control Register B (see below) |

**ICR ($DC0D) -- Read (status, clears on read):**

| Bit | Name | Description |
|-----|------|-------------|
| 7 | IR | 1 = at least one interrupt source is active AND enabled |
| 4 | FLG | FLAG pin interrupt (accent grave key on keyboard connector) |
| 3 | SP | Serial data register full/empty |
| 2 | ALARM | TOD alarm match |
| 1 | TB | Timer B underflow |
| 0 | TA | Timer A underflow |

**ICR ($DC0D) -- Write (mask):**

| Bit | Description |
|-----|-------------|
| 7 | Source bit: 1=SET mask bits, 0=CLEAR mask bits |
| 4-0 | Same sources as read; 1 in any position sets/clears that mask |

**CRA ($DC0E):**

| Bit | Name | Description |
|-----|------|-------------|
| 7 | TODIN | TOD clock source: 0=60Hz, 1=50Hz |
| 6 | SPMODE | Serial port direction: 0=input, 1=output |
| 5 | INMODE | Timer A input: 0=system clock (phi2), 1=CNT pin |
| 4 | LOAD | Force load: 1=transfer latch to counter (strobe, always reads 0) |
| 3 | RUNMODE | 0=continuous, 1=one-shot |
| 2 | OUTMODE | Port B bit 6 output: 0=pulse, 1=toggle |
| 1 | PBON | 1=Timer A output on Port B bit 6 |
| 0 | START | 1=start timer A |

**CRB ($DC0F):**

| Bit | Name | Description |
|-----|------|-------------|
| 7 | ALARM | TOD write mode: 0=set clock, 1=set alarm |
| 6-5 | INMODE | Timer B input: 00=phi2 clock, 01=CNT pin, 10=Timer A underflow, 11=Timer A underflow while CNT high |
| 4 | LOAD | Force load latch to counter |
| 3 | RUNMODE | 0=continuous, 1=one-shot |
| 2 | OUTMODE | Port B bit 7 output: 0=pulse, 1=toggle |
| 1 | PBON | 1=Timer B output on Port B bit 7 |
| 0 | START | 1=start timer B |

**Keyboard Matrix Scanning:**

CIA #1 Port A ($DC00) selects keyboard columns (active low); Port B ($DC01)
reads rows (0=key pressed). The 64 keys form an 8x8 matrix. To scan a specific
key, write the column select pattern to Port A and read the row result from
Port B.

**Joystick Connections:**

| Port | Register | Bits | Direction |
|------|----------|------|-----------|
| Joystick 2 | $DC00 (PRA) | Bit 0=up, 1=down, 2=left, 3=right, 4=fire | 0=active |
| Joystick 1 | $DC01 (PRB) | Same bit mapping | 0=active |

### 4.5 CIA #2 -- Serial Bus, VIC Bank, NMI ($DD00-$DDFF)

CIA #2 has the same register structure as CIA #1. Its interrupt output is
connected to the CPU's **NMI** line (not IRQ).

| Addr | Register | R/W | Description |
|------|----------|-----|-------------|
| $DD00 | PRA | R/W | Port A: VIC bank (bits 0-1), serial bus (bits 2-7), RS-232 output |
| $DD01 | PRB | R/W | Port B: User port data, RS-232 signals |
| $DD02 | DDRA | R/W | Data Direction Register A. Default: $3F |
| $DD03 | DDRB | R/W | Data Direction Register B. Default: $00 |
| $DD04-$DD07 | | | Timer A/B (same as CIA #1) |
| $DD08-$DD0B | | | TOD clock (same as CIA #1) |
| $DD0C | SDR | R/W | Serial Data Register |
| $DD0D | ICR | R/W | Interrupt Control Register (triggers NMI, not IRQ) |
| $DD0E-$DD0F | | | Control Registers A/B (same as CIA #1) |

**Port A ($DD00) bit definitions:**

| Bit | Name | Description |
|-----|------|-------------|
| 0-1 | VIC Bank | VIC-II bank selection (inverted: 11=bank 0, 00=bank 3) |
| 2 | RS232 TXD | RS-232 data output |
| 3 | ATN OUT | Serial bus ATN signal output |
| 4 | CLK OUT | Serial bus CLOCK output |
| 5 | DATA OUT | Serial bus DATA output |
| 6 | CLK IN | Serial bus CLOCK input (active low) |
| 7 | DATA IN | Serial bus DATA input (active low) |

### 4.6 Expansion I/O ($DE00-$DFFF)

| Addr | Description |
|------|-------------|
| $DE00-$DEFF | I/O Area 1 -- directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly -- directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly  directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly generated directly by directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly cartridge directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly connected directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly to expansion port directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly directly for directly directly directly directly directly directly cartridge I/O registers |
| $DF00-$DFFF | I/O Area 2 -- directly connected to expansion port for cartridge I/O registers |

These areas generate active I/O1 and I/O2 select signals on the expansion port
when accessed. Cartridge hardware uses these for bank switching registers,
configuration registers, etc.

---

## 5. Hardcore Details

### 5.1 VIC-II / CPU Bus Sharing and Cycle Timing

The C64 uses a two-phase clock system. The VIC-II is the bus master that
generates the system clock and controls CPU bus access.

**Phase 1 (phi1, phi2 low):** VIC-II accesses the bus for its own needs
(character pointers, graphics data, sprite data, DRAM refresh).

**Phase 2 (phi2 high):** CPU accesses the bus for its instruction execution.

Under normal conditions, VIC and CPU alternate access every half-cycle, giving
the CPU access on every cycle. The VIC uses phase 1 for its c-accesses
(character/color reads) and g-accesses (graphics data reads).

#### Clock Rates

| System | VIC Chip | Phi2 Clock | Cycles/Line | Lines/Frame |
|--------|----------|------------|-------------|-------------|
| NTSC | 6567R8 | 1.022727 MHz | 65 | 263 |
| PAL | 6569 | 0.985248 MHz | 63 | 312 |

#### Bad Lines

A **bad line** occurs when all of the following are true at the falling edge of
phi1:

1. RASTER >= $30 and RASTER <= $F7
2. The low 3 bits of RASTER equal YSCROLL (bits 0-2 of $D011)
3. DEN (bit 4 of $D011) was set during some cycle of raster line $30

When a bad line occurs, the VIC needs to fetch 40 bytes of character pointers
(c-accesses) in addition to its normal graphics data fetches. Since phase 1
alone cannot accommodate this, the VIC **steals phase 2 cycles from the CPU**
by asserting BA low (Bus Available = no) and then AEC low (Address Enable
Control).

The CPU is stunned for **40-43 cycles** on a bad line (exact count depends on
the instruction being executed when BA goes low). This is because BA goes low
3 cycles before the VIC actually needs the bus, to account for the 6510's
write-cycle pipeline (the 6510 can complete up to 3 write cycles after RDY goes
low before actually stopping).

A normal raster line uses 40 phase-1 cycles for g-accesses. A bad line uses
40 phase-1 cycles for g-accesses plus 40 phase-2 cycles for c-accesses,
effectively halving CPU throughput for that line.

#### Sprite DMA Stealing

Each enabled sprite requires additional bus cycles:

1. **p-access (sprite pointer):** 1 cycle to read the sprite data pointer from
   the screen matrix area (at VIC address VM + $03F8 + sprite#). This happens
   every raster line for each enabled sprite.

2. **s-access (sprite data):** 3 consecutive cycles to read 3 bytes of sprite
   data. This occurs only on raster lines where the sprite is being displayed.

BA goes low 3 cycles before the s-accesses begin. Including the stun delay:

| Sprites active | Cycles stolen from CPU per line |
|---------------|-------------------------------|
| 0 | 0 |
| 1 | 2 (stun delay) + 3 (data) = ~5 |
| 2 | ~7 (overlapping stun windows) |
| 8 | ~40 |

When all 8 sprites are active on one line AND it is a bad line, the CPU gets
almost no cycles.

#### VIC-II Access Pattern Per Raster Line

On a PAL system (63 cycles per line), the VIC performs these accesses in
phase 1:

```
Cycle 1-5:     5 DRAM refresh accesses (r-access)
Cycle 6-9:     Sprite 3,4,5,6 pointer accesses (p-access, if needed)
Cycle 10:      Sprite 7 pointer access
Cycle 11:      Idle
Cycle 12-14:   Sprite 0,1,2 pointer accesses
Cycle 15-54:   40 g-accesses (graphics data fetches)
                On bad lines: also 40 c-accesses in phase 2
Cycle 55-57:   Sprite 0,1,2 s-accesses
Cycle 58-63:   Sprite 3,4 s-accesses
```

(Exact cycle assignments vary slightly; consult Christian Bauer's VIC-II article
for the definitive per-cycle breakdown.)

### 5.2 Ghost Bytes and Open Bus Behavior

The 6510 CPU performs a memory access on **every** clock cycle, even during
internal operations (like the extra cycle in indexed addressing page crosses).
When the CPU does not need to read meaningful data, it still drives the address
bus and reads whatever appears on the data bus. These are called **ghost reads**
or **phantom accesses**.

Similarly, the VIC-II accesses memory on every phase-1 cycle. During idle
lines (above or below the display area), the VIC performs "idle accesses" that
read from a fixed address:

- In normal modes: $3FFF within the current VIC bank
- In ECM mode: $39FF within the current VIC bank

The byte read during idle accesses is still processed by the VIC's graphics
pipeline and can produce visible artifacts if the memory at that address
contains nonzero data.

**Reading unmapped addresses:** In Ultimax mode (modes 16-23), accessing
unmapped regions ($1000-$7FFF, $A000-$CFFF) results in open bus behavior. The
data bus retains the value from the previous access (bus capacitance), so reads
return semi-random values related to preceding bus activity.

**Reading write-only registers:** Reading SID write-only registers ($D400-$D418)
returns the value of the last byte written to the SID's data bus (for the 6581)
or $00 (for the 8580). Reading unused VIC-II registers ($D02F-$D03F) returns
$FF.

### 5.3 Transparent ROM Access (Reading ROM from Underneath)

The CPU can read ROM content even when RAM is banked in, through a technique
exploiting the bank-switch mechanism:

1. Bank in ROM by setting the appropriate processor port bits
2. Read the desired byte from the ROM address
3. Bank RAM back in

This must be done with interrupts disabled if the ROM region overlaps I/O,
since interrupt handlers typically need I/O access.

```asm
; Read a byte from KERNAL ROM while normally running in all-RAM mode
; Assumes code is running with $0001 = $35 (RAM + I/O)
SEI
LDA $01
PHA             ; Save current config
LDA #$37        ; KERNAL + BASIC + I/O
STA $01
LDA $FFD2       ; Read from KERNAL ROM
STA $FB         ; Store somewhere safe
PLA
STA $01         ; Restore previous config
CLI
```

However, there is an important subtlety: **the bank switch takes effect
immediately on the next read cycle**. Since `STA $01` is a write instruction,
the very next read (the opcode fetch of the following instruction) will see
the new mapping. This means you cannot have your code itself in a region that
changes mapping, or the CPU will fetch garbage.

### 5.4 RAM Under ROM -- Writing and Reading

**Writing to ROM addresses always writes to underlying RAM.** This is a
fundamental design feature: the PLA only controls what the CPU *reads*. All
writes go to DRAM regardless of banking configuration.

This means you can store data or code "under" ROM without ever banking it in:

```asm
; Write to RAM at $A000 while BASIC ROM is visible
LDA #$EA        ; NOP opcode
STA $A000       ; Writes to RAM, NOT to BASIC ROM
; The BASIC ROM is unaffected; RAM now has $EA at $A000
```

**Reading RAM under ROM** requires temporarily switching the bank:

```asm
; Read RAM at $E000 (under KERNAL ROM)
SEI
LDA #$34        ; All RAM, no I/O
STA $01
LDA $E000       ; Reads from RAM, not KERNAL ROM
STA $FB
LDA #$37        ; Restore default
STA $01
CLI
```

**Common use cases:**

1. **Copy ROM to RAM, then modify:** Copy KERNAL or BASIC ROM to the underlying
   RAM, patch specific routines, then bank out ROM. The CPU now executes your
   modified version from RAM.

2. **Store ML code under BASIC ROM ($A000-$BFFF):** BASIC programs can SYS to
   routines that bank out BASIC ROM and execute from the RAM beneath it, gaining
   8KB of code space invisible to BASIC.

3. **Use all 64KB as data storage:** With appropriate bank switching, every byte
   of the 64KB DRAM is accessible for data, even the portions normally hidden
   under ROM.

### 5.5 Bank Switching Timing and Caveats

**Immediate effect:** Writes to $0001 take effect on the next bus cycle. If the
code performing the write is in a region affected by the switch (e.g., code at
$E000 banking out KERNAL), the CPU will fetch the next opcode from RAM instead
of ROM. This is a feature, not a bug, but requires careful planning.

**Three-cycle write safety:** The 6510 can execute up to 3 write cycles after
RDY is deasserted. This means that a `STA $01` instruction that changes banking
will complete its write and the CPU will not be interrupted by VIC DMA in the
middle of the bank switch. However, the *next* instruction's opcode fetch can
be affected by VIC DMA and will see the new banking.

**Interrupt considerations:**

- The default IRQ handler reads from CIA #1 at $DC0D (I/O space). If you bank
  out I/O, you must either disable interrupts (SEI) or install a custom IRQ
  handler that banks I/O back in before accessing hardware.
- NMI cannot be disabled by SEI. If you must run code with I/O banked out and
  need NMI protection, you can set CIA #2 ICR ($DD0D) to disable all NMI
  sources. However, the NMI edge trigger means a pending NMI will still fire
  once when you re-enable.
- The RESTORE key triggers NMI via CIA #2. With I/O banked out, the NMI
  handler at $FE43 (ROM) or your custom handler at ($0318) must handle the
  situation.

**VIC-II bank switch atomicity:** Changing CIA #2 PA0-PA1 ($DD00) affects VIC
bank selection immediately. If done mid-frame, the VIC will begin fetching from
the new bank on the next VIC access cycle. This can cause visible glitches
for one scan line. To avoid artifacts, change VIC banks during the vertical
blanking interval.

### 5.6 VIC-II Memory View vs. CPU Memory View

The CPU and VIC-II see different memory maps simultaneously:

| Address range | CPU sees | VIC sees |
|---------------|----------|----------|
| $0000-$0FFF | Always RAM | RAM (in bank 0 or 2) |
| $1000-$1FFF | RAM | Character ROM (banks 0 and 2) or RAM (banks 1 and 3) |
| $A000-$BFFF | BASIC ROM, CART, or RAM | RAM only (in bank 2) |
| $D000-$DFFF | I/O, CHAR ROM, or RAM | RAM only (in bank 3) |
| $E000-$FFFF | KERNAL ROM or RAM | RAM only (in bank 3) |

The VIC-II never sees ROM (except Character ROM at bank-relative $1000-$1FFF in
banks 0 and 2) or I/O registers. It always sees RAM. This means:

- If you want the VIC to display data from $A000-$BFFF (bank 2), the data must
  be in RAM, not in the BASIC ROM that the CPU sees at those addresses.
- Screen memory, bitmap data, and sprite data must all reside in RAM within the
  selected VIC bank.
- Character ROM is only accessible to VIC at $1000-$1FFF offset in banks 0 and
  2 (absolute addresses $1000-$1FFF and $9000-$9FFF).

### 5.7 The $D018 Pointer Arithmetic

VIC register $D018 controls where the VIC looks for screen data and character/
bitmap data within its 16KB bank.

```
Bit 7 6 5 4   3 2 1   0
    |VM13-VM10| CB13-CB11| -

Screen base = (($D018 >> 4) & $0F) * $0400 + bank_base
Char base   = (($D018 >> 1) & $07) * $0800 + bank_base
Bitmap base = (($D018 >> 3) & $01) * $2000 + bank_base
```

**Sprite pointer calculation:**

Sprite data pointers are stored at the last 8 bytes of the 1KB screen
matrix area:

```
Pointer address = screen_base + $03F8 + sprite_number
Sprite data address = pointer_value * 64 + bank_base
```

---

## References

1. **Joe Forster/STA's C64 Memory Map**
   https://sta.c64.org/cbm64mem.html

2. **C64-Wiki: Memory Map**
   https://www.c64-wiki.com/wiki/Memory_Map

3. **C64-Wiki: Bank Switching**
   https://www.c64-wiki.com/wiki/Bank_Switching

4. **C64-Wiki: Zeropage**
   https://www.c64-wiki.com/wiki/Zeropage

5. **C64-Wiki: CIA**
   https://www.c64-wiki.com/wiki/CIA

6. **C64-Wiki: SID**
   https://www.c64-wiki.com/wiki/SID

7. **C64-Wiki: Color RAM**
   https://www.c64-wiki.com/wiki/Color_RAM

8. **Ultimate C64 Memory Map (Michael Steil / pagetable.com)**
   https://www.pagetable.com/c64ref/c64mem/

9. **Christian Bauer: The MOS 6567/6569 VIC-II and its application in the C64**
   https://www.zimmers.net/cbmpics/cbm/c64/vic-ii.txt

10. **Zimmers.net: C64 Memory Map text file**
    https://www.zimmers.net/anonftp/pub/cbm/maps/C64.MemoryMap.txt

11. **Joe Forster/STA: C64 Standard KERNAL Functions**
    https://sta.c64.org/cbm64krnfunc.html

12. **Codebase64 Wiki: Memory Management**
    https://codebase64.c64.org/doku.php?id=base:memmanage

13. **Commodore 64 Programmer's Reference Guide** (Commodore, 1982)
    Chapter 5: "BASIC to Machine Language" and Appendix G: "Memory Map"

14. **Mapping the Commodore 64** by Sheldon Leemon (COMPUTE! Books, 1984)
