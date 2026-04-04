import * as vscode from "vscode";
import * as path from "path";
import * as fs from "fs";

interface CorkTaskDefinition extends vscode.TaskDefinition {
  file: string;
  output?: string;
}

export class CorkTaskProvider implements vscode.TaskProvider {
  provideTasks(): vscode.Task[] {
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.document.languageId !== "cork") {
      return [];
    }
    return [this.createBuildTask(editor.document.uri.fsPath)];
  }

  resolveTask(task: vscode.Task): vscode.Task | undefined {
    const def = task.definition as CorkTaskDefinition;
    if (def.file) {
      return this.createBuildTask(def.file, def.output);
    }
    return undefined;
  }

  private createBuildTask(filePath: string, output?: string): vscode.Task {
    const outPath =
      output ?? path.join(
        path.dirname(filePath),
        path.basename(filePath, ".cork") + ".prg"
      );

    const { command, args } = this.getCompilerCommand(filePath, outPath);
    const shellCmd = [command, ...args]
      .map((a) => (a.includes(" ") ? `"${a}"` : a))
      .join(" ");

    const def: CorkTaskDefinition = { type: "cork", file: filePath, output: outPath };
    const execution = new vscode.ShellExecution(shellCmd);

    const task = new vscode.Task(
      def,
      vscode.TaskScope.Workspace,
      "Build",
      "cork",
      execution,
      "$cork"
    );
    task.group = vscode.TaskGroup.Build;
    return task;
  }

  private getCompilerCommand(
    filePath: string,
    outPath: string
  ): { command: string; args: string[] } {
    const config = vscode.workspace.getConfiguration("cork");
    const compilerPath = config.get<string>("compilerPath", "");

    if (compilerPath) {
      return { command: compilerPath, args: [filePath, "-o", outPath] };
    }

    const workspaceFolder = vscode.workspace.getWorkspaceFolder(
      vscode.Uri.file(filePath)
    );
    if (workspaceFolder) {
      const candidates = [
        path.join(workspaceFolder.uri.fsPath, "src", "Cork.Compiler"),
        path.join(workspaceFolder.uri.fsPath, "..", "src", "Cork.Compiler"),
      ];
      for (const projectPath of candidates) {
        if (fs.existsSync(projectPath)) {
          return {
            command: "dotnet",
            args: ["run", "--project", projectPath, "--", filePath, "-o", outPath],
          };
        }
      }
    }

    return { command: "cork", args: [filePath, "-o", outPath] };
  }
}
