import * as vscode from "vscode";
import { execFile, spawn } from "child_process";
import * as path from "path";
import * as fs from "fs";

export function registerViceCommands(context: vscode.ExtensionContext) {
  context.subscriptions.push(
    vscode.commands.registerCommand("cork.runInVice", runInVice)
  );
}

async function runInVice() {
  const editor = vscode.window.activeTextEditor;
  if (!editor || editor.document.languageId !== "cork") {
    vscode.window.showWarningMessage("Open a .cork file first.");
    return;
  }

  const doc = editor.document;
  if (doc.isDirty) {
    await doc.save();
  }

  const config = vscode.workspace.getConfiguration("cork");
  const vicePath = config.get<string>("vicePath", "");
  const x64sc = findVice(vicePath);

  if (!x64sc) {
    const choice = await vscode.window.showErrorMessage(
      "VICE emulator (x64sc) not found. Set cork.vicePath in settings.",
      "Open Settings"
    );
    if (choice === "Open Settings") {
      vscode.commands.executeCommand(
        "workbench.action.openSettings",
        "cork.vicePath"
      );
    }
    return;
  }

  const filePath = doc.uri.fsPath;
  const prgPath = path.join("/tmp", path.basename(filePath, ".cork") + ".prg");

  // Compile first
  const { command, args } = getCompilerCommand(filePath, prgPath);

  vscode.window.withProgress(
    {
      location: vscode.ProgressLocation.Notification,
      title: "Cork: Building and launching in VICE...",
      cancellable: false,
    },
    async () => {
      try {
        await execPromise(command, args, path.dirname(filePath));
      } catch (err: unknown) {
        const message = err instanceof Error ? err.message : String(err);
        vscode.window.showErrorMessage(`Cork build failed: ${message}`);
        return;
      }

      if (!fs.existsSync(prgPath)) {
        vscode.window.showErrorMessage(`Build did not produce ${prgPath}`);
        return;
      }

      // Launch VICE detached
      const viceProc = spawn(x64sc, ["-autostart", prgPath], {
        detached: true,
        stdio: "ignore",
      });
      viceProc.unref();
    }
  );
}

function findVice(configPath: string): string | undefined {
  if (configPath && fs.existsSync(configPath)) {
    return configPath;
  }

  // Common locations
  const candidates = [
    "/opt/homebrew/Cellar/vice/3.10/bin/x64sc",
    "/opt/homebrew/bin/x64sc",
    "/usr/local/bin/x64sc",
    "/usr/bin/x64sc",
    "/snap/bin/x64sc",
  ];

  for (const c of candidates) {
    if (fs.existsSync(c)) return c;
  }

  // Try PATH via which
  try {
    const { execFileSync } = require("child_process");
    const result = execFileSync("which", ["x64sc"], { encoding: "utf8" }).trim();
    if (result && fs.existsSync(result)) return result;
  } catch {
    // not on PATH
  }

  return undefined;
}

function getCompilerCommand(
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

function execPromise(
  command: string,
  args: string[],
  cwd: string
): Promise<string> {
  return new Promise((resolve, reject) => {
    execFile(command, args, { cwd, timeout: 30000 }, (err, stdout, stderr) => {
      if (err) {
        reject(new Error(stderr || err.message));
      } else {
        resolve(stdout);
      }
    });
  });
}
