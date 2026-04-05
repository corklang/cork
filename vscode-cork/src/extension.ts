import * as vscode from "vscode";
import { CorkCompiler } from "./compiler";
import { CorkTaskProvider } from "./taskProvider";
import { CorkCompletionProvider } from "./completionProvider";
import { CorkHoverProvider } from "./hoverProvider";
import { CorkFormattingProvider } from "./formatProvider";
import { SpritePreviewProvider, SpriteHoverProvider, SpritePreviewPanelManager } from "./spritePreview";
import { registerViceCommands } from "./viceIntegration";
import { CorkDocumentSymbolProvider } from "./outlineProvider";
import { CorkDefinitionProvider } from "./definitionProvider";
import { CorkReferenceProvider } from "./referenceProvider";
import { CorkRenameProvider } from "./renameProvider";
import { CorkColorProvider } from "./colorProvider";
import { PrgViewerProvider } from "./prgViewer";

const CORK_SELECTOR: vscode.DocumentSelector = { language: "cork", scheme: "file" };

let compiler: CorkCompiler;

export function activate(context: vscode.ExtensionContext) {
  compiler = new CorkCompiler(context);

  context.subscriptions.push(
    vscode.tasks.registerTaskProvider("cork", new CorkTaskProvider())
  );

  context.subscriptions.push(
    vscode.languages.registerCompletionItemProvider(
      CORK_SELECTOR,
      new CorkCompletionProvider(),
      ".", ":"
    )
  );

  context.subscriptions.push(
    vscode.languages.registerHoverProvider(CORK_SELECTOR, new CorkHoverProvider())
  );

  context.subscriptions.push(
    vscode.languages.registerDocumentFormattingEditProvider(
      CORK_SELECTOR,
      new CorkFormattingProvider()
    )
  );

  context.subscriptions.push(
    vscode.languages.registerCodeLensProvider(
      CORK_SELECTOR,
      new SpritePreviewProvider()
    )
  );

  const spritePanelManager = new SpritePreviewPanelManager();
  context.subscriptions.push({ dispose: () => spritePanelManager.dispose() });

  context.subscriptions.push(
    vscode.commands.registerCommand("cork.previewSprite", (docUri: string, startLine: number) => {
      spritePanelManager.open(docUri, startLine);
    })
  );

  context.subscriptions.push(
    vscode.languages.registerDocumentSymbolProvider(CORK_SELECTOR, new CorkDocumentSymbolProvider())
  );

  context.subscriptions.push(
    vscode.languages.registerDefinitionProvider(CORK_SELECTOR, new CorkDefinitionProvider())
  );

  context.subscriptions.push(
    vscode.languages.registerReferenceProvider(CORK_SELECTOR, new CorkReferenceProvider())
  );

  context.subscriptions.push(
    vscode.languages.registerRenameProvider(CORK_SELECTOR, new CorkRenameProvider())
  );

  context.subscriptions.push(
    vscode.languages.registerColorProvider(CORK_SELECTOR, new CorkColorProvider())
  );

  context.subscriptions.push(
    vscode.window.registerCustomEditorProvider(
      "cork.prgViewer",
      new PrgViewerProvider(),
      { supportsMultipleEditorsPerDocument: true }
    )
  );

  registerViceCommands(context);

  context.subscriptions.push(
    vscode.commands.registerCommand("cork.build", () => {
      const editor = vscode.window.activeTextEditor;
      if (editor && editor.document.languageId === "cork") {
        compiler.compile(editor.document);
      }
    })
  );

  context.subscriptions.push(
    vscode.workspace.onDidSaveTextDocument((doc) => {
      if (doc.languageId === "cork") {
        const config = vscode.workspace.getConfiguration("cork");
        if (config.get<boolean>("buildOnSave", true)) {
          compiler.compile(doc);
        }
      }
    })
  );

  context.subscriptions.push(
    vscode.workspace.onDidCloseTextDocument((doc) => {
      if (doc.languageId === "cork") {
        compiler.clearDiagnostics(doc.uri);
      }
    })
  );
}

export function deactivate() {
  compiler?.dispose();
}
