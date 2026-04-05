import * as vscode from "vscode";
import { CorkCompiler } from "./compiler";
import { CorkTaskProvider } from "./taskProvider";
import { CorkCompletionProvider } from "./completionProvider";
import { CorkHoverProvider } from "./hoverProvider";
import { CorkFormattingProvider } from "./formatProvider";
import { SpritePreviewProvider, SpriteHoverProvider, SpritePreviewPanelManager } from "./spritePreview";
import { registerViceCommands } from "./viceIntegration";

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
