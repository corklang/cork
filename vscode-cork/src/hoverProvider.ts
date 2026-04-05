import * as vscode from "vscode";

const COLOR_VALUES: Record<string, { value: number; hex: string }> = {
  black:      { value: 0,  hex: "#000000" },
  white:      { value: 1,  hex: "#FFFFFF" },
  red:        { value: 2,  hex: "#894036" },
  cyan:       { value: 3,  hex: "#7ABFC7" },
  purple:     { value: 4,  hex: "#8A46AE" },
  green:      { value: 5,  hex: "#68A941" },
  blue:       { value: 6,  hex: "#3E31A2" },
  yellow:     { value: 7,  hex: "#D0DC71" },
  orange:     { value: 8,  hex: "#905F25" },
  brown:      { value: 9,  hex: "#5C4700" },
  lightRed:   { value: 10, hex: "#BB776D" },
  darkGrey:   { value: 11, hex: "#555555" },
  grey:       { value: 12, hex: "#808080" },
  lightGreen: { value: 13, hex: "#AAFF66" },
  lightBlue:  { value: 14, hex: "#7C70DA" },
  lightGrey:  { value: 15, hex: "#ABABAB" },
};

const VIC_REGISTERS: Record<string, string> = {
  "0xD000": "VIC-II — Sprite 0 X position",
  "0xD001": "VIC-II — Sprite 0 Y position",
  "0xD002": "VIC-II — Sprite 1 X position",
  "0xD003": "VIC-II — Sprite 1 Y position",
  "0xD004": "VIC-II — Sprite 2 X position",
  "0xD005": "VIC-II — Sprite 2 Y position",
  "0xD006": "VIC-II — Sprite 3 X position",
  "0xD007": "VIC-II — Sprite 3 Y position",
  "0xD008": "VIC-II — Sprite 4 X position",
  "0xD009": "VIC-II — Sprite 4 Y position",
  "0xD00A": "VIC-II — Sprite 5 X position",
  "0xD00B": "VIC-II — Sprite 5 Y position",
  "0xD00C": "VIC-II — Sprite 6 X position",
  "0xD00D": "VIC-II — Sprite 6 Y position",
  "0xD00E": "VIC-II — Sprite 7 X position",
  "0xD00F": "VIC-II — Sprite 7 Y position",
  "0xD010": "VIC-II — Sprite X position MSB (bit 8 for each sprite)",
  "0xD011": "VIC-II — Control register 1 (scroll Y, screen height, bitmap mode, ECM, raster bit 8)",
  "0xD012": "VIC-II — Raster counter (current scanline)",
  "0xD015": "VIC-II — Sprite enable register",
  "0xD016": "VIC-II — Control register 2 (scroll X, screen width, multicolor mode)",
  "0xD017": "VIC-II — Sprite Y expand",
  "0xD018": "VIC-II — Memory pointers (screen RAM, character/bitmap base)",
  "0xD019": "VIC-II — Interrupt register",
  "0xD01A": "VIC-II — Interrupt enable",
  "0xD01B": "VIC-II — Sprite-to-background priority",
  "0xD01C": "VIC-II — Sprite multicolor enable",
  "0xD01D": "VIC-II — Sprite X expand",
  "0xD01E": "VIC-II — Sprite-sprite collision (read clears)",
  "0xD01F": "VIC-II — Sprite-background collision (read clears)",
  "0xD020": "VIC-II — Border color",
  "0xD021": "VIC-II — Background color 0",
  "0xD022": "VIC-II — Background color 1 (multicolor/ECM)",
  "0xD023": "VIC-II — Background color 2 (multicolor/ECM)",
  "0xD024": "VIC-II — Background color 3 (ECM)",
  "0xD025": "VIC-II — Sprite multicolor 0",
  "0xD026": "VIC-II — Sprite multicolor 1",
  "0xD027": "VIC-II — Sprite 0 color",
  "0xD028": "VIC-II — Sprite 1 color",
  "0xD029": "VIC-II — Sprite 2 color",
  "0xD02A": "VIC-II — Sprite 3 color",
  "0xD02B": "VIC-II — Sprite 4 color",
  "0xD02C": "VIC-II — Sprite 5 color",
  "0xD02D": "VIC-II — Sprite 6 color",
  "0xD02E": "VIC-II — Sprite 7 color",
};

const SID_REGISTERS: Record<string, string> = {
  "0xD400": "SID — Voice 1 frequency (lo)",
  "0xD401": "SID — Voice 1 frequency (hi)",
  "0xD402": "SID — Voice 1 pulse width (lo)",
  "0xD403": "SID — Voice 1 pulse width (hi)",
  "0xD404": "SID — Voice 1 control register",
  "0xD405": "SID — Voice 1 attack/decay",
  "0xD406": "SID — Voice 1 sustain/release",
  "0xD418": "SID — Filter mode / volume",
};

const CIA_REGISTERS: Record<string, string> = {
  "0xDC00": "CIA1 — Port A (keyboard/joystick)",
  "0xDC01": "CIA1 — Port B (keyboard/joystick)",
  "0xDD00": "CIA2 — Port A (VIC bank select, serial bus)",
};

const MEMORY_AREAS: Record<string, string> = {
  "0x0400": "Screen RAM (default: $0400-$07E7, 40x25 chars)",
  "0xD800": "Color RAM ($D800-$DBE7, 4 bits per char cell)",
  "0x07F8": "Sprite pointers ($07F8-$07FF for sprites 0-7)",
};

const KEYWORD_DOCS: Record<string, string> = {
  entry: "Marks a scene as the program entry point. Exactly one scene must be `entry`.",
  scene: "Declares a scene — the primary architectural unit. Contains hardware config, sprites, lifecycle blocks, variables, and methods.",
  struct: "Declares a value type with fields and methods. No inheritance.",
  enum: "Declares an enumeration with a backing primitive type.",
  flags: "Modifier for `enum` — enables bitwise operations on members.",
  const: "Declares an immutable binding. Value must be a compile-time constant.",
  import: "Imports another Cork source file or standard library module.",
  hardware: "Declarative VIC-II configuration block inside a scene.",
  enter: "Lifecycle block — runs once when the scene is entered.",
  frame: "Lifecycle block — runs every frame (1/60s PAL, 1/50s NTSC). Must complete within raster budget.",
  relaxed: "Modifier for `frame` — compiler warns instead of erroring on potential budget overrun.",
  raster: "Raster interrupt block at a specific scanline. Multiple allowed; compiler chains them as IRQ handlers.",
  exit: "Lifecycle block — runs when transitioning away from this scene.",
  sprite: "Declares a hardware sprite (0-7) with data, position, and color.",
  go: "Transitions to another scene. Unloads current scene, loads target.",
  as: "Explicit type cast (e.g., `bigScore as byte`).",
  fallthrough: "Modifier for `switch` — enables C-style fall-through behavior.",
  var: "Type inference — the compiler deduces the type from the initializer.",
};

const TYPE_DOCS: Record<string, string> = {
  byte: "Unsigned 8-bit integer (0-255). Stored in zero page when possible.",
  sbyte: "Signed 8-bit integer (-128 to 127).",
  word: "Unsigned 16-bit integer (0-65535). Stored as two bytes, little-endian.",
  sword: "Signed 16-bit integer (-32768 to 32767).",
  fixed: "Unsigned 8.8 fixed-point (0.0 to 255.996). Two bytes.",
  sfixed: "Signed 8.8 fixed-point (-128.0 to 127.996). Two bytes.",
  bool: "Boolean — `true` or `false`. Stored as a byte.",
  string: "PETSCII string. `string[N]` for fixed-size, plain `string` infers from literal.",
};

const JOYSTICK_DOCS: Record<string, string> = {
  "joystick.port1": "Joystick in port 1 (directly reads CIA1 $DC01).",
  "joystick.port2": "Joystick in port 2 (directly reads CIA1 $DC00).",
  "joystick.port1.left": "Port 1 joystick left (bit 2).",
  "joystick.port1.right": "Port 1 joystick right (bit 3).",
  "joystick.port1.up": "Port 1 joystick up (bit 0).",
  "joystick.port1.down": "Port 1 joystick down (bit 1).",
  "joystick.port1.fire": "Port 1 fire button (bit 4).",
  "joystick.port2.left": "Port 2 joystick left (bit 2).",
  "joystick.port2.right": "Port 2 joystick right (bit 3).",
  "joystick.port2.up": "Port 2 joystick up (bit 0).",
  "joystick.port2.down": "Port 2 joystick down (bit 1).",
  "joystick.port2.fire": "Port 2 fire button (bit 4).",
};

export class CorkHoverProvider implements vscode.HoverProvider {
  provideHover(
    doc: vscode.TextDocument,
    position: vscode.Position
  ): vscode.Hover | undefined {
    const range = doc.getWordRangeAtPosition(position, /[\w.]+/);
    if (!range) return undefined;
    const word = doc.getText(range);

    // Color.xxx hover with swatch
    const colorMatch = word.match(/^Color\.(\w+)$/);
    if (colorMatch) {
      const c = COLOR_VALUES[colorMatch[1]];
      if (c) {
        const md = new vscode.MarkdownString();
        md.appendMarkdown(
          `**Color.${colorMatch[1]}** = \`${c.value}\`\n\n` +
          `$(color:${c.hex}) \`${c.hex}\` (approximate C64 palette)`
        );
        md.isTrusted = true;
        return new Hover(md, range);
      }
    }

    // Joystick registers
    const joyDoc = JOYSTICK_DOCS[word];
    if (joyDoc) {
      return new Hover(new vscode.MarkdownString(`**${word}**\n\n${joyDoc}`), range);
    }

    // Hex address hover — VIC, SID, CIA, memory
    const hexMatch = word.match(/^0x([0-9A-Fa-f]+)$/);
    if (hexMatch) {
      const normalized = "0x" + hexMatch[1].toUpperCase();
      const allRegs = { ...VIC_REGISTERS, ...SID_REGISTERS, ...CIA_REGISTERS };
      const desc = allRegs[normalized];
      if (desc) {
        return new Hover(new vscode.MarkdownString(`**\`${normalized}\`** — ${desc}`), range);
      }
      // Check memory area ranges
      for (const [addr, info] of Object.entries(MEMORY_AREAS)) {
        if (normalized === addr.toUpperCase() || normalized === addr) {
          return new Hover(new vscode.MarkdownString(`**\`${normalized}\`** — ${info}`), range);
        }
      }
    }

    // Keyword hover
    const kwDoc = KEYWORD_DOCS[word];
    if (kwDoc) {
      return new Hover(
        new vscode.MarkdownString(`**\`${word}\`** — ${kwDoc}`),
        range
      );
    }

    // Type hover
    const typeDoc = TYPE_DOCS[word];
    if (typeDoc) {
      return new Hover(
        new vscode.MarkdownString(`**\`${word}\`** — ${typeDoc}`),
        range
      );
    }

    return undefined;
  }
}

class Hover extends vscode.Hover {}
