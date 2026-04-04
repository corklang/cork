# Cork VS Code Extension

Design document for the Cork language VS Code extension.

## Syntax Highlighting

TextMate grammar for `.cork` files.

### Token Scopes

| Category | Tokens | Scope |
|----------|--------|-------|
| Keywords (type) | `byte`, `sbyte`, `word`, `sword`, `fixed`, `sfixed`, `bool`, `string`, `var` | `storage.type.cork` |
| Keywords (declaration) | `struct`, `enum`, `flags`, `scene`, `entry`, `const`, `import` | `keyword.declaration.cork` |
| Keywords (control) | `if`, `else`, `while`, `for`, `in`, `switch`, `case`, `default`, `break`, `continue`, `return`, `go` | `keyword.control.cork` |
| Keywords (lifecycle) | `hardware`, `enter`, `frame`, `relaxed`, `raster`, `exit` | `keyword.other.lifecycle.cork` |
| Keywords (hardware) | `sprite` | `keyword.other.hardware.cork` |
| Keywords (other) | `as`, `true`, `false` | `keyword.other.cork` / `constant.language.cork` |
| Numbers (decimal) | `42`, `1000` | `constant.numeric.decimal.cork` |
| Numbers (hex) | `0xFF`, `0xD020` | `constant.numeric.hex.cork` |
| Numbers (fixed) | `1.5`, `0.75` | `constant.numeric.float.cork` |
| Strings | `"HELLO"` | `string.quoted.double.cork` |
| Sprite patterns | `` ` ... ` `` | `string.other.sprite-pattern.cork` |
| Comments (line) | `// ...` | `comment.line.double-slash.cork` |
| Comments (block) | `/* ... */` | `comment.block.cork` |
| Operators | `+`, `-`, `*`, `/`, `%`, `&`, `\|`, `^`, `<<`, `>>` | `keyword.operator.cork` |
| Assignment ops | `=`, `+=`, `-=`, `*=`, `/=`, `%=`, etc. | `keyword.operator.assignment.cork` |
| Comparison | `==`, `!=`, `<`, `>`, `<=`, `>=` | `keyword.operator.comparison.cork` |
| Method colon | `:` in message sends | `punctuation.separator.method.cork` |
| Built-in types | `Color.red`, `Color.blue`, etc. | `support.constant.color.cork` |
| Scene names | After `scene` / `go` keywords | `entity.name.type.scene.cork` |
| Struct names | After `struct` keyword, in type position | `entity.name.type.struct.cork` |
| Enum names | After `enum` keyword | `entity.name.type.enum.cork` |
| Method selectors | `update:`, `moveTo:y:` | `entity.name.function.cork` |
| Hardware registers | `joystick.port1.*`, `joystick.port2.*` | `support.variable.hardware.cork` |
| Sprite properties | `.x`, `.y`, `.color` on sprite instances | `variable.other.property.cork` |

### Sprite Pattern Highlighting

Inside backtick-delimited sprite patterns, character-level highlighting:

| Character | Meaning | Color |
|-----------|---------|-------|
| `.` | Transparent | dim/grey |
| `#` | Sprite color (hi-res) | bright/white |
| `1` | Multicolor 0 | color 1 (e.g., cyan) |
| `2` | Sprite color (multicolor) | color 2 (e.g., green) |
| `3` | Multicolor 1 | color 3 (e.g., red) |

This would use an embedded grammar inside the backtick scope, with regex matching for each character.

## Semantic Highlighting

Beyond TextMate, a language server could provide semantic tokens:

- **Global variables** vs **scene-local variables** vs **constants** (different colors)
- **Sprite names** as special identifiers
- **Struct field access** chains (`player.pos.x`)
- **Unused declarations** (dimmed, matching the DCE analysis)

## Error Diagnostics

The compiler already produces errors with `file(line,col)` format. The extension should:

1. Run the Cork compiler on save (or on keystroke with debounce)
2. Parse stderr for `file(line,col): message` patterns
3. Display as VS Code diagnostics (red squiggles, Problems panel)

### Error Format

Current compiler output:

```
Error: samples/test.cork(5,15): poke address must be constant, constant + expr, or word variable
```

The extension parses this with a regex like:

```
^Error: (.+)\((\d+),(\d+)\): (.+)$
```

### Warning Support (Future)

The compiler could emit warnings (unused variables, unreachable code) in the same format:

```
Warning: samples/test.cork(12,5): variable 'temp' is never used
```

## Autocomplete

### Keyword Completion

Trigger on typing — suggest keywords contextually:

- Top level: `entry`, `scene`, `struct`, `enum`, `const`, `import`, `byte`, `word`, etc.
- Inside scene: `hardware`, `enter`, `frame`, `exit`, `raster`, `sprite`, type keywords
- Inside hardware block: `mode`, `border`, `background`, `background1`, `background2`, `background3`, `multicolor0`, `multicolor1`
- Inside sprite block: `data`, `x`, `y`, `color`, `multicolor`, `expandX`, `expandY`, `priority`
- Inside control flow: `if`, `else`, `while`, `for`, `switch`, `case`, `break`, `continue`, `return`, `go`

### Color Constants

When typing `Color.`, suggest all C64 colors:

```
black, white, red, cyan, purple, green, blue, yellow,
orange, brown, lightRed, darkGrey, grey, lightGreen, lightBlue, lightGrey
```

### Graphics Mode Completion

When typing `mode:`, suggest:

```
text, multicolorText, bitmap, multicolorBitmap, ecm
```

### Scene Name Completion

After `go`, suggest all scene names from the current file and imports.

### Method Completion

After typing an identifier followed by space, suggest known method selectors for that type (struct methods, global methods).

## Snippets

| Prefix | Expands to |
|--------|-----------|
| `scene` | Full scene skeleton with hardware, enter, frame |
| `entry` | Entry scene skeleton |
| `sprite` | Sprite block with data, position, color |
| `spritedata` | 24x21 hi-res sprite pattern template (blank) |
| `spritemc` | 12x21 multicolor sprite pattern template |
| `raster` | Raster interrupt block |
| `for` | C-style for loop |
| `foreach` | For-each loop |
| `while` | While loop |
| `if` | If/else block |
| `switch` | Switch statement skeleton |
| `struct` | Struct with fields and methods |
| `enum` | Enum declaration |
| `method` | Global method declaration |
| `poke` | poke: addr value: val |
| `printAt` | printAt: pos text: str |

### Scene Snippet Example

```json
{
  "Scene": {
    "prefix": "scene",
    "body": [
      "scene ${1:Name} {",
      "\thardware {",
      "\t\tborder: Color.${2:black};",
      "\t\tbackground: Color.${3:black};",
      "\t}",
      "",
      "\tenter {",
      "\t\t$0",
      "\t}",
      "",
      "\tframe {",
      "\t\t",
      "\t}",
      "}"
    ]
  }
}
```

## Sprite Pattern Preview

A custom editor feature: when the cursor is inside a backtick sprite pattern, show a small preview panel that renders the sprite visually.

- Parse the pattern characters in real-time
- Render a scaled pixel grid (e.g., 8x or 12x zoom)
- Show transparent pixels as checkerboard
- Use approximate C64 palette colors for multicolor patterns
- Update live as the user edits

This could be implemented as:

- A CodeLens decoration above the pattern
- A hover tooltip

## Build Integration

### Tasks

Register Cork compilation as a VS Code build task:

```json
{
  "label": "Cork: Build",
  "type": "shell",
  "command": "cork",
  "args": ["${file}", "-o", "${fileDirname}/${fileBasenameNoExtension}.prg"],
  "group": "build",
  "problemMatcher": {
    "pattern": {
      "regexp": "^Error: (.+)\\((\\d+),(\\d+)\\): (.+)$",
      "file": 1,
      "line": 2,
      "column": 3,
      "message": 4
    }
  }
}
```

### Build on Save

Option to automatically compile on save, showing errors inline.

## VICE Integration

### Run in Emulator

Command: "Cork: Run in VICE" — compiles and launches x64sc with the output PRG:

```
cork ${file} -o /tmp/output.prg && x64sc -autostart /tmp/output.prg
```

### Debug in VICE

Future: if the compiler generates a VICE monitor label file (`.vs` format), the extension could map breakpoints to source locations.

Label file format:

```
al 080E .scene_Game
al 0850 .method_clearScreen
al 08A0 .frame_loop_Game
```

## Memory Usage Display

After compilation, show memory usage in the status bar:

```
Cork: 680/38897 bytes (1%) | Peephole: 4 bytes saved
```

Click to see detailed breakdown (code, data, sprites, runtime library).

## Outline View

The document outline should show the program structure:

```
- Globals
  - const byte spritePtr
  - clearScreen:
- Scene: TitleScreen (entry)
  - hardware
  - sprite 0 player
  - sprite 1 enemy
  - enter
  - frame
  - raster 100
  - raster 200
  - moveLeft:
- Scene: GameLevel
  - ...
- Struct: Player
  - byte x
  - byte health
  - moveLeft:
  - moveRight:
- Enum: State
  - title
  - playing
  - paused
```

## Go to Definition

- Click on a method call (`clearScreen:`) → jump to the method declaration
- Click on a struct type (`Player`) → jump to the struct declaration
- Click on an enum member (`State.playing`) → jump to the enum
- Click on `go GameLevel` → jump to the scene declaration
- Click on `import "lib/utils.cork"` → open the imported file
- Click on a variable → jump to its declaration

## Hover Information

Hover over identifiers to see type and value information:

- Variable: `byte x — zero page $02`
- Constant: `const byte MAX_X = 250`
- Struct field: `player.x — byte, zero page $04`
- Color: `Color.red — 2` (with a color swatch)
- Hardware register: `$D020 — VIC-II border color`
- Sprite: `sprite 0 player — x: word ($02-$03), y: byte ($04)`
- Method: `clearScreen: — global method, 45 bytes`

## Implementation Phases

### Phase 1: Syntax Highlighting + Snippets

- TextMate grammar for `.cork` files
- Basic snippets for common patterns
- File icon

### Phase 2: Error Diagnostics + Build Tasks

- Run compiler on save, parse errors
- Problem matcher for build tasks
- Status bar memory usage

### Phase 3: Autocomplete + Hover

- Keyword and constant completion
- Scene/method/type name completion
- Basic hover information

### Phase 4: Sprite Preview + VICE Integration

- Inline sprite pattern visualization
- Run/debug commands with VICE

### Phase 5: Language Server (Full)

- Semantic highlighting
- Go to definition
- Find references
- Rename symbol
- Document outline
- Unused code detection
