# Cork

A programming language that compiles to Commodore 64 machine code.

Cork pairs a modern developer experience with the raw power of the C64's hardware. Write expressive, structured code with structs, scenes, message-passing syntax, and fixed-point math — the compiler handles the 6510 assembly, memory layout, and hardware register juggling.

## Quick Start

```bash
# Compile a Cork program
dotnet run --project src/Cork.Compiler -- samples/gravity.cork -o gravity.prg

# Run in VICE emulator
x64sc -autostart gravity.prg
```

## What Cork Looks Like

```cork
struct Ball {
    fixed x = 100.0;
    fixed y = 80.0;
    sfixed dx = 0.75;
    sfixed dy = 0.5;

    update: {
        x += dx;
        y += dy;
        if (x > 220.0) { dx = -0.75; }
        if (x < 40.0)  { dx = 0.75; }
        if (y > 210.0) { dy = -0.5; }
        if (y < 60.0)  { dy = 0.5; }
    }
}

entry scene Game {
    hardware {
        border: Color.black;
        background: Color.black;
    }

    Ball ball;

    enter {
        poke: 0x07F8 value: spritePtr;
        poke: 0xD015 value: 1;
        poke: 0xD027 value: 1;
    }

    frame {
        ball update:;
        poke: 0xD000 value: ball.x;
        poke: 0xD001 value: ball.y;
    }

    raster 100 {
        poke: 0xD020 value: 6;
    }

    raster 200 {
        poke: 0xD020 value: 0;
    }
}
```

## Design Philosophy

- **Expression over ceremony** — Code should read clearly and feel good to write.
- **Compile-time everything** — Type checks, memory validation, and shadowing prevention are all resolved at compile time. Zero runtime overhead for safety.
- **Fully static memory** — No heap, no garbage collection, no reference counting. Every variable's size and location is known at compile time.
- **The hardware is the SDK** — VIC-II, SID, CIA, sprites, and raster interrupts surface as language constructs, not a bolted-on library.
- **Scenes as architecture** — Programs are organized into scenes (title screen, game level, etc.) that the compiler packs and validates. If it doesn't fit in 64KB, you get a compiler error, not a runtime crash.
- **Dot means data, colon means code** — `enemy.health` is always a direct field read. `enemy update:` is always a method call. No hidden costs.

## Syntax

Cork blends C-style declarations with Smalltalk/Objective-C message passing.

### Variables and Types

```cork
byte x = 5;              // unsigned 8-bit (0-255)
sbyte velocity = -2;     // signed 8-bit (-128 to 127)
word score = 1000;        // unsigned 16-bit (0-65535)
fixed spriteX = 160.0;   // unsigned 8.8 fixed-point
sfixed speed = -1.5;      // signed 8.8 fixed-point
var inferred = 42;        // type inferred from literal
const byte MAX = 200;     // compile-time constant (zero cost)
```

### Message Passing

The colon means "I'm calling something." Always.

```cork
// Method calls with arguments
enemy moveTo: 100 y: 50;
player takeDamage: weapon.power + bonus;

// No-arg calls
enemy update:;
Music stop:;

// In expressions (parens delimit)
var d = (enemy distanceFrom: player);
```

### Structs

Value types with methods. No inheritance, no heap — composition for reuse.

```cork
struct Player {
    byte x = 160;
    byte health = 5;

    moveLeft: {
        x -= 1;
    }

    moveRight: {
        x += 1;
    }
}

Player player;
player moveLeft:;    // method call
player.x;            // field access
```

### Scenes

The primary architectural unit. Each scene is a self-contained state with its own hardware setup, variables, and lifecycle.

```cork
entry scene TitleScreen {
    hardware {
        border: Color.blue;
        background: Color.black;
    }

    frame {
        if (joystick.port2.fire) {
            go GameLevel;
        }
    }
}

scene GameLevel {
    hardware {
        border: Color.black;
        background: Color.black;
    }

    enter { clearScreen:; }
    frame { /* game logic */ }
    exit { /* cleanup */ }
}
```

### Raster Interrupts

Declare raster blocks in a scene. The compiler generates the full IRQ chain — CIA setup, vector installation, dispatch, register save/restore.

```cork
raster 50 {
    poke: 0xD020 value: 2;   // red border
    poke: 0xD021 value: 2;
}

raster 200 {
    poke: 0xD020 value: 0;   // black border
    poke: 0xD021 value: 0;
}
```

### Control Flow

```cork
// For loops
for (byte i = 0; i < 40; i += 1) {
    poke: 0x0400 + i value: 32;
}

// Switch (no fallthrough by default)
switch (gameState) {
    case State.playing: updateGame:;
    case State.paused:  drawPause:;
}

// Expression switch
switch (true) {
    case health < 10: flashWarning:;
    case health < 30: showDamage:;
    default:          showNormal:;
}
```

### Enums

```cork
enum State : byte {
    title = 0,
    playing = 1,
    paused = 2,
    gameover = 3
}

flags enum SpriteFlags : byte {
    visible    = 0x01,
    multicolor = 0x02,
    expandX    = 0x04
}
```

### Fixed-Point Math

8.8 fixed-point for sub-pixel movement. The compiler includes a runtime multiply library (only emitted when used).

```cork
fixed ballY = 60.0;
sfixed velY = 0.0;
sfixed gravity = 0.1;

velY += gravity;            // smooth acceleration
ballY += velY;              // sub-pixel position
velY *= -0.9;               // signed multiply with dampening
poke: 0xD001 value: ballY;  // integer part used for sprite position
```

## Compiler Output

| Format | Description |
|--------|-------------|
| `.prg` | Standard C64 program with BASIC SYS stub |

The compiler generates a complete, ready-to-run C64 binary: BASIC stub for auto-start, 6510 machine code, data section, and runtime library (only the parts you use).

## What Cork Does NOT Have

| Feature | Why |
|---------|-----|
| Classes / Inheritance | Structs + composition. Simpler, no vtable overhead. |
| Heap / GC / Refcounting | Fully static memory. All lifetimes known at compile time. |
| Null | Every value always exists. Use `bool active` patterns. |
| Inline assembly | The language should be expressive enough without it. |
| Generics | Complexity not justified for 8-bit target. |
| Lambdas | Runtime cost too high on a 1 MHz CPU. |
| `this` keyword | Fields are directly in scope. No shadowing allowed. |

## Building the Compiler

Requires .NET 10 SDK.

```bash
dotnet build Cork.slnx
dotnet test --solution Cork.slnx --ignore-exit-code 8
```

The compiler is AOT-compatible — publish as a native binary:

```bash
dotnet publish src/Cork.Compiler -c Release
```

## Sample Programs

| Sample | Description |
|--------|-------------|
| `hello.cork` | Hello World — write text to screen |
| `joystick.cork` | Move a character with the joystick |
| `scenes.cork` | Two scenes with transitions and global state |
| `structs.cork` | Struct with fields and methods |
| `word.cork` | 16-bit arithmetic, full-screen movement |
| `forloop.cork` | For loops, break, continue |
| `enumswitch.cork` | Enums and switch statements |
| `signed.cork` | Signed byte for velocity |
| `fixedpoint.cork` | Smooth sprite bouncing with fixed-point |
| `gravity.cork` | Physics: gravity, dampened bouncing, sfixed multiply |
| `raster.cork` | Raster interrupt color bars |
| `sprite.cork` | Hardware sprite with joystick control |
| `combined.cork` | Everything: structs, sprites, raster, scenes, globals |

## Project Structure

```
Cork.slnx                    .NET solution
src/
  Cork.Compiler/              CLI entry point
  Cork.Language/              Lexer, Parser, AST
  Cork.CodeGen/               6510 code generation
  Cork.Output/                .prg file writer
  Cork.Runtime/               Runtime library (embedded)
tests/
  Cork.Language.Tests/        Lexer and parser tests
  Cork.CodeGen.Tests/         Code generation tests
  Cork.Integration.Tests/     Snapshot tests for all samples
samples/                      13 example programs
design/
  language-design.md          Language specification
  grammar.ebnf                Formal EBNF grammar
  compiler-plan.md            Compiler architecture plan
research/                     22 C64 reference documents
```

## Status

Cork is in active development. The compiler handles a substantial subset of the language: structs, scenes, raster interrupts, sprites, fixed-point math, enums, switch, for loops, and more. See the language design doc for the full feature list and what's planned next.

## License

TBD
