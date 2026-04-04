namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;

/// <summary>
/// Determines which global methods, variables, and const arrays are reachable
/// from scene code. Unreachable declarations can be omitted from output.
/// </summary>
public static class ReachabilityAnalysis
{
    public record ReachableSet(
        HashSet<string> Methods,
        HashSet<string> GlobalVars,
        HashSet<string> ConstArrays,
        HashSet<string> Identifiers
    );

    public static ReachableSet Analyze(ProgramNode program)
    {
        var methods = new HashSet<string>();
        var globalVars = new HashSet<string>();
        var constArrays = new HashSet<string>();
        var identifiers = new HashSet<string>();

        // Collect all global method names for lookup
        var globalMethodBodies = new Dictionary<string, BlockNode>();
        foreach (var decl in program.Declarations)
            if (decl is GlobalMethodNode gm)
                globalMethodBodies[gm.SelectorName] = gm.Body;

        // Collect all const array names
        var allConstArrays = new HashSet<string>();
        foreach (var decl in program.Declarations)
            if (decl is ConstArrayDeclNode ca)
                allConstArrays.Add(ca.Name);

        // Collect all global var names
        var allGlobalVars = new HashSet<string>();
        foreach (var decl in program.Declarations)
            if (decl is GlobalVarDeclNode gv)
                allGlobalVars.Add(gv.Name);

        // Collect all references from scene code (methods + identifiers)
        CollectMethodCalls(program, methods, identifiers);

        // Transitive closure: walk called methods for more calls
        var processed = new HashSet<string>();
        var queue = new Queue<string>(methods);
        while (queue.Count > 0)
        {
            var selector = queue.Dequeue();
            if (!processed.Add(selector)) continue;

            if (globalMethodBodies.TryGetValue(selector, out var body))
            {
                var newMethods = new HashSet<string>();
                var newIdents = new HashSet<string>();
                CollectFromBlock(body, newMethods, newIdents);

                foreach (var m in newMethods)
                    if (methods.Add(m))
                        queue.Enqueue(m);

                foreach (var id in newIdents)
                    identifiers.Add(id);
            }
        }

        // Resolve all collected identifiers to global vars and const arrays
        foreach (var id in identifiers)
        {
            if (allGlobalVars.Contains(id)) globalVars.Add(id);
            if (allConstArrays.Contains(id)) constArrays.Add(id);
        }

        // Sprite data: 63-byte const arrays referenced as sprite data need their
        // matching pointer globals (found by naming convention in EmitSpriteCopies/SceneEmitter).
        // If the array is reachable, include the pointer global too.
        foreach (var ca in program.Declarations.OfType<ConstArrayDeclNode>())
        {
            if (ca.Size != 63 || !constArrays.Contains(ca.Name)) continue;
            string[] ptrNames = [
                ca.Name.Replace("Data", "Ptr"),
                ca.Name.EndsWith("Sprite") ? ca.Name[..^6] + "Ptr" : "",
                "spritePtr"
            ];
            foreach (var ptrName in ptrNames)
            {
                if (ptrName != "" && allGlobalVars.Contains(ptrName))
                {
                    globalVars.Add(ptrName);
                    break;
                }
            }
        }

        return new ReachableSet(methods, globalVars, constArrays, identifiers);
    }

    private static void CollectMethodCalls(ProgramNode program, HashSet<string> methods, HashSet<string> identifiers)
    {
        foreach (var scene in program.Declarations.OfType<SceneNode>())
        {
            foreach (var member in scene.Members)
            {
                switch (member)
                {
                    case EnterBlockNode enter:
                        CollectFromBlock(enter.Body, methods, identifiers);
                        break;
                    case FrameBlockNode frame:
                        CollectFromBlock(frame.Body, methods, identifiers);
                        break;
                    case ExitBlockNode exit:
                        CollectFromBlock(exit.Body, methods, identifiers);
                        break;
                    case SceneMethodNode sm:
                        CollectFromBlock(sm.Body, methods, identifiers);
                        break;
                    case SpriteBlockNode sprite:
                        foreach (var s in sprite.Settings)
                            CollectFromExpr(s.Value, methods, identifiers);
                        break;
                    case HardwareBlockNode hw:
                        foreach (var s in hw.Settings)
                            CollectFromExpr(s.Value, methods, identifiers);
                        break;
                    case SceneVarDeclNode vd:
                        if (vd.Initializer != null)
                            CollectFromExpr(vd.Initializer, methods, identifiers);
                        break;
                }
            }
        }
    }

    private static void CollectFromScene(SceneNode scene, HashSet<string> identifiers)
    {
        // Scene-level sprite data references
        foreach (var member in scene.Members)
        {
            if (member is SpriteBlockNode sprite)
            {
                foreach (var s in sprite.Settings)
                    if (s.Name == "data" && s.Value is IdentifierExpr dataIdent)
                        identifiers.Add(dataIdent.Name);
            }
        }
    }

    private static void CollectFromBlock(BlockNode block, HashSet<string> methods, HashSet<string> identifiers)
    {
        foreach (var stmt in block.Statements)
            CollectFromStmt(stmt, methods, identifiers);
    }

    private static void CollectFromStmt(StmtNode stmt, HashSet<string> methods, HashSet<string> identifiers)
    {
        switch (stmt)
        {
            case MessageSendStmt msg:
                if (msg.Receiver == null)
                {
                    var selector = string.Join("", msg.Segments.Select(s => s.Name + ":"));
                    methods.Add(selector);
                }
                if (msg.Receiver != null)
                    CollectFromExpr(msg.Receiver, methods, identifiers);
                foreach (var seg in msg.Segments)
                    if (seg.Argument != null)
                        CollectFromExpr(seg.Argument, methods, identifiers);
                break;

            case VarDeclStmt vd:
                if (vd.Initializer != null)
                    CollectFromExpr(vd.Initializer, methods, identifiers);
                break;

            case AssignmentStmt assign:
                CollectFromExpr(assign.Target, methods, identifiers);
                CollectFromExpr(assign.Value, methods, identifiers);
                break;

            case IfStmt ifStmt:
                CollectFromExpr(ifStmt.Condition, methods, identifiers);
                CollectFromBlock(ifStmt.ThenBody, methods, identifiers);
                if (ifStmt.ElseBody != null)
                    CollectFromBlock(ifStmt.ElseBody, methods, identifiers);
                foreach (var (cond, body) in ifStmt.ElseIfs)
                {
                    CollectFromExpr(cond, methods, identifiers);
                    CollectFromBlock(body, methods, identifiers);
                }
                break;

            case WhileStmt w:
                CollectFromExpr(w.Condition, methods, identifiers);
                CollectFromBlock(w.Body, methods, identifiers);
                break;

            case ForStmt f:
                if (f.Init != null) CollectFromStmt(f.Init, methods, identifiers);
                CollectFromExpr(f.Condition, methods, identifiers);
                if (f.Step != null) CollectFromStmt(f.Step, methods, identifiers);
                CollectFromBlock(f.Body, methods, identifiers);
                break;

            case ForEachStmt fe:
                CollectFromExpr(fe.Collection, methods, identifiers);
                CollectFromBlock(fe.Body, methods, identifiers);
                break;

            case SwitchStmt sw:
                CollectFromExpr(sw.Subject, methods, identifiers);
                foreach (var c in sw.Cases)
                {
                    CollectFromExpr(c.Value, methods, identifiers);
                    foreach (var s in c.Body)
                        CollectFromStmt(s, methods, identifiers);
                }
                if (sw.DefaultBody != null)
                    CollectFromBlock(sw.DefaultBody, methods, identifiers);
                break;

            case ReturnStmt ret:
                if (ret.Value != null)
                    CollectFromExpr(ret.Value, methods, identifiers);
                break;
        }
    }

    private static void CollectFromExpr(ExprNode expr, HashSet<string> methods, HashSet<string> identifiers)
    {
        switch (expr)
        {
            case IdentifierExpr ident:
                identifiers.Add(ident.Name);
                break;
            case BinaryExpr bin:
                CollectFromExpr(bin.Left, methods, identifiers);
                CollectFromExpr(bin.Right, methods, identifiers);
                break;
            case UnaryExpr un:
                CollectFromExpr(un.Operand, methods, identifiers);
                break;
            case MemberAccessExpr member:
                CollectFromExpr(member.Receiver, methods, identifiers);
                break;
            case IndexExpr idx:
                CollectFromExpr(idx.Receiver, methods, identifiers);
                CollectFromExpr(idx.Index, methods, identifiers);
                break;
            case CastExpr cast:
                CollectFromExpr(cast.Operand, methods, identifiers);
                break;
            case MessageSendExpr msgExpr:
                if (msgExpr.Receiver == null)
                {
                    var selector = string.Join("", msgExpr.Segments.Select(s => s.Name + ":"));
                    methods.Add(selector);
                }
                if (msgExpr.Receiver != null)
                    CollectFromExpr(msgExpr.Receiver, methods, identifiers);
                foreach (var seg in msgExpr.Segments)
                    if (seg.Argument != null)
                        CollectFromExpr(seg.Argument, methods, identifiers);
                break;
            case StructInitExpr si:
                foreach (var (_, val) in si.FieldInits)
                    CollectFromExpr(val, methods, identifiers);
                break;
        }
    }
}
