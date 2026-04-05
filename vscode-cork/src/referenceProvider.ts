import * as vscode from "vscode";
import { parseSymbols, flattenSymbols } from "./symbols";

export class CorkReferenceProvider implements vscode.ReferenceProvider {
  provideReferences(
    doc: vscode.TextDocument,
    position: vscode.Position,
    context: vscode.ReferenceContext
  ): vscode.Location[] {
    const wordRange = doc.getWordRangeAtPosition(position, /\w+/);
    if (!wordRange) return [];
    const word = doc.getText(wordRange);

    const locations: vscode.Location[] = [];
    const text = doc.getText();

    // Find all occurrences of the word as a whole word
    const re = new RegExp(`\\b${escapeRegex(word)}\\b`, "g");
    let match;
    while ((match = re.exec(text)) !== null) {
      const pos = doc.positionAt(match.index);
      const range = new vscode.Range(pos, doc.positionAt(match.index + word.length));

      // Include declaration if requested
      if (!context.includeDeclaration) {
        const symbols = flattenSymbols(parseSymbols(doc));
        const isDecl = symbols.some(
          (s) =>
            (s.name === word || s.name === word + ":") &&
            s.nameRange.contains(pos)
        );
        if (isDecl) continue;
      }

      locations.push(new vscode.Location(doc.uri, range));
    }

    return locations;
  }
}

function escapeRegex(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
