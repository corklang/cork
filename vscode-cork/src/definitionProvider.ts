import * as vscode from "vscode";
import * as path from "path";
import { parseSymbols, flattenSymbols } from "./symbols";

export class CorkDefinitionProvider implements vscode.DefinitionProvider {
  provideDefinition(
    doc: vscode.TextDocument,
    position: vscode.Position
  ): vscode.Definition | undefined {
    const wordRange = doc.getWordRangeAtPosition(position, /[\w.]+/);
    if (!wordRange) return undefined;
    const word = doc.getText(wordRange);
    const line = doc.lineAt(position.line).text;

    // import "path" — open the file
    const importMatch = /import\s+"([^"]+)"/.exec(line);
    if (importMatch && wordRange.start.character >= line.indexOf('"')) {
      const importPath = importMatch[1];
      const resolved = path.resolve(path.dirname(doc.uri.fsPath), importPath);
      return new vscode.Location(vscode.Uri.file(resolved), new vscode.Position(0, 0));
    }

    // go SceneName — jump to scene
    const goMatch = /\bgo\s+(\w+)/.exec(line);
    if (goMatch && word === goMatch[1]) {
      return this.findSymbol(doc, goMatch[1], "scene");
    }

    // Enum.member — jump to enum
    const enumDotMatch = word.match(/^(\w+)\.(\w+)$/);
    if (enumDotMatch) {
      const loc = this.findSymbol(doc, enumDotMatch[1], "enum");
      if (loc) return loc;
      // Could also be Color.xxx — no definition for builtins
    }

    // Method call — name: or (receiver name:)
    // Check if the word is followed by : on the line
    const afterWord = line.substring(wordRange.end.character);
    if (afterWord.startsWith(":")) {
      return this.findSymbol(doc, word + ":", "method");
    }

    // General: find any symbol with this name
    return this.findAnySymbol(doc, word);
  }

  private findSymbol(
    doc: vscode.TextDocument,
    name: string,
    kind: string
  ): vscode.Location | undefined {
    const symbols = flattenSymbols(parseSymbols(doc));
    const sym = symbols.find((s) => s.name === name && s.kind === kind);
    if (sym) {
      return new vscode.Location(doc.uri, sym.nameRange);
    }
    // Search imported files
    return this.searchImports(doc, name, kind);
  }

  private findAnySymbol(
    doc: vscode.TextDocument,
    name: string
  ): vscode.Location | undefined {
    const symbols = flattenSymbols(parseSymbols(doc));
    const sym = symbols.find((s) => s.name === name || s.name === name + ":");
    if (sym) {
      return new vscode.Location(doc.uri, sym.nameRange);
    }
    return this.searchImports(doc, name, undefined);
  }

  private searchImports(
    doc: vscode.TextDocument,
    name: string,
    kind: string | undefined
  ): vscode.Location | undefined {
    const text = doc.getText();
    const importRe = /import\s+"([^"]+)"\s*;/g;
    let m;
    while ((m = importRe.exec(text)) !== null) {
      const importPath = path.resolve(path.dirname(doc.uri.fsPath), m[1]);
      try {
        const importUri = vscode.Uri.file(importPath);
        const importDoc = vscode.workspace.textDocuments.find(
          (d) => d.uri.fsPath === importUri.fsPath
        );
        if (!importDoc) continue;

        const symbols = flattenSymbols(parseSymbols(importDoc));
        const sym = kind
          ? symbols.find((s) => s.name === name && s.kind === kind)
          : symbols.find((s) => s.name === name || s.name === name + ":");
        if (sym) {
          return new vscode.Location(importDoc.uri, sym.nameRange);
        }
      } catch {
        // file not open or doesn't exist
      }
    }
    return undefined;
  }
}
