import * as vscode from "vscode";
import { parseSymbols, CorkSymbol, CorkSymbolKind } from "./symbols";

const KIND_MAP: Record<CorkSymbolKind, vscode.SymbolKind> = {
  scene: vscode.SymbolKind.Class,
  struct: vscode.SymbolKind.Struct,
  enum: vscode.SymbolKind.Enum,
  enumMember: vscode.SymbolKind.EnumMember,
  method: vscode.SymbolKind.Method,
  variable: vscode.SymbolKind.Variable,
  constant: vscode.SymbolKind.Constant,
  field: vscode.SymbolKind.Field,
  sprite: vscode.SymbolKind.Object,
  lifecycle: vscode.SymbolKind.Event,
  import: vscode.SymbolKind.Module,
};

export class CorkDocumentSymbolProvider
  implements vscode.DocumentSymbolProvider
{
  provideDocumentSymbols(
    doc: vscode.TextDocument
  ): vscode.DocumentSymbol[] {
    const symbols = parseSymbols(doc);
    return symbols.map(toDocSymbol);
  }
}

function toDocSymbol(sym: CorkSymbol): vscode.DocumentSymbol {
  const ds = new vscode.DocumentSymbol(
    sym.name,
    sym.detail ?? "",
    KIND_MAP[sym.kind] ?? vscode.SymbolKind.Variable,
    sym.range,
    sym.nameRange
  );
  ds.children = sym.children.map(toDocSymbol);
  return ds;
}
