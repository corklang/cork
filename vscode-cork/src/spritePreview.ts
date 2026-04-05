import * as vscode from "vscode";

// Approximate C64 palette (same as hoverProvider)
const C64_PALETTE: Record<string, string> = {
  transparent: "#1a1a2e", // dark bg for checkerboard
  ".": "",                // handled specially as checkerboard
  "#": "#FFFFFF",         // hi-res sprite color (white)
  "1": "#7ABFC7",         // multicolor 0 (cyan)
  "2": "#68A941",         // sprite color (green)
  "3": "#894036",         // multicolor 1 (red)
};

const CHECKER_LIGHT = "#2a2a3e";
const CHECKER_DARK = "#1a1a2e";

interface SpritePattern {
  /** Line number of the opening backtick */
  startLine: number;
  /** Line number of the closing backtick */
  endLine: number;
  /** Parsed pixel rows: each row is array of character tokens */
  rows: string[][];
  /** true if multicolor (uses 1/2/3), false if hires (uses #) */
  isMulticolor: boolean;
  /** Pixel width per cell (2 for multicolor, 1 for hires) */
  pixelWidth: number;
}

export class SpritePreviewProvider implements vscode.CodeLensProvider {
  private _onDidChangeCodeLenses = new vscode.EventEmitter<void>();
  readonly onDidChangeCodeLenses = this._onDidChangeCodeLenses.event;

  constructor() {
    vscode.workspace.onDidChangeTextDocument(() => this._onDidChangeCodeLenses.fire());
  }

  provideCodeLenses(doc: vscode.TextDocument): vscode.CodeLens[] {
    const patterns = findSpritePatterns(doc);
    return patterns.map((pat) => {
      const range = new vscode.Range(pat.startLine, 0, pat.startLine, 0);
      const svg = renderSpriteSvg(pat);
      const dataUri = `data:image/svg+xml;base64,${Buffer.from(svg).toString("base64")}`;

      const width = pat.isMulticolor ? 12 : 24;
      const label = pat.isMulticolor
        ? `Multicolor ${width}x${pat.rows.length}`
        : `Hi-res ${width}x${pat.rows.length}`;

      return new vscode.CodeLens(range, {
        title: `$(preview) ${label} sprite`,
        command: "cork.previewSprite",
        arguments: [doc.uri.toString(), pat.startLine],
      });
    });
  }
}

export class SpriteHoverProvider implements vscode.HoverProvider {
  provideHover(
    doc: vscode.TextDocument,
    position: vscode.Position
  ): vscode.Hover | undefined {
    const patterns = findSpritePatterns(doc);
    const pat = patterns.find(
      (p) => position.line >= p.startLine && position.line <= p.endLine
    );
    if (!pat) return undefined;

    const svg = renderSpriteSvg(pat);
    const dataUri = `data:image/svg+xml;base64,${Buffer.from(svg).toString("base64")}`;

    const md = new vscode.MarkdownString();
    md.isTrusted = true;
    md.supportHtml = true;

    const width = pat.isMulticolor ? 12 : 24;
    md.appendMarkdown(
      `**Sprite preview** (${width}x${pat.rows.length})\n\n` +
      `<img src="${dataUri}" width="${width * 8}" height="${pat.rows.length * 8}"/>`
    );

    return new vscode.Hover(
      md,
      new vscode.Range(pat.startLine, 0, pat.endLine, 999)
    );
  }
}

// Hires toggles: . ↔ #
// Multicolor cycles: . → 1 → 2 → 3 → .
const HIRES_TOGGLE: Record<string, string> = { ".": "#", "#": "." };
const MC_CYCLE: Record<string, string> = { ".": "1", "1": "2", "2": "3", "3": "." };

interface PanelState {
  panel: vscode.WebviewPanel;
  docUri: string;
  startLine: number;
  updating: boolean;
}

export class SpritePreviewPanelManager {
  private panels = new Map<string, PanelState>();
  private disposables: vscode.Disposable[] = [];

  constructor() {
    this.disposables.push(
      vscode.workspace.onDidChangeTextDocument((e) => {
        if (e.document.languageId === "cork") {
          this.updateAll(e.document);
        }
      })
    );
  }

  open(docUri: string, startLine: number) {
    const key = `${docUri}:${startLine}`;
    const existing = this.panels.get(key);
    if (existing) {
      existing.panel.reveal(vscode.ViewColumn.Beside);
      return;
    }

    const doc = vscode.workspace.textDocuments.find(
      (d) => d.uri.toString() === docUri
    );
    if (!doc) return;

    const pat = findSpritePatterns(doc).find((p) => p.startLine === startLine);
    if (!pat) return;

    const width = pat.isMulticolor ? 12 : 24;
    const label = pat.isMulticolor
      ? `Multicolor ${width}x${pat.rows.length}`
      : `Hi-res ${width}x${pat.rows.length}`;

    const panel = vscode.window.createWebviewPanel(
      "corkSpritePreview",
      `Sprite: ${label}`,
      vscode.ViewColumn.Beside,
      { enableScripts: true }
    );

    const state: PanelState = { panel, docUri, startLine, updating: false };

    panel.webview.html = buildInteractiveHtml(pat);

    panel.webview.onDidReceiveMessage((msg) => {
      if (msg.type === "toggle") {
        this.handleToggle(state, msg.x, msg.y);
      } else if (msg.type === "paint") {
        this.handlePaint(state, msg.pixels);
      }
    });

    panel.onDidDispose(() => {
      this.panels.delete(key);
    });

    this.panels.set(key, state);
  }

  private async handleToggle(state: PanelState, x: number, y: number) {
    if (state.updating) return;
    state.updating = true;

    try {
      const doc = vscode.workspace.textDocuments.find(
        (d) => d.uri.toString() === state.docUri
      );
      if (!doc) return;

      const pat = findSpritePatterns(doc).find(
        (p) => p.startLine === state.startLine
      ) ?? findSpritePatterns(doc).find(
        (p) => Math.abs(p.startLine - state.startLine) <= 3
      );
      if (!pat) return;
      if (y < 0 || y >= pat.rows.length) return;
      if (x < 0 || x >= pat.rows[y].length) return;

      const currentChar = pat.rows[y][x];
      const newChar = pat.isMulticolor
        ? (MC_CYCLE[currentChar] ?? ".")
        : (HIRES_TOGGLE[currentChar] ?? ".");

      // Find the source line: pattern rows start at startLine + 1
      const sourceLine = pat.startLine + 1 + y;
      const lineText = doc.lineAt(sourceLine).text;

      // Find the column of the xth token in the line
      const col = findNthTokenColumn(lineText, x);
      if (col === -1) return;

      const edit = new vscode.WorkspaceEdit();
      edit.replace(
        doc.uri,
        new vscode.Range(sourceLine, col, sourceLine, col + currentChar.length),
        newChar
      );
      await vscode.workspace.applyEdit(edit);

      // Refresh immediately after our own edit
      const updatedDoc = vscode.workspace.textDocuments.find(
        (d) => d.uri.toString() === state.docUri
      );
      if (updatedDoc) {
        this.refreshPanel(state, updatedDoc);
      }
    } finally {
      state.updating = false;
    }
  }

  private async handlePaint(
    state: PanelState,
    pixels: { x: number; y: number; ch: string }[]
  ) {
    if (state.updating || pixels.length === 0) return;
    state.updating = true;

    try {
      const doc = vscode.workspace.textDocuments.find(
        (d) => d.uri.toString() === state.docUri
      );
      if (!doc) return;

      const pat =
        findSpritePatterns(doc).find(
          (p) => p.startLine === state.startLine
        ) ??
        findSpritePatterns(doc).find(
          (p) => Math.abs(p.startLine - state.startLine) <= 3
        );
      if (!pat) return;

      const edit = new vscode.WorkspaceEdit();

      for (const px of pixels) {
        if (px.y < 0 || px.y >= pat.rows.length) continue;
        if (px.x < 0 || px.x >= pat.rows[px.y].length) continue;

        const currentChar = pat.rows[px.y][px.x];
        if (currentChar === px.ch) continue;

        const sourceLine = pat.startLine + 1 + px.y;
        const lineText = doc.lineAt(sourceLine).text;
        const col = findNthTokenColumn(lineText, px.x);
        if (col === -1) continue;

        edit.replace(
          doc.uri,
          new vscode.Range(sourceLine, col, sourceLine, col + currentChar.length),
          px.ch
        );
      }

      await vscode.workspace.applyEdit(edit);

      const updatedDoc = vscode.workspace.textDocuments.find(
        (d) => d.uri.toString() === state.docUri
      );
      if (updatedDoc) {
        this.refreshPanel(state, updatedDoc);
      }
    } finally {
      state.updating = false;
    }
  }

  private updateAll(doc: vscode.TextDocument) {
    const docUri = doc.uri.toString();

    for (const [, state] of this.panels) {
      if (state.docUri !== docUri) continue;
      if (state.updating) continue;
      this.refreshPanel(state, doc);
    }
  }

  private refreshPanel(state: PanelState, doc: vscode.TextDocument) {
    const patterns = findSpritePatterns(doc);
    const pat =
      patterns.find((p) => p.startLine === state.startLine) ??
      patterns.find((p) => Math.abs(p.startLine - state.startLine) <= 3);
    if (pat) {
      state.panel.webview.html = buildInteractiveHtml(pat);
    }
  }

  dispose() {
    for (const d of this.disposables) d.dispose();
    for (const [, state] of this.panels) state.panel.dispose();
  }
}

/** Find the column offset of the nth single-character token in a line */
function findNthTokenColumn(line: string, n: number): number {
  let count = 0;
  let i = 0;
  while (i < line.length) {
    // Skip whitespace
    while (i < line.length && /\s/.test(line[i])) i++;
    if (i >= line.length) break;

    // Found a token
    if (count === n) return i;
    count++;

    // Skip the token
    while (i < line.length && !/\s/.test(line[i])) i++;
  }
  return -1;
}

function colorForChar(ch: string): string {
  if (ch === ".") return "transparent";
  return C64_PALETTE[ch] ?? "#FF00FF";
}

function buildInteractiveHtml(pat: SpritePattern): string {
  const cellSize = 20;
  const cellW = pat.pixelWidth * cellSize;
  const cols = pat.rows[0]?.length ?? 0;
  const gridWidth = cols * cellW;
  const gridHeight = pat.rows.length * cellSize;

  let cells = "";
  for (let y = 0; y < pat.rows.length; y++) {
    for (let x = 0; x < pat.rows[y].length; x++) {
      const ch = pat.rows[y][x];
      const px = x * cellW;
      const py = y * cellSize;
      const color = colorForChar(ch);

      let style: string;
      if (color === "transparent") {
        // Checkerboard
        const c1 = (x + y) % 2 === 0 ? CHECKER_LIGHT : CHECKER_DARK;
        const c2 = (x + y) % 2 === 0 ? CHECKER_DARK : CHECKER_LIGHT;
        style = `background:repeating-conic-gradient(${c1} 0% 25%, ${c2} 0% 50%) 50%/50% 50%;`;
      } else {
        style = `background:${color};`;
      }

      cells += `<div class="cell" data-x="${x}" data-y="${y}" style="left:${px}px;top:${py}px;width:${cellW}px;height:${cellSize}px;${style}"></div>`;
    }
  }

  const mode = pat.isMulticolor ? "Multicolor" : "Hi-res";
  const ismc = pat.isMulticolor;
  const hint = ismc
    ? "Click to cycle, drag to paint"
    : "Click to toggle, drag to paint";

  // Encode pixel data as JSON for the JS side
  const pixelData = JSON.stringify(pat.rows);

  return `<!DOCTYPE html>
<html><head><style>
  body {
    background: #1a1a2e;
    margin: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    height: 100vh;
    font-family: monospace;
    color: #888;
    user-select: none;
  }
  .grid {
    position: relative;
    width: ${gridWidth}px;
    height: ${gridHeight}px;
    cursor: crosshair;
  }
  .cell {
    position: absolute;
    box-sizing: border-box;
    border: 1px solid rgba(255,255,255,0.05);
  }
  .cell:hover {
    border-color: rgba(255,255,255,0.4);
  }
  .info {
    margin-top: 12px;
    font-size: 12px;
  }
</style></head><body>
  <div class="grid" id="grid">${cells}</div>
  <div class="info">${mode} ${cols}x${pat.rows.length} — ${hint}</div>
  <script>
    const vscode = acquireVsCodeApi();
    const grid = document.getElementById('grid');
    const pixels = ${pixelData};
    const isMulticolor = ${ismc};

    const hiresToggle = { '.': '#', '#': '.' };
    const mcCycle = { '.': '1', '1': '2', '2': '3', '3': '.' };

    const palette = {
      '#': '${C64_PALETTE["#"]}',
      '1': '${C64_PALETTE["1"]}',
      '2': '${C64_PALETTE["2"]}',
      '3': '${C64_PALETTE["3"]}',
    };
    const checkerLight = '${CHECKER_LIGHT}';
    const checkerDark = '${CHECKER_DARK}';

    let painting = false;
    let paintChar = null;
    let painted = {};

    function cellKey(x, y) { return x + ',' + y; }

    function toggleChar(ch) {
      return isMulticolor ? (mcCycle[ch] || '.') : (hiresToggle[ch] || '.');
    }

    function getCellAt(e) {
      const cell = document.elementFromPoint(e.clientX, e.clientY);
      if (!cell || !cell.classList.contains('cell')) return null;
      return cell;
    }

    function updateCellVisual(cell, ch) {
      const x = parseInt(cell.dataset.x);
      const y = parseInt(cell.dataset.y);
      if (ch === '.') {
        const c1 = (x + y) % 2 === 0 ? checkerLight : checkerDark;
        const c2 = (x + y) % 2 === 0 ? checkerDark : checkerLight;
        cell.style.background = 'repeating-conic-gradient(' + c1 + ' 0% 25%, ' + c2 + ' 0% 50%) 50%/50% 50%';
      } else {
        cell.style.background = palette[ch] || '#FF00FF';
      }
    }

    function paintCell(cell) {
      const x = parseInt(cell.dataset.x);
      const y = parseInt(cell.dataset.y);
      const key = cellKey(x, y);
      if (painted[key]) return;
      painted[key] = { x, y, ch: paintChar };
      updateCellVisual(cell, paintChar);
    }

    grid.addEventListener('mousedown', (e) => {
      e.preventDefault();
      const cell = getCellAt(e);
      if (!cell) return;

      const x = parseInt(cell.dataset.x);
      const y = parseInt(cell.dataset.y);
      const currentCh = pixels[y] && pixels[y][x] || '.';

      paintChar = toggleChar(currentCh);
      painting = true;
      painted = {};
      paintCell(cell);
    });

    document.addEventListener('mousemove', (e) => {
      if (!painting) return;
      const cell = getCellAt(e);
      if (cell) paintCell(cell);
    });

    document.addEventListener('mouseup', (e) => {
      if (!painting) return;
      painting = false;

      const pixelList = Object.values(painted);
      if (pixelList.length === 0) return;

      if (pixelList.length === 1) {
        vscode.postMessage({ type: 'toggle', x: pixelList[0].x, y: pixelList[0].y });
      } else {
        vscode.postMessage({ type: 'paint', pixels: pixelList });
      }
      painted = {};
    });
  </script>
</body></html>`;
}

function findSpritePatterns(doc: vscode.TextDocument): SpritePattern[] {
  const patterns: SpritePattern[] = [];
  const text = doc.getText();
  const lines = text.split("\n");

  let inPattern = false;
  let startLine = 0;
  let rows: string[][] = [];

  for (let i = 0; i < lines.length; i++) {
    const trimmed = lines[i].trim();

    if (!inPattern) {
      // Look for opening backtick (at end of line)
      const backticks = (trimmed.match(/`/g) || []).length;
      if (backticks === 1 && trimmed.endsWith("`")) {
        inPattern = true;
        startLine = i;
        rows = [];
      }
    } else {
      if (trimmed.includes("`")) {
        // Closing backtick
        inPattern = false;

        if (rows.length > 0) {
          const hasMulticolor = rows.some((row) =>
            row.some((ch) => ch === "1" || ch === "2" || ch === "3")
          );
          patterns.push({
            startLine,
            endLine: i,
            rows,
            isMulticolor: hasMulticolor,
            pixelWidth: hasMulticolor ? 2 : 1,
          });
        }
      } else {
        // Parse pixel row — extract meaningful characters
        const chars = trimmed.split(/\s+/).filter((ch) => ch.length === 1);
        if (chars.length > 0) {
          rows.push(chars);
        }
      }
    }
  }

  return patterns;
}

function renderSpriteSvg(pat: SpritePattern): string {
  const scale = 8;
  const cellW = pat.pixelWidth * scale;
  const cellH = scale;
  const width = (pat.rows[0]?.length ?? 0) * cellW;
  const height = pat.rows.length * cellH;

  let rects = "";

  for (let y = 0; y < pat.rows.length; y++) {
    const row = pat.rows[y];
    for (let x = 0; x < row.length; x++) {
      const ch = row[x];
      const px = x * cellW;
      const py = y * cellH;

      if (ch === ".") {
        // Checkerboard for transparent
        rects += `<rect x="${px}" y="${py}" width="${cellW}" height="${cellH}" fill="${CHECKER_DARK}"/>`;
        // Draw 2x2 checker squares
        const half = cellW / 2;
        const halfH = cellH / 2;
        if ((x + y) % 2 === 0) {
          rects += `<rect x="${px}" y="${py}" width="${half}" height="${halfH}" fill="${CHECKER_LIGHT}"/>`;
          rects += `<rect x="${px + half}" y="${py + halfH}" width="${half}" height="${halfH}" fill="${CHECKER_LIGHT}"/>`;
        } else {
          rects += `<rect x="${px + half}" y="${py}" width="${half}" height="${halfH}" fill="${CHECKER_LIGHT}"/>`;
          rects += `<rect x="${px}" y="${py + halfH}" width="${half}" height="${halfH}" fill="${CHECKER_LIGHT}"/>`;
        }
      } else {
        const color = C64_PALETTE[ch] ?? "#FF00FF";
        rects += `<rect x="${px}" y="${py}" width="${cellW}" height="${cellH}" fill="${color}"/>`;
      }
    }
  }

  return (
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">` +
    rects +
    `</svg>`
  );
}
