namespace Cork.Language.Parsing;

using Cork.Language.Ast;
using Cork.Language.Lexing;

public sealed class Parser(List<Token> tokens)
{
    private int _pos;

    public ProgramNode ParseProgram()
    {
        var loc = CurrentLocation;
        var declarations = new List<TopLevelNode>();

        while (!IsAtEnd)
        {
            declarations.Add(ParseTopLevel());
        }

        return new ProgramNode(declarations, loc);
    }

    // ============================================================
    // Top-level
    // ============================================================

    private TopLevelNode ParseTopLevel()
    {
        if (Check(TokenKind.EntryKw) || Check(TokenKind.SceneKw))
            return ParseScene();

        if (Check(TokenKind.StructKw))
            return ParseStructDecl();

        if (Check(TokenKind.ConstKw))
            return ParseConstDecl();

        // Global method: name: { ... } or returnType name: (params) { ... }
        if (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon))
            return ParseGlobalMethod();
        if (IsTypeKeyword(Current.Kind) && PeekIs(1, TokenKind.Identifier) && PeekIs(2, TokenKind.Colon))
            return ParseGlobalMethod();

        // Global variable: type name = expr;
        if (IsTypeKeyword(Current.Kind) || (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Identifier)))
            return ParseGlobalVarDecl();

        throw Error($"Expected top-level declaration, got '{Current.Text}'");
    }

    private GlobalMethodNode ParseGlobalMethod()
    {
        var loc = CurrentLocation;
        string? returnType = null;

        if (IsTypeKeyword(Current.Kind) && PeekIs(1, TokenKind.Identifier) && PeekIs(2, TokenKind.Colon))
            returnType = Advance().Text;

        var parameters = new List<MethodParameter>();
        while (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon))
        {
            var selectorName = Advance().Text;
            Advance(); // colon

            if (Check(TokenKind.OpenParen))
            {
                Advance();
                var paramType = Advance().Text;
                var paramName = Expect(TokenKind.Identifier, "parameter name").Text;
                Expect(TokenKind.CloseParen, ")");
                parameters.Add(new MethodParameter(selectorName, paramType, paramName));
            }
            else
            {
                parameters.Add(new MethodParameter(selectorName, "", ""));
            }
        }

        var body = ParseBlock();
        return new GlobalMethodNode(returnType, parameters, body, loc);
    }

    private StructDeclNode ParseStructDecl()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.StructKw, "struct");
        var name = Expect(TokenKind.Identifier, "struct name").Text;
        Expect(TokenKind.OpenBrace, "{");

        var fields = new List<StructFieldNode>();
        var methods = new List<StructMethodNode>();

        while (!Check(TokenKind.CloseBrace) && !IsAtEnd)
        {
            // Method: name: { ... } or name: (type param) ... { ... }
            if (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon))
            {
                methods.Add(ParseStructMethod());
            }
            // Method with return type: type name: ...
            else if (IsTypeKeyword(Current.Kind) && PeekIs(1, TokenKind.Identifier) && PeekIs(2, TokenKind.Colon))
            {
                methods.Add(ParseStructMethod());
            }
            // Field: type name [= expr];
            else if (IsTypeKeyword(Current.Kind))
            {
                var fieldLoc = CurrentLocation;
                var typeName = Advance().Text;
                var fieldName = Expect(TokenKind.Identifier, "field name").Text;
                ExprNode? defaultVal = null;
                if (TryConsume(TokenKind.Equal))
                    defaultVal = ParseExpression();
                Expect(TokenKind.Semicolon, ";");
                fields.Add(new StructFieldNode(typeName, fieldName, defaultVal, fieldLoc));
            }
            else
            {
                throw Error($"Expected struct field or method, got '{Current.Text}'");
            }
        }

        Expect(TokenKind.CloseBrace, "}");
        return new StructDeclNode(name, fields, methods, loc);
    }

    private StructMethodNode ParseStructMethod()
    {
        var loc = CurrentLocation;
        string? returnType = null;

        if (IsTypeKeyword(Current.Kind) && PeekIs(1, TokenKind.Identifier) && PeekIs(2, TokenKind.Colon))
            returnType = Advance().Text;

        var parameters = new List<MethodParameter>();
        while (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon))
        {
            var selectorName = Advance().Text;
            Advance(); // colon

            if (Check(TokenKind.OpenParen))
            {
                Advance();
                var paramType = Advance().Text;
                var paramName = Expect(TokenKind.Identifier, "parameter name").Text;
                Expect(TokenKind.CloseParen, ")");
                parameters.Add(new MethodParameter(selectorName, paramType, paramName));
            }
            else
            {
                parameters.Add(new MethodParameter(selectorName, "", ""));
            }
        }

        var body = ParseBlock();
        return new StructMethodNode(returnType, parameters, body, loc);
    }

    private SceneNode ParseScene()
    {
        var loc = CurrentLocation;
        var isEntry = TryConsume(TokenKind.EntryKw);
        Expect(TokenKind.SceneKw, "scene");
        var name = Expect(TokenKind.Identifier, "scene name").Text;
        Expect(TokenKind.OpenBrace, "{");

        var members = new List<SceneMemberNode>();
        while (!Check(TokenKind.CloseBrace) && !IsAtEnd)
        {
            members.Add(ParseSceneMember());
        }

        Expect(TokenKind.CloseBrace, "}");
        return new SceneNode(name, isEntry, members, loc);
    }

    private TopLevelNode ParseConstDecl()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.ConstKw, "const");
        var typeName = Expect(IsTypeKeyword, "type").Text;

        // const byte name = value; (scalar constant)
        if (Check(TokenKind.Identifier) && !PeekIs(1, TokenKind.OpenBracket))
        {
            var scalarName = Advance().Text;
            Expect(TokenKind.Equal, "=");
            var value = ParseExpression();
            Expect(TokenKind.Semicolon, ";");
            return new GlobalVarDeclNode(typeName, scalarName, value, loc);
        }

        // const byte[N] name = { ... }; (array constant)
        Expect(TokenKind.OpenBracket, "[");
        var sizeExpr = ParseExpression();
        var size = sizeExpr is IntLiteralExpr intLit
            ? (int)intLit.Value
            : throw Error("Array size must be an integer literal");
        Expect(TokenKind.CloseBracket, "]");
        var name = Expect(TokenKind.Identifier, "name").Text;
        Expect(TokenKind.Equal, "=");
        Expect(TokenKind.OpenBrace, "{");

        var values = new List<ExprNode>();
        if (!Check(TokenKind.CloseBrace))
        {
            values.Add(ParseExpression());
            while (TryConsume(TokenKind.Comma))
                values.Add(ParseExpression());
        }

        Expect(TokenKind.CloseBrace, "}");
        Expect(TokenKind.Semicolon, ";");
        return new ConstArrayDeclNode(typeName, size, name, values, loc);
    }

    private GlobalVarDeclNode ParseGlobalVarDecl()
    {
        var loc = CurrentLocation;
        var typeName = Advance().Text;
        var name = Expect(TokenKind.Identifier, "name").Text;
        ExprNode? init = null;
        if (TryConsume(TokenKind.Equal))
            init = ParseExpression();
        Expect(TokenKind.Semicolon, ";");
        return new GlobalVarDeclNode(typeName, name, init, loc);
    }

    // ============================================================
    // Scene members
    // ============================================================

    private SceneMemberNode ParseSceneMember()
    {
        if (Check(TokenKind.HardwareKw))
            return ParseHardwareBlock();
        if (Check(TokenKind.EnterKw))
            return ParseEnterBlock();
        if (Check(TokenKind.FrameKw))
            return ParseFrameBlock();
        if (Check(TokenKind.RelaxedKw))
            return ParseRelaxedFrameBlock();
        if (Check(TokenKind.ExitKw))
            return ParseExitBlock();
        if (Check(TokenKind.RasterKw))
            return ParseRasterBlock();

        // Scene-local variable: type name [= expr]; (primitive or struct type)
        if (IsTypeKeyword(Current.Kind) && PeekIs(1, TokenKind.Identifier) && !PeekIs(2, TokenKind.Colon))
            return ParseSceneVarDecl();
        // Struct-typed variable: StructName varName;
        if (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Identifier) && !PeekIs(2, TokenKind.Colon))
            return ParseSceneVarDecl();

        // Scene method with return type: type name: ...
        if (IsTypeKeyword(Current.Kind) && PeekIs(1, TokenKind.Identifier) && PeekIs(2, TokenKind.Colon))
            return ParseSceneMethod();

        // Scene method without return type: name: { ... } or name: (type param) ...
        if (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon))
            return ParseSceneMethod();

        throw Error($"Expected scene member, got '{Current.Text}'");
    }

    private HardwareBlockNode ParseHardwareBlock()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.HardwareKw, "hardware");
        Expect(TokenKind.OpenBrace, "{");

        var settings = new List<HardwareSetting>();
        while (!Check(TokenKind.CloseBrace) && !IsAtEnd)
        {
            var settingLoc = CurrentLocation;
            var name = Expect(TokenKind.Identifier, "setting name").Text;
            Expect(TokenKind.Colon, ":");
            var value = ParseExpression();
            Expect(TokenKind.Semicolon, ";");
            settings.Add(new HardwareSetting(name, value, settingLoc));
        }

        Expect(TokenKind.CloseBrace, "}");
        return new HardwareBlockNode(settings, loc);
    }

    private EnterBlockNode ParseEnterBlock()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.EnterKw, "enter");
        var body = ParseBlock();
        return new EnterBlockNode(body, loc);
    }

    private FrameBlockNode ParseFrameBlock()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.FrameKw, "frame");
        var body = ParseBlock();
        return new FrameBlockNode(false, body, loc);
    }

    private FrameBlockNode ParseRelaxedFrameBlock()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.RelaxedKw, "relaxed");
        Expect(TokenKind.FrameKw, "frame");
        var body = ParseBlock();
        return new FrameBlockNode(true, body, loc);
    }

    private ExitBlockNode ParseExitBlock()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.ExitKw, "exit");
        var body = ParseBlock();
        return new ExitBlockNode(body, loc);
    }

    private RasterBlockNode ParseRasterBlock()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.RasterKw, "raster");
        var lineExpr = ParseExpression();
        var line = lineExpr is IntLiteralExpr intLit
            ? (int)intLit.Value
            : throw Error("Raster line must be an integer literal");
        var body = ParseBlock();
        return new RasterBlockNode(line, body, loc);
    }

    private SceneVarDeclNode ParseSceneVarDecl()
    {
        var loc = CurrentLocation;
        var typeName = Advance().Text;
        var name = Expect(TokenKind.Identifier, "name").Text;
        ExprNode? init = null;
        if (TryConsume(TokenKind.Equal))
            init = ParseExpression();
        Expect(TokenKind.Semicolon, ";");
        return new SceneVarDeclNode(typeName, name, init, loc);
    }

    private SceneMethodNode ParseSceneMethod()
    {
        var loc = CurrentLocation;
        string? returnType = null;

        // Check for return type (type keyword before selector name)
        if (IsTypeKeyword(Current.Kind) && PeekIs(1, TokenKind.Identifier) && PeekIs(2, TokenKind.Colon))
            returnType = Advance().Text;

        // Parse selector segments with parameters
        var parameters = new List<MethodParameter>();
        while (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon))
        {
            var selectorName = Advance().Text;
            Advance(); // consume colon

            // Check for parameter: (type name)
            if (Check(TokenKind.OpenParen))
            {
                Advance(); // (
                var paramType = Advance().Text;
                var paramName = Expect(TokenKind.Identifier, "parameter name").Text;
                Expect(TokenKind.CloseParen, ")");
                parameters.Add(new MethodParameter(selectorName, paramType, paramName));
            }
            else
            {
                // No parameter for this segment (e.g., clearScreen:)
                parameters.Add(new MethodParameter(selectorName, "", ""));
            }
        }

        var body = ParseBlock();
        return new SceneMethodNode(returnType, parameters, body, loc);
    }

    // ============================================================
    // Statements
    // ============================================================

    private BlockNode ParseBlock()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.OpenBrace, "{");

        var stmts = new List<StmtNode>();
        while (!Check(TokenKind.CloseBrace) && !IsAtEnd)
        {
            stmts.Add(ParseStatement());
        }

        Expect(TokenKind.CloseBrace, "}");
        return new BlockNode(stmts, loc);
    }

    private StmtNode ParseStatement()
    {
        if (Check(TokenKind.WhileKw)) return ParseWhile();
        if (Check(TokenKind.IfKw)) return ParseIf();
        if (Check(TokenKind.ReturnKw)) return ParseReturn();
        if (Check(TokenKind.GoKw)) return ParseGo();
        if (Check(TokenKind.BreakKw)) { var loc = CurrentLocation; Advance(); Expect(TokenKind.Semicolon, ";"); return new BreakStmt(loc); }
        if (Check(TokenKind.ContinueKw)) { var loc = CurrentLocation; Advance(); Expect(TokenKind.Semicolon, ";"); return new ContinueStmt(loc); }

        // Variable declaration: type name = expr;
        if (IsTypeKeyword(Current.Kind) && PeekIs(1, TokenKind.Identifier) && !PeekIs(2, TokenKind.Colon))
            return ParseLocalVarDecl();

        // var name = expr;
        if (Check(TokenKind.VarKw))
            return ParseLocalVarDecl();

        // Expression-based: could be assignment or message send
        return ParseExpressionStatement();
    }

    private WhileStmt ParseWhile()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.WhileKw, "while");
        Expect(TokenKind.OpenParen, "(");
        var condition = ParseExpression();
        Expect(TokenKind.CloseParen, ")");
        var body = ParseBlock();
        return new WhileStmt(condition, body, loc);
    }

    private IfStmt ParseIf()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.IfKw, "if");
        Expect(TokenKind.OpenParen, "(");
        var condition = ParseExpression();
        Expect(TokenKind.CloseParen, ")");
        var thenBody = ParseBlock();

        var elseIfs = new List<(ExprNode, BlockNode)>();
        BlockNode? elseBody = null;

        while (TryConsume(TokenKind.ElseKw))
        {
            if (Check(TokenKind.IfKw))
            {
                Advance();
                Expect(TokenKind.OpenParen, "(");
                var eifCond = ParseExpression();
                Expect(TokenKind.CloseParen, ")");
                var eifBody = ParseBlock();
                elseIfs.Add((eifCond, eifBody));
            }
            else
            {
                elseBody = ParseBlock();
                break;
            }
        }

        return new IfStmt(condition, thenBody, elseIfs, elseBody, loc);
    }

    private ReturnStmt ParseReturn()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.ReturnKw, "return");
        ExprNode? value = null;
        if (!Check(TokenKind.Semicolon))
            value = ParseExpression();
        Expect(TokenKind.Semicolon, ";");
        return new ReturnStmt(value, loc);
    }

    private GoStmt ParseGo()
    {
        var loc = CurrentLocation;
        Expect(TokenKind.GoKw, "go");
        var sceneName = Expect(TokenKind.Identifier, "scene name").Text;
        Expect(TokenKind.Semicolon, ";");
        return new GoStmt(sceneName, loc);
    }

    private VarDeclStmt ParseLocalVarDecl()
    {
        var loc = CurrentLocation;
        var typeName = Advance().Text; // type keyword or 'var'
        var name = Expect(TokenKind.Identifier, "name").Text;
        ExprNode? init = null;
        if (TryConsume(TokenKind.Equal))
            init = ParseExpression();
        Expect(TokenKind.Semicolon, ";");
        return new VarDeclStmt(typeName, name, init, loc);
    }

    private StmtNode ParseExpressionStatement()
    {
        var loc = CurrentLocation;
        var expr = ParsePostfix(ParsePrimary());

        // Check for message send: expr identifier colon
        if (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon))
        {
            var segments = ParseMessageSegments();
            Expect(TokenKind.Semicolon, ";");
            return new MessageSendStmt(expr, segments, loc);
        }

        // Identifier followed by colon: standalone message send (no receiver)
        // Could be no-arg (name:;) or with args (name: arg ...;)
        if (expr is IdentifierExpr identExpr && Check(TokenKind.Colon))
        {
            var selectorName = identExpr.Name;
            Advance(); // consume colon

            if (Check(TokenKind.Semicolon))
            {
                // No-arg: name:;
                Expect(TokenKind.Semicolon, ";");
                return new MessageSendStmt(null, [new SelectorSegment(selectorName, null)], loc);
            }

            // Has argument: name: arg [name: arg ...];
            var segments = new List<SelectorSegment>();
            var firstArg = ParseMessageArgExpression();
            segments.Add(new SelectorSegment(selectorName, firstArg));

            // Parse additional segments
            while (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon))
            {
                var segName = Advance().Text;
                Advance(); // consume colon
                if (Check(TokenKind.Semicolon))
                {
                    segments.Add(new SelectorSegment(segName, null));
                }
                else
                {
                    segments.Add(new SelectorSegment(segName, ParseMessageArgExpression()));
                }
            }

            Expect(TokenKind.Semicolon, ";");
            return new MessageSendStmt(null, segments, loc);
        }

        // Check for assignment
        if (IsAssignmentOp(Current.Kind))
        {
            var op = Advance().Kind;
            var value = ParseExpression();
            Expect(TokenKind.Semicolon, ";");
            return new AssignmentStmt(expr, op, value, loc);
        }

        Expect(TokenKind.Semicolon, ";");
        return new MessageSendStmt(null, [], loc); // bare expression statement
    }

    private List<SelectorSegment> ParseMessageSegments()
    {
        var segments = new List<SelectorSegment>();

        while (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon))
        {
            var name = Advance().Text;
            Advance(); // consume colon

            if (Check(TokenKind.Semicolon) || Check(TokenKind.CloseParen) ||
                (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon)))
            {
                segments.Add(new SelectorSegment(name, null));
            }
            else
            {
                segments.Add(new SelectorSegment(name, ParseMessageArgExpression()));
            }
        }

        return segments;
    }

    // ============================================================
    // Expressions (precedence climbing)
    // ============================================================

    private ExprNode ParseExpression() => ParseLogicalOr();

    private ExprNode ParseLogicalOr()
    {
        var left = ParseLogicalAnd();
        while (TryConsume(TokenKind.PipePipe))
            left = new BinaryExpr(left, TokenKind.PipePipe, ParseLogicalAnd(), left.Location);
        return left;
    }

    private ExprNode ParseLogicalAnd()
    {
        var left = ParseBitwiseOr();
        while (TryConsume(TokenKind.AmpAmp))
            left = new BinaryExpr(left, TokenKind.AmpAmp, ParseBitwiseOr(), left.Location);
        return left;
    }

    private ExprNode ParseBitwiseOr()
    {
        var left = ParseBitwiseXor();
        while (TryConsume(TokenKind.Pipe))
            left = new BinaryExpr(left, TokenKind.Pipe, ParseBitwiseXor(), left.Location);
        return left;
    }

    private ExprNode ParseBitwiseXor()
    {
        var left = ParseBitwiseAnd();
        while (TryConsume(TokenKind.Caret))
            left = new BinaryExpr(left, TokenKind.Caret, ParseBitwiseAnd(), left.Location);
        return left;
    }

    private ExprNode ParseBitwiseAnd()
    {
        var left = ParseEquality();
        while (TryConsume(TokenKind.Ampersand))
            left = new BinaryExpr(left, TokenKind.Ampersand, ParseEquality(), left.Location);
        return left;
    }

    private ExprNode ParseEquality()
    {
        var left = ParseComparison();
        while (Check(TokenKind.EqualEqual) || Check(TokenKind.BangEqual))
        {
            var op = Advance().Kind;
            left = new BinaryExpr(left, op, ParseComparison(), left.Location);
        }
        return left;
    }

    private ExprNode ParseComparison()
    {
        var left = ParseShift();
        while (Check(TokenKind.Less) || Check(TokenKind.Greater) ||
               Check(TokenKind.LessEqual) || Check(TokenKind.GreaterEqual))
        {
            var op = Advance().Kind;
            left = new BinaryExpr(left, op, ParseShift(), left.Location);
        }
        return left;
    }

    private ExprNode ParseShift()
    {
        var left = ParseAdditive();
        while (Check(TokenKind.ShiftLeft) || Check(TokenKind.ShiftRight))
        {
            var op = Advance().Kind;
            left = new BinaryExpr(left, op, ParseAdditive(), left.Location);
        }
        return left;
    }

    private ExprNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Check(TokenKind.Plus) || Check(TokenKind.Minus))
        {
            var op = Advance().Kind;
            left = new BinaryExpr(left, op, ParseMultiplicative(), left.Location);
        }
        return left;
    }

    private ExprNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Check(TokenKind.Star) || Check(TokenKind.Slash) || Check(TokenKind.Percent))
        {
            var op = Advance().Kind;
            left = new BinaryExpr(left, op, ParseUnary(), left.Location);
        }
        return left;
    }

    private ExprNode ParseUnary()
    {
        if (Check(TokenKind.Minus) || Check(TokenKind.Bang) || Check(TokenKind.Tilde))
        {
            var loc = CurrentLocation;
            var op = Advance().Kind;
            return new UnaryExpr(op, ParseUnary(), loc);
        }
        return ParsePostfix(ParsePrimary());
    }

    private ExprNode ParsePostfix(ExprNode left)
    {
        while (true)
        {
            if (TryConsume(TokenKind.Dot))
            {
                var name = Expect(TokenKind.Identifier, "member name").Text;
                left = new MemberAccessExpr(left, name, left.Location);
            }
            else if (TryConsume(TokenKind.OpenBracket))
            {
                var index = ParseExpression();
                Expect(TokenKind.CloseBracket, "]");
                left = new IndexExpr(left, index, left.Location);
            }
            else
            {
                break;
            }
        }
        return left;
    }

    private ExprNode ParsePrimary()
    {
        var loc = CurrentLocation;

        if (Check(TokenKind.IntegerLiteral))
        {
            var token = Advance();
            return new IntLiteralExpr((long)token.LiteralValue!, loc);
        }

        if (Check(TokenKind.FixedLiteral))
        {
            var token = Advance();
            return new FixedLiteralExpr((double)token.LiteralValue!, loc);
        }

        if (Check(TokenKind.StringLiteral))
        {
            var token = Advance();
            return new StringLiteralExpr((string)token.LiteralValue!, loc);
        }

        if (Check(TokenKind.TrueLiteral))
        {
            Advance();
            return new BoolLiteralExpr(true, loc);
        }

        if (Check(TokenKind.FalseLiteral))
        {
            Advance();
            return new BoolLiteralExpr(false, loc);
        }

        if (Check(TokenKind.Identifier))
        {
            var name = Advance().Text;
            return new IdentifierExpr(name, loc);
        }

        if (TryConsume(TokenKind.OpenParen))
        {
            var expr = ParseExpression();

            // Check for parenthesized message send: (receiver segment: arg)
            if (Check(TokenKind.Identifier) && PeekIs(1, TokenKind.Colon))
            {
                var segments = ParseMessageSegments();
                Expect(TokenKind.CloseParen, ")");
                return new MessageSendExpr(expr, segments, loc);
            }

            Expect(TokenKind.CloseParen, ")");
            return expr;
        }

        throw Error($"Expected expression, got '{Current.Text}'");
    }

    // ============================================================
    // Message argument expression
    // ============================================================

    /// <summary>
    /// Parse a message argument: a full expression, but stop when we see
    /// identifier-colon (next selector segment) or semicolon (end of statement).
    /// This is the same as ParseExpression() — the termination happens naturally
    /// because identifier-colon is not a valid continuation of a binary expression.
    /// </summary>
    private ExprNode ParseMessageArgExpression() => ParseExpression();

    // ============================================================
    // Helpers
    // ============================================================

    private Token Current => _pos < tokens.Count ? tokens[_pos] : tokens[^1];
    private SourceLocation CurrentLocation => Current.Location;
    private bool IsAtEnd => _pos >= tokens.Count || Current.Kind == TokenKind.Eof;

    private bool Check(TokenKind kind) => !IsAtEnd && Current.Kind == kind;

    private bool PeekIs(int offset, TokenKind kind)
    {
        var idx = _pos + offset;
        return idx < tokens.Count && tokens[idx].Kind == kind;
    }

    private Token Advance()
    {
        var token = Current;
        _pos++;
        return token;
    }

    private bool TryConsume(TokenKind kind)
    {
        if (Check(kind))
        {
            Advance();
            return true;
        }
        return false;
    }

    private Token Expect(TokenKind kind, string expected)
    {
        if (Check(kind)) return Advance();
        throw Error($"Expected {expected}, got '{Current.Text}'");
    }

    private Token Expect(Func<TokenKind, bool> predicate, string expected)
    {
        if (predicate(Current.Kind)) return Advance();
        throw Error($"Expected {expected}, got '{Current.Text}'");
    }

    private static bool IsTypeKeyword(TokenKind kind) => kind is
        TokenKind.ByteKw or TokenKind.SbyteKw or
        TokenKind.WordKw or TokenKind.SwordKw or
        TokenKind.BoolKw or TokenKind.FixedKw or
        TokenKind.StringKw or TokenKind.VarKw;

    private static bool IsAssignmentOp(TokenKind kind) => kind is
        TokenKind.Equal or TokenKind.PlusEqual or TokenKind.MinusEqual or
        TokenKind.StarEqual or TokenKind.SlashEqual or TokenKind.PercentEqual or
        TokenKind.AmpEqual or TokenKind.PipeEqual or TokenKind.CaretEqual or
        TokenKind.ShiftLeftEqual or TokenKind.ShiftRightEqual;

    private Exception Error(string message) =>
        new InvalidOperationException($"{CurrentLocation}: {message}");
}
