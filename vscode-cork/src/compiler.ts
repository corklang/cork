import * as vscode from "vscode";
import { execFile } from "child_process";
import * as path from "path";
import * as fs from "fs";

const DIAG_RE = /^(?:Error|Warning):\s*(.+)\((\d+),(\d+)\):\s*(.+)$/;
const PLAIN_ERROR_RE = /^Error:\s*(.+)$/;
const RAM_RE = /RAM:\s*(\d+)\/(\d+)\s*bytes\s*used\s*\((\d+)%\)/;
const PEEPHOLE_RE = /Peephole:\s*(\d+)\s*bytes\s*removed/;

export class CorkCompiler {
  private diagnostics: vscode.DiagnosticCollection;
  private statusBar: vscode.StatusBarItem;
  private compiling = false;

  constructor(context: vscode.ExtensionContext) {
    this.diagnostics = vscode.languages.createDiagnosticCollection("cork");
    context.subscriptions.push(this.diagnostics);

    this.statusBar = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Left,
      100
    );
    this.statusBar.command = "cork.build";
    context.subscriptions.push(this.statusBar);
  }

  clearDiagnostics(uri: vscode.Uri) {
    this.diagnostics.delete(uri);
  }

  async compile(doc: vscode.TextDocument): Promise<void> {
    if (this.compiling) return;
    this.compiling = true;
    this.statusBar.text = "$(loading~spin) Cork: compiling...";
    this.statusBar.show();

    try {
      const { command, args } = this.getCompilerCommand(doc.uri.fsPath);
      const result = await this.exec(command, args, doc.uri.fsPath);

      this.diagnostics.delete(doc.uri);
      const diags = this.parseDiagnostics(result.stderr, doc.uri);
      if (diags.length > 0) {
        this.diagnostics.set(doc.uri, diags);
      }

      this.updateStatusBar(result.stdout, result.exitCode, diags.length);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      this.statusBar.text = "$(error) Cork: compiler not found";
      this.statusBar.tooltip = message;
      this.statusBar.show();
    } finally {
      this.compiling = false;
    }
  }

  private getCompilerCommand(filePath: string): {
    command: string;
    args: string[];
  } {
    const config = vscode.workspace.getConfiguration("cork");
    const compilerPath = config.get<string>("compilerPath", "");

    if (compilerPath) {
      return { command: compilerPath, args: [filePath] };
    }

    // Try to find the compiler project relative to the workspace or parent
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
            args: ["run", "--project", projectPath, "--", filePath],
          };
        }
      }
    }

    // Fall back to cork on PATH
    return { command: "cork", args: [filePath] };
  }

  private exec(
    command: string,
    args: string[],
    filePath: string
  ): Promise<{ stdout: string; stderr: string; exitCode: number }> {
    return new Promise((resolve, reject) => {
      const cwd =
        vscode.workspace.getWorkspaceFolder(vscode.Uri.file(filePath))?.uri
          .fsPath ?? path.dirname(filePath);

      execFile(command, args, { cwd, timeout: 30000 }, (err, stdout, stderr) => {
        if (err && (err as NodeJS.ErrnoException).code === "ENOENT") {
          reject(
            new Error(
              `Compiler not found: ${command}. Set cork.compilerPath in settings.`
            )
          );
          return;
        }
        resolve({
          stdout: stdout ?? "",
          stderr: stderr ?? "",
          exitCode: err ? (err as { code?: number }).code ?? 1 : 0,
        });
      });
    });
  }

  private parseDiagnostics(
    stderr: string,
    docUri: vscode.Uri
  ): vscode.Diagnostic[] {
    const diags: vscode.Diagnostic[] = [];

    for (const line of stderr.split("\n")) {
      const trimmed = line.trim();
      if (!trimmed) continue;

      const match = DIAG_RE.exec(trimmed);
      if (match) {
        const lineNum = Math.max(0, parseInt(match[2], 10) - 1);
        const col = Math.max(0, parseInt(match[3], 10) - 1);
        const message = match[4];
        const severity = trimmed.startsWith("Warning")
          ? vscode.DiagnosticSeverity.Warning
          : vscode.DiagnosticSeverity.Error;

        const range = new vscode.Range(lineNum, col, lineNum, col + 1);
        const diag = new vscode.Diagnostic(range, message, severity);
        diag.source = "cork";
        diags.push(diag);
        continue;
      }

      // Plain "Error: message" without location
      const plainMatch = PLAIN_ERROR_RE.exec(trimmed);
      if (plainMatch) {
        const range = new vscode.Range(0, 0, 0, 0);
        const diag = new vscode.Diagnostic(
          range,
          plainMatch[1],
          vscode.DiagnosticSeverity.Error
        );
        diag.source = "cork";
        diags.push(diag);
      }
    }

    return diags;
  }

  private updateStatusBar(
    stdout: string,
    exitCode: number,
    errorCount: number
  ) {
    if (exitCode !== 0 || errorCount > 0) {
      this.statusBar.text = `$(error) Cork: ${errorCount} error${errorCount !== 1 ? "s" : ""}`;
      this.statusBar.tooltip = "Click to rebuild";
      this.statusBar.show();
      return;
    }

    // Parse memory usage from stdout
    const ramMatch = RAM_RE.exec(stdout);
    const peepMatch = PEEPHOLE_RE.exec(stdout);

    if (ramMatch) {
      const used = ramMatch[1];
      const total = ramMatch[2];
      const pct = ramMatch[3];
      let text = `Cork: ${used}/${total} bytes (${pct}%)`;
      if (peepMatch) {
        text += ` | Peephole: ${peepMatch[1]} bytes saved`;
      }
      this.statusBar.text = `$(check) ${text}`;
      this.statusBar.tooltip = "Build succeeded. Click to rebuild.";
    } else {
      this.statusBar.text = "$(check) Cork: build succeeded";
      this.statusBar.tooltip = "Click to rebuild";
    }
    this.statusBar.show();
  }

  dispose() {
    this.diagnostics.dispose();
    this.statusBar.dispose();
  }
}
