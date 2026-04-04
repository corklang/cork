namespace Cork.Language.Lexing;

public enum TokenKind
{
    // Literals
    IntegerLiteral,
    FixedLiteral,
    StringLiteral,
    TrueLiteral,
    FalseLiteral,
    SpritePatternLiteral,

    // Identifiers
    Identifier,

    // Primitive type keywords
    ByteKw,
    SbyteKw,
    WordKw,
    SwordKw,
    BoolKw,
    FixedKw,
    SfixedKw,
    StringKw,

    // Inference keyword
    VarKw,

    // Hardware type keywords
    SpriteKw,
    CharsetKw,
    MusicKw,
    SoundKw,
    TilemapKw,

    // Declaration keywords
    StructKw,
    EnumKw,
    FlagsKw,
    SceneKw,
    EntryKw,
    ConstKw,
    ImportKw,

    // Control flow keywords
    IfKw,
    ElseKw,
    WhileKw,
    ForKw,
    InKw,
    SwitchKw,
    CaseKw,
    DefaultKw,
    FallthroughKw,
    BreakKw,
    ContinueKw,
    ReturnKw,
    GoKw,

    // Scene lifecycle keywords
    HardwareKw,
    EnterKw,
    FrameKw,
    RelaxedKw,
    RasterKw,
    ExitKw,

    // Other keywords
    AsKw,

    // Punctuation
    Semicolon,
    Comma,
    Dot,
    Colon,
    OpenParen,
    CloseParen,
    OpenBrace,
    CloseBrace,
    OpenBracket,
    CloseBracket,

    // Arithmetic operators
    Plus,
    Minus,
    Star,
    Slash,
    Percent,

    // Bitwise operators
    Ampersand,
    Pipe,
    Caret,
    Tilde,

    // Logical operators
    Bang,
    AmpAmp,
    PipePipe,

    // Comparison operators
    Less,
    Greater,
    LessEqual,
    GreaterEqual,
    EqualEqual,
    BangEqual,

    // Shift operators
    ShiftLeft,
    ShiftRight,

    // Assignment operators
    Equal,
    PlusEqual,
    MinusEqual,
    StarEqual,
    SlashEqual,
    PercentEqual,
    AmpEqual,
    PipeEqual,
    CaretEqual,
    ShiftLeftEqual,
    ShiftRightEqual,

    // Special
    Eof,
}
