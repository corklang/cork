import * as vscode from "vscode";

export class CorkFormattingProvider
  implements vscode.DocumentFormattingEditProvider
{
  provideDocumentFormattingEdits(
    doc: vscode.TextDocument,
    options: vscode.FormattingOptions
  ): vscode.TextEdit[] {
    const indent = options.insertSpaces ? " ".repeat(options.tabSize) : "\t";
    const lines = doc.getText().split("\n");
    const formatted = formatLines(lines, indent);

    const fullRange = new vscode.Range(
      new vscode.Position(0, 0),
      doc.lineAt(doc.lineCount - 1).range.end
    );
    return [vscode.TextEdit.replace(fullRange, formatted)];
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
        // Indent the closing backtick line
        result.push(indent.repeat(depth) + trimmed);
      } else {
        // Preserve sprite pattern content, just re-indent
        result.push(indent.repeat(depth) + trimmed);
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
      depth += countBraceChange(formatted);
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

    // Closing brace decreases indent before the line
    if (trimmed.startsWith("}")) {
      depth = Math.max(0, depth - 1);
    }

    const formatted = formatCodeLine(trimmed);
    result.push(indent.repeat(depth) + formatted);

    // Opening brace increases indent after the line
    depth += countBraceChange(formatted);
    // Correct for lines that both close and open, e.g. `} else {`
    // countBraceChange already handles this
  }

  // Remove trailing blank lines, ensure single newline at end
  while (result.length > 0 && result[result.length - 1].trim() === "") {
    result.pop();
  }

  return result.join("\n") + "\n";
}

function countBraceChange(line: string): number {
  let change = 0;
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
    if (ch === "{") change++;
    else if (ch === "}") change--;
  }
  return change;
}

function formatCodeLine(line: string): string {
  // Don't touch comments
  if (line.startsWith("//")) return line;

  let result = "";
  let inString = false;
  let escape = false;
  let i = 0;

  while (i < line.length) {
    const ch = line[i];

    // Handle escape sequences in strings
    if (escape) {
      result += ch;
      escape = false;
      i++;
      continue;
    }
    if (ch === "\\") {
      escape = true;
      result += ch;
      i++;
      continue;
    }

    // Toggle string state
    if (ch === '"') {
      inString = !inString;
      result += ch;
      i++;
      continue;
    }

    // Pass through string content unchanged
    if (inString) {
      result += ch;
      i++;
      continue;
    }

    // Handle // line comments — pass the rest through
    if (ch === "/" && i + 1 < line.length && line[i + 1] === "/") {
      // Ensure space before comment if not at start
      if (result.length > 0 && !result.endsWith(" ")) {
        result += " ";
      }
      result += line.substring(i);
      break;
    }

    // Normalize spacing: no space before semicolons
    if (ch === ";") {
      result = result.trimEnd();
      result += ";";
      i++;
      continue;
    }

    result += ch;
    i++;
  }

  return result.trimEnd();
}
