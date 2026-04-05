import * as vscode from "vscode";

const COLORS = [
  { name: "black", value: 0 },
  { name: "white", value: 1 },
  { name: "red", value: 2 },
  { name: "cyan", value: 3 },
  { name: "purple", value: 4 },
  { name: "green", value: 5 },
  { name: "blue", value: 6 },
  { name: "yellow", value: 7 },
  { name: "orange", value: 8 },
  { name: "brown", value: 9 },
  { name: "lightRed", value: 10 },
  { name: "darkGrey", value: 11 },
  { name: "grey", value: 12 },
  { name: "lightGreen", value: 13 },
  { name: "lightBlue", value: 14 },
  { name: "lightGrey", value: 15 },
];

const GFX_MODES = ["text", "multicolorText", "bitmap", "multicolorBitmap", "ecm"];

const HARDWARE_SETTINGS = [
  { name: "mode", detail: "Graphics mode" },
  { name: "border", detail: "Border color" },
  { name: "background", detail: "Background color 0" },
  { name: "background1", detail: "Background color 1" },
  { name: "background2", detail: "Background color 2" },
  { name: "background3", detail: "Background color 3" },
  { name: "multicolor0", detail: "Sprite multicolor 0" },
  { name: "multicolor1", detail: "Sprite multicolor 1" },
];

const SPRITE_SETTINGS = [
  { name: "data", detail: "Sprite data (pattern or const ref)" },
  { name: "x", detail: "X position (word)" },
  { name: "y", detail: "Y position (byte)" },
  { name: "color", detail: "Sprite color" },
  { name: "multicolor", detail: "Enable multicolor mode" },
  { name: "expandX", detail: "Double width" },
  { name: "expandY", detail: "Double height" },
  { name: "priority", detail: "Sprite priority" },
];

const TYPE_KEYWORDS = ["byte", "sbyte", "word", "sword", "fixed", "sfixed", "bool", "string", "var"];
const DECL_KEYWORDS = ["entry", "scene", "struct", "enum", "flags", "const", "import"];
const CONTROL_KEYWORDS = ["if", "else", "while", "for", "in", "switch", "case", "default", "fallthrough", "break", "continue", "return", "go"];
const LIFECYCLE_KEYWORDS = ["hardware", "enter", "frame", "relaxed", "raster", "exit", "sprite"];

type BlockContext = "top" | "scene" | "hardware" | "sprite" | "code";

export class CorkCompletionProvider implements vscode.CompletionItemProvider {
  provideCompletionItems(
    doc: vscode.TextDocument,
    position: vscode.Position
  ): vscode.CompletionItem[] {
    const lineText = doc.lineAt(position).text;
    const linePrefix = lineText.substring(0, position.character);

    // Color. completions
    if (linePrefix.match(/Color\.\w*$/)) {
      return COLORS.map((c) => {
        const item = new vscode.CompletionItem(c.name, vscode.CompletionItemKind.EnumMember);
        item.detail = `Color.${c.name} = ${c.value}`;
        return item;
      });
    }

    // mode: completions
    if (linePrefix.match(/mode:\s*\w*$/)) {
      return GFX_MODES.map((m) => {
        const item = new vscode.CompletionItem(m, vscode.CompletionItemKind.EnumMember);
        item.detail = `Graphics mode: ${m}`;
        return item;
      });
    }

    // "go " completions — scene names from the file
    if (linePrefix.match(/go\s+\w*$/)) {
      return this.findSceneNames(doc).map((name) => {
        const item = new vscode.CompletionItem(name, vscode.CompletionItemKind.Class);
        item.detail = "Scene";
        return item;
      });
    }

    const ctx = this.getBlockContext(doc, position);
    const items: vscode.CompletionItem[] = [];

    if (ctx === "hardware") {
      for (const s of HARDWARE_SETTINGS) {
        const item = new vscode.CompletionItem(s.name, vscode.CompletionItemKind.Property);
        item.detail = s.detail;
        item.insertText = new vscode.SnippetString(`${s.name}: \$0;`);
        items.push(item);
      }
      return items;
    }

    if (ctx === "sprite") {
      for (const s of SPRITE_SETTINGS) {
        const item = new vscode.CompletionItem(s.name, vscode.CompletionItemKind.Property);
        item.detail = s.detail;
        item.insertText = new vscode.SnippetString(`${s.name}: \$0;`);
        items.push(item);
      }
      return items;
    }

    // Contextual keyword completions
    if (ctx === "top") {
      this.addKeywords(items, DECL_KEYWORDS, "Declaration keyword");
      this.addKeywords(items, TYPE_KEYWORDS, "Type");
    } else if (ctx === "scene") {
      this.addKeywords(items, LIFECYCLE_KEYWORDS, "Lifecycle keyword");
      this.addKeywords(items, TYPE_KEYWORDS, "Type");
      this.addKeywords(items, CONTROL_KEYWORDS, "Control flow");
      this.addKeywords(items, DECL_KEYWORDS.filter((k) => k === "const"), "Declaration keyword");
    } else {
      this.addKeywords(items, TYPE_KEYWORDS, "Type");
      this.addKeywords(items, CONTROL_KEYWORDS, "Control flow");
    }

    // Add scene names, struct names, enum names from the file
    for (const name of this.findSceneNames(doc)) {
      const item = new vscode.CompletionItem(name, vscode.CompletionItemKind.Class);
      item.detail = "Scene";
      items.push(item);
    }
    for (const name of this.findNames(doc, /\bstruct\s+([A-Z]\w*)/g)) {
      const item = new vscode.CompletionItem(name, vscode.CompletionItemKind.Struct);
      item.detail = "Struct";
      items.push(item);
    }
    for (const name of this.findNames(doc, /\benum\s+([A-Z]\w*)/g)) {
      const item = new vscode.CompletionItem(name, vscode.CompletionItemKind.Enum);
      item.detail = "Enum";
      items.push(item);
    }

    // Add Color as a completion
    const colorItem = new vscode.CompletionItem("Color", vscode.CompletionItemKind.Class);
    colorItem.detail = "C64 color constants";
    colorItem.commitCharacters = ["."];
    items.push(colorItem);

    return items;
  }

  private addKeywords(items: vscode.CompletionItem[], keywords: string[], detail: string) {
    for (const kw of keywords) {
      const item = new vscode.CompletionItem(kw, vscode.CompletionItemKind.Keyword);
      item.detail = detail;
      items.push(item);
    }
  }

  private findSceneNames(doc: vscode.TextDocument): string[] {
    return this.findNames(doc, /\bscene\s+([A-Z]\w*)/g);
  }

  private findNames(doc: vscode.TextDocument, re: RegExp): string[] {
    const text = doc.getText();
    const names: string[] = [];
    let m;
    while ((m = re.exec(text)) !== null) {
      if (!names.includes(m[1])) names.push(m[1]);
    }
    return names;
  }

  /** Determine what block context the cursor is in by counting braces. */
  private getBlockContext(
    doc: vscode.TextDocument,
    position: vscode.Position
  ): BlockContext {
    const text = doc.getText(new vscode.Range(new vscode.Position(0, 0), position));

    // Walk backwards through brace-matched blocks to find the nearest enclosing keyword
    let depth = 0;
    for (let i = text.length - 1; i >= 0; i--) {
      const ch = text[i];
      if (ch === "}") depth++;
      else if (ch === "{") {
        if (depth > 0) {
          depth--;
        } else {
          // This is the opening brace we're inside — check what precedes it
          const preceding = text.substring(Math.max(0, i - 40), i).trim();
          if (/hardware\s*$/.test(preceding)) return "hardware";
          if (/\bsprite\s+\d+\s+\w+\s*$/.test(preceding)) return "sprite";
          if (/\bscene\s+\w+\s*$/.test(preceding)) return "scene";
          if (/entry\s+scene\s+\w+\s*$/.test(preceding)) return "scene";
          // Inside some other block within a scene — treat as code
          return "code";
        }
      }
    }

    return "top";
  }
}
