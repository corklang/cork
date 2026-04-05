import * as vscode from "vscode";
import { parseSymbols, flattenSymbols } from "./symbols";

export class CorkRenameProvider implements vscode.RenameProvider {
  prepareRename(
    doc: vscode.TextDocument,
    position: vscode.Position
  ): vscode.Range | { range: vscode.Range; placeholder: string } | undefined {
    const wordRange = doc.getWordRangeAtPosition(position, /\w+/);
    if (!wordRange) return undefined;

    const word = doc.getText(wordRange);

    // Don't rename keywords, built-in types, or Color constants
    const reserved = new Set([
      "byte", "sbyte", "word", "sword", "fixed", "sfixed", "bool", "string",
      "var", "const", "if", "else", "while", "for", "in", "switch", "case",
      "default", "break", "continue", "return", "go", "entry", "scene",
      "struct", "enum", "flags", "import", "hardware", "enter", "frame",
      "exit", "raster", "sprite", "as", "true", "false", "Color",
      "joystick", "port1", "port2", "left", "right", "up", "down", "fire",
      "fallthrough", "relaxed",
    ]);

    if (reserved.has(word)) return undefined;

    return { range: wordRange, placeholder: word };
  }

  provideRenameEdits(
    doc: vscode.TextDocument,
    position: vscode.Position,
    newName: string
  ): vscode.WorkspaceEdit | undefined {
    const wordRange = doc.getWordRangeAtPosition(position, /\w+/);
    if (!wordRange) return undefined;
    const oldName = doc.getText(wordRange);

    const edit = new vscode.WorkspaceEdit();
    const text = doc.getText();

    const re = new RegExp(`\\b${escapeRegex(oldName)}\\b`, "g");
    let match;
    while ((match = re.exec(text)) !== null) {
      const pos = doc.positionAt(match.index);
      const range = new vscode.Range(pos, doc.positionAt(match.index + oldName.length));
      edit.replace(doc.uri, range, newName);
    }

    return edit;
  }
}

function escapeRegex(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
