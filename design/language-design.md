# Cork Language Design

## Vision

Cork is a high-level, expressive programming language that compiles to Commodore 64 machine code. It pairs a modern developer experience — OOP, type inference, null safety, automatic memory management — with a built-in SDK that makes C64 hardware feel native to the language. The compiler, written in C# (.NET 10, AOT compatible), handles all the brutal realities of the 6510 CPU and 64KB address space so the developer can focus on their vision.

**Primary audience:** Game developers who want productivity and expression.  
**Secondary audience:** General application developers targeting the C64.

---

## Core Principles

1. **Expression over ceremony** — The language should read clearly and feel good to write.
2. **Compile-time everything** — Every safety feature (null checks, type checks, memory validation) is resolved at compile time. Zero runtime overhead for safety.
3. **No manual memory management** — The compiler owns the memory map. Reference counting handles object lifetimes, with static lifetime analysis eliminating refcount overhead where possible.
4. **The hardware is the SDK** — C64 hardware (VIC-II, SID, CIA, sprites, raster interrupts) surfaces as declarative, first-class language constructs — not a bolted-on library.
5. **Scenes as architecture** — Programs are organized into scenes that the compiler packs, validates, and loads automatically. If it doesn't fit in memory, it's a compiler error — not a runtime crash.
6. **The colon means "invoke"** — The colon is the message-passing operator. It always means "I'm calling something." It never appears in type annotations or declarations.

---

## Syntax Overview

Cork's syntax blends C-style declarations with Objective-C/Smalltalk-style message passing. Semicolons and braces are required. The colon is reserved exclusively for method calls and declarations.

```cork
// Variable declaration — C-style, no colons
byte score = 0;
var speed = 1.5;                  // inferred as fixed
const byte MAX_LIVES = 3;

// Message-passing calls — colons signal invocation
player moveTo: 100 y: 50;
bullet spawn: player.x y: player.y heading: Direction.up;

// No-arg calls — trailing colon
player update:;
Music stop:;

// Property access — dot syntax
var h = enemy.health;
enemy.x = 100;
```

---

## Type System

### Primitive Types

| Type     | Size   | Description                        |
|----------|--------|------------------------------------|
| `byte`   | 8-bit  | Unsigned 0-255                     |
| `sbyte`  | 8-bit  | Signed -128 to 127                 |
| `word`   | 16-bit | Unsigned 0-65535                   |
| `sword`  | 16-bit | Signed -32768 to 32767             |
| `bool`   | 8-bit  | true/false                         |
| `fixed`  | 16-bit | 8.8 fixed-point for sub-pixel math |
| `string` | varies | PETSCII string (v1: basic support) |

### Built-in Hardware Types

These are lowercase built-in types that behave like types but represent C64 hardware resources. They are always available — no imports needed.

| Type       | Description                           |
|------------|---------------------------------------|
| `sprite`   | A hardware sprite (data + properties) |
| `charset`  | A character set (2KB of glyph data)   |
| `music`    | A SID music file                      |
| `sound`    | A sound effect                        |
| `tilemap`  | A tile/character map                  |

### Variable Declarations — C-style

The type appears before the name. No colons in declarations. `var` enables type inference. `const` for immutable values.

```cork
// Explicit type
byte x = 5;
word score = 0;
string name = "HELLO";
Enemy? target = null;          // nullable

// Inferred type
var x = 5;                     // byte
var speed = 1.5;               // fixed
var pos = 1000;                // word

// Constants
const byte MAX_LIVES = 3;
const MAX_ENEMIES = 8;         // inferred
```

### Null Safety

All types are non-nullable by default. Use `?` to mark a type as nullable. The compiler enforces null checks at compile time — zero runtime cost.

```cork
Enemy? target = null;          // nullable
Enemy player = new Enemy:;     // non-nullable, must be initialized

// Compiler error: target might be null
target takeDamage: 1;

// OK: null check
if (target != null) {
    target takeDamage: 1;
}

// OK: null-conditional
target?.takeDamage: 1;
```

### Fixed-Size Arrays

```cork
byte[20] bullets;                        // 20-element byte array
Enemy[8] enemies;                        // 8-element array of Enemy
const byte[256] sineTable = { ... };     // initialized constant array
```

### Enums and Constants

```cork
enum Direction : byte {
    up = 0,
    down = 1,
    left = 2,
    right = 3
}

const byte MAX_ENEMIES = 8;
const byte RASTER_SPLIT = 200;
```

### Flags Enums

```cork
flags enum SpriteFlags : byte {
    none       = 0x00,
    visible    = 0x01,
    multicolor = 0x02,
    expandX    = 0x04,
    expandY    = 0x08
}
```

### Numeric Literals

```cork
byte a = 255;          // decimal
byte b = 0xFF;         // hexadecimal
word c = 10_000;       // underscore separator
word d = 0xFF_00;      // hex with separator
```

---

## Message-Passing Syntax

Cork uses Smalltalk/Objective-C-style message passing for all method calls. The colon is the universal "invoke" operator.

### The Rules

| What | Syntax |
|------|--------|
| Call with args | `receiver segment: arg segment: arg;` |
| Call with args in expression | `(receiver segment: arg segment: arg)` |
| No-arg call | `receiver method:;` |
| Property access | `receiver.property` |
| Constructor (no-arg) | `new Type:;` |
| Constructor (with args) | `new Type segment: arg segment: arg;` |

The colon means "I'm sending a message." The dot means "I'm accessing data."

### Method Calls

```cork
// With arguments — colons delimit each argument
enemy moveTo: 100 y: 50;
enemy takeDamage: (weapon.power + bonus);
Music play: bgTheme;

// Spacing around colons is flexible
enemy moveTo:100 y:50;
enemy moveTo: 100 y: 50;

// Complex expressions use parens
enemy takeDamage: (weapon.power + bonus);

// No-arg calls — trailing colon
enemy update:;
Music stop:;
player respawn:;

// In expressions — parens delimit the message send
var d = (enemy distanceFrom: player);
if ((enemy distanceFrom: player) > 5) { ... }
screen draw: (enemy distanceFrom: player);
```

### Method Declarations

Return type is C-style (before the name). No `func` keyword — the signature is enough.

```cork
// Void, no args
update: {
    animFrame = (animFrame + 1) % 4;
}

// Void, with args
moveTo: (byte x) y: (byte y) {
    this.x = x;
    this.y = y;
}

// With return type
byte distanceFromX: (byte x) toY: (byte y) {
    // ...
}

// The full method identity is its segment names: "distanceFromX:toY:"
```

### Constructors

Declared with `ctor`. Called with `new Type`.

```cork
class Enemy : GameEntity, IDamageable {
    private byte health;
    private byte x;

    // No-arg constructor
    ctor: {
        health = 3;
        x = 0;
    }

    // Named constructor
    ctor withHealth: (byte h) atX: (byte startX) {
        health = h;
        x = startX;
    }
}

// Usage:
var e1 = new Enemy:;
var e2 = new Enemy withHealth: 100 atX: 50;
```

---

## Object-Oriented Programming

### Classes

Single inheritance, multiple interfaces. C#-style syntax.

```cork
class Enemy : GameEntity, IDamageable {
    private byte health = 3;
    private byte animFrame = 0;

    public takeDamage: (byte amount) {
        health -= amount;
        if (health == 0) {
            destroy:;
        }
    }

    public update: {
        animFrame = (animFrame + 1) % 4;
    }
}
```

### Interfaces

```cork
interface IDamageable {
    takeDamage: (byte amount);
    byte health { get; }
}

interface IAnimatable {
    animate:;
}
```

### Abstract Classes

```cork
abstract class GameEntity {
    public fixed x = 0;
    public fixed y = 0;
    public bool active = false;

    public abstract update:;

    public fixed distanceTo: (GameEntity other) {
        // ...
    }
}
```

### Properties

Zero-cost when trivial (compiled to direct field access). Only generates method calls when custom logic is present.

```cork
class Paddle {
    private byte _x = 0;

    // Trivial — compiles to direct field access
    public byte y { get; set; }

    // Custom logic — compiles to method calls
    public byte x {
        get { return _x; }
        set {
            _x = (value clamp: 0 max: 255);
        }
    }
}
```

### Access Modifiers

| Modifier    | Visibility                                |
|-------------|-------------------------------------------|
| `public`    | Accessible from anywhere                  |
| `private`   | Accessible only within the declaring type |
| `protected` | Accessible within the type and subclasses |

---

## Memory Management

### Static Ownership

Cork uses fully static ownership — no reference counting, no heap allocator, no garbage collection. Every object's lifetime is known at compile time. Zero runtime overhead.

Objects live in one of three places:

| Owner | Lifetime | Example |
|-------|----------|---------|
| **Global** | Entire program | `word highScore = 0;` |
| **Scene** | While scene is active | Scene-local variables and resources |
| **Array slot** | Lifetime of the array | `Enemy[8] enemies;` owns its 8 enemies |

References to objects are **borrows** — the compiler proves at compile time that no reference outlives its owner. If it can't prove this, it's a compile error.

```cork
Enemy[8] enemies;
var target = enemies[3];      // borrow — compiler proves target
                              // doesn't outlive the array
target takeDamage: 1;         // OK: array is still alive
// target goes out of scope — nothing to free
```

### No Manual Memory

The developer never allocates, frees, or thinks about memory addresses. The compiler owns the entire 64KB layout and decides where code, data, sprites, charsets, and variables live.

---

## Scenes

Scenes are the primary architectural unit in Cork. A scene represents a self-contained state of the program — a title screen, a game level, an inventory screen. The compiler validates that global resources + any single scene fit within the C64's memory.

### Scene Lifecycle

Every scene has built-in lifecycle phases. These are **keywords**, not methods.

| Phase            | Description                                        |
|------------------|----------------------------------------------------|
| `enter`          | Runs once when the scene becomes active             |
| `frame`          | Runs every frame — compiler **errors** if estimated cycle budget overruns |
| `relaxed frame`  | Runs every frame — compiler **warns** but allows potential overruns |
| `raster N`| Runs at a specific rasterline (compiler wires IRQ)  |
| `exit`           | Runs once before leaving the scene                  |

#### Frame Budget Validation

The compiler estimates the worst-case cycle cost of the `frame` block, accounting for the scene's graphics mode (badlines), active sprites (DMA), and raster handlers. With `frame`, exceeding the budget is a compile error. With `relaxed frame`, it's a warning.

```
error CORK002: Scene 'GameLevel' frame block estimated at ~14,200 cycles
worst-case. Available budget after raster handlers and VIC-II overhead:
12,800 cycles. Frame overrun likely when all 8 enemies are active.
Hint: Use 'relaxed frame' if occasional frame drops are acceptable.
```

Entity dispatch is manual — the developer loops explicitly. No magic, no hidden code generation:

```cork
frame {
    for (enemy in enemies) {
        if (enemy.active) {
            enemy update:;
        }
    }
    checkCollisions:;
    updateHud:;
}
```

### Entry Scene

Exactly one scene must be marked `entry`. This is where the program starts.

```cork
entry scene TitleScreen {
    // Hardware configuration — declarative block
    hardware {
        border: Color.blue;
        background: Color.black;
        mode: text;
    }

    // Resources owned by this scene
    charset font = import("font.bin");

    // Nested hardware declarations
    sprite logo {
        data: import("logo.spr");
        x: 160;
        y: 100;
        color: Color.white;
    }

    enter {
        Music play: titleTheme;
        logo.visible = true;
    }

    frame {
        logo.y += (Sin lookup: time) * 2;

        if (joystick.port2.fire) {
            go GameLevel;
        }
    }

    raster 200 {
        border: Color.red;
    }

    exit {
        Music stop:;
    }

    // Scene-private helper methods
    spawnStars: {
        // ...
    }
}
```

### Scene Transitions

```cork
go TitleScreen;          // transition to another scene
```

The compiler handles unloading the current scene's resources and loading the next scene from disk if needed.

### Global Scope

Resources and state declared outside any scene are always in memory and accessible from all scenes.

```cork
// Always in memory
charset systemFont = import("system-font.bin");
music titleTheme = import("title.sid");
music gameTheme = import("game.sid");

word highScore = 0;
byte currentLevel = 1;

entry scene TitleScreen {
    // can access highScore, systemFont, etc.
}

scene GameLevel {
    // can also access highScore, systemFont, etc.
}
```

### Memory Validation

The compiler calculates the memory footprint of:
- Global scope (always resident)
- Each scene individually

If **global + any single scene** exceeds available memory, the compiler emits an error:

```
error CORK001: Scene 'Level1' requires 42,318 bytes but only 38,911 bytes
are available after global allocations (25,089 bytes).
Consider moving resources to scene-local scope or splitting into
multiple scenes.
```

---

## Hardware SDK

C64 hardware is exposed through declarative syntax and built-in types. No imports needed — the hardware *is* the language.

### VIC-II (Graphics)

```cork
// Declarative in scene hardware block
hardware {
    border: Color.lightBlue;
    background: Color.blue;
    mode: multicolorText;
}

// Imperative in code
vic.border = Color.red;
vic.background = Color.black;
```

### Sprites

```cork
// Declarative
sprite player {
    data: import("player.spr");
    x: 160;
    y: 150;
    color: Color.green;
    multicolor: true;
    multicolor1: Color.white;
    multicolor2: Color.black;
    expandX: false;
    expandY: false;
    priority: SpritePriority.front;
}

// Imperative
player.x += 2;
player.visible = true;
player.frame = walkAnimation[frameIndex];

// Collision (compiler reads and clears hardware registers correctly)
if (player collidesWith: enemy) {
    // ...
}
```

### SID (Sound)

```cork
// Music — import and play SID files
music bgm = import("music.sid");
Music play: bgm;
Music stop:;

// Sound effects
sound explosion = import("boom.sfx");
Sound play: explosion onVoice: 3;
```

### Input

```cork
// Joystick
if (joystick.port2.up)    { player.y -= speed; }
if (joystick.port2.down)  { player.y += speed; }
if (joystick.port2.left)  { player.x -= speed; }
if (joystick.port2.right) { player.x += speed; }
if (joystick.port2.fire)  { shoot:; }

// Keyboard
if (keyboard pressed: Key.space) { jump:; }
if (keyboard down: Key.f1)       { pause:; }
```

### Raster Interrupts

Declared as part of the scene. The compiler handles all the IRQ setup, chaining, and stable raster timing.

```cork
scene GameLevel {
    raster 0 {
        vic.border = Color.blue;
        vic.background = Color.blue;
    }

    raster 200 {
        vic.border = Color.black;
        vic.background = Color.black;
    }
}
```

Multiple raster blocks are allowed. The compiler sorts them by line number and chains them as IRQ handlers automatically.

---

## Imports and Libraries

### File Imports

```cork
import "enemies.cork";
import "ui.cork";
```

### Binary Resource Imports

```cork
charset font = import("font.bin");
music bgm = import("soundtrack.sid");
tilemap level = import("level1.map");
```

### Libraries

Libraries are reusable Cork packages that can be imported.

```cork
import Cork.Math;       // fixed-point math utilities
import Cork.Text;       // text rendering helpers
```

(Library system details TBD for v1.)

---

## Control Flow

### If / Else

```cork
if (health <= 0) {
    destroy:;
} else if (health < 3) {
    flash:;
}
```

### While

```cork
while (enemy.active) {
    enemy update:;
}
```

### For

```cork
for (var i = 0; i < enemies.length; i++) {
    enemies[i] update:;
}
```

### For-each

```cork
for (enemy in enemies) {
    enemy update:;
}
```

### Switch (no fallthrough — safe default)

No `break` needed. Each case is independent. Supports both constant and expression cases.

```cork
// Constant cases — compiler generates jump table (fast)
switch (direction) {
    case Direction.up:
        player.y -= 1;
    case Direction.down:
        player.y += 1;
    case Direction.left:
        player.x -= 1;
    case Direction.right:
        player.x += 1;
}

// Expression cases — compiler generates if/else chain (same performance)
switch (true) {
    case health < 10:
        flashWarning:;
    case health < 30:
        showDamage:;
    case armor > 50:
        showShield:;
    default:
        showNormal:;
}
```

### Fallthrough Switch (C-style — requires break)

```cork
fallthrough switch (command) {
    case 1:
    case 2:
    case 3:
        handleLowCommands:;
        break;
    case 4:
        handleSpecial:;
        break;
}
```

### Default Clause

`default:` is never required, but the compiler provides smart warnings:

| Situation | Behavior |
|-----------|----------|
| Enum switch, all cases covered | No warning |
| Enum switch, cases missing, no default | Warning: "cases X, Y not handled" |
| Non-enum switch, no default | Warning: "not all values handled" |
| Default present | No warning |

---

## Comments

```cork
// Single-line comment

/* 
   Multi-line
   comment
*/
```

---

## Compiler Output

The Cork compiler can emit multiple output formats:

| Format | Description                           |
|--------|---------------------------------------|
| `.prg`  | Standard C64 program file            |
| `.d64`  | Disk image (for multi-scene programs)|
| `.crt`  | Cartridge image                      |

For single-scene programs that fit in memory, a `.prg` is sufficient. Multi-scene programs require a `.d64` (or `.crt`) so scenes can be loaded from disk.

The compiler automatically generates:
- BASIC stub for auto-start
- Memory layout
- Scene packing and load addresses
- Raster IRQ setup code
- Reference counting runtime (minimal, only if needed)

---

## Keyword Modifiers

Cork uses keyword modifiers for variant behavior. This is a consistent pattern throughout the language:

| Default | Modified | Effect |
|---------|----------|--------|
| `scene` | `entry scene` | Marks the program's starting scene |
| `frame` | `relaxed frame` | Allows potential frame overruns (warning instead of error) |
| `enum`  | `flags enum` | Enables bitwise flag combination |
| `switch` | `fallthrough switch` | C-style fallthrough, requires `break` |

---

## What Cork Does NOT Have (v1)

| Feature              | Reason                                         |
|----------------------|------------------------------------------------|
| Inline assembly      | Language should be expressive enough without it |
| Generics             | Complexity not justified for 8-bit target      |
| Lambdas/closures     | Runtime cost too high, implementation complex   |
| Operator overloading | Overkill for target use cases                  |
| Dynamic arrays       | Fixed-size only; 64KB demands predictability    |
| Exceptions           | Runtime overhead; errors are compile-time       |
| Manual memory mgmt   | Compiler owns the memory map                   |
| Garbage collection   | Static ownership — all lifetimes compile-time   |
| Reference counting   | Static ownership eliminates the need entirely   |
| Heap allocation      | All objects are static, scene-scoped, or array-owned |
| `func` keyword       | Method signatures are self-evident              |
| Dot-enum shorthand   | Always use `Type.value` for clarity             |

---

## Open Questions for Future Discussion

1. **Error handling for I/O** — Scene loading can fail (disk error). How should this surface? A built-in retry/error screen? A callback?
2. **Generics** — Could be valuable for container types in a future version.
3. **Debugging support** — Source-level debugging in VICE? Source maps?
4. **Standard library scope** — What math, string, and utility functions ship built-in?
5. **Build system** — CLI tool? Project file format? Watch mode?
6. **Advanced graphics modes** — How do bitmap mode, FLI, sprite multiplexing surface in the language?
7. **Optimization hints** — Can the developer annotate hot paths or suggest memory placement?
8. **Library system** — How are libraries authored, versioned, and distributed?
