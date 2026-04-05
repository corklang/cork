import * as vscode from "vscode";

export class PrgViewerProvider implements vscode.CustomReadonlyEditorProvider {
  openCustomDocument(uri: vscode.Uri): vscode.CustomDocument {
    return { uri, dispose() {} };
  }

  async resolveCustomEditor(
    document: vscode.CustomDocument,
    webviewPanel: vscode.WebviewPanel
  ): Promise<void> {
    webviewPanel.webview.options = { enableScripts: false };

    const data = await vscode.workspace.fs.readFile(document.uri);
    const bytes = new Uint8Array(data);

    webviewPanel.webview.html = buildPrgHtml(bytes, document.uri);
  }
}

function buildPrgHtml(bytes: Uint8Array, uri: vscode.Uri): string {
  const filename = uri.path.split("/").pop() ?? "unknown.prg";

  if (bytes.length < 2) {
    return page(filename, `<p class="error">File too small (${bytes.length} bytes)</p>`);
  }

  const loadAddr = bytes[0] | (bytes[1] << 8);
  const payload = bytes.slice(2);
  const endAddr = loadAddr + payload.length - 1;

  // Detect BASIC stub (starts at $0801 with a SYS line)
  let basicEnd = 0;
  let sysAddr = 0;
  if (loadAddr === 0x0801 && payload.length > 10) {
    const stub = detectBasicStub(payload, loadAddr);
    basicEnd = stub.end;
    sysAddr = stub.sysAddr;
  }

  const totalRam = 0x9FFF - loadAddr;
  const usedPct = Math.round((payload.length / totalRam) * 100);

  let summary = `
    <div class="summary">
      <div class="row"><span class="label">File</span><span class="value">${filename}</span></div>
      <div class="row"><span class="label">Size</span><span class="value">${bytes.length} bytes (${payload.length} payload)</span></div>
      <div class="row"><span class="label">Load address</span><span class="value">$${hex16(loadAddr)}</span></div>
      <div class="row"><span class="label">End address</span><span class="value">$${hex16(endAddr)}</span></div>
      <div class="row"><span class="label">RAM usage</span><span class="value">${payload.length}/${totalRam} bytes (${usedPct}%)</span></div>`;

  if (sysAddr > 0) {
    summary += `
      <div class="row"><span class="label">BASIC stub</span><span class="value">SYS ${sysAddr} ($${hex16(sysAddr)})</span></div>
      <div class="row"><span class="label">Code starts</span><span class="value">$${hex16(sysAddr)}</span></div>`;
  }

  summary += `</div>`;

  // Memory bar
  const barHtml = buildMemoryBar(loadAddr, payload.length, basicEnd, sysAddr);

  // Hex dump
  const hexHtml = buildHexDump(payload, loadAddr, basicEnd, sysAddr);

  // Disassembly (from SYS address or start of payload)
  const codeOffset = sysAddr > 0 ? sysAddr - loadAddr : 0;
  const disasmHtml = buildDisassembly(payload, loadAddr, codeOffset, 200);

  return page(
    filename,
    summary +
    barHtml +
    `<h2>Hex Dump</h2>` + hexHtml +
    `<h2>Disassembly</h2>` + disasmHtml
  );
}

function detectBasicStub(
  payload: Uint8Array,
  loadAddr: number
): { end: number; sysAddr: number } {
  // BASIC line format: [next-ptr-lo] [next-ptr-hi] [line-num-lo] [line-num-hi] [tokens...] [0x00]
  // SYS token = 0x9E, followed by ASCII digits
  let sysAddr = 0;
  let end = 0;

  let i = 0;
  while (i < payload.length - 4) {
    const nextPtr = payload[i] | (payload[i + 1] << 8);
    if (nextPtr === 0) {
      end = i + 2;
      break;
    }

    // Scan this line for SYS token (0x9E)
    let j = i + 4; // skip next-ptr and line-num
    while (j < payload.length && payload[j] !== 0) {
      if (payload[j] === 0x9E) {
        // Next bytes are ASCII digits (possibly with leading space)
        j++;
        while (j < payload.length && payload[j] === 0x20) j++;
        let numStr = "";
        while (j < payload.length && payload[j] >= 0x30 && payload[j] <= 0x39) {
          numStr += String.fromCharCode(payload[j]);
          j++;
        }
        if (numStr) sysAddr = parseInt(numStr, 10);
        break;
      }
      j++;
    }

    // Advance to next line
    const offset = nextPtr - loadAddr;
    if (offset <= i || offset >= payload.length) {
      end = i;
      break;
    }
    i = offset;
  }

  return { end, sysAddr };
}

function buildMemoryBar(
  loadAddr: number,
  payloadLen: number,
  basicEnd: number,
  sysAddr: number
): string {
  const total = 0xA000 - 0x0800;
  const start = loadAddr - 0x0800;
  const barWidth = 600;

  const toX = (addr: number) => Math.round(((addr - 0x0800) / total) * barWidth);

  const codeStart = sysAddr > 0 ? sysAddr : loadAddr;
  const codeEnd = loadAddr + payloadLen;

  let bars = "";

  // BASIC stub
  if (basicEnd > 0) {
    const x1 = toX(loadAddr);
    const x2 = toX(loadAddr + basicEnd);
    bars += `<div class="bar-seg basic" style="left:${x1}px;width:${x2 - x1}px" title="BASIC stub $${hex16(loadAddr)}-$${hex16(loadAddr + basicEnd)}"></div>`;
  }

  // Code
  const cx1 = toX(codeStart);
  const cx2 = toX(codeEnd);
  bars += `<div class="bar-seg code" style="left:${cx1}px;width:${cx2 - cx1}px" title="Code $${hex16(codeStart)}-$${hex16(codeEnd)}"></div>`;

  return `
    <div class="membar-container">
      <div class="membar" style="width:${barWidth}px">
        ${bars}
      </div>
      <div class="membar-labels">
        <span>$0800</span>
        <span>$2000</span>
        <span>$4000</span>
        <span>$6000</span>
        <span>$8000</span>
        <span>$A000</span>
      </div>
      <div class="membar-legend">
        <span class="legend-item"><span class="swatch basic"></span>BASIC stub</span>
        <span class="legend-item"><span class="swatch code"></span>Code + data</span>
        <span class="legend-item"><span class="swatch free"></span>Free</span>
      </div>
    </div>`;
}

function buildHexDump(
  payload: Uint8Array,
  loadAddr: number,
  basicEnd: number,
  sysAddr: number
): string {
  const bytesPerRow = 16;
  const maxRows = 64; // Show first 1KB
  let html = `<div class="hex-dump"><pre>`;

  const rows = Math.min(Math.ceil(payload.length / bytesPerRow), maxRows);

  for (let row = 0; row < rows; row++) {
    const offset = row * bytesPerRow;
    const addr = loadAddr + offset;
    let hexPart = "";
    let asciiPart = "";

    for (let col = 0; col < bytesPerRow; col++) {
      const idx = offset + col;
      if (idx < payload.length) {
        const b = payload[idx];
        const absAddr = loadAddr + idx;

        let cls = "";
        if (basicEnd > 0 && idx < basicEnd) cls = "basic";
        else if (sysAddr > 0 && absAddr >= sysAddr) cls = "code";

        hexPart += `<span class="${cls}">${hex8(b)}</span> `;
        asciiPart += printable(b);
      } else {
        hexPart += "   ";
        asciiPart += " ";
      }
    }

    html += `<span class="addr">${hex16(addr)}</span>  ${hexPart} <span class="ascii">${asciiPart}</span>\n`;
  }

  if (payload.length > maxRows * bytesPerRow) {
    html += `\n... ${payload.length - maxRows * bytesPerRow} more bytes ...\n`;
  }

  html += `</pre></div>`;
  return html;
}

// 6510 instruction table: [mnemonic, addressing mode, byte count]
type AddrMode = "imp" | "imm" | "zp" | "zpx" | "zpy" | "abs" | "abx" | "aby" | "ind" | "izx" | "izy" | "rel" | "acc";

const OPCODES: Record<number, [string, AddrMode, number]> = {
  0x00: ["BRK", "imp", 1], 0x01: ["ORA", "izx", 2], 0x05: ["ORA", "zp", 2],
  0x06: ["ASL", "zp", 2], 0x08: ["PHP", "imp", 1], 0x09: ["ORA", "imm", 2],
  0x0A: ["ASL", "acc", 1], 0x0D: ["ORA", "abs", 3], 0x0E: ["ASL", "abs", 3],
  0x10: ["BPL", "rel", 2], 0x11: ["ORA", "izy", 2], 0x15: ["ORA", "zpx", 2],
  0x16: ["ASL", "zpx", 2], 0x18: ["CLC", "imp", 1], 0x19: ["ORA", "aby", 3],
  0x1D: ["ORA", "abx", 3], 0x1E: ["ASL", "abx", 3],
  0x20: ["JSR", "abs", 3], 0x21: ["AND", "izx", 2], 0x24: ["BIT", "zp", 2],
  0x25: ["AND", "zp", 2], 0x26: ["ROL", "zp", 2], 0x28: ["PLP", "imp", 1],
  0x29: ["AND", "imm", 2], 0x2A: ["ROL", "acc", 1], 0x2C: ["BIT", "abs", 3],
  0x2D: ["AND", "abs", 3], 0x2E: ["ROL", "abs", 3],
  0x30: ["BMI", "rel", 2], 0x31: ["AND", "izy", 2], 0x35: ["AND", "zpx", 2],
  0x36: ["ROL", "zpx", 2], 0x38: ["SEC", "imp", 1], 0x39: ["AND", "aby", 3],
  0x3D: ["AND", "abx", 3], 0x3E: ["ROL", "abx", 3],
  0x40: ["RTI", "imp", 1], 0x41: ["EOR", "izx", 2], 0x45: ["EOR", "zp", 2],
  0x46: ["LSR", "zp", 2], 0x48: ["PHA", "imp", 1], 0x49: ["EOR", "imm", 2],
  0x4A: ["LSR", "acc", 1], 0x4C: ["JMP", "abs", 3], 0x4D: ["EOR", "abs", 3],
  0x4E: ["LSR", "abs", 3],
  0x50: ["BVC", "rel", 2], 0x51: ["EOR", "izy", 2], 0x55: ["EOR", "zpx", 2],
  0x56: ["LSR", "zpx", 2], 0x58: ["CLI", "imp", 1], 0x59: ["EOR", "aby", 3],
  0x5D: ["EOR", "abx", 3], 0x5E: ["LSR", "abx", 3],
  0x60: ["RTS", "imp", 1], 0x61: ["ADC", "izx", 2], 0x65: ["ADC", "zp", 2],
  0x66: ["ROR", "zp", 2], 0x68: ["PLA", "imp", 1], 0x69: ["ADC", "imm", 2],
  0x6A: ["ROR", "acc", 1], 0x6C: ["JMP", "ind", 3], 0x6D: ["ADC", "abs", 3],
  0x6E: ["ROR", "abs", 3],
  0x70: ["BVS", "rel", 2], 0x71: ["ADC", "izy", 2], 0x75: ["ADC", "zpx", 2],
  0x76: ["ROR", "zpx", 2], 0x78: ["SEI", "imp", 1], 0x79: ["ADC", "aby", 3],
  0x7D: ["ADC", "abx", 3], 0x7E: ["ROR", "abx", 3],
  0x81: ["STA", "izx", 2], 0x84: ["STY", "zp", 2], 0x85: ["STA", "zp", 2],
  0x86: ["STX", "zp", 2], 0x88: ["DEY", "imp", 1], 0x8A: ["TXA", "imp", 1],
  0x8C: ["STY", "abs", 3], 0x8D: ["STA", "abs", 3], 0x8E: ["STX", "abs", 3],
  0x90: ["BCC", "rel", 2], 0x91: ["STA", "izy", 2], 0x94: ["STY", "zpx", 2],
  0x95: ["STA", "zpx", 2], 0x96: ["STX", "zpy", 2], 0x98: ["TYA", "imp", 1],
  0x99: ["STA", "aby", 3], 0x9A: ["TXS", "imp", 1], 0x9D: ["STA", "abx", 3],
  0xA0: ["LDY", "imm", 2], 0xA1: ["LDA", "izx", 2], 0xA2: ["LDX", "imm", 2],
  0xA4: ["LDY", "zp", 2], 0xA5: ["LDA", "zp", 2], 0xA6: ["LDX", "zp", 2],
  0xA8: ["TAY", "imp", 1], 0xA9: ["LDA", "imm", 2], 0xAA: ["TAX", "imp", 1],
  0xAC: ["LDY", "abs", 3], 0xAD: ["LDA", "abs", 3], 0xAE: ["LDX", "abs", 3],
  0xB0: ["BCS", "rel", 2], 0xB1: ["LDA", "izy", 2], 0xB4: ["LDY", "zpx", 2],
  0xB5: ["LDA", "zpx", 2], 0xB6: ["LDX", "zpy", 2], 0xB8: ["CLV", "imp", 1],
  0xB9: ["LDA", "aby", 3], 0xBA: ["TSX", "imp", 1], 0xBC: ["LDY", "abx", 3],
  0xBD: ["LDA", "abx", 3], 0xBE: ["LDX", "aby", 3],
  0xC0: ["CPY", "imm", 2], 0xC1: ["CMP", "izx", 2], 0xC4: ["CPY", "zp", 2],
  0xC5: ["CMP", "zp", 2], 0xC6: ["DEC", "zp", 2], 0xC8: ["INY", "imp", 1],
  0xC9: ["CMP", "imm", 2], 0xCA: ["DEX", "imp", 1], 0xCC: ["CPY", "abs", 3],
  0xCD: ["CMP", "abs", 3], 0xCE: ["DEC", "abs", 3],
  0xD0: ["BNE", "rel", 2], 0xD1: ["CMP", "izy", 2], 0xD5: ["CMP", "zpx", 2],
  0xD6: ["DEC", "zpx", 2], 0xD8: ["CLD", "imp", 1], 0xD9: ["CMP", "aby", 3],
  0xDD: ["CMP", "abx", 3], 0xDE: ["DEC", "abx", 3],
  0xE0: ["CPX", "imm", 2], 0xE1: ["SBC", "izx", 2], 0xE4: ["CPX", "zp", 2],
  0xE5: ["SBC", "zp", 2], 0xE6: ["INC", "zp", 2], 0xE8: ["INX", "imp", 1],
  0xE9: ["SBC", "imm", 2], 0xEA: ["NOP", "imp", 1], 0xEC: ["CPX", "abs", 3],
  0xED: ["SBC", "abs", 3], 0xEE: ["INC", "abs", 3],
  0xF0: ["BEQ", "rel", 2], 0xF1: ["SBC", "izy", 2], 0xF5: ["SBC", "zpx", 2],
  0xF6: ["INC", "zpx", 2], 0xF8: ["SED", "imp", 1], 0xF9: ["SBC", "aby", 3],
  0xFD: ["SBC", "abx", 3], 0xFE: ["INC", "abx", 3],
};

function buildDisassembly(
  payload: Uint8Array,
  loadAddr: number,
  codeOffset: number,
  maxLines: number
): string {
  let html = `<div class="disasm"><pre>`;
  let i = codeOffset;
  let lines = 0;

  while (i < payload.length && lines < maxLines) {
    const addr = loadAddr + i;
    const opcode = payload[i];
    const info = OPCODES[opcode];

    if (!info) {
      html += `<span class="addr">${hex16(addr)}</span>  <span class="bytes">${hex8(opcode)}</span>         <span class="mnemonic">.byte</span> $${hex8(opcode)}\n`;
      i++;
      lines++;
      continue;
    }

    const [mnemonic, mode, size] = info;

    // Read operand bytes
    const rawBytes = [opcode];
    for (let b = 1; b < size && i + b < payload.length; b++) {
      rawBytes.push(payload[i + b]);
    }

    const bytesStr = rawBytes.map(hex8).join(" ").padEnd(8);
    const operand = formatOperand(mode, payload, i, loadAddr, size);

    html += `<span class="addr">${hex16(addr)}</span>  <span class="bytes">${bytesStr}</span>  <span class="mnemonic">${mnemonic}</span> ${operand}\n`;

    i += size;
    lines++;

    // Stop after RTS/BRK/JMP unless we have more code
    if (mnemonic === "RTS" || mnemonic === "BRK") {
      if (i < payload.length && lines < maxLines) {
        html += `\n`;
      }
    }
  }

  if (i < payload.length) {
    html += `\n... ${payload.length - i} more bytes ...\n`;
  }

  html += `</pre></div>`;
  return html;
}

function formatOperand(
  mode: AddrMode,
  payload: Uint8Array,
  offset: number,
  loadAddr: number,
  size: number
): string {
  const lo = offset + 1 < payload.length ? payload[offset + 1] : 0;
  const hi = offset + 2 < payload.length ? payload[offset + 2] : 0;
  const addr16 = lo | (hi << 8);

  switch (mode) {
    case "imp": return "";
    case "acc": return "A";
    case "imm": return `#$${hex8(lo)}`;
    case "zp":  return `$${hex8(lo)}`;
    case "zpx": return `$${hex8(lo)},X`;
    case "zpy": return `$${hex8(lo)},Y`;
    case "abs": return `$${hex16(addr16)}`;
    case "abx": return `$${hex16(addr16)},X`;
    case "aby": return `$${hex16(addr16)},Y`;
    case "ind": return `($${hex16(addr16)})`;
    case "izx": return `($${hex8(lo)},X)`;
    case "izy": return `($${hex8(lo)}),Y`;
    case "rel": {
      const branchOffset = lo > 127 ? lo - 256 : lo;
      const target = loadAddr + offset + 2 + branchOffset;
      return `$${hex16(target)}`;
    }
    default: return "";
  }
}

function hex8(n: number): string {
  return n.toString(16).toUpperCase().padStart(2, "0");
}

function hex16(n: number): string {
  return n.toString(16).toUpperCase().padStart(4, "0");
}

function printable(b: number): string {
  if (b >= 0x20 && b < 0x7F) return String.fromCharCode(b);
  return ".";
}

function page(title: string, body: string): string {
  return `<!DOCTYPE html>
<html><head><style>
  body {
    background: #1e1e2e;
    color: #cdd6f4;
    font-family: 'SF Mono', 'Cascadia Code', 'Fira Code', monospace;
    font-size: 13px;
    padding: 20px;
    line-height: 1.5;
  }
  h1 { color: #89b4fa; font-size: 18px; margin: 0 0 16px 0; }
  h2 { color: #89b4fa; font-size: 14px; margin: 24px 0 8px 0; }
  .summary { margin-bottom: 16px; }
  .row { display: flex; gap: 12px; padding: 2px 0; }
  .label { color: #6c7086; min-width: 120px; }
  .value { color: #cdd6f4; }
  .error { color: #f38ba8; }

  .membar-container { margin: 16px 0; }
  .membar {
    height: 20px;
    background: #313244;
    border-radius: 4px;
    position: relative;
    overflow: hidden;
  }
  .bar-seg {
    position: absolute;
    top: 0;
    height: 100%;
  }
  .bar-seg.basic { background: #fab387; }
  .bar-seg.code { background: #89b4fa; }
  .membar-labels {
    display: flex;
    justify-content: space-between;
    color: #6c7086;
    font-size: 10px;
    margin-top: 2px;
  }
  .membar-legend {
    display: flex;
    gap: 16px;
    margin-top: 6px;
    font-size: 11px;
    color: #6c7086;
  }
  .legend-item { display: flex; align-items: center; gap: 4px; }
  .swatch {
    display: inline-block;
    width: 10px;
    height: 10px;
    border-radius: 2px;
  }
  .swatch.basic { background: #fab387; }
  .swatch.code { background: #89b4fa; }
  .swatch.free { background: #313244; }

  .hex-dump pre, .disasm pre {
    margin: 0;
    white-space: pre;
    line-height: 1.4;
  }
  .addr { color: #6c7086; }
  .ascii { color: #a6e3a1; }
  .basic { color: #fab387; }
  .code { color: #89b4fa; }
  .bytes { color: #6c7086; }
  .mnemonic { color: #cba6f7; font-weight: bold; }
</style></head>
<body>
  <h1>${title}</h1>
  ${body}
</body></html>`;
}
