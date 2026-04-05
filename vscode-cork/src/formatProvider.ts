import * as vscode from "vscode";

export class CorkFormattingProvider
  implements vscode.DocumentFormattingEditProvider
{
  provideDocumentFormattingEdits(
    doc: vscode.TextDocument,
    options: vscode.FormattingOptions
  ): vscode.TextEdit[] {
    console.log("[Cork] Format requested for", doc.uri.fsPath);
    try {
      const indent = options.insertSpaces ? " ".repeat(options.tabSize) : "\t";
      const lines = doc.getText().split("\n");
      const formatted = formatLines(lines, indent);
      console.log("[Cork] Formatted", lines.length, "lines, changed:", formatted !== doc.getText());

      const fullRange = new vscode.Range(
        new vscode.Position(0, 0),
        doc.lineAt(doc.lineCount - 1).range.end
      );
      return [vscode.TextEdit.replace(fullRange, formatted)];
    } catch (err) {
      console.error("[Cork] Format error:", err);
      return [];
    }
  }
}

function formatLines(lines: string[], indent: string): string {
  const result: string[] = [];
  let depth = 0;
  let inSpritePattern = false;
  let consecutiveBlanks = 0;

  for (let i = 0; i < lines.length; i++) {
    const raw = lines[i];
    const trimmed = raw.trim();

    // Track backtick sprite patterns — don't reformat inside them
    if (inSpritePattern) {
      // Check if this line closes the pattern
      if (trimmed.includes("`")) {
        inSpritePattern = false;
        result.push(indent.repeat(depth) + trimmed.replace(/`\s*;/, "`;"));
      } else {
        // Sprite pattern content gets one extra indent level
        result.push(indent.repeat(depth + 1) + trimmed);
      }
      consecutiveBlanks = 0;
      continue;
    }

    // Check if a sprite pattern opens on this line (and doesn't close)
    const backtickCount = (trimmed.match(/`/g) || []).length;
    if (backtickCount === 1 && trimmed.endsWith("`")) {
      // Opening backtick at end of line, e.g. `data: \``
      inSpritePattern = true;
      const formatted = formatCodeLine(trimmed);
      result.push(indent.repeat(depth) + formatted);
      depth += countBraces(formatted).opens - countBraces(formatted).closes;
      consecutiveBlanks = 0;
      continue;
    }

    // Blank line handling: max 1 consecutive, none at start of blocks
    if (trimmed === "") {
      consecutiveBlanks++;
      if (consecutiveBlanks <= 1 && result.length > 0) {
        // Don't add blank line right after an opening brace
        const prev = result[result.length - 1].trim();
        if (prev.endsWith("{")) continue;
        result.push("");
      }
      continue;
    }
    consecutiveBlanks = 0;

    const formatted = formatCodeLine(trimmed);
    const { opens, closes } = countBraces(formatted);

    // For lines that start with }, dedent before printing.
    // Lines like "} else {" (starts with close) dedent first, then re-indent after.
    // Inline blocks like "if (x) { break; }" don't start with } so no dedent.
    const leadingCloses = formatted.startsWith("}") ? closes : 0;
    depth = Math.max(0, depth - leadingCloses);
    result.push(indent.repeat(depth) + formatted);
    // Net change for next line: opens minus any non-leading closes are balanced
    depth += opens - (closes - leadingCloses);
  }

  // Remove trailing blank lines, ensure single newline at end
  while (result.length > 0 && result[result.length - 1].trim() === "") {
    result.pop();
  }

  return result.join("\n") + "\n";
}

function countBraces(line: string): { opens: number; closes: number } {
  let opens = 0;
  let closes = 0;
  let inString = false;
  let escape = false;
  for (const ch of line) {
    if (escape) {
      escape = false;
      continue;
    }
    if (ch === "\\") {
      escape = true;
      continue;
    }
    if (ch === '"') {
      inString = !inString;
      continue;
    }
    if (inString) continue;
    if (ch === "{") opens++;
    else if (ch === "}") closes++;
  }
  return { opens, closes };
}

function formatCodeLine(line: string): string {
  // Don't touch full-line comments
  if (line.startsWith("//")) return line;

  // Split line into code segments and string/comment literals,
  // so we only normalize spacing in code.
  const segments: { text: string; isCode: boolean }[] = [];
  let current = "";
  let inString = false;
  let escape = false;

  for (let i = 0; i < line.length; i++) {
    const ch = line[i];

    if (escape) {
      current += ch;
      escape = false;
      continue;
    }
    if (ch === "\\") {
      escape = true;
      current += ch;
      continue;
    }

    // Line comment — rest of line is non-code
    if (!inString && ch === "/" && i + 1 < line.length && line[i + 1] === "/") {
      if (current) segments.push({ text: current, isCode: true });
      segments.push({ text: line.substring(i), isCode: false });
      current = "";
      break;
    }

    if (ch === '"') {
      if (!inString) {
        // Start of string — flush code segment
        if (current) segments.push({ text: current, isCode: true });
        current = '"';
        inString = true;
      } else {
        // End of string
        current += '"';
        segments.push({ text: current, isCode: false });
        current = "";
        inString = false;
      }
      continue;
    }

    current += ch;
  }
  if (current) segments.push({ text: current, isCode: !inString });

  // Normalize spacing in code segments
  const formatted = segments
    .map((seg) => (seg.isCode ? normalizeCodeSpacing(seg.text) : seg.text))
    .join("");

  return formatted.trimEnd();
}

function normalizeCodeSpacing(code: string): string {
  let s = code;

  // Collapse multiple spaces to one
  s = s.replace(/  +/g, " ");

  // No space before ; or ,
  s = s.replace(/\s+;/g, ";");
  s = s.replace(/\s+,/g, ",");

  // Single space after ; and , (but not at end of string)
  s = s.replace(/;(?=\S)/g, "; ");
  s = s.replace(/,(?=\S)/g, ", ");

  // Colon in method calls / hardware settings: exactly one space after
  // Match word: followed by spaces — normalize to word: single-space
  s = s.replace(/(\w):\s{2,}/g, "$1: ");

  // No space before :
  s = s.replace(/\s+:/g, ":");

  // No space after ( or before )
  s = s.replace(/\(\s+/g, "(");
  s = s.replace(/\s+\)/g, ")");

  // No space after [ or before ]
  s = s.replace(/\[\s+/g, "[");
  s = s.replace(/\s+\]/g, "]");

  // Space after { and before } (for inline blocks like `{ player.x += 1; }`)
  // But not for empty {} and not for standalone braces
  s = s.replace(/\{(?!\s|\})/g, "{ ");
  s = s.replace(/(\S)\}/g, "$1 }");

  // Normalize operators — order matters: longest first, comparisons before assignment

  // Shift operators and shift-assign (must come before < > and =)
  s = s.replace(/\s*(<<=|>>=)\s*/g, " $1 ");
  s = s.replace(/\s*(<<|>>)\s*/g, " $1 ");

  // Comparison operators
  s = s.replace(/\s*(==|!=|<=|>=)\s*/g, " $1 ");
  // Standalone < > (not part of << >> <= >=)
  s = s.replace(/(?<!<)\s*<\s*(?!<|=)/g, " < ");
  s = s.replace(/(?<!>)\s*>\s*(?!>|=)/g, " > ");

  // Compound assignment operators
  s = s.replace(/\s*(\+=|-=|\*=|\/=|%=|&=|\|=|\^=)\s*/g, " $1 ");

  // Plain = (not ==, !=, <=, >=, +=, -=, *=, /=, %=, &=, |=, ^=, <<=, >>=)
  s = s.replace(/(?<![=!<>+\-*/%&|^])\s*=\s*(?!=)/g, " = ");

  // Collapse any double-spaces introduced by the above
  s = s.replace(/  +/g, " ");

  return s;
}
