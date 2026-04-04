# Cork Language Design

## Vision

Cork is a high-level, expressive programming language that compiles to Commodore 64 machine code. It pairs a modern developer experience — structs with methods, type inference, fully static memory management — with a built-in SDK that makes C64 hardware feel native to the language. The compiler, written in C# (.NET 10, AOT compatible), handles all the brutal realities of the 6510 CPU and 64KB address space so the developer can focus on their vision.

**Primary audience:** Game developers who want productivity and expression.  
**Secondary audience:** General application developers targeting the C64.

---

## Core Principles

1. **Expression over ceremony** — The language should read clearly and feel good to write.
2. **Compile-time everything** — Every safety feature (type checks, memory validation, shadowing prevention) is resolved at compile time. Zero runtime overhead for safety.
3. **Fully static memory** — No heap, no garbage collection, no reference counting. Every variable's size and location is known at compile time. The compiler owns the 64KB layout.
4. **The hardware is the SDK** — C64 hardware (VIC-II, SID, CIA, sprites, raster interrupts) surfaces as declarative, first-class language constructs — not a bolted-on library.
5. **Scenes as architecture** — Programs are organized into scenes that the compiler packs, validates, and loads automatically. If it doesn't fit in memory, it's a compiler error — not a runtime crash.
6. **The colon means "invoke"** — The colon is the message-passing operator. It always means "I'm calling something." It never appears in type annotations or declarations.
7. **Dot means data, colon means code** — `enemy.health` is always a direct field read. `enemy update:` is always a method call. No hidden costs behind either syntax.

---

## Syntax Overview

Cork's syntax blends C-style declarations with Objective-C/Smalltalk-style message passing. Semicolons and braces are required. The colon is reserved exclusively for method calls and declarations.

```cork
// Variable declaration — C-style, no colons
byte score = 0;
var speed = 1.5;                  // inferred as fixed
const byte MAX_LIVES = 3;

// Structs with field defaults
struct Enemy {
    fixed x = 0;
    fixed y = 0;
    byte health = 3;
    bool active = false;
}

// Struct initialization
var enemy = Enemy { health = 10, x = 100 };
Enemy[8] enemies;                 // 8 enemies, all at defaults

// Message-passing calls — colons signal invocation
player moveTo: 100 y: 50;
enemy takeDamage: weapon.power + bonus;

// No-arg calls — trailing colon
player update:;
Music stop:;

// Property access — dot syntax, always direct field access
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
| `fixed`  | 16-bit | Unsigned 8.8 fixed-point (0.0 to 255.996) |
| `sfixed` | 16-bit | Signed 8.8 fixed-point (-128.0 to 127.996) |
| `string` | fixed  | Fixed-size PETSCII string (size set at declaration) |

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

// Inferred type
var x = 5;                     // byte
var speed = 1.5;               // fixed
var pos = 1000;                // word

// Constants
const byte MAX_LIVES = 3;
const MAX_ENEMIES = 8;         // inferred
```

### Strings

Strings are fixed-size, stored as C64 screen codes. The size is determined at declaration — either from an explicit size or inferred from the initial value.

```cork
string name = "HELLO";         // fixed at 5 bytes, inferred from literal
string[20] buffer = "HI";      // fixed at 20 bytes, padded with spaces
printAt: 100 text: "SCORE: 0"; // string literals work directly in printAt
printAt: 200 text: name;       // string variables too
```

String literals in Cork source are automatically converted to C64 screen codes at compile time. The compiler handles the ASCII→screen code mapping.

**Supported characters in string literals:**
- Letters: `A`-`Z`, `a`-`z` (both map to uppercase, C64 has no lowercase in default charset)
- Digits: `0`-`9`
- Space
- Punctuation: `!` `"` `#` `$` `%` `&` `'` `(` `)` `*` `+` `,` `-` `.` `/` `:` `;` `<` `=` `>` `?` `@` `[` `]` `^` `_`

Unsupported characters (like `{`, `}`, `~`, `|`, non-ASCII) produce a compile error with a clear message.

Strings are stored inline in zero-page memory. They cannot grow or shrink at runtime.

### No Null

Cork has no `null`. Every variable always exists and is initialized. Structs are value types — declaring one allocates it with field defaults. There is no "absence of a value."

To represent optional presence, use an explicit flag:

```cork
struct Enemy {
    bool active = false;
    byte health = 3;
    fixed x = 0;
    fixed y = 0;
}

// Check relevance explicitly
if (enemy.active) {
    enemy update:;
}
```

### Fixed-Size Arrays

```cork
byte[20] bullets;                        // 20-element byte array
Enemy[8] enemies;                        // 8 enemies at field defaults
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

### Type Casting

Use `as` for explicit type conversion. Widening conversions (no data loss) are implicit. Narrowing conversions (potential data loss) require `as`.

```cork
// Implicit widening — always safe, no syntax needed
byte b = 5;
word w = b;              // byte -> word: OK
fixed f = b;             // byte -> fixed (5.0): OK

// Explicit narrowing — requires "as"
word big = 1000;
byte small = big as byte;    // truncates to low byte
fixed speed = 2.75;
byte whole = speed as byte;  // truncates fractional part (2)

// Signed/unsigned conversions require "as"
byte b = 200;
sbyte s = b as sbyte;        // reinterprets bits (-56)
```

The compiler warns when a narrowing `as` would lose data in a constant expression:

```
warning CORK020: Narrowing conversion of 1000 to byte truncates to 232.
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
| Field access | `receiver.field` |
| Nested field access | `enemy.pos.x` |

The colon means "I'm sending a message." The dot means "I'm accessing data." There is never ambiguity — dots are always direct field reads, colons are always method calls.

### Method Calls

```cork
// With arguments — colons delimit each argument
enemy moveTo: 100 y: 50;
enemy takeDamage: weapon.power + bonus;
Music play: bgTheme;

// Spacing around colons is flexible
enemy moveTo:100 y:50;
enemy moveTo: 100 y: 50;

// Full expressions work as arguments — no parens needed for math
enemy takeDamage: health + bonus;
enemy moveTo: x + 1 y: y + 1;
enemy foo: a * b + c bar: d > 5;

// Parens only needed when an argument contains a message send
enemy foo: (other getX:) + offset;

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
moveTo: (byte newX) y: (byte newY) {
    x = newX;
    y = newY;
}

// With return type
byte distanceFromX: (byte x) toY: (byte y) {
    // ...
}

// The full method identity is its segment names: "distanceFromX:toY:"
```

### No Method Overloading

The selector (segment names with colons) is the method's unique identity. You cannot have two methods with the same selector but different parameter types. This matches Objective-C and Smalltalk semantics.

```cork
// OK — different selectors
takeDamage: (byte amount) { ... }
takeHeavyDamage: (word amount) { ... }

// COMPILE ERROR — duplicate selector "takeDamage:"
takeDamage: (byte amount) { ... }
takeDamage: (word amount) { ... }
```

### Return in Lifecycle Blocks

`return` inside `enter`, `frame`, `relaxed frame`, `exit`, or `raster` blocks means early exit from that block. No value may be returned.

```cork
frame {
    if (!gameStarted) { return; }
    // rest of frame logic...
}
```

---

## Structs

Structs are Cork's only composite type. They are value types with known size at compile time. All fields are public. Structs can have methods.

### Declaration

```cork
struct Enemy {
    fixed x = 0;
    fixed y = 0;
    byte health = 3;
    bool active = false;
    byte animFrame = 0;

    update: {
        y += 1.0;
        if (y > 250) { active = false; }
        animFrame = (animFrame + 1) % 4;
    }

    takeDamage: (byte amount) {
        health -= amount;
        if (health == 0) { active = false; }
    }

    bool isAlive: {
        return health > 0;
    }

    fixed distanceTo: (Enemy other) {
        // ...
    }
}
```

### Initialization

Declaring a struct variable gives it field defaults. Use initializer syntax to override specific fields:

```cork
// All defaults
Enemy enemy;

// Override specific fields — only these emit code
var enemy = Enemy { health = 10, x = 100, y = 50 };

// Array of structs — all at defaults
Enemy[8] enemies;
```

The compiler is smart: only non-default values emit code. If `health` defaults to 3 and you write 10, it sets 10. Fields you don't mention keep their defaults with no code generated.

### Methods

Methods on structs can access the struct's fields directly — no `this` keyword needed. Field names are always in scope. Parameter names must not shadow field names (the compiler enforces this). All dispatch is static (no vtables, no indirection).

```cork
struct Player {
    fixed x = 160;
    fixed y = 200;
    byte health = 5;

    handleInput: {
        if (joystick.port2.left)  { x -= 1.5; }
        if (joystick.port2.right) { x += 1.5; }
        if (joystick.port2.up)    { y -= 1.5; }
        if (joystick.port2.down)  { y += 1.5; }
    }
}

// Usage
Player player;
player handleInput:;        // fields accessed directly inside the method
player.x;                   // direct field access, no method call
```

### Composition

Code reuse is achieved through composition, not inheritance:

```cork
struct Position {
    fixed x = 0;
    fixed y = 0;

    moveTo: (fixed newX) y: (fixed newY) {
        x = newX;
        y = newY;
    }
}

struct Enemy {
    Position pos;
    byte health = 3;
    bool active = false;

    update: {
        pos.y += 1.0;
        if (pos.y > 250) { active = false; }
    }
}

// Usage
var enemy = Enemy { pos = Position { x = 100, y = 0 }, health = 5 };
enemy.pos.x;                // nested field access
enemy update:;              // method call
```

### Struct-of-Arrays Optimization

For arrays of structs, the compiler uses struct-of-arrays layout internally. `Enemy[8]` is stored as parallel arrays:

```
enemy_x[8]       — all x values contiguous
enemy_y[8]       — all y values contiguous  
enemy_health[8]  — all health values contiguous
enemy_active[8]  — all active flags contiguous
```

This allows the 6510 to iterate fields with fast indexed addressing (`LDA health_array,X`) instead of pointer arithmetic. The developer writes `enemies[i].health` and the compiler translates it to the optimal access pattern.

---

## Memory Management

### Fully Static

Cork has no heap, no garbage collection, no reference counting. Every variable's size and memory location is determined at compile time.

Objects live in one of three places:

| Owner | Lifetime | Example |
|-------|----------|---------|
| **Global** | Entire program | `word highScore = 0;` |
| **Scene** | While scene is active | Scene-local variables and resources |
| **Local** | Current scope/block | Function-local variables |

All struct instances are value types with in-place semantics. Arrays of structs own their elements. There is no indirection, no pointers, no shared mutable state.

### In-Place Semantics

The compiler always operates on structs in place. Method calls, field writes, and for-each loops modify the original — not a copy. Copying only happens on explicit variable assignment.

```cork
// In-place — modifies the array element directly
enemies[3] update:;
enemies[3].health = 10;

// In-place — each iteration modifies the actual array element
for (enemy in enemies) {
    enemy update:;              // modifies enemies[0], enemies[1], etc.
}

// In-place — nested struct modified in place
enemy.pos moveTo: 100 y: 50;   // modifies enemy.pos, not a copy

// COPY — explicit variable assignment creates an independent copy
var snapshot = enemies[3];      // snapshot is a copy
snapshot.health = 0;            // does not affect enemies[3]
```

On the 6510, this is natural — the compiler passes addresses internally. No copying overhead unless you ask for it.

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
| `raster N`       | Runs at a specific rasterline (compiler wires IRQ)  |
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
    player handleInput:;
    for (e in enemies) {
        if (e.active) {
            e update:;
        }
    }
    checkCollisions:;
}
```

### Entry Scene

Exactly one scene must be marked `entry`. This is where the program starts.

```cork
entry scene TitleScreen {
    hardware {
        border: Color.blue;
        background: Color.black;
        mode: text;
    }

    charset font = import("font.bin");

    sprite logo {
        data: import("logo.spr");
        x: 160;
        y: 100;
        color: Color.white;
    }

    sbyte logoDirection = 1;

    enter {
        Music play: titleTheme;
        logo.visible = true;
    }

    frame {
        logo.y += logoDirection;
        if (logo.y > 200) { logoDirection = -1; }
        if (logo.y < 50)  { logoDirection = 1; }

        if (joystick.port2.fire) {
            go GameLevel;
        }
    }

    raster 200 {
        vic.border = Color.red;
    }

    exit {
        Music stop:;
    }

    // Scene-private helper methods
    drawStarfield: {
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

Variables, methods, and resources declared outside any scene are always in memory and accessible from all scenes. Their code and data count against the global memory budget.

```cork
charset systemFont = import("system-font.bin");
music titleTheme = import("title.sid");

word highScore = 0;
byte currentLevel = 1;

// Global method — callable from any scene
clearScreen: {
    byte i = 0;
    while (i < 250) {
        poke: (0x0400 + i) value: 32;
        i += 1;
    }
}

entry scene TitleScreen {
    enter { clearScreen:; }
    // can access highScore, systemFont, etc.
}

scene GameLevel {
    enter { clearScreen:; }
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
hardware {
    mode: text;               // text | multicolorText | bitmap | multicolorBitmap | ecm
    border: Color.lightBlue;
    background: Color.blue;
    background1: Color.red;   // ECM and multicolor text: $D022
    background2: Color.green; // ECM and multicolor text: $D023
    background3: Color.yellow; // ECM only: $D024
    multicolor0: Color.white; // Shared sprite multicolor: $D025
    multicolor1: Color.red;   // Shared sprite multicolor: $D026
}
```

The `mode:` setting writes $D011, $D016, and $D018 to configure the VIC-II. Every mode explicitly sets all three registers so scene transitions between modes are clean.

| Mode | Resolution | Colors | Notes |
|------|-----------|--------|-------|
| `text` | 40×25 chars | 1 per char | Default. $D018=$15 |
| `multicolorText` | 40×25 chars | 4 per char | Half horizontal res. Color RAM bit 3 enables per-char |
| `bitmap` | 320×200 px | 2 per 8×8 cell | Bitmap at $2000. $D018=$1D |
| `multicolorBitmap` | 160×200 px | 4 per 4×8 cell | Bitmap at $2000. Background via $D021 |
| `ecm` | 40×25 chars | 4 backgrounds | Char bits 6-7 select background. Only 64 chars |

### Sprites

Sprites are declared inside scenes with an explicit hardware slot number (0-7):

```cork
sprite 0 player {
    data: `
        . . . . . . . . # # # # # # . . . . . . . . . .
        . . . . . . # # # # # # # # # # . . . . . . . .
        . . . . # # # # # # # # # # # # # # . . . . . .
        ...21 rows of 24 pixels...
    `;
    x: 172;
    y: 200;
    color: Color.green;
}
```

**Inline sprite patterns** use backtick-delimited pixel art. Whitespace is ignored — add any spacing for readability. Hi-res sprites use `.` (transparent) and `#` (sprite color), 24×21 pixels. Multicolor sprites use `.`/`1`/`2`/`3` for the four color values, 12×21 fat pixels.

The compiler validates dimensions, converts to 63 bytes, and places the data 64-byte aligned in the output so VIC-II reads it directly — no runtime copy.

The `data:` setting accepts three forms:
- Backtick pattern: `data: \`...pixels...\`;` — inline pixel art
- Identifier: `data: spriteName;` — reference to a `const byte[63]` array
- Future: `data: import("file.bin");` — binary file import

**Sprite settings:**

| Setting | Type | VIC-II Register | Notes |
|---------|------|----------------|-------|
| `x:` | byte | $D000+n*2 | Initial X position |
| `y:` | byte | $D001+n*2 | Initial Y position |
| `color:` | byte | $D027+n | Sprite color |
| `multicolor:` | bool | $D01C bit n | Enable multicolor mode |
| `expandX:` | bool | $D01D bit n | Double width |
| `expandY:` | bool | $D017 bit n | Double height |
| `priority:` | `back` | $D01B bit n | Behind background |
| `data:` | pattern/ident | $07F8+n | Sprite data pointer |

Shared multicolor registers (`multicolor0:`/`multicolor1:` → $D025/$D026) go in the `hardware` block, not in sprite declarations, since they're global to all sprites.

**Auto-sync:** Writing to sprite fields automatically updates the corresponding VIC-II register. No manual poke needed:

```cork
player.x += 2;   // updates ZP field AND writes $D000
player.y -= 1;   // updates ZP field AND writes $D001
```

**Collision detection:**

```cork
if ((player collidedWith: enemy)) {
    poke: 0xD020 value: Color.red;
}
```

Reads the sprite-sprite collision register ($D01E) and checks both sprite bits. Parentheses required around the message send in condition position.

**Multicolor sprite example** (12×21, 2 bits per pixel):

```cork
sprite 1 alien {
    data: `
        .   .   .   .   2   2   2   2   .   .   .   .
        .   .   2   2   1   3   1   3   2   2   .   .
        .   2   2   2   2   2   2   2   2   2   2   .
        2   2   2   2   2   2   2   2   2   2   2   2
        ...
    `;
    multicolor: true;
    color: Color.lightGreen;   // bit pair 10
}
// In hardware block: multicolor0: Color.white (01), multicolor1: Color.red (11)
```

**Scene safety:** When a `go` statement transitions to another scene, the compiler clears only the VIC-II sprite registers that the current scene actually touched. Scenes without sprites pay zero overhead.

### SID (Sound)

```cork
music bgm = import("music.sid");
Music play: bgm;
Music stop:;

sound explosion = import("boom.sfx");
Sound play: explosion onVoice: 3;
```

### Input

```cork
if (joystick.port2.up)    { player.y -= speed; }
if (joystick.port2.down)  { player.y += speed; }
if (joystick.port2.left)  { player.x -= speed; }
if (joystick.port2.right) { player.x += speed; }
if (joystick.port2.fire)  { shoot:; }

if (keyboard pressed: Key.space) { jump:; }
if (keyboard down: Key.f1)       { pause:; }
```

### Raster Interrupts

Declared as part of the scene. The compiler handles all the IRQ setup, chaining, and stable raster timing.

```cork
scene GameLevel {
    raster 0 {
        vic.border = Color.blue;
    }

    raster 200 {
        vic.border = Color.black;
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

### Continue and Break

```cork
for (enemy in enemies) {
    if (!enemy.active) { continue; }
    enemy update:;
}

while (true) {
    if (done) { break; }
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

// Expression cases — compiler generates if/else chain
switch (true) {
    case health < 10:
        flashWarning:;
    case health < 30:
        showDamage:;
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
| Classes              | Structs with methods cover all use cases        |
| Inheritance          | Composition is simpler and C64-appropriate      |
| Interfaces           | Not needed without polymorphic dispatch          |
| Inline assembly      | Language should be expressive enough without it |
| Generics             | Complexity not justified for 8-bit target      |
| Lambdas/closures     | Runtime cost too high, implementation complex   |
| Operator overloading | Overkill for target use cases                  |
| Dynamic arrays       | Fixed-size only; 64KB demands predictability    |
| Exceptions           | Runtime overhead; errors are compile-time       |
| Manual memory mgmt   | Compiler owns the memory map                   |
| Garbage collection   | Fully static memory — all lifetimes compile-time |
| Reference counting   | Fully static memory eliminates the need         |
| Heap allocation      | All objects are static, scene-scoped, or local  |
| Null                 | All values always exist; use `bool active` patterns |
| Nullable types       | No null means no need for `Type?` or `?.`       |
| Properties           | Direct field access (dot) and methods (colon) are clearer |
| `func` keyword       | Method signatures are self-evident              |
| Dot-enum shorthand   | Always use `Type.value` for clarity             |
| Access modifiers     | Everything is public                            |
| Constructors         | Field defaults + initializer syntax             |
| `this` keyword       | Fields are in scope directly; no shadowing allowed |
| Method overloading   | Selector is the unique identity; use different names |
| Dynamic strings      | Strings are fixed-size, determined at declaration   |

---

## Designed But Not Yet Implemented

These features are specified in the language design and grammar but not yet in the compiler:

| Feature | Notes |
|---------|-------|
| `as` type casting | `big as byte`, `speed as fixed` — token and grammar exist |
| `for (x in array)` | For-each iteration — token exists |
| Struct initializer syntax | `Enemy { x = 10, y = 20 }` — in grammar |
| `return` with value | Methods returning computed values |
| `string` type | Fixed-size PETSCII strings |
| `import "file.cork"` | Multi-file programs |
| Struct composition | Structs containing other structs |
| Sprite declarations | `sprite name { ... }` declarative blocks in scenes |
| Scene-local resource imports | `charset font = import("font.bin")` inside scenes |
| D64 / CRT output | Disk image and cartridge generation |

## Open Questions for Future Discussion

1. **Error handling for I/O** — Scene loading can fail (disk error). How should this surface? A built-in retry/error screen? A callback?
2. **Debugging support** — Source-level debugging in VICE? Source maps?
3. **Standard library scope** — What math, string, and utility functions ship built-in?
4. **Build system** — CLI tool? Project file format? Watch mode?
5. **Advanced graphics modes** — How do bitmap mode, FLI, sprite multiplexing surface in the language?
6. **Optimization hints** — Can the developer annotate hot paths or suggest memory placement?
7. **Library system** — How are libraries authored, versioned, and distributed?
