# Cork

**A programming language for the Commodore 64.**

Cork pairs a modern developer experience with the raw power of the C64's hardware. Write expressive, structured code — the compiler handles 6510 assembly, memory layout, and hardware register juggling.

```
                    ╭──────────────────────────────────╮
   .cork source --> │  Lexer  Parser  CodeGen  Output  │ --> .prg binary
                    ╰──────────────────────────────────╯
                           Cork Compiler (.NET)
```

---

## Install

```bash
brew install corklang/tap/corklang
```

Then compile and run:

```bash
cork samples/gravity.cork -o gravity.prg
x64sc -autostart gravity.prg
```

### Building from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build Cork.slnx
dotnet run --project src/Cork.Compiler -- samples/gravity.cork -o gravity.prg
```

---

## What Cork Looks Like

A bouncing ball with gravity, sprite pixel art, raster color bars, and joystick input — in one file:

```
entry scene Demo {
    hardware {
        border: Color.darkGrey;
        background: Color.black;
    }

    sprite 0 ball {
        data: `
            . . . . . . . . . # # # # # # . . . . . . . . .
            . . . . . . . # # # # # # # # # # . . . . . . .
            . . . . . # # # # # # # # # # # # # # . . . . .
            . . . . # # # # # # # # # # # # # # # # . . . .
            . . . # # # # # # # # # # # # # # # # # # . . .
            . . # # # # # # # # # # # # # # # # # # # # . .
            . # # # # # # # # # # # # # # # # # # # # # # .
            . # # # # # # # # # # # # # # # # # # # # # # .
            . # # # # # # # # # # # # # # # # # # # # # # .
            . . # # # # # # # # # # # # # # # # # # # # . .
            . . . # # # # # # # # # # # # # # # # # # . . .
            . . . . . # # # # # # # # # # # # # # . . . . .
            . . . . . . . . . # # # # # # . . . . . . . . .
        `;
        x: 72; y: 55;
        color: Color.white;
    }

    fixed ballY = 55.0;
    sfixed velY = 0.0;
    sfixed gravity = 0.1;

    frame {
        velY += gravity;
        ballY += velY;

        if (ballY > 231.0) {
            ballY = 231.0;
            velY *= -0.7;           // dampened bounce
        }

        poke: 0xD001 value: ballY;  // integer part -> sprite Y

        if (joystick.port2.left)  { ball.x -= 1; }
        if (joystick.port2.right) { ball.x += 1; }
        if (joystick.port2.fire)  { velY = -4.0; }
    }

    raster 100 { poke: 0xD020 value: 6; }  // gold bar
    raster 200 { poke: 0xD020 value: 0; }  // back to black
}
```

The compiler turns this into a ready-to-run `.prg` with BASIC stub, 6510 machine code, sprite data, and IRQ chain — no manual assembly required.

---

## Design Philosophy

| Principle | What it means |
|:--|:--|
| **Expression over ceremony** | Code should read clearly and feel good to write |
| **Compile-time everything** | Type checks, memory validation, shadowing prevention. Zero runtime overhead |
| **Fully static memory** | No heap, no GC, no refcounting. All lifetimes known at compile time |
| **The hardware is the SDK** | VIC-II, SID, CIA, sprites, raster interrupts are language constructs |
| **Scenes as architecture** | Programs organized into scenes the compiler packs and validates |
| **Dot means data, colon means code** | `enemy.health` reads a field. `enemy update:` calls a method. No hidden costs |

---

## Language Overview

### Types

```
byte x = 5;              // unsigned 8-bit (0-255)
sbyte velocity = -2;     // signed 8-bit (-128 to 127)
word score = 1000;       // unsigned 16-bit (0-65535)
fixed spriteX = 160.0;   // unsigned 8.8 fixed-point
sfixed speed = -1.5;     // signed 8.8 fixed-point
bool alive = true;
string[16] name = "PLAYER ONE";
var inferred = 42;       // type inferred from literal
const byte MAX = 200;    // compile-time constant (zero cost)
```

### Message Passing

Cork uses Smalltalk/Objective-C-style method calls. The colon means "I'm calling something." Always.

```
enemy moveTo: 100 y: 50;
player takeDamage: weapon.power + bonus;
enemy update:;

var d = (enemy distanceFrom: player);
```

### Structs

Value types with methods. No inheritance, no heap — composition for reuse.

```
struct Position {
    byte x = 0;
    byte y = 0;
}

struct Enemy {
    Position pos;
    byte health = 3;

    takeDamage: (byte amount) {
        health -= amount;
    }
}
```

### Scenes and Hardware

Each scene is a self-contained state with its own hardware setup, variables, and lifecycle.

```
entry scene TitleScreen {
    hardware {
        border: Color.blue;
        background: Color.black;
    }

    enter { printAt: 175 text: "PRESS FIRE"; }

    frame {
        if (joystick.port2.fire) { go GameLevel; }
    }
}
```

### Sprites

Inline pixel art with `#`/`.` for hi-res and `1`/`2`/`3`/`.` for multicolor. The compiler handles VIC-II bank placement, pointer registers, and auto-sync.

```
sprite 1 alien {
    data: `
        .   .   2   2   2   2   2   2   .   .   .   .
        .   2   1   1   2   2   1   1   2   .   .   .
        2   2   2   2   2   2   2   2   2   2   .   .
        2   2   3   2   2   2   2   3   2   2   .   .
        2   2   2   3   3   3   3   2   2   2   .   .
    `;
    x: 100; y: 80;
    color: Color.lightGreen;
    multicolor: true;
}
```

### Raster Interrupts

Declare raster blocks — the compiler generates the full IRQ chain (CIA setup, vector installation, dispatch, register save/restore).

```
raster 50  { poke: 0xD020 value: 2; }   // red
raster 150 { poke: 0xD020 value: 6; }   // blue
raster 250 { poke: 0xD020 value: 0; }   // black
```

### Control Flow

```
for (byte i = 0; i < 40; i += 1) {
    poke: 0x0400 + i value: 32;
}

switch (gameState) {
    case State.playing: updateGame:;
    case State.paused:  drawPause:;
    default:            showMenu:;
}
```

### Enums

```
enum State : byte {
    title, playing, paused, gameover
}

flags enum SpriteFlags : byte {
    visible    = 0x01,
    multicolor = 0x02,
    expandX    = 0x04
}
```

### Fixed-Point Math

8.8 fixed-point for sub-pixel movement. The runtime multiply library is only emitted when used.

```
fixed ballY = 60.0;
sfixed velY = 0.0;
sfixed gravity = 0.1;

velY += gravity;            // smooth acceleration
ballY += velY;              // sub-pixel position
velY *= -0.9;               // signed multiply with dampening
poke: 0xD001 value: ballY;  // integer part -> sprite Y
```

---

## Standard Library

```
import "stdlib/screen.cork";
import "stdlib/math.cork";
```

Installed automatically with `brew install corklang/tap/corklang`. Modules available:

| Module | What it provides |
|:--|:--|
| `screen` | `clearScreen:`, `fillScreen:`, `setChar:`, `hline:`, `vline:`, `drawBox:` |
| `color` | `setBorderColor:`, `setBackgroundColor:` |
| `math` | `abs:`, `min:and:`, `max:and:`, `clamp:low:high:`, `lerp:to:t:` |
| `input` | `waitForFire:`, `waitForJoystick:`, `delay:` |
| `keyboard` | Direct CIA1 matrix scanning, PETSCII key constants |
| `print` | `printByte:at:`, `printHex:at:`, `printWord:at:` |
| `bitmap` | Bitmap mode helpers, `plotPixel`, `clearPixel` |
| `scroll` | `scrollUp:`, `shiftRowLeft:`, `shiftRowRight:` |
| `sprite` | Hardware sprite utilities |
| `random` | Random number generation |
| `memory` | Memory utilities |

---

## What Cork Does NOT Have

| Feature | Why |
|:--|:--|
| Classes / Inheritance | Structs + composition. Simpler, no vtable overhead |
| Heap / GC / Refcounting | Fully static memory. All lifetimes known at compile time |
| Null | Every value always exists. Use `bool active` patterns |
| Inline assembly | The language should be expressive enough without it |
| Generics | Complexity not justified for 8-bit target |
| Lambdas | Runtime cost too high on a 1 MHz CPU |
| `this` keyword | Fields are directly in scope. No shadowing allowed |

---

## Samples

The [`samples/`](samples/) directory contains 33 example programs:

| | | |
|:--|:--|:--|
| **Basics** | **Hardware** | **Advanced** |
| `hello` — text output | `sprite` — joystick-controlled sprite | `gravity` — physics with fixed-point |
| `joystick` — joystick input | `spritepattern` — inline pixel art | `multicolor` — 4-color sprites + collision |
| `forloop` — loops, break, continue | `raster` — IRQ color bars | `gfxmodes` — all 5 VIC-II modes |
| `enumswitch` — enums and switch | `keyboard` — CIA matrix scanning | `pixeldemo` — bitmap pixel plotting |
| `structs` — fields and methods | `strings` — text and screen codes | `combined` — multi-scene game |
| `composition` — nested structs | `imports` — modular source files | `sinecurve` — 320-point sine lookup |
| `fixedpoint` — sub-pixel movement | `scroll` — hardware scrolling | `arrayparam` — pass-by-reference |

---

## Project Structure

```
Cork.slnx                       .NET solution
src/
  Cork.Compiler/                 CLI entry point
  Cork.Language/                 Lexer, Parser, AST
  Cork.CodeGen/                  6510 code generation
  Cork.Output/                   .prg file writer
  Cork.Runtime/                  Runtime library (embedded in output)
tests/
  Cork.Language.Tests/           Lexer and parser tests
  Cork.CodeGen.Tests/            Code generation tests
  Cork.Integration.Tests/        Snapshot tests for all samples
samples/                         33 example programs
stdlib/                          Standard library modules
design/
  language-design.md             Language specification
  grammar.ebnf                   Formal EBNF grammar
  compiler-plan.md               Compiler architecture
```

---

## Status

Cork is in active development. The compiler handles structs, scenes, raster interrupts, sprites (hi-res and multicolor), fixed-point math, enums, switch, for/for-each loops, strings, arrays, keyboard input, bitmap graphics, dead code elimination, constant folding, and a peephole optimizer. See [`design/language-design.md`](design/language-design.md) for the full specification.

## License

[MIT](LICENSE)
