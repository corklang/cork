# Modern Cross-Development Tools and Workflow for the Commodore 64

Comprehensive reference covering cross-assemblers, C compilers, higher-level languages,
emulators, graphics tools, music tools, build systems, debugging techniques, and advanced
toolchain integration for modern Commodore 64 development.


---

## 1. Overview

Modern Commodore 64 development has evolved dramatically from the days of typing assembly
mnemonics into a native monitor or BASIC editor on the machine itself. Today's C64
developers use powerful cross-development workflows: writing code on modern hardware (PC,
Mac, Linux), assembling or compiling it with cross-tools, and testing in cycle-accurate
emulators -- with the real hardware reserved for final verification.

### 1.1 The Cross-Development Model

The fundamental workflow is:

1. **Write** source code in a modern editor (VS Code, Sublime Text, Vim, IntelliJ) with
   syntax highlighting, auto-completion, and project management.
2. **Assemble or compile** using a cross-assembler or cross-compiler that runs natively on
   the host OS but produces 6502/6510 machine code.
3. **Test** in an emulator (typically VICE) that provides cycle-accurate execution, a
   built-in monitor/debugger, and automated launch capabilities.
4. **Debug** using the emulator's monitor, external debuggers (IceBroLite), label files
   exported from the assembler, and profiling tools.
5. **Deploy** to real hardware via SD2IEC, Kung Fu Flash, EasyFlash, Ultimate 64, or
   1541 Ultimate for final testing and distribution.

### 1.2 Why Cross-Development?

| Aspect                | Native Development           | Cross-Development               |
|-----------------------|------------------------------|----------------------------------|
| Edit speed            | Painfully slow on C64        | Full modern editor experience    |
| Build time            | Minutes (on C64)             | Sub-second on modern hardware    |
| Debugging             | Poke and pray                | Source-level, breakpoints, watch |
| Version control       | Non-existent                 | Full Git integration             |
| Iteration cycle       | 5-10 minutes                 | 2-5 seconds                      |
| Collaboration         | Swap disks                   | GitHub, CI/CD pipelines          |

### 1.3 Toolchain Categories

The modern C64 toolchain consists of several categories of tools:

- **Cross-assemblers**: KickAssembler, ACME, ca65, 64tass, DASM
- **C compilers**: cc65, Oscar64
- **Higher-level languages**: Prog8, Millfork, XC=BASIC
- **Emulators**: VICE (primary), Hoxs64, Micro64
- **Graphics editors**: CharPad, SpritePad, Pixcen, Multipaint, PETSCII editors
- **Music trackers**: GoatTracker, SID-Wizard, CheeseCutter, SID Factory II
- **Build tools**: Make, Gradle (c64lib plugin), shell scripts
- **Debuggers**: VICE monitor, IceBroLite, C64 Debugger
- **Testing frameworks**: 64spec, BDD6502, sim6502


---

## 2. Cross-Assemblers

### 2.1 KickAssembler

**Website**: https://theweb.dk/KickAssembler/
**Platform**: Java (cross-platform: Windows, macOS, Linux)
**License**: Freeware
**Author**: Mads Nielsen (Camelot/Oxyron)

KickAssembler is arguably the most popular cross-assembler in the C64 demoscene. It
combines a full 6510 assembler with a powerful JavaScript-like scripting language that
runs at assembly time.

**Core Features:**

- All documented and undocumented 6502/6510 opcodes
- C64 DTV extended opcodes
- Powerful macro system with pseudo-commands
- Built-in JavaScript-like scripting language with `.eval`, `.var`, `.for`, `.function`
- Direct import of SID files, standard C64 graphic formats (Koala, FLI, etc.)
- Preprocessor (added in version 4.x) with `#import`, `#define`, `#if`
- 3rd-party Java plugin support (e.g., cruncher plugins for Exomizer, ByteBoozer)
- Debug data export for VICE (`.breakpoint` directives, label files)
- Kick Assembler debug data (.dbg) files for IceBroLite source-level debugging

**Scripting Example:**

```
// Generate a sine table at assembly time
.var sineTable = List()
.for (var i = 0; i < 256; i++) {
    .eval sineTable.add(round(127.5 + 127.5 * sin(toRadians(i * 360 / 256))))
}
sine_data:
    .fill 256, sineTable.get(i)
```

**Strengths:**
- Scripting language is deeply integrated with the assembler (not a separate prepass)
- Extremely popular in the demoscene -- vast community knowledge and examples
- Active development and regular updates
- Excellent VS Code extension ("Kick Assembler 8-Bit Retro Studio") with build,
  run, and debug integration
- Can generate data procedurally (sine tables, sprite multiplexer tables, etc.)
  without external tools
- Library ecosystem (c64lib provides reusable KickAssembler macro libraries)

**Weaknesses:**
- Requires Java runtime (JDK 11+)
- Freeware but not open source
- Scripting language is unique to KickAssembler -- learning curve for the full system
- No linker -- single-pass compilation model (though segments and memory management
  are supported within the assembler)

**IDE Integration:**
- VS Code: "Kick Assembler 8-Bit Retro Studio" extension (build, run, debug)
- Sublime Text: "Kick Assembler (C64)" package
- IntelliJ IDEA: "kick-assembler-acbg" plugin
- C64 Studio: native support

### 2.2 ACME

**Website**: https://sourceforge.net/projects/acme-crossass/
**Platform**: Native (Windows, macOS, Linux, Amiga, DOS)
**License**: GNU GPL
**Original Author**: Marco Baye

ACME is a free, open-source cross-assembler with a long history and straightforward
design. It compiles to native code on the host platform, so it is fast and has no
runtime dependencies.

**Core Features:**

- 6502, 6510 (including illegal opcodes), 65C02, 65816 support
- Macro system with conditional assembly and looping
- Zone-based scoping (zones may be nested)
- Local and global labels
- Offset assembly (assemble code for one address while placing it at another)
- Binary includes
- Integer and floating-point calculations
- Can produce PDB files for source-level debugging in VICE

**Strengths:**
- Native compiled binary -- no Java or other runtime needed
- Lightweight, fast, simple to learn
- Good for projects that want a "no-frills" assembler
- Open source with active maintenance (Martin Piper's fork adds features)
- PDB file generation enables advanced debugging

**Weaknesses:**
- Zone-based scoping can be confusing (child zones cannot access parent labels by default)
- No built-in scripting language for data generation
- Smaller community than KickAssembler in the demoscene
- Less sophisticated macro system compared to KickAssembler or 64tass

**Syntax Example:**

```
!zone main {
    !macro clear_screen .char {
        lda #.char
        ldx #0
    .loop:
        sta $0400,x
        sta $0500,x
        sta $0600,x
        sta $0700,x
        dex
        bne .loop
    }
    +clear_screen $20
}
```

### 2.3 ca65 / cc65

**Website**: https://cc65.github.io/
**Platform**: Native (Windows, macOS, Linux)
**License**: zlib license
**Primary Authors**: Ullrich von Bassewitz, community

ca65 is the macro assembler component of the cc65 cross-development suite. It is
notable for having a proper separate linker (ld65) with configurable linker scripts,
making it the most "traditional" assembler toolchain in the C64 space.

**Core Features:**

- 6502, 65C02, 65816 support
- Full macro system with conditional assembly
- Relocatable object files (.o) with a separate linking step
- Configurable linker scripts for arbitrary memory layouts
- Segment-based code organization (CODE, DATA, RODATA, BSS, ZEROPAGE, etc.)
- Debug info generation (--debug-info, .debuginfo)
- VICE label file export via ld65 `-Ln` switch
- Can be used standalone or as part of the cc65 C compiler toolchain
- Struct and union support in assembly

**Linker Script Example (C64):**

```
MEMORY {
    ZP:     start = $02,    size = $1A, type = rw;
    HEADER: start = $0801,  size = $000D, file = %O;
    MAIN:   start = $080E,  size = $C7F2, file = %O;
}
SEGMENTS {
    ZEROPAGE: load = ZP,     type = zp;
    STARTUP:  load = HEADER, type = ro;
    CODE:     load = MAIN,   type = ro;
    RODATA:   load = MAIN,   type = ro;
    DATA:     load = MAIN,   type = rw;
    BSS:      load = MAIN,   type = bss, define = yes;
}
```

**Strengths:**
- Proper linker with segments and memory configuration -- essential for large projects
- Excellent for projects mixing C and assembly
- Multiple source files compiled independently and linked together
- Well-documented with extensive online manuals
- Mature, stable, widely used beyond C64 (NES, Atari, Apple II, etc.)
- Open source with active community maintenance

**Weaknesses:**
- More complex setup (assembler + linker + config files)
- Assembly syntax can feel verbose compared to KickAssembler
- Local labels use `@` prefix within `.proc` / `.endproc` blocks
- No built-in scripting for data generation
- Less popular than KickAssembler for demoscene work

### 2.4 64tass

**Website**: https://sourceforge.net/projects/tass64/
**Platform**: Native (Windows, macOS, Linux)
**License**: GNU GPL v2
**Author**: Soci/Singular (maintained by community)

64tass is a multi-pass optimizing macro assembler for the 65xx family. It strives for
compatibility with the original Omicron Turbo Assembler syntax while offering extensive
modern features. Many consider it to have the best overall design among 6502
cross-assemblers.

**Core Features:**

- All major 6502 CPU variants: 6502, 6510, 65C02, R65C02, W65C02, 65CE02, 65816,
  DTV, 65EL02, 4510
- Arbitrary-precision integers and bit strings
- Double-precision floating point
- UTF-8, UTF-16, and 8-bit RAW source file encoding
- Unicode character strings and identifier support
- Built-in linker with section support
- Structures, unions, and scopes with arbitrary nesting
- Conditional compilation, macros, and C-style expressions
- Various output formats: binary, PRG, Intel HEX, S-record, etc.
- Label file export for VICE

**Strengths:**
- Best-in-class scoping with arbitrarily nested blocks
- Structures and unions directly in the assembler
- Multi-pass optimization (resolves forward references automatically)
- Supports nearly every 6502 variant ever made
- Written in portable C -- compiles easily on any platform
- Active development and maintenance
- Required by Prog8 (which uses 64tass as its backend assembler)

**Weaknesses:**
- Turbo Assembler compatibility can be confusing for newcomers
- Less IDE integration than KickAssembler
- Smaller community presence in the C64 demoscene
- Anonymous labels use strings of `+`/`-` characters (e.g., `--` refers to the
  second-previous anonymous label)

### 2.5 DASM

**Website**: https://dasm-assembler.github.io/
**Platform**: Native (Windows, macOS, Linux, Raspberry Pi)
**License**: GNU GPL v2
**Original Author**: Matthew Dillon (1987)

DASM is a venerable macro assembler primarily known in the Atari 2600 community but
fully capable of C64 development. It supports multiple processor families.

**Core Features:**

- 6502, 6507, 68xx, F8 processor support
- Macro system with local labels
- Multiple passes for forward reference resolution
- Output formats: binary, Intel HEX
- Symbol table output

**Strengths:**
- Extremely mature and stable (since 1987)
- Simple, no-nonsense syntax
- Well-documented and widely known
- Default assembler for Atari 2600 development
- Used as backend by XC=BASIC

**Weaknesses:**
- Limited advanced features compared to modern assemblers
- No built-in scripting
- Smaller C64-specific community
- Fewer output format options
- Development pace is slow

### 2.6 Cross-Assembler Comparison

| Feature              | KickAssembler | ACME    | ca65/ld65 | 64tass  | DASM    |
|----------------------|---------------|---------|-----------|---------|---------|
| Runtime dependency   | Java 11+      | None    | None      | None    | None    |
| Scripting language   | Yes (rich)    | No      | No        | Limited | No      |
| Linker               | Built-in      | No      | Separate  | Built-in| No      |
| Segments             | Yes           | No      | Yes       | Yes     | Limited |
| Structs/unions       | Via scripts   | No      | Yes       | Yes     | No      |
| 65816 support        | No            | Yes     | Yes       | Yes     | No      |
| Illegal opcodes      | Yes           | Yes     | Yes       | Yes     | Yes     |
| Macro system         | Advanced      | Good    | Advanced  | Advanced| Basic   |
| VICE label export    | Yes           | Yes     | Yes       | Yes     | Yes     |
| Debug data (.dbg)    | Yes           | PDB     | .dbg      | No      | No      |
| IDE support          | Excellent     | Good    | Good      | Basic   | Basic   |
| Demoscene popularity | Very high     | Medium  | Medium    | Medium  | Low     |
| Open source          | No            | Yes     | Yes       | Yes     | Yes     |
| Data generation      | Built-in      | External| External  | Limited | External|


---

## 3. C Compilers

### 3.1 cc65

**Website**: https://cc65.github.io/
**License**: zlib license

cc65 is the most established C cross-compiler for 6502 targets. It is a complete
toolchain including a C compiler (cc65), macro assembler (ca65), linker (ld65),
librarian (ar65), and several utility tools.

**C Language Support:**
- Close to ANSI C89/C90 compliance
- Most of C99 is not supported (no variable-length arrays, no `_Bool`, etc.)
- No floating-point support
- 8-bit `char`, 16-bit `int`, 32-bit `long`
- No 32-bit arithmetic in generated code -- `long` operations are done via library calls

**C64-Specific Libraries:**

| Header       | Purpose                                          |
|--------------|--------------------------------------------------|
| `<c64.h>`    | C64 hardware register definitions (VIC, SID, CIA)|
| `<cbm.h>`    | CBM-family common functions (KERNAL calls)       |
| `<conio.h>`  | Text console I/O (cursor positioning, colors)    |
| `<tgi.h>`    | Graphics library (320x200 hires)                 |
| `<joy.h>`    | Joystick driver support                          |
| `<mouse.h>`  | Mouse driver (1351, joystick-emulated, lightpen) |
| `<em.h>`     | Extended memory (REU, GeoRAM, RamCart)            |
| `<ser.h>`    | Serial communication (SwiftLink, up to 38400 baud)|

**Memory Layout (default c64.cfg):**

```
$0000-$00FF   Zero page (partially used by runtime)
$0100-$01FF   Hardware stack
$0200-$07FF   System area
$0801-$080D   BASIC stub (SYS xxxx)
$080E-$CFFF   Program code, data, heap, C stack (~50 KB)
$D000-$DFFF   I/O area (always mapped)
$E000-$FFFF   KERNAL ROM (always mapped)
```

**Code Quality and Performance:**

cc65 generates notoriously slow code compared to hand-written assembly. The compiler
uses a software stack for function parameters and local variables (the 6502 hardware
stack is only 256 bytes, too small for C's needs). This means every function call
involves pushing parameters to a zero-page-pointer-based software stack.

Benchmark comparisons typically show cc65-compiled code running 10-50x slower than
optimized hand-written assembly, depending on the operation. Simple operations like
incrementing a byte variable compile to 5-10 instructions instead of the 3 that assembly
would require.

**When to Use cc65:**
- Rapid prototyping of game logic or utilities
- Projects where development speed matters more than runtime speed
- Tools and utilities that are not cycle-sensitive
- Mixing C for high-level logic with assembly for performance-critical inner loops
- Cross-platform development targeting multiple 6502 machines

**When NOT to Use cc65:**
- Demoscene effects requiring cycle-exact timing
- Real-time graphics routines (scrollers, raster effects)
- Music drivers or interrupt handlers
- Any code that runs in a tight loop every frame

### 3.2 Oscar64

**Website**: https://github.com/drmortalwombat/oscar64
**License**: MIT
**Author**: Dr. Mortal Wombat

Oscar64 is a newer C/C++ cross-compiler specifically designed for the 6502 family,
with a strong focus on optimization and code density. It represents a significant step
forward from cc65 in terms of code quality.

**Language Support:**
- C99 compliant
- Many C++ features: classes, templates (including variadic), lambda functions,
  operator overloading, references, namespaces
- This is remarkably advanced for a 6502 target

**Code Generation:**
- Dual target: virtual machine bytecode (compact) or native 6510 machine code (fast)
- Native code mode with `-O3` achieves performance competitive with moderately
  optimized hand-written assembly
- 442 Dhrystone V2.2 iterations per second on C64 (native, -O3)
- Significantly faster than cc65 for equivalent code

**Key Innovations:**
- No traditional library files -- headers use `#pragma` to include library source,
  allowing whole-program optimization
- Support for disk overlays (loading code segments from disk at runtime)
- Support for banked EasyFlash cartridges
- Designed from the ground up for small-memory targets

**Strengths:**
- Much better code quality than cc65
- C++ support is remarkable for a 6502 compiler
- Active development with frequent releases (232+ releases)
- Several commercial-quality C64 games built with it
- Whole-program optimization

**Weaknesses:**
- Less mature than cc65 (smaller community, less documentation)
- Not as widely tested across different 6502 platforms
- The virtual machine mode trades speed for code density
- Fewer platform-specific library drivers than cc65

### 3.3 C on the C64: Fundamental Limitations

The 6502 architecture is fundamentally hostile to C compilation:

1. **Three 8-bit registers** (A, X, Y) with no general-purpose register file.
   C compilers constantly spill to memory.

2. **256-byte hardware stack** is too small for C's function call model. Both cc65
   and Oscar64 use a software stack in main RAM, adding overhead to every call.

3. **No 16-bit operations.** Every 16-bit addition requires ADC on the low byte,
   then ADC on the high byte. Pointer arithmetic (fundamental to C) is expensive.

4. **No multiply or divide instructions.** Even `i * 2` requires a shift routine.

5. **64 KB address space.** C programs must carefully manage memory, and the linker
   configuration is crucial.

6. **No position-independent code.** The 6502 uses absolute addressing extensively,
   making relocatable code difficult.

Despite these limitations, C can be practical for:
- Game logic and state machines (not inner loops)
- Tool and utility programs
- Prototyping before rewriting hot paths in assembly
- Projects where developer productivity outweighs runtime performance


---

## 4. Higher-Level Languages

### 4.1 Prog8

**Website**: https://prog8.readthedocs.io/
**Repository**: https://github.com/irmen/prog8
**License**: GNU GPL v3 (compiler); output files are unrestricted
**Author**: Irmen de Jong

Prog8 is a structured programming language designed specifically for 8-bit 6502/65C02
machines. It sits between BASIC and C in terms of abstraction: higher than assembly but
closer to the metal than C.

**Key Features:**
- Data types: `byte` (unsigned 8-bit), `word` (unsigned 16-bit), `ubyte`, `uword`,
  `long`, `float`, strings with PETSCII encoding, arrays, structs
- Modular subroutines with symbol scoping
- `when` statements (compile to jump tables)
- `defer` statements for cleanup logic
- Conditional expressions mapping to CPU status flags
- Built-in functions: `lsb()`, `msb()`, `min()`, `max()`, `rol()`, `ror()`
- Inline assembly support
- Direct hardware register access

**Compilation Pipeline:**
Prog8 compiles to 64tass assembly source, which is then assembled to machine code.
This means the generated code benefits from 64tass's multi-pass optimization.

**Performance:**
- Generated programs are "much smaller than equivalent C code compiled with cc65"
- Competitive performance with cc65 or better, due to direct register mapping
- Still slower than hand-written assembly for tight loops

**Target Platforms:**
- Commodore 64 and C128 (primary)
- Commander X16 (65C02)
- Commodore PET
- Atari 800 XL
- NES (experimental)
- Neo6502

**Requirements:** Java 11+, 64tass assembler, optionally VICE for testing.

### 4.2 Millfork

**Website**: https://karols.github.io/millfork/
**Repository**: https://github.com/KarolS/millfork
**License**: GNU GPL v3 (compiler); standard library is zlib licensed
**Author**: Karol S.

Millfork is a "middle-level" language -- more productive than assembly but avoiding
the overhead of C's abstractions that map poorly to 8-bit CPUs.

**Design Philosophy:**

The name stands for "Middle Level Language FOR Kommodore computers." It targets
developers who "have little use for advanced features of C, but don't have time
to write assembly."

**Key Features:**
- Simple memory model that avoids using the stack (reducing overhead)
- Not a single byte of memory used unless for code or explicitly declared variables
- Inline assembly support
- Multi-pass whole-program optimizer that can optimize even hand-written assembly
- Macro system
- Supports both 6502 and Z80 families

**Supported Platforms (6502 family):**
- Commodore 64, VIC-20, PET, Plus/4
- Atari 8-bit, Apple II, BBC Micro
- NES, Atari Lynx, Atari 2600
- Commander X16

**Strengths:**
- Very efficient code generation through whole-program optimization
- Zero unnecessary memory overhead
- Can optimize inline assembly alongside generated code
- Broad platform support

**Weaknesses:**
- Development appears less active than Prog8
- Smaller community
- Less documentation and fewer examples
- Learning curve for the unique syntax

### 4.3 XC=BASIC

**Website**: https://xc-basic.net/
**Repository**: https://github.com/neilsf/xc-basic3
**License**: MIT
**Author**: Neil (neilsf)

XC=BASIC is a compiled BASIC dialect that offers a familiar syntax while generating
fast machine code through the DASM assembler as its backend.

**Performance:**
- ~20x faster than interpreted BASIC V2
- ~6x faster than BASIC V2 compiled with native compilers (BASIC Boss, AustroSpeed)
- Compiles to pure 6510 machine code (no interpreter overhead)

**Key Features:**
- Syntax similar to QuickBASIC and CBM BASIC
- Banks out the BASIC ROM, providing continuous RAM from $0801 to $CFFF (~50 KB)
- Extensible language design
- Cross-platform: runs on Windows, Linux, macOS
- IDE support through XC-Edit

**Strengths:**
- Familiar BASIC syntax for retro programmers
- Much faster than interpreted BASIC
- Good for game development by BASIC-experienced programmers

**Weaknesses:**
- Not 100% BASIC V2 compatible
- Smaller community than Prog8 or cc65
- Less sophisticated optimization than Oscar64 or Millfork

### 4.4 Other BASIC Cross-Compilers

**CBM prg Studio**: A Windows IDE (by Arthur Jordison) that started as a simple BASIC-to-PRG
converter and grew into a full development environment with assembler, sprite editor,
character editor, and screen editor. It can integrate with external compilers like MOSpeed.

**MOSpeed**: An optimizing BASIC cross-compiler written in Java. Expects BASIC V2 text files
and produces optimized machine code. Useful for taking existing BASIC programs and
compiling them to run faster.

**BasicV2**: A Commodore BASIC V2 interpreter/compiler written in Java by EgonOlsen71,
available on GitHub.

### 4.5 Why Build a New Language?

Existing options each have significant gaps:

| Language    | Problem                                                   |
|-------------|-----------------------------------------------------------|
| Assembly    | Extremely productive only for experts; huge source files   |
| cc65 (C)    | Poor code quality; C abstractions map badly to 6502       |
| Oscar64     | Better C, but still fighting the language/architecture gap |
| Prog8       | Good but opinionated; tied to 64tass backend              |
| Millfork    | Less actively developed; unique syntax                    |
| XC=BASIC    | Limited optimization; not designed for advanced projects   |

A new language can be designed to:
- Map directly to 6502 idioms (zero-page usage, page-aligned tables, register hints)
- Generate code quality approaching hand-written assembly
- Provide modern developer experience (types, modules, good errors)
- Target the C64's specific memory architecture from the ground up
- Support cycle-counting and timing annotations
- Integrate with the existing ecosystem (VICE, SID files, graphics formats)


---

## 5. Emulators

### 5.1 VICE (Versatile Commodore Emulator)

**Website**: https://vice-emu.sourceforge.io/
**Platform**: Windows, macOS, Linux, many others
**License**: GNU GPL v2
**Status**: Actively developed (current version 3.8+)

VICE is the gold standard emulator for C64 development. It emulates the entire
Commodore 8-bit family: C64, C128, VIC-20, Plus/4, PET, CBM-II, and SCPU.

**Accuracy:**
- Cycle-accurate 6502/6510 CPU emulation including all undocumented opcodes
- Cycle-accurate CIA emulation
- Highly accurate VIC-II emulation (badlines, sprite timing, border tricks)
- Accurate SID emulation via reSID and reSID-fp libraries
- Two SID models: 6581 (old) and 8580 (new) with distinct filter characteristics
- Both PAL and NTSC machine variants

**Key Features:**

| Feature                   | Description                                        |
|---------------------------|----------------------------------------------------|
| Built-in monitor          | Full ML monitor with breakpoints, watches, labels  |
| Warp mode                 | Run at maximum speed (no speed limit, no sound)    |
| Snapshots                 | Save/restore complete machine state to file         |
| Event history             | Record and replay input sequences                  |
| Remote monitor            | TCP-based text monitor (port 6510)                 |
| Binary monitor            | TCP-based binary protocol (port 6502)              |
| Autostart                 | Load and run PRG/D64/CRT from command line          |
| Screenshot/video capture  | Save screen as PNG or record video                 |
| Cartridge emulation       | All major cartridge types supported                |
| Drive emulation           | True 1541 emulation or fast virtual filesystem     |
| Joystick/mouse support    | Keyboard mapping, host controllers, mouse emulation|
| Profiling                 | Built-in instruction-level profiler                |

**Command-Line Usage:**

```bash
# Launch C64 and autostart a PRG file
x64sc -autostart myprogram.prg

# Launch with monitor commands (load labels, set breakpoints)
x64sc -autostart myprogram.prg -moncommands labels.mon

# Launch headless for CI testing (GTK version with no window)
x64sc -console -sound -sounddev dummy -warp -limitcycles 5000000 \
      -autostart test.prg -moncommands test_runner.mon

# Enable remote monitors for external debugger connection
x64sc -remotemonitor -remotemonitoraddress ip4://127.0.0.1:6510 \
      -binarymonitor -binarymonitoraddress ip4://127.0.0.1:6502
```

**Important Note**: VICE ships two C64 executables:
- `x64` -- older, faster, less accurate (uses some shortcuts)
- `x64sc` -- "SC" = SubCycle accurate. This is the recommended version for
  development as it correctly emulates all VIC-II timing edge cases.

### 5.2 Hoxs64

**Website**: https://www.hoxs64.net/
**Platform**: Windows only
**License**: Freeware
**Author**: David Horrocks

Hoxs64 claims to be the most cycle-accurate C64 emulator in existence. It implements
fully cycle-based emulation of sprites, mid-line graphics changes, and other edge
cases that even VICE x64sc does not handle perfectly.

**Accuracy Claims:**
- Fully cycle-based sprite emulation
- Correct mid-line graphics data changes
- Passes EmuFuxxor v1 and v2 (emulator detection tests)
- Correct SounDemon waveform test results
- Passes Krestage 3 VIC detection

**Tradeoffs:**
- Windows-only (source available on GitHub: github.com/davidhorrocks/hoxs64)
- Resource-intensive, especially 8580 SID emulation in "resample" mode
- No built-in development-focused features (no monitor, no remote debug protocol)
- Not useful as a development tool, but excellent for final accuracy verification

### 5.3 Micro64

**Website**: Various community pages
**Status**: In development / limited release

Micro64 focuses on visual authenticity rather than pure chip-level accuracy. It
emulates the visual properties of a CRT display, including phosphor decay, scanline
effects, and other analog display characteristics.

**Strengths:**
- Exceptional CRT display emulation
- Interesting for visual authenticity testing
- PAL display emulation

**Weaknesses:**
- Incomplete emulation
- Not suitable for development work
- Small community

### 5.4 Emulator Comparison for Development

| Feature              | VICE (x64sc) | Hoxs64      | Micro64     |
|----------------------|-------------- |-------------|-------------|
| CPU accuracy         | Excellent     | Best        | Good        |
| VIC-II accuracy      | Very high     | Highest     | Moderate    |
| SID accuracy         | Excellent     | Excellent   | Limited     |
| Built-in debugger    | Yes (full)    | No          | No          |
| Remote monitor       | Yes           | No          | No          |
| Command-line control | Yes           | Limited     | No          |
| Cross-platform       | Yes           | Windows     | Limited     |
| CI/CD suitable       | Yes           | No          | No          |
| Active development   | Very active   | Active      | Slow        |
| Best for             | Development   | Verification| Visual test |

**Recommendation**: Use VICE x64sc for all development work. Use Hoxs64 for
verification of timing-critical effects. Micro64 is primarily of academic interest.


---

## 6. Graphics Tools

### 6.1 CharPad

**Website**: https://subchristsoftware.itch.io/charpad-c64-pro
**Platform**: Windows (runs under WINE on macOS/Linux)
**Developer**: Subchrist Software
**Editions**: Free and Pro

CharPad is the industry-standard tool for creating C64 character-based graphics:
character sets, tiles, fonts, and tile maps for 2D games and demos.

**Features:**
- Design character sets with all C64 video mode constraints enforced in real time
- Tile editor supporting sizes from 1x1 to 10x10 characters (100 different sizes)
- Map editor for creating tile-based level layouts
- Import and convert any bitmap image into a C64-compatible tile map
- Interactive ripping of graphics from VICE snapshot files
- Colour palette editing in RGB, HSL, and YUV formats
- Export formats: binary, assembly source, C source, JSON

**Pro Edition Additional Features:**
- Animated tile support
- Multiple character set banks
- Undo/redo
- Project-based workflow

**Export for Assembly:**
CharPad can export character data, tile definitions, and map data in formats directly
usable by KickAssembler, ca65, or other assemblers. Typical export includes:
- Character set binary (2048 bytes for 256 chars)
- Tile definitions (indices into character set)
- Map data (indices into tile set)
- Attribute/color data

### 6.2 SpritePad

**Website**: https://subchristsoftware.itch.io/spritepad-c64-pro
**Platform**: Windows (WINE compatible)
**Developer**: Subchrist Software
**Editions**: Free and Pro

SpritePad is the companion to CharPad, focused on C64 sprite creation and animation.

**Features:**
- Hi-res (24x21, single color + transparent) and multicolor (12x21, 3 colors +
  transparent + background) sprite editing
- Sprite animation timeline with preview
- Sprite overlay composition
- Tile-based sprite sheets
- Animated GIF import/export
- Export to binary, assembly source, C source

**Pro Edition:**
- Extended sprite tile support
- Advanced animation features
- Multiple sprite bank management

### 6.3 Pixcen

**Website**: http://hammarberg.github.io/pixcen/
**Repository**: https://github.com/Hammarberg/pixcen
**Platform**: Windows
**License**: Open source
**Developer**: Censor Design

Pixcen is a low-level pixel editor purpose-built for C64 graphics. It is a
"programmer's art tool" -- no lines, circles, or rectangles, just pixel editing
with full enforcement of C64 video mode constraints.

**Supported Modes:**
- Hi-res bitmap (320x200, 2 colors per 8x8 cell)
- Multicolor bitmap (160x200, 4 colors per 4x8 cell)
- FLI (Flexible Line Interpretation)
- AFLI
- Sprite editing (hi-res and multicolor)
- Character mode editing

**Key Strength:**
Rules of the selected screen mode are checked as each pixel is changed. You cannot
accidentally create an image that violates C64 hardware constraints. Every pixel you
place is guaranteed to be valid for the target video mode.

### 6.4 Multipaint

**Website**: http://multipaint.kameli.net/
**Platform**: Java (cross-platform)
**Developer**: Dr. TerrorZ / Multipaint team

Multipaint is a cross-platform paint program designed for 8-bit and 16-bit computer
graphics with authentic color limitation enforcement.

**Supported C64 Modes:**
- Standard character mode
- Multicolor character mode
- Hi-res bitmap
- Multicolor bitmap

**Also Supports:**
- Commodore Plus/4, VIC-20
- ZX Spectrum, MSX, Amstrad CPC
- Commodore Amiga, Atari ST
- Sinclair QL

**Strengths:**
- Cross-platform (Java)
- Authentic painting experience with platform color constraints
- Good for artists who work across multiple retro platforms

### 6.5 PETSCII Editors

**PETSCII Editor (by Krissz)**: https://petscii.krissz.hu/
- Web-based PETSCII art editor
- Uses the C64's 256 PETSCII characters
- One global background color plus one color per 8x8 character cell
- Export to various formats (PRG, binary, assembly)
- Collaborative features

**PETMATE**: A desktop PETSCII editor (Electron-based) with layers, copy/paste,
and export to multiple formats.

### 6.6 Image Converters

For converting modern image formats to C64 graphics:

| Tool            | Input         | Output                         | Notes                    |
|-----------------|---------------|--------------------------------|--------------------------|
| png2prg         | PNG/GIF/JPEG  | PRG (hires, multicolor, chars) | Auto palette matching    |
| SPOT            | PNG/BMP/KLA   | Optimized C64 formats          | Compression-optimized    |
| Convertron3000  | Various       | Koala, hires                   | Python-based             |
| Petsciiator     | JPG/PNG       | PETSCII, Koala, hires          | Auto-dithering           |
| c64img          | Various       | C64 formats                    | Python library (pip)     |

**Workflow tip**: For best results, pre-scale images to 320x200 (hires) or 160x200
(multicolor) and manually adjust the palette before conversion. Automated converters
produce better results with source images that already approximate C64 constraints.


---

## 7. Music Tools

### 7.1 GoatTracker 2

**Website**: https://sourceforge.net/projects/goattracker2/
**Platform**: Windows, macOS, Linux
**License**: GNU GPL
**Author**: Lasse Oorni (Cadaver)

GoatTracker is the most widely used cross-platform SID music tracker. It uses the
reSID library for accurate SID emulation and supports HardSID and CatWeasel hardware
for playback on real SID chips.

**Features:**
- 3-channel tracker interface (matching the SID's 3 voices)
- Instrument editor with ADSR, waveform, pulse width, and filter control
- Pattern-based sequencing
- Vibrato, portamento, and other effects
- Support for both 6581 and 8580 SID models
- Export to relocatable SID player routine + data
- Direct .SID file export for HVSC (High Voltage SID Collection)
- Real-time preview

**Music Driver Integration:**

GoatTracker exports a complete player routine that can be included in your program:

```
; GoatTracker player integration
; Load music data and player at desired address
; Call init once:
    lda #0          ; song number
    jsr music_init  ; initialize player
; Call play once per frame (in raster IRQ):
    jsr music_play  ; update player
```

The player routine is relocatable and typically occupies 1-2 KB. Song data size
depends on composition complexity.

### 7.2 SID-Wizard

**Website**: https://sourceforge.net/projects/sid-wizard/
**Platform**: Runs natively on Commodore 64 (and in VICE)
**Author**: Mihaly Horvath (Hermit)

SID-Wizard is unique in that it runs natively on the C64 hardware. This means the
musician hears exactly what the final product will sound like -- no emulation artifacts.

**Features:**
- Native C64 execution for authentic sound
- Configurable engine features (toggle off unused features to reduce memory footprint)
- Export to .SID format
- Support for all SID chip features including filters
- Multispeed support (2x, 4x play speed)

**Integration:**
SID-Wizard's player engine is modular. Features not used in a composition can be
compiled out, minimizing the memory footprint from ~2 KB down to well under 1 KB for
simple tunes.

### 7.3 CheeseCutter

**Website**: http://theyamo.kapsi.fi/ccutter/
**Platform**: Windows, Linux, macOS
**Author**: Yamo

CheeseCutter is a SID tracker that is "extremely similar to GoatTracker II; both in
interface and features." It uses the reSID engine for SID emulation.

**Key Differences from GoatTracker:**
- Different internal data representation
- Some UI workflow improvements
- Active community of users
- Can be imported into SID Factory II

### 7.4 SID Factory II

**Website**: https://blog.chordian.net/sf2/
**Repository**: https://github.com/Chordian/sidfactory2
**Platform**: Windows, macOS, Linux
**License**: Open source
**Status**: Open beta

SID Factory II is a cross-platform tracker with a heritage going back to the legendary
SID composers JCH and Laxity. It represents the state of the art in C64 music creation.

**Features:**
- Tracker interface using JCH's contiguous sequence stacking system
- Protracker-style note input layout
- Choice of music drivers by JCH and Laxity, optimized for demos and games
- Both 6581 and 8580 SID model support, PAL and NTSC
- Imports GoatTracker, CheeseCutter, and MOD files
- ASID support for playback on real SID hardware
- Built-in packer and relocator (position music anywhere in C64 memory)
- Real-time packing -- "what you hear is pretty much what you get"
- Built-in SID emulation via reSID

**Music Driver Options:**

SID Factory II includes multiple music drivers with different size/feature tradeoffs:

| Driver        | Approx. Size | Features                                |
|---------------|-------------|------------------------------------------|
| Laxity driver | ~1.5 KB     | Full-featured, optimized for demos       |
| JCH driver    | ~1.2 KB     | Compact, game-oriented                   |
| Custom        | Varies      | User-configurable feature set            |

### 7.5 Music Driver Integration Workflow

Integrating tracker music into a C64 program typically follows this pattern:

1. **Compose** the music in your tracker of choice.
2. **Export** the music data and player routine. Most trackers export:
   - Player code (relocatable binary, typically $0800-$1000)
   - Song data (patterns, instruments, sequences)
   - An init routine (call once with song number in accumulator)
   - A play routine (call once per frame, typically on a raster IRQ)
3. **Include** the exported binary in your assembler project:
   ```
   music_player:
       .import binary "music_player.bin"
   music_data:
       .import binary "music_data.bin"
   ```
4. **Initialize** during program startup.
5. **Call the play routine** every frame from your raster interrupt handler.

**Memory considerations:** The combined player + song data typically requires 2-8 KB,
depending on song complexity and driver features. Plan your memory map accordingly.


---

## 8. Build Systems and Workflow

### 8.1 Makefile-Based Builds

The most common build system for C64 projects is GNU Make. A typical Makefile handles
assembling, linking, disk image creation, and emulator launching.

**Example Makefile (KickAssembler):**

```makefile
# Tools
KICK = java -jar /path/to/KickAss.jar
VICE = /path/to/x64sc
C1541 = /path/to/c1541

# Sources
SRC = main.asm
OUT = game.prg
D64 = game.d64
LABELS = game.vs

# Build
.PHONY: all run clean

all: $(D64)

$(OUT): $(SRC) $(wildcard *.asm) $(wildcard *.bin)
	$(KICK) $(SRC) -o $(OUT) -vicesymbols -showmem

$(D64): $(OUT)
	$(C1541) -format "game,01" d64 $(D64) -write $(OUT) "game"

run: $(D64)
	$(VICE) -autostart $(D64) -moncommands $(LABELS)

clean:
	rm -f $(OUT) $(D64) $(LABELS)
```

**Example Makefile (ca65/ld65):**

```makefile
# Tools
CA65 = ca65
LD65 = ld65
VICE = x64sc

# Config
CFG = c64.cfg
SOURCES = main.s irq.s graphics.s music.s
OBJECTS = $(SOURCES:.s=.o)
TARGET = game.prg
LABELS = game.lbl
MAP = game.map

all: $(TARGET)

%.o: %.s
	$(CA65) --target c64 -g -o $@ $<

$(TARGET): $(OBJECTS)
	$(LD65) -C $(CFG) -o $@ -Ln $(LABELS) -m $(MAP) $(OBJECTS) c64.lib

run: $(TARGET)
	$(VICE) -autostart $(TARGET) -moncommands $(LABELS)

clean:
	rm -f $(OBJECTS) $(TARGET) $(LABELS) $(MAP)
```

### 8.2 Gradle (c64lib Retro Assembler Plugin)

The c64lib project provides a Gradle plugin that brings dependency management and
CI/CD capabilities to KickAssembler projects.

**Setup (build.gradle):**

```groovy
plugins {
    id "com.github.c64lib.retro-assembler" version "1.7.0"
}

retroAssembler {
    dialect = "KickAssembler"
    dialectVersion = "5.25"
    libDirs = [".ra/deps/c64lib"]
    srcDirs = ["src/main/asm"]
}
```

**Features:**
- Automatic KickAssembler download and management
- Dependency management (import c64lib libraries from Maven Central)
- CircleCI and TravisCI integration
- Docker image (c64lib/c64libci) for cloud CI
- Automatic 64spec test execution in headless VICE
- Build results influenced by test outcomes

### 8.3 Typical Development Cycle

```
┌─────────────────────────────────────────────┐
│  1. Edit source in VS Code / Vim / Sublime  │
│     (with syntax highlighting + snippets)   │
└────────────────────┬────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────┐
│  2. Save triggers build (Makefile / Gradle) │
│     - Assemble source files                 │
│     - Export labels / debug info            │
│     - Create PRG / D64 / CRT               │
└────────────────────┬────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────┐
│  3. Auto-launch in VICE x64sc              │
│     - Load labels into monitor              │
│     - Set breakpoints from source           │
│     - Program starts running                │
└────────────────────┬────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────┐
│  4. Test / Debug                            │
│     - Use VICE monitor or IceBroLite        │
│     - Check raster timing, memory, sprites  │
│     - Profile performance                   │
└────────────────────┬────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────┐
│  5. Iterate (back to step 1)               │
│     Total cycle time: 2-5 seconds           │
└─────────────────────────────────────────────┘
```

### 8.4 Automated Testing with VICE

VICE can be driven from the command line for automated testing:

**Approach 1: Monitor Command Scripts**

Create a `.mon` file that VICE executes on startup:

```
; test_runner.mon
; Wait for program to reach test completion address
break exec $c000
; When breakpoint hits, dump test results from memory
command 1 "mem $0400 $04ff ; x"
```

Launch with: `x64sc -autostart test.prg -moncommands test_runner.mon`

**Approach 2: VICE Remote Monitor**

Start VICE with remote monitor enabled:
```bash
x64sc -remotemonitor -remotemonitoraddress ip4://127.0.0.1:6510 &
```

Send commands via TCP:
```bash
echo "l \"test.prg\" 0" | nc localhost 6510
echo "g 0801" | nc localhost 6510
```

**Approach 3: Limit Cycles and Exit**

```bash
x64sc -warp -limitcycles 10000000 -exitscreenshot result.png \
      -autostart test.prg
```

This runs the program for exactly 10 million cycles, takes a screenshot, and exits.
A test harness can then compare the screenshot to a reference image.

### 8.5 CI/CD for C64 Projects

**GitHub Actions Example:**

```yaml
name: Build C64 Project
on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    container:
      image: c64lib/c64libci:latest
    steps:
      - uses: actions/checkout@v4
      - name: Build
        run: make all
      - name: Run tests
        run: |
          x64sc -console -sound -sounddev dummy -warp \
                -limitcycles 5000000 \
                -autostart tests/test_suite.prg \
                -moncommands tests/test_runner.mon
      - name: Upload artifacts
        uses: actions/upload-artifact@v4
        with:
          name: build-output
          path: build/*.prg
```

**CircleCI with c64lib:**

```yaml
version: 2.1
jobs:
  build:
    docker:
      - image: c64lib/c64libci
    steps:
      - checkout
      - run: gradle build
      - run: gradle test
```

The c64lib Docker image includes KickAssembler, VICE (headless), and all
dependencies needed for building and testing KickAssembler projects in CI.


---

## 9. Debugging

### 9.1 VICE Monitor Command Reference

The VICE monitor is the primary debugging tool for C64 development. It is accessed by
pressing Alt+M (or the menu equivalent) in the emulator, or via the remote monitor
protocol.

**Essential Commands:**

| Command               | Shortcut | Description                                    |
|-----------------------|----------|------------------------------------------------|
| `break [addr]`        | `bk`     | Set execution breakpoint                       |
| `watch [addr]`        | `w`      | Set read/write watchpoint                      |
| `trace [addr]`        | `tr`     | Set tracepoint (log without stopping)          |
| `until [addr]`        | `un`     | Set temporary breakpoint (deletes on hit)      |
| `delete [num]`        | `del`    | Remove a breakpoint/watchpoint                 |
| `enable [num]`        |          | Enable a disabled checkpoint                   |
| `disable [num]`       |          | Disable a checkpoint without deleting it       |
| `condition [n] if [e]`| `cond`   | Set conditional expression on checkpoint       |
| `registers`           | `r`      | Show/modify CPU registers                      |
| `step`                | `z`      | Single-step one instruction                    |
| `next`                | `n`      | Step over subroutine calls                     |
| `return`              | `ret`    | Execute until RTS/RTI                          |
| `goto [addr]`         | `g`      | Set PC and continue execution                  |
| `disass [range]`      | `d`      | Disassemble memory                             |
| `mem [range]`         | `m`      | Display memory hex dump                        |
| `> [addr] [data]`     |          | Write data to memory                           |
| `fill [range] [data]` | `f`      | Fill memory region with data                   |
| `hunt [range] [data]` | `h`      | Search for byte pattern in memory              |
| `compare [range] [a]` | `c`      | Compare two memory regions                     |
| `move [range] [addr]` | `t`      | Copy memory block                              |
| `a [addr] [instr]`    |          | Assemble instruction at address                |
| `io [addr]`           |          | Display I/O register values                    |
| `screen`              | `sc`     | Display current screen contents                |
| `backtrace`           | `bt`     | Print JSR call chain                           |
| `cpuhistory [count]`  | `chis`   | Show recently executed instructions            |
| `stopwatch`           |          | Display/reset cycle counter                    |
| `warp [on|off]`       |          | Toggle warp mode                               |
| `keybuf [string]`     |          | Type text into C64 keyboard buffer             |
| `exit`                | `x`      | Leave monitor, resume execution                |
| `quit`                | `q`      | Exit the emulator entirely                     |

**Label Commands:**

| Command                       | Description                                |
|-------------------------------|--------------------------------------------|
| `load_labels "file.vs"` (ll)  | Import labels from assembler               |
| `save_labels "file.vs"` (sl)  | Export current labels                      |
| `add_label addr name` (al)    | Manually add a label                       |
| `delete_label name` (dl)      | Remove a label                             |
| `show_labels` (shl)           | Display all loaded labels                  |
| `clear_labels` (cl)           | Remove all labels                          |

**Profiling Commands (VICE 3.6+):**

| Command                        | Description                                |
|--------------------------------|--------------------------------------------|
| `profile on`                   | Start collecting profiling data            |
| `profile off`                  | Stop collecting profiling data             |
| `profile flat [n]`             | Show top N functions by self time          |
| `profile graph [ctx] [depth]`  | Display call graph                         |
| `profile func [function]`      | Show statistics for specific function      |
| `profile disass [function]`    | Per-instruction profile within function    |
| `profile clear [function]`     | Reset profiling data                       |

**File Operations:**

| Command                             | Description                           |
|-------------------------------------|---------------------------------------|
| `load "file" device [addr]` (l)     | Load file into memory                 |
| `save "file" device addr1 addr2` (s)| Save memory range to file             |
| `bload "file" device addr` (bl)     | Load without 2-byte header            |
| `bsave "file" device addr1 addr2`   | Save without header                   |
| `attach "file" device`              | Mount disk image                      |
| `autostart "file"`                  | Boot a program                        |
| `screenshot "file" [format]`        | Capture screen                        |

### 9.2 Label Files

Label files are the bridge between your source code and the VICE debugger. They map
symbolic names to addresses, so instead of seeing `JSR $1A3F` in the disassembly,
you see `JSR clear_screen`.

**VICE Label File Format (.vs / .lbl):**

```
; VICE monitor label file
; Generated by KickAssembler / ca65 / etc.
al C:0810 .main_loop
al C:0830 .clear_screen
al C:0850 .init_irq
al C:0870 .irq_handler
al C:08A0 .scroll_text
al C:1000 .charset_data
al C:2000 .music_init
al C:2003 .music_play
```

Format: `al <device>:<address> .<label_name>`

Labels must start with a dot (`.`) for VICE to recognize them. The device prefix
is typically `C:` for the computer's main memory.

**Generating Label Files from Assemblers:**

| Assembler      | Command                                                    |
|----------------|------------------------------------------------------------|
| KickAssembler  | `-vicesymbols` flag (generates `.vs` file)                 |
| ca65/ld65      | `ld65 -Ln labels.lbl` flag                                |
| ACME           | `--vicelabels labels.lbl` flag                             |
| 64tass         | `--vice-labels labels.lbl` flag                            |

**Loading Labels in VICE:**

```
# From command line:
x64sc -moncommands labels.vs -autostart program.prg

# From within the monitor:
ll "labels.vs"
```

### 9.3 Source-Level Debugging

Full source-level debugging (seeing your original assembly source alongside execution)
requires debug data files beyond simple label files.

**KickAssembler Debug Data (.dbg):**

KickAssembler can generate comprehensive debug data with the `-debugdump` flag,
producing a `.dbg` file that maps addresses to source file lines. IceBroLite reads
these files to provide integrated source-level debugging.

**IceBroLite Setup:**

1. Build with debug info: `java -jar KickAss.jar main.asm -debugdump -vicesymbols`
2. Start VICE with binary monitor: `x64sc -binarymonitor -autostart main.prg`
3. Launch IceBroLite, connect to `127.0.0.1:6502`
4. IceBroLite automatically finds `.dbg`, `.sym`, or `.vs` files
5. Source code appears in the Code View alongside disassembly

**IceBroLite Debugging Features:**
- Code View: disassembly and source code side-by-side
- Watch View: live expression monitoring (arithmetic, bitwise operators)
- Memory View: real-time inspection and editing
- Register View: CPU register state
- Graphics View: visualize screen memory, sprites, characters in various C64 modes
- Breakpoint/Trace/Watch management
- Symbol and section browsing
- Step, step over, run to, conditional breakpoints

### 9.4 Profiling

**VICE Built-In Profiler (3.6+):**

```
; Enable profiling
profile on

; Run your code for a while...
; (exit to monitor when ready)

; See which routines take the most time
profile flat 20

; Examine a specific routine's call graph
profile graph irq_handler 3

; Per-instruction breakdown
profile disass scroll_routine
```

**Cycle Counter:**

```
; Reset the cycle counter
stopwatch reset

; Run to a specific point
until $0830

; Check elapsed cycles
stopwatch
```

This is invaluable for measuring how many cycles a routine takes, especially for
raster-critical code that must complete within a specific number of scanlines.

### 9.5 Raster Timing Debugging

Raster effects require cycle-exact timing. Debugging them involves:

**1. Visual Border Color Method:**

The classic technique: change the border color at the start and end of your routine.
The height of the colored region shows how much raster time you are using.

```
irq_handler:
    inc $d020       ; set border color (visual timing indicator)
    ; ... your time-critical code ...
    dec $d020       ; restore border color
    rti
```

**2. VICE Raster Line Display:**

VICE can overlay the current raster position, helping you see exactly where the beam
is when your code executes. Enable via Settings > Video > Show raster line.

**3. Cycle Counting with Breakpoints:**

```
; In VICE monitor:
break exec $0850   ; start of routine
break exec $0890   ; end of routine

; When first breakpoint hits:
stopwatch reset

; Continue to second breakpoint:
g

; Check cycles:
stopwatch
; Output: "14 cycles" -- did we fit in our raster budget?
```

**4. PAL Timing Budget:**

| Raster Situation    | Available CPU Cycles |
|---------------------|---------------------|
| Normal line         | 63 per line         |
| Bad line            | 20-23 per line      |
| Total per frame     | 19,656              |
| Visible area        | 200 lines           |
| VBlank area         | 112 lines           |

Code in the visible area must account for bad lines (every 8th line in the default
configuration) stealing 40-43 cycles.


---

## 10. Hardcore Details

### 10.1 VICE Cycle Accuracy Verification

When developing raster effects or cycle-exact code, verifying that VICE matches
real hardware is critical.

**Known VICE (x64sc) Accuracy:**
- CPU: Cycle-exact including undocumented opcodes, page-crossing penalties,
  and RMW dummy write timing
- VIC-II: Accurate sprite DMA timing, badline stealing, border open tricks
- CIA: Cycle-accurate timer behavior including edge cases
- SID: Accurate via reSID-fp (analog filter modeling)

**Where VICE May Differ from Real Hardware:**
- Extremely obscure VIC-II edge cases (mid-instruction register changes)
- SID filter characteristics vary between individual chips
- 1541 drive emulation timing can differ slightly from specific drive revisions

**Verification Strategy:**
1. Write test programs that exhibit the behavior you are relying on
2. Run on VICE x64sc and record results
3. Run on Hoxs64 and compare
4. Run on real hardware (preferably multiple machines) and compare
5. Check community resources (VICE test suite, CSDb compatibility lists)

**Key Test Programs:**
- EmuFuxxor v1 and v2 (emulator detection tests)
- Wolfgang Lorenz test suite (CPU instruction tests)
- VICE's own test suite (included in source distribution)
- Visual VIC-II tests by Pepto

### 10.2 Automated Test Harnesses

**64spec (KickAssembler Unit Testing):**

64spec is a single-file testing framework for KickAssembler that runs tests directly
on the C64 (or in VICE).

```
.import source "64spec.asm"

sfspec: :init_spec()

    :describe("accumulator operations")

    :it("should add correctly")
        lda #$10
        clc
        adc #$20
        :assert_a_equal #$30

    :it("should handle carry")
        lda #$FF
        clc
        adc #$01
        :assert_a_equal #$00
        :assert_c_set

:finish_spec()
```

Run in VICE headless mode for CI:
```bash
x64sc -console -sound -sounddev dummy -warp \
      -limitcycles 5000000 -autostart test_suite.prg
```

**BDD6502 (Behavior-Driven Development):**

BDD6502 by Martin Piper brings Cucumber/Gherkin-style BDD to 6502 development.
Tests are written in human-readable feature files:

```gherkin
Feature: Player movement

  Scenario: Player moves right
    Given I have a running C64 with program "game.prg"
    And I write memory at $d000 with $01
    When I execute 6502 code for 1000 cycles
    Then memory at $0400 should contain $01
```

The framework uses a 6502 simulator (Symon) in the JVM and can also interface
with VICE's remote monitor for cycle-accurate testing.

**sim6502:**

A 6502 unit test CLI with an internal simulator for vanilla 6502/6510/65C02.
For cycle-accurate C64 testing, it provides a VICE backend. It has no concept
of C64-specific hardware (VIC-II, SID, CIA) in simulator mode -- for that, the
VICE backend is required.

**6502 Test Executor:**

A cross-platform unit testing tool by AsaiYusuke specifically for MOS 6502
assembly. Tests can set up memory state, execute code, and make assertions
about registers and memory contents.

### 10.3 KickAssembler Advanced Scripting

KickAssembler's scripting language goes far beyond simple macros. It is essentially
a complete JavaScript-like programming language that runs at assembly time.

**Custom Functions for Data Generation:**

```
// Generate a perspective-correct distance table for a 3D tunnel effect
.function calcTunnelDist(x, y) {
    .var dx = x - 160
    .var dy = y - 100
    .var dist = sqrt(dx*dx + dy*dy)
    .if (dist == 0) .return 0
    .return round(8000 / dist) & $ff
}

tunnel_table:
.for (var y = 0; y < 200; y++) {
    .for (var x = 0; x < 40; x++) {
        .byte calcTunnelDist(x * 8 + 4, y)
    }
}
```

**Importing and Converting Graphics at Assembly Time:**

```
// Load a PNG and convert to C64 multicolor bitmap
.var picture = LoadPicture("artwork.png")
.for (var y = 0; y < 200; y++) {
    .for (var x = 0; x < 160; x++) {
        .eval var color = picture.getPixel(x, y)
        // ... convert to C64 color index ...
    }
}
```

**Java Plugin System:**

KickAssembler supports 3rd-party Java plugins. The most notable are the cruncher
plugins that provide data compression at assembly time:

```
// Using Exomizer compression plugin
.plugin "se.transen.kickass.CruncherPlugins"

.var compressedData = Exomizer("raw_data.bin")
compressed_data:
    .fill compressedData.getSize(), compressedData.get(i)
```

Available cruncher plugins (from kickass-cruncher-plugins):
- ByteBoozer
- Exomizer
- PUCrunch
- Level Squeeze

**Preprocessor (v4.x+):**

```
#import "library.asm"
#importonce              // import only once (include guard)

#define DEBUG
#if DEBUG
    inc $d020            // timing indicator
#endif
```

### 10.4 ca65 Linker Scripts in Depth

The ld65 linker's configuration system is the most flexible in the C64 assembler
world. It allows precise control over memory layout.

**Advanced C64 Linker Configuration:**

```
# Memory areas for a complex C64 project with banked loading
MEMORY {
    # Zero page variables
    ZP:       start = $02,    size = $8E,   type = rw;

    # BASIC stub area
    LOADADDR: start = $0801,  size = $0002, file = %O;
    STUB:     start = $0801,  size = $000D, file = %O;

    # Main program area (below I/O)
    MAIN:     start = $080E,  size = $97F2, file = %O;

    # Area under I/O ($D000-$DFFF) -- usable when I/O is banked out
    UNDERIO:  start = $D000,  size = $1000, file = %O;

    # Area under KERNAL ($E000-$FFFF) -- usable when KERNAL is banked out
    UNDERKERN: start = $E000, size = $2000, file = %O;

    # Separate load file for disk-loaded overlay
    OVERLAY1: start = $4000,  size = $4000, file = "overlay1.prg";
}

SEGMENTS {
    ZEROPAGE:  load = ZP,       type = zp;
    LOADADDR:  load = LOADADDR, type = ro;
    STARTUP:   load = STUB,     type = ro;
    CODE:      load = MAIN,     type = ro;
    RODATA:    load = MAIN,     type = ro;
    DATA:      load = MAIN,     type = rw;
    BSS:       load = MAIN,     type = bss,  define = yes;
    SPRITES:   load = MAIN,     type = ro,   align = $40;
    CHARSET:   load = UNDERIO,  type = ro;
    MUSIC:     load = UNDERKERN, type = ro;
    OVERLAY:   load = OVERLAY1, type = ro;
}
```

**Segment Usage in Assembly:**

```
.segment "ZEROPAGE"
    ptr1:   .res 2
    temp:   .res 1

.segment "CODE"
    jsr init_screen

.segment "SPRITES"
    .align 64              ; sprites must be 64-byte aligned
    .incbin "hero.spr"

.segment "CHARSET"
    .incbin "custom_font.bin"

.segment "MUSIC"
    .incbin "music.bin"

.segment "OVERLAY"
    ; This code is loaded from disk when needed
    .include "level2.s"
```

### 10.5 Tool Integration Pipelines

A sophisticated C64 project may integrate multiple tools in a single build pipeline:

```
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  Multipaint /    │    │  CharPad /       │    │  GoatTracker /   │
│  Pixcen          │    │  SpritePad       │    │  SID Factory II  │
│  (graphics)      │    │  (tiles/sprites) │    │  (music)         │
└────────┬─────────┘    └────────┬─────────┘    └────────┬─────────┘
         │                       │                       │
         v                       v                       v
    koala.bin               tiles.bin               music.bin
    bitmap.bin              sprites.bin             player.bin
         │                       │                       │
         └───────────┬───────────┘───────────┬───────────┘
                     │                       │
                     v                       v
            ┌────────────────────────────────────────────┐
            │  KickAssembler / ca65                      │
            │  (assembles code, imports all binaries,    │
            │   generates label files and debug data)    │
            └────────────────────┬───────────────────────┘
                                 │
                                 v
                     ┌───────────────────────┐
                     │  Cruncher (Exomizer,  │
                     │  ByteBoozer, etc.)    │
                     │  -- optional step --  │
                     └───────────┬───────────┘
                                 │
                                 v
                     ┌───────────────────────┐
                     │  c1541 / cartconv     │
                     │  (create D64 / CRT)   │
                     └───────────┬───────────┘
                                 │
                    ┌────────────┼────────────┐
                    v            v            v
              ┌──────────┐ ┌──────────┐ ┌──────────┐
              │  VICE    │ │ Real HW  │ │  CI/CD   │
              │  testing │ │ via cart │ │  tests   │
              └──────────┘ └──────────┘ └──────────┘
```

**VICE c1541 Tool:**

The c1541 command-line tool (included with VICE) creates and manipulates D64, D71,
D81, and other Commodore disk image formats:

```bash
# Create a new D64 image and add files
c1541 -format "mygame,01" d64 mygame.d64
c1541 -attach mygame.d64 -write main.prg "main"
c1541 -attach mygame.d64 -write level1.prg "level1"
c1541 -attach mygame.d64 -write music.prg "music"
c1541 -attach mygame.d64 -list
```

**cartconv Tool:**

Also included with VICE, cartconv creates CRT cartridge images:

```bash
# Create an 8K generic cartridge
cartconv -t normal -i game.bin -o game.crt -l 0x8000

# Create an EasyFlash cartridge
cartconv -t easyflash -i easyflash.bin -o game.crt
```

### 10.6 Flash Cart Development Workflow

Modern C64 developers often use flash cartridges for testing on real hardware.

**Kung Fu Flash:**
- Open source cartridge that emulates multiple cartridge types
- Loads PRG files, D64 disk images, and CRT cartridge images from microSD
- Transfer time: under 10 seconds for most files
- Supported formats: Generic 8K/16K/Ultimax, Action Replay, Final Cartridge III,
  EasyFlash, Ocean, Magic Desk, and many more
- Active development (moved to Codeberg: codeberg.org/KimJorgensen/KungFuFlash)

**EasyFlash:**
- 1 MB flash cartridge with multiple bank configurations
- CRT file format for distribution
- EasyProg: native C64 tool for programming the flash
- EasySDK: development kit for creating EasyFlash programs
- Supports up to 1 MB of code/data in 64 banks of 16 KB each
- Two 8 KB ROM windows visible simultaneously (ROML at $8000, ROMH at $A000 or $E000)
- 256 bytes of battery-backed RAM for save games

**1541 Ultimate / Ultimate 64:**
- Sophisticated hardware that integrates SD card, REU emulation, GeoRAM, cartridge
  emulation, and a full 1541 drive emulator
- Network connectivity for file transfer
- Can load programs directly from a network share
- Ideal for development: instant loading, no wear on vintage hardware

**Development Workflow with Flash Carts:**

1. Build on PC (assembler + Makefile)
2. Copy output to SD card (or transfer via network for Ultimate)
3. Insert cart, power on C64
4. Select program from cart menu
5. Test on real hardware
6. Note any differences from emulator behavior
7. Return to step 1

For the most efficient workflow, use VICE for 95% of testing and reserve real
hardware testing for:
- SID filter tuning (each physical SID chip sounds different)
- Timing-critical effects that push emulator accuracy limits
- Final release verification
- Drive loading timing
- Hardware-specific peripherals (paddles, mice, cartridge banking)


---

## References

### Cross-Assemblers

- KickAssembler Official: https://theweb.dk/KickAssembler/
- KickAssembler Reference Manual: https://theweb.dk/KickAssembler/KickAssembler.pdf
- KickAssembler C64-Wiki: https://www.c64-wiki.com/wiki/KickAssembler
- ACME GitHub: https://github.com/meonwax/acme
- ACME SourceForge: https://sourceforge.net/projects/acme-crossass/
- ca65 Users Guide: https://cc65.github.io/doc/ca65.html
- ld65 Users Guide: https://cc65.github.io/doc/ld65.html
- 64tass SourceForge: https://sourceforge.net/projects/tass64/
- 64tass GitHub Mirror: https://github.com/irmen/64tass
- DASM Official: https://dasm-assembler.github.io/
- Cross-Assembler Comparison: https://bumbershootsoft.wordpress.com/2016/01/31/a-tour-of-6502-cross-assemblers/
- Cross Assembler C64-Wiki: https://www.c64-wiki.com/wiki/Cross_Assembler

### C Compilers

- cc65 Official: https://cc65.github.io/
- cc65 C64 Documentation: https://cc65.github.io/doc/c64.html
- cc65 GitHub: https://github.com/cc65/cc65
- cc65 C64-Wiki: https://www.c64-wiki.com/wiki/cc65
- Oscar64 GitHub: https://github.com/drmortalwombat/oscar64
- Oscar64 C64-Wiki: https://www.c64-wiki.com/wiki/oscar64
- 6502 C Compilers Comparison: https://gglabs.us/node/2293

### Higher-Level Languages

- Prog8 Documentation: https://prog8.readthedocs.io/en/stable/
- Prog8 GitHub: https://github.com/irmen/prog8
- Millfork GitHub: https://github.com/KarolS/millfork
- Millfork Documentation: https://karols.github.io/millfork/
- XC=BASIC Wiki: https://xc-basic.net/
- XC=BASIC v3 GitHub: https://github.com/neilsf/xc-basic3
- CBM prg Studio: https://www.ajordison.co.uk/
- Cross-Development C64-Wiki: https://www.c64-wiki.com/wiki/Cross-Development

### Emulators

- VICE Official: https://vice-emu.sourceforge.io/
- VICE Monitor Manual: https://vice-emu.sourceforge.io/vice_12.html
- VICE Binary Monitor: https://vice-emu.sourceforge.io/vice_13.html
- VICE Snapshots: https://vice-emu.sourceforge.io/vice_9.html
- Hoxs64 Official: https://www.hoxs64.net/
- Hoxs64 GitHub: https://github.com/davidhorrocks/hoxs64
- Using Emulators with cc65: https://cc65.github.io/doc/debugging.html

### Debuggers

- IceBro GitHub: https://github.com/Sakrac/IceBro
- IceBroLite GitHub: https://github.com/Sakrac/IceBroLite
- IceBroLite Manual: https://sakrac.github.io/IceBroLite/
- C64 Debugger: https://github.com/sunsided/c64-debugger
- VICE Remote Development: https://codebase.c64.org/doku.php?id=base:using_a_running_vice_session_for_development
- Modern VICE PDB Monitor: https://github.com/MihaMarkic/modern-vice-pdb-monitor

### Graphics Tools

- CharPad C64 Pro: https://subchristsoftware.itch.io/charpad-c64-pro
- CharPad C64 Free: https://subchristsoftware.itch.io/charpad-c64-free
- SpritePad C64 Pro: https://subchristsoftware.itch.io/spritepad-c64-pro
- Pixcen GitHub: https://github.com/Hammarberg/pixcen
- Multipaint: http://multipaint.kameli.net/
- PETSCII Editor: https://petscii.krissz.hu/
- png2prg: https://github.com/staD020/png2prg
- SPOT: https://github.com/spartaomg/SPOT
- Convertron3000: https://github.com/fieserWolF/convertron3000
- C64 Graphics Tools Directory: https://c64gfx.com/gfxtools

### Music Tools

- GoatTracker 2: https://sourceforge.net/projects/goattracker2/
- SID-Wizard: https://sourceforge.net/projects/sid-wizard/
- CheeseCutter: http://theyamo.kapsi.fi/ccutter/
- SID Factory II: https://blog.chordian.net/sf2/
- SID Factory II GitHub: https://github.com/Chordian/sidfactory2
- High Voltage SID Collection: https://www.hvsc.c64.org/
- DeepSID (online SID player): https://deepsid.chordian.net/

### Build Systems and CI/CD

- c64lib Documentation: https://c64lib.github.io/
- Gradle Retro Assembler Plugin: https://github.com/c64lib/gradle-retro-assembler-plugin
- Using GNU Make with cc65: https://cc65.github.io/doc/using-make.html
- KickAssembler Libraries Guide: https://maciejmalecki.github.io/blog/assembler-libraries

### Testing Frameworks

- 64spec: https://github.com/64bites/64spec
- BDD6502: https://github.com/martinpiper/BDD6502
- sim6502: https://github.com/barryw/sim6502
- 6502 Test Executor: https://github.com/AsaiYusuke/6502_test_executor
- Klaus 6502 Functional Tests: https://github.com/Klaus2m5/6502_65C02_functional_tests

### Flash Carts and Hardware

- Kung Fu Flash: https://github.com/KimJorgensen/KungFuFlash
- EasyFlash: https://skoe.de/easyflash/efintro/
- EasyFlash C64-Wiki: https://www.c64-wiki.com/wiki/EasyFlash

### IDE Support

- Kick Assembler 8-Bit Retro Studio (VS Code): https://marketplace.visualstudio.com/items?itemName=paulhocker.kick-assembler-vscode-ext
- VS64 (VS Code): https://marketplace.visualstudio.com/items?itemName=rosc.vs64
- ACME Cross Assembler (VS Code): https://marketplace.visualstudio.com/items?itemName=TonyLandi.acmecrossassembler
- C64 Studio: https://endurion.itch.io/c64studio
- Relaunch64: https://www.c64-wiki.com/wiki/Cross_Assembler (listed under tools)

### Community Resources

- Codebase 64: https://codebase64.org/
- CSDb (C64 Scene Database): https://csdb.dk/
- Lemon64 Forums: https://www.lemon64.com/forum/
- 6502.org Tools: http://www.6502.org/tools/asm/
- C64-Wiki: https://www.c64-wiki.com/
