import * as vscode from "vscode";
import { CorkCompiler } from "./compiler";
import { CorkTaskProvider } from "./taskProvider";

let compiler: CorkCompiler;
let taskProvider: vscode.Disposable;

export function activate(context: vscode.ExtensionContext) {
  compiler = new CorkCompiler(context);

  taskProvider = vscode.tasks.registerTaskProvider("cork", new CorkTaskProvider());
  context.subscriptions.push(taskProvider);

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
