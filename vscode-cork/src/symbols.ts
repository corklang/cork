import * as vscode from "vscode";

export interface CorkSymbol {
  name: string;
  kind: CorkSymbolKind;
  range: vscode.Range;
  nameRange: vscode.Range;
  detail?: string;
  children: CorkSymbol[];
}

export type CorkSymbolKind =
  | "scene"
  | "struct"
  | "enum"
  | "enumMember"
  | "method"
  | "variable"
  | "constant"
  | "field"
  | "sprite"
  | "lifecycle"
  | "import";

// Regex patterns for declarations
const SCENE_RE = /\b(entry\s+)?scene\s+(\w+)\s*\{/g;
const STRUCT_RE = /\bstruct\s+(\w+)\s*\{/g;
const ENUM_RE = /\b(?:flags\s+)?enum\s+(\w+)\s*(?::\s*\w+)?\s*\{/g;
const ENUM_MEMBER_RE = /^\s*(\w+)\s*=/;
const METHOD_RE = /^(\s*)(?:(byte|sbyte|word|sword|fixed|sfixed|bool)\s+)?(\w+):\s*(?:\(|{)/;
const VARIABLE_RE = /^\s*(byte|sbyte|word|sword|fixed|sfixed|bool|string|var)\s+(\w+)/;
const CONST_RE = /^\s*const\s+(?:(?:byte|sbyte|word|sword|fixed|sfixed|bool|string)(?:\[\d+\])?\s+)?(\w+)\s*=/;
const FIELD_RE = /^\s*(byte|sbyte|word|sword|fixed|sfixed|bool|string|(?:[A-Z]\w*))\s+(\w+)\s*(?:=|;)/;
const SPRITE_RE = /^\s*sprite\s+(\d+)\s+(\w+)\s*\{/;
const LIFECYCLE_RE = /^\s*(enter|frame|exit|hardware)\s*\{/;
const RASTER_RE = /^\s*raster\s+(\d+)\s*\{/;
const IMPORT_RE = /^\s*import\s+"([^"]+)"\s*;/;
const STRUCT_ARRAY_RE = /^\s*(\w+)\[(\d+)\]\s+(\w+)\s*;/;

export function parseSymbols(doc: vscode.TextDocument): CorkSymbol[] {
  const text = doc.getText();
  const lines = text.split("\n");
  const symbols: CorkSymbol[] = [];

  // First pass: find top-level block ranges (scenes, structs, enums)
  const blocks = findTopLevelBlocks(lines);

  for (const block of blocks) {
    if (block.kind === "scene") {
      const children = parseSceneChildren(lines, block.bodyStart, block.end);
      symbols.push({
        name: block.name,
        kind: "scene",
        detail: block.detail,
        range: new vscode.Range(block.start, 0, block.end, lines[block.end].length),
        nameRange: nameRangeOnLine(lines[block.start], block.name, block.start),
        children,
      });
    } else if (block.kind === "struct") {
      const children = parseStructChildren(lines, block.bodyStart, block.end);
      symbols.push({
        name: block.name,
        kind: "struct",
        range: new vscode.Range(block.start, 0, block.end, lines[block.end].length),
        nameRange: nameRangeOnLine(lines[block.start], block.name, block.start),
        children,
      });
    } else if (block.kind === "enum") {
      const children = parseEnumChildren(lines, block.bodyStart, block.end);
      symbols.push({
        name: block.name,
        kind: "enum",
        range: new vscode.Range(block.start, 0, block.end, lines[block.end].length),
        nameRange: nameRangeOnLine(lines[block.start], block.name, block.start),
        children,
      });
    }
  }

  // Find global declarations (outside any block)
  const blockRanges = blocks.map((b) => [b.start, b.end] as [number, number]);
  for (let i = 0; i < lines.length; i++) {
    if (isInsideAny(i, blockRanges)) continue;
    const line = lines[i];

    const importMatch = IMPORT_RE.exec(line);
    if (importMatch) {
      symbols.push({
        name: importMatch[1],
        kind: "import",
        range: lineRange(i, line),
        nameRange: nameRangeOnLine(line, importMatch[1], i),
        children: [],
      });
      continue;
    }

    const constMatch = CONST_RE.exec(line);
    if (constMatch) {
      symbols.push({
        name: constMatch[1],
        kind: "constant",
        range: lineRange(i, line),
        nameRange: nameRangeOnLine(line, constMatch[1], i),
        children: [],
      });
      continue;
    }

    const methodMatch = METHOD_RE.exec(line);
    if (methodMatch) {
      const name = methodMatch[3] + ":";
      const end = findBlockEnd(lines, i);
      symbols.push({
        name,
        kind: "method",
        detail: methodMatch[2] ? `${methodMatch[2]}` : undefined,
        range: new vscode.Range(i, 0, end, lines[end].length),
        nameRange: nameRangeOnLine(line, methodMatch[3], i),
        children: [],
      });
      continue;
    }

    const varMatch = VARIABLE_RE.exec(line);
    if (varMatch) {
      symbols.push({
        name: varMatch[2],
        kind: "variable",
        detail: varMatch[1],
        range: lineRange(i, line),
        nameRange: nameRangeOnLine(line, varMatch[2], i),
        children: [],
      });
    }
  }

  return symbols;
}

interface BlockInfo {
  kind: "scene" | "struct" | "enum";
  name: string;
  detail?: string;
  start: number;
  bodyStart: number;
  end: number;
}

function findTopLevelBlocks(lines: string[]): BlockInfo[] {
  const blocks: BlockInfo[] = [];
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];

    // Scene
    const sceneMatch = /\b(entry\s+)?scene\s+(\w+)\s*\{/.exec(line);
    if (sceneMatch) {
      const end = findBlockEnd(lines, i);
      blocks.push({
        kind: "scene",
        name: sceneMatch[2],
        detail: sceneMatch[1] ? "entry" : undefined,
        start: i,
        bodyStart: i + 1,
        end,
      });
      i = end + 1;
      continue;
    }

    // Struct
    const structMatch = /\bstruct\s+(\w+)\s*\{/.exec(line);
    if (structMatch) {
      const end = findBlockEnd(lines, i);
      blocks.push({
        kind: "struct",
        name: structMatch[1],
        start: i,
        bodyStart: i + 1,
        end,
      });
      i = end + 1;
      continue;
    }

    // Enum
    const enumMatch = /\b(?:flags\s+)?enum\s+(\w+)\s*(?::\s*\w+)?\s*\{/.exec(line);
    if (enumMatch) {
      const end = findBlockEnd(lines, i);
      blocks.push({
        kind: "enum",
        name: enumMatch[1],
        start: i,
        bodyStart: i + 1,
        end,
      });
      i = end + 1;
      continue;
    }

    i++;
  }

  return blocks;
}

function parseSceneChildren(
  lines: string[],
  start: number,
  end: number
): CorkSymbol[] {
  const children: CorkSymbol[] = [];
  let i = start;

  while (i < end) {
    const line = lines[i];

    const spriteMatch = SPRITE_RE.exec(line);
    if (spriteMatch) {
      const blockEnd = findBlockEnd(lines, i);
      children.push({
        name: `sprite ${spriteMatch[1]} ${spriteMatch[2]}`,
        kind: "sprite",
        range: new vscode.Range(i, 0, blockEnd, lines[blockEnd].length),
        nameRange: nameRangeOnLine(line, spriteMatch[2], i),
        children: [],
      });
      i = blockEnd + 1;
      continue;
    }

    const lifecycleMatch = LIFECYCLE_RE.exec(line);
    if (lifecycleMatch) {
      const blockEnd = findBlockEnd(lines, i);
      children.push({
        name: lifecycleMatch[1],
        kind: "lifecycle",
        range: new vscode.Range(i, 0, blockEnd, lines[blockEnd].length),
        nameRange: nameRangeOnLine(line, lifecycleMatch[1], i),
        children: [],
      });
      i = blockEnd + 1;
      continue;
    }

    const rasterMatch = RASTER_RE.exec(line);
    if (rasterMatch) {
      const blockEnd = findBlockEnd(lines, i);
      children.push({
        name: `raster ${rasterMatch[1]}`,
        kind: "lifecycle",
        range: new vscode.Range(i, 0, blockEnd, lines[blockEnd].length),
        nameRange: nameRangeOnLine(line, rasterMatch[1], i),
        children: [],
      });
      i = blockEnd + 1;
      continue;
    }

    const methodMatch = METHOD_RE.exec(line);
    if (methodMatch) {
      const name = methodMatch[3] + ":";
      const blockEnd = findBlockEnd(lines, i);
      children.push({
        name,
        kind: "method",
        detail: methodMatch[2] ? `${methodMatch[2]}` : undefined,
        range: new vscode.Range(i, 0, blockEnd, lines[blockEnd].length),
        nameRange: nameRangeOnLine(line, methodMatch[3], i),
        children: [],
      });
      i = blockEnd + 1;
      continue;
    }

    const constMatch = CONST_RE.exec(line);
    if (constMatch) {
      children.push({
        name: constMatch[1],
        kind: "constant",
        range: lineRange(i, line),
        nameRange: nameRangeOnLine(line, constMatch[1], i),
        children: [],
      });
      i++;
      continue;
    }

    const structArrayMatch = STRUCT_ARRAY_RE.exec(line);
    if (structArrayMatch) {
      children.push({
        name: structArrayMatch[3],
        kind: "variable",
        detail: `${structArrayMatch[1]}[${structArrayMatch[2]}]`,
        range: lineRange(i, line),
        nameRange: nameRangeOnLine(line, structArrayMatch[3], i),
        children: [],
      });
      i++;
      continue;
    }

    const varMatch = VARIABLE_RE.exec(line);
    if (varMatch && !line.trim().startsWith("//")) {
      children.push({
        name: varMatch[2],
        kind: "variable",
        detail: varMatch[1],
        range: lineRange(i, line),
        nameRange: nameRangeOnLine(line, varMatch[2], i),
        children: [],
      });
      i++;
      continue;
    }

    i++;
  }

  return children;
}

function parseStructChildren(
  lines: string[],
  start: number,
  end: number
): CorkSymbol[] {
  const children: CorkSymbol[] = [];

  for (let i = start; i < end; i++) {
    const line = lines[i];

    const methodMatch = METHOD_RE.exec(line);
    if (methodMatch) {
      const name = methodMatch[3] + ":";
      const blockEnd = findBlockEnd(lines, i);
      children.push({
        name,
        kind: "method",
        detail: methodMatch[2] ? `${methodMatch[2]}` : undefined,
        range: new vscode.Range(i, 0, blockEnd, lines[blockEnd].length),
        nameRange: nameRangeOnLine(line, methodMatch[3], i),
        children: [],
      });
      i = blockEnd;
      continue;
    }

    const fieldMatch = FIELD_RE.exec(line);
    if (fieldMatch && !line.trim().startsWith("//")) {
      children.push({
        name: fieldMatch[2],
        kind: "field",
        detail: fieldMatch[1],
        range: lineRange(i, line),
        nameRange: nameRangeOnLine(line, fieldMatch[2], i),
        children: [],
      });
    }
  }

  return children;
}

function parseEnumChildren(
  lines: string[],
  start: number,
  end: number
): CorkSymbol[] {
  const children: CorkSymbol[] = [];

  for (let i = start; i < end; i++) {
    const match = ENUM_MEMBER_RE.exec(lines[i]);
    if (match) {
      children.push({
        name: match[1],
        kind: "enumMember",
        range: lineRange(i, lines[i]),
        nameRange: nameRangeOnLine(lines[i], match[1], i),
        children: [],
      });
    }
  }

  return children;
}

/** Find the line of the closing brace that matches the opening brace on `startLine`. */
function findBlockEnd(lines: string[], startLine: number): number {
  let depth = 0;
  let inBacktick = false;

  for (let i = startLine; i < lines.length; i++) {
    const line = lines[i];
    for (const ch of line) {
      if (ch === "`") {
        inBacktick = !inBacktick;
        continue;
      }
      if (inBacktick) continue;
      if (ch === "{") depth++;
      else if (ch === "}") {
        depth--;
        if (depth === 0) return i;
      }
    }
  }
  return lines.length - 1;
}

function isInsideAny(line: number, ranges: [number, number][]): boolean {
  return ranges.some(([start, end]) => line >= start && line <= end);
}

function lineRange(line: number, text: string): vscode.Range {
  return new vscode.Range(line, 0, line, text.length);
}

function nameRangeOnLine(
  lineText: string,
  name: string,
  line: number
): vscode.Range {
  const col = lineText.indexOf(name);
  if (col === -1) return new vscode.Range(line, 0, line, name.length);
  return new vscode.Range(line, col, line, col + name.length);
}

/** Flatten all symbols and their children into a single list. */
export function flattenSymbols(symbols: CorkSymbol[]): CorkSymbol[] {
  const result: CorkSymbol[] = [];
  for (const sym of symbols) {
    result.push(sym);
    result.push(...flattenSymbols(sym.children));
  }
  return result;
}
