namespace Cork.Language.Ast;

using Cork.Language.Lexing;

// Base node — all AST nodes carry their source location
public abstract record AstNode(SourceLocation Location);

// ============================================================
// Program
// ============================================================

public sealed record ProgramNode(
    List<TopLevelNode> Declarations,
    SourceLocation Location
) : AstNode(Location);

// ============================================================
// Top-level declarations
// ============================================================

public abstract record TopLevelNode(SourceLocation Location) : AstNode(Location);

public sealed record SceneNode(
    string Name,
    bool IsEntry,
    List<SceneMemberNode> Members,
    SourceLocation Location
) : TopLevelNode(Location);

public sealed record ConstArrayDeclNode(
    string ElementType,
    int Size,
    string Name,
    List<ExprNode> Values,
    SourceLocation Location
) : TopLevelNode(Location);

public sealed record EnumDeclNode(
    string Name,
    string BackingType,
    bool IsFlags,
    List<EnumMemberNode> Members,
    SourceLocation Location
) : TopLevelNode(Location);

public sealed record EnumMemberNode(
    string Name,
    long Value,
    SourceLocation Location
);

public sealed record StructDeclNode(
    string Name,
    List<StructFieldNode> Fields,
    List<StructMethodNode> Methods,
    SourceLocation Location
) : TopLevelNode(Location);

public sealed record StructFieldNode(
    string TypeName,
    string Name,
    ExprNode? DefaultValue,
    SourceLocation Location
);

public sealed record StructMethodNode(
    string? ReturnType,
    List<MethodParameter> Parameters,
    BlockNode Body,
    SourceLocation Location
)
{
    public string SelectorName => string.Join("", Parameters.Select(p => p.SelectorName + ":"));
}

public sealed record GlobalMethodNode(
    string? ReturnType,
    List<MethodParameter> Parameters,
    BlockNode Body,
    SourceLocation Location
) : TopLevelNode(Location)
{
    public string SelectorName => string.Join("", Parameters.Select(p => p.SelectorName + ":"));
}

public sealed record GlobalVarDeclNode(
    string TypeName,
    string Name,
    ExprNode? Initializer,
    SourceLocation Location
) : TopLevelNode(Location);

// ============================================================
// Scene members
// ============================================================

public abstract record SceneMemberNode(SourceLocation Location) : AstNode(Location);

public sealed record HardwareBlockNode(
    List<HardwareSetting> Settings,
    SourceLocation Location
) : SceneMemberNode(Location);

public sealed record HardwareSetting(string Name, ExprNode Value, SourceLocation Location);

public sealed record EnterBlockNode(
    BlockNode Body,
    SourceLocation Location
) : SceneMemberNode(Location);

public sealed record FrameBlockNode(
    bool IsRelaxed,
    BlockNode Body,
    SourceLocation Location
) : SceneMemberNode(Location);

public sealed record RasterBlockNode(
    int Line,
    BlockNode Body,
    SourceLocation Location
) : SceneMemberNode(Location);

public sealed record ExitBlockNode(
    BlockNode Body,
    SourceLocation Location
) : SceneMemberNode(Location);

public sealed record SceneVarDeclNode(
    string TypeName,
    string Name,
    ExprNode? Initializer,
    bool IsConst,
    SourceLocation Location
) : SceneMemberNode(Location);

public sealed record SceneMethodNode(
    string? ReturnType,
    List<MethodParameter> Parameters,
    BlockNode Body,
    SourceLocation Location
) : SceneMemberNode(Location)
{
    public string SelectorName => string.Join("", Parameters.Select(p => p.SelectorName + ":"));
}

// ============================================================
// Statements
// ============================================================

public abstract record StmtNode(SourceLocation Location) : AstNode(Location);

public sealed record BlockNode(
    List<StmtNode> Statements,
    SourceLocation Location
) : AstNode(Location);

public sealed record VarDeclStmt(
    bool IsConst,
    string TypeName,
    string Name,
    ExprNode? Initializer,
    SourceLocation Location
) : StmtNode(Location);

public sealed record AssignmentStmt(
    ExprNode Target,
    TokenKind Op,  // Equal, PlusEqual, MinusEqual, etc.
    ExprNode Value,
    SourceLocation Location
) : StmtNode(Location);

public sealed record WhileStmt(
    ExprNode Condition,
    BlockNode Body,
    SourceLocation Location
) : StmtNode(Location);

public sealed record IfStmt(
    ExprNode Condition,
    BlockNode ThenBody,
    List<(ExprNode Condition, BlockNode Body)> ElseIfs,
    BlockNode? ElseBody,
    SourceLocation Location
) : StmtNode(Location);

public sealed record MessageSendStmt(
    ExprNode? Receiver,
    List<SelectorSegment> Segments,
    SourceLocation Location
) : StmtNode(Location);

public sealed record ReturnStmt(
    ExprNode? Value,
    SourceLocation Location
) : StmtNode(Location);

public sealed record SwitchStmt(
    ExprNode Subject,
    List<SwitchCase> Cases,
    BlockNode? DefaultBody,
    bool IsFallthrough,
    SourceLocation Location
) : StmtNode(Location);

public sealed record SwitchCase(
    ExprNode Value,
    List<StmtNode> Body
);

public sealed record ForStmt(
    StmtNode Init,
    ExprNode Condition,
    StmtNode Step,
    BlockNode Body,
    SourceLocation Location
) : StmtNode(Location);

public sealed record GoStmt(
    string SceneName,
    SourceLocation Location
) : StmtNode(Location);

public sealed record BreakStmt(SourceLocation Location) : StmtNode(Location);
public sealed record ContinueStmt(SourceLocation Location) : StmtNode(Location);

// ============================================================
// Selector segments
// ============================================================

// For call sites: name: argument
public sealed record SelectorSegment(
    string Name,
    ExprNode? Argument
);

// For method declarations: name: (type paramName)
public sealed record MethodParameter(
    string SelectorName,
    string TypeName,
    string ParamName
);

// ============================================================
// Expressions
// ============================================================

public abstract record ExprNode(SourceLocation Location) : AstNode(Location);

public sealed record IntLiteralExpr(
    long Value,
    SourceLocation Location
) : ExprNode(Location);

public sealed record FixedLiteralExpr(
    double Value,
    SourceLocation Location
) : ExprNode(Location);

public sealed record StringLiteralExpr(
    string Value,
    SourceLocation Location
) : ExprNode(Location);

public sealed record BoolLiteralExpr(
    bool Value,
    SourceLocation Location
) : ExprNode(Location);

public sealed record IdentifierExpr(
    string Name,
    SourceLocation Location
) : ExprNode(Location);

public sealed record BinaryExpr(
    ExprNode Left,
    TokenKind Op,
    ExprNode Right,
    SourceLocation Location
) : ExprNode(Location);

public sealed record UnaryExpr(
    TokenKind Op,
    ExprNode Operand,
    SourceLocation Location
) : ExprNode(Location);

public sealed record MemberAccessExpr(
    ExprNode Receiver,
    string MemberName,
    SourceLocation Location
) : ExprNode(Location);

public sealed record IndexExpr(
    ExprNode Receiver,
    ExprNode Index,
    SourceLocation Location
) : ExprNode(Location);

public sealed record MessageSendExpr(
    ExprNode Receiver,
    List<SelectorSegment> Segments,
    SourceLocation Location
) : ExprNode(Location);

public sealed record ArrayInitExpr(
    List<ExprNode> Elements,
    SourceLocation Location
) : ExprNode(Location);
