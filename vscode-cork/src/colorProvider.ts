import * as vscode from "vscode";

// C64 palette as normalized RGBA (0-1)
const C64_COLORS: Record<string, [number, number, number]> = {
  black:      [0x00 / 255, 0x00 / 255, 0x00 / 255],
  white:      [0xFF / 255, 0xFF / 255, 0xFF / 255],
  red:        [0x89 / 255, 0x40 / 255, 0x36 / 255],
  cyan:       [0x7A / 255, 0xBF / 255, 0xC7 / 255],
  purple:     [0x8A / 255, 0x46 / 255, 0xAE / 255],
  green:      [0x68 / 255, 0xA9 / 255, 0x41 / 255],
  blue:       [0x3E / 255, 0x31 / 255, 0xA2 / 255],
  yellow:     [0xD0 / 255, 0xDC / 255, 0x71 / 255],
  orange:     [0x90 / 255, 0x5F / 255, 0x25 / 255],
  brown:      [0x5C / 255, 0x47 / 255, 0x00 / 255],
  lightRed:   [0xBB / 255, 0x77 / 255, 0x6D / 255],
  darkGrey:   [0x55 / 255, 0x55 / 255, 0x55 / 255],
  grey:       [0x80 / 255, 0x80 / 255, 0x80 / 255],
  lightGreen: [0xAA / 255, 0xFF / 255, 0x66 / 255],
  lightBlue:  [0x7C / 255, 0x70 / 255, 0xDA / 255],
  lightGrey:  [0xAB / 255, 0xAB / 255, 0xAB / 255],
};

const COLOR_NAMES = Object.keys(C64_COLORS);
const COLOR_RE = /\bColor\.(black|white|red|cyan|purple|green|blue|yellow|orange|brown|lightRed|darkGrey|grey|lightGreen|lightBlue|lightGrey)\b/g;

export class CorkColorProvider implements vscode.DocumentColorProvider {
  provideDocumentColors(doc: vscode.TextDocument): vscode.ColorInformation[] {
    const colors: vscode.ColorInformation[] = [];
    const text = doc.getText();

    let match;
    while ((match = COLOR_RE.exec(text)) !== null) {
      const name = match[1];
      const rgb = C64_COLORS[name];
      if (!rgb) continue;

      const start = doc.positionAt(match.index);
      const end = doc.positionAt(match.index + match[0].length);
      const range = new vscode.Range(start, end);
      const color = new vscode.Color(rgb[0], rgb[1], rgb[2], 1);
      colors.push(new vscode.ColorInformation(range, color));
    }

    // Reset regex lastIndex
    COLOR_RE.lastIndex = 0;
    return colors;
  }

  provideColorPresentations(
    color: vscode.Color,
    context: { document: vscode.TextDocument; range: vscode.Range }
  ): vscode.ColorPresentation[] {
    // Find the closest C64 color
    const name = closestC64Color(color.red, color.green, color.blue);
    return [new vscode.ColorPresentation(`Color.${name}`)];
  }
}

function closestC64Color(r: number, g: number, b: number): string {
  let bestName = "black";
  let bestDist = Infinity;

  for (const name of COLOR_NAMES) {
    const [cr, cg, cb] = C64_COLORS[name];
    const dist = (r - cr) ** 2 + (g - cg) ** 2 + (b - cb) ** 2;
    if (dist < bestDist) {
      bestDist = dist;
      bestName = name;
    }
  }

  return bestName;
}
