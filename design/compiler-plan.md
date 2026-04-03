# Cork Compiler Implementation Plan

### 1. Project Structure

The solution should be a single .NET 10 solution with five projects, organized to keep the compiler pipeline cleanly separated:

```
Cork.sln
  src/
    Cork.Compiler/          -- Main entry point, CLI driver
    Cork.Language/           -- Lexer, Parser, AST, Semantic Analysis
    Cork.CodeGen/            -- IR, 6510 code generation, memory layout
    Cork.Output/             -- .prg, .d64, .crt emitters
    Cork.Runtime/            -- Runtime library source (6510 asm as embedded resources)
  tests/
    Cork.Language.Tests/     -- Lexer, parser, semantic analysis unit tests
    Cork.CodeGen.Tests/      -- Code generation unit tests
    Cork.Integration.Tests/  -- End-to-end: Cork source -> .prg -> VICE verification
  samples/
    hello.cork
    bouncing-ball.cork
    title-game.cork
```

**Namespace layout within Cork.Language:**
- `Cork.Language.Lexing` -- Token, TokenKind, Lexer, SourceLocation
- `Cork.Language.Parsing` -- Parser, all AST node types
- `Cork.Language.Ast` -- Node base class, visitor interfaces
- `Cork.Language.Semantics` -- TypeChecker, ScopeResolver, MemoryCalculator, StructSizeCalculator

**Namespace layout within Cork.CodeGen:**
- `Cork.CodeGen.Ir` -- IR instructions, basic blocks, control flow graph
- `Cork.CodeGen.Emit` -- 6510 instruction encoder, register allocator
- `Cork.CodeGen.Layout` -- Memory map planner, scene packer
- `Cork.CodeGen.Cycles` -- Cycle estimator

**Namespace layout within Cork.Output:**
- `Cork.Output.Prg` -- PRG file writer (BASIC stub + binary)
- `Cork.Output.D64` -- D64 disk image builder
- `Cork.Output.Crt` -- CRT cartridge image builder

Each project targets `net10.0` with `<PublishAot>true</PublishAot>` in the `.csproj`. The main `Cork.Compiler` project is a console app; the others are class libraries.

---

### 2. Implementation Phases

**Phase 1 -- Minimal Viable Compiler (MVP)**

Goal: Compile a trivial Cork program to a working .prg that runs in VICE.

Milestone program:
```cork
entry scene Hello {
    hardware {
        border: Color.black;
        background: Color.black;
    }

    enter {
        byte i = 0;
        while (i < 11) {
            poke: (0x0400 + i) value: helloData[i];
            i += 1;
        }
    }

    frame {
    }
}

const byte[11] helloData = { 8, 5, 12, 12, 15, 32, 23, 15, 18, 12, 4 };
```

What Phase 1 builds:
- Lexer (all token types)
- Parser (scenes, hardware blocks, enter/frame/exit, variable declarations, const arrays, while loops, if/else, assignments, basic expressions, poke intrinsic)
- Minimal AST
- Minimal semantic analysis (type checking primitives, scope resolution within a scene)
- Direct 6510 code emission (no IR yet -- straight to bytes)
- PRG output with BASIC stub
- Memory layout: fixed addresses (code at $0810, data after code)
- No structs, no message passing yet

**Phase 2 -- Message Passing and Functions**

Milestone program:
```cork
entry scene Main {
    hardware {
        border: Color.blue;
        background: Color.blue;
    }

    byte playerX = 160;

    enter {
        initScreen:;
    }

    frame {
        if (joystick.port2.left)  { playerX -= 1; }
        if (joystick.port2.right) { playerX += 1; }
        drawPlayer: playerX;
    }

    initScreen: {
        // clear screen
    }

    drawPlayer: (byte x) {
        poke: 0x0400 + 12 * 40 + x value: 42;
    }
}
```

What Phase 2 adds:
- Scene-local method declarations and calls (message-passing syntax with colons)
- No-arg calls (`initScreen:;`)
- Single-argument calls (`drawPlayer: playerX;`)
- Multi-segment calls (`moveTo: x y: y;`)
- Joystick input (reading CIA registers)
- Frame loop (main loop with VSync wait)
- Hardware block codegen (writing VIC-II registers)
- For loops, for-each loops

**Phase 3 -- Types, Enums, Multiple Scenes**

Milestone program:
```cork
enum Direction : byte {
    up = 0, down = 1, left = 2, right = 3
}

word score = 0;

entry scene Title {
    enter { /* draw title */ }
    frame {
        if (joystick.port2.fire) { go Game; }
    }
}

scene Game {
    byte playerX = 160;
    byte playerY = 140;

    frame {
        // movement, scoring
    }
}
```

What Phase 3 adds:
- Enum declarations (standard and flags)
- Multiple scenes with `go` transitions
- Global variables shared across scenes
- Scene loading/unloading codegen
- Memory validation (global + scene fits in 64KB)
- Switch statements
- Word (16-bit) arithmetic codegen
- Fixed-point type support

**Phase 4 -- Structs**

Milestone program:
```cork
struct Bullet {
    byte x = 0;
    byte y = 0;
    bool active = false;

    update: {
        if (active) {
            y -= 2;
            if (y < 5) { active = false; }
        }
    }
}

entry scene Game {
    Bullet[4] bullets;

    frame {
        for (b in bullets) {
            b update:;
        }
    }
}
```

What Phase 4 adds:
- Struct declarations (fields with defaults, methods)
- Struct initializer syntax: `Bullet { x = 100, y = 50, active = true }`
- Struct-of-arrays memory layout for arrays
- Field access via dot syntax
- `this` reference in struct methods
- Struct arrays
- All dispatch is static (no vtables, no indirection)
- Composition (structs containing other structs)
- `var` type inference

**Phase 5 -- Hardware SDK**

What Phase 5 adds:
- Sprite declarations (declarative blocks mapping to VIC-II registers)
- Sprite collision detection intrinsics
- Raster interrupt blocks (compiler-generated IRQ chain)
- Music/sound imports and playback (SID player integration)
- Charset imports and VIC-II bank configuration
- Cycle budget estimation for frame blocks

**Phase 6 -- Output Formats and Polish**

What Phase 6 adds:
- D64 disk image generation (for multi-scene programs that need scene loading from disk)
- CRT cartridge image generation
- Comprehensive error messages with source locations
- Compiler warnings (unused variables, missing switch cases, etc.)
- Optimization passes (constant folding, dead code elimination, strength reduction)

---

### 3. Lexer Design

The lexer is a hand-written single-pass scanner. It produces a flat `Token[]` array (or `ImmutableArray<Token>`). No generator tools -- this keeps AOT compatibility simple and gives full control over the colon-handling logic.

**Token structure:**

```csharp
public readonly record struct Token(
    TokenKind Kind,
    ReadOnlyMemory<char> Text,
    SourceLocation Location,
    object? LiteralValue  // int, fixed-point value, string, bool, null
);

public readonly record struct SourceLocation(
    string FilePath,
    int Line,
    int Column,
    int Offset,
    int Length
);
```

Note: The `object?` for `LiteralValue` boxes value types on the heap. For AOT, this is fine -- `object` boxing is fully supported. However, if profiling shows this is a hotspot, it can be replaced with a discriminated union struct later. In phase 1, keep it simple.

**Token kinds (partial list of the key ones):**

```
// Literals
IntegerLiteral, FixedLiteral, StringLiteral, TrueLiteral, FalseLiteral

// Identifiers and keywords
Identifier,
// Type keywords
ByteKw, SbyteKw, WordKw, SwordKw, BoolKw, FixedKw, StringKw, VarKw,
// Hardware type keywords
SpriteKw, CharsetKw, MusicKw, SoundKw, TilemapKw,
// Declaration keywords
ClassKw, AbstractKw, InterfaceKw, EnumKw, FlagsKw, SceneKw, EntryKw,
PublicKw, PrivateKw, ProtectedKw, ConstKw, ImportKw, NewKw, CtorKw,
// Control flow
IfKw, ElseKw, WhileKw, ForKw, InKw, SwitchKw, CaseKw, DefaultKw,
FallthroughKw, BreakKw, ReturnKw, GoKw,
// Scene lifecycle
HardwareKw, EnterKw, FrameKw, RelaxedKw, RasterKw, ExitKw,
// Special
ThisKw, ValueKw,

// Punctuation
Semicolon, Comma, Dot,
OpenParen, CloseParen, OpenBrace, CloseBrace, OpenBracket, CloseBracket,

// Operators
Plus, Minus, Star, Slash, Percent,
Ampersand, Pipe, Caret, Tilde,
Bang, AmpAmp, PipePipe,
Less, Greater, LessEqual, GreaterEqual, EqualEqual, BangEqual,
ShiftLeft, ShiftRight,
Equal, PlusEqual, MinusEqual, StarEqual, SlashEqual, PercentEqual,
AmpEqual, PipeEqual, CaretEqual, ShiftLeftEqual, ShiftRightEqual,

// The critical one
Colon,

// Special
Eof
```

**Handling the colon syntax:**

The colon is just `Colon` at the lexer level. The lexer does NOT try to distinguish "selector colon" from "enum backing type colon" or "switch case colon" -- that is the parser's job. The lexer emits a plain `Colon` token every time it sees `:`.

Cork has no nullable types or null-conditional operators, so `?` is not a valid token.

**Handling `selector:` (identifier immediately before colon):**

The parser, not the lexer, determines whether an `Identifier Colon` sequence is a selector. The lexer keeps them as two separate tokens. This is clean and simple -- the grammar is designed so the parser can always tell from context.

**Numeric literal handling:**

- Decimal: sequence of digits and underscores
- Hex: `0x` followed by hex digits and underscores
- Fixed-point: digits, `.`, digits (with optional underscores). The lexer distinguishes this from an integer followed by a dot-access by requiring at least one digit after the dot. `5.` is integer `5` then `Dot`; `5.0` is `FixedLiteral`.

**Comment handling:**

The lexer strips `//` (to end of line) and `/* ... */` (with nesting support) during scanning. Comments are not emitted as tokens.

---

### 4. Parser Design

The parser is a **hand-written recursive descent parser**. This is the right choice for Cork because:

1. The grammar is LL(1) in most cases, LL(2) in a few (distinguishing `variable_declaration` from `expression_statement` or `message_send_statement` requires lookahead).
2. Recursive descent gives excellent error messages with precise source locations.
3. No dependency on parser generators (ANTLR, etc.), which may have AOT issues.
4. Full control over the tricky colon disambiguation logic.

**Key parsing challenges and solutions:**

**Challenge 1: Distinguishing statements that start with an identifier.**

When the parser sees an identifier at statement position, it could be:
- A variable declaration: `byte x = 5;` or `Enemy target;`
- An assignment: `x = 5;` or `enemy.health -= 1;`
- A message send: `enemy update:;` or `enemy moveTo: 100 y: 50;`
- An expression statement: `someFunction:;` (though this is also a message send)

**Resolution strategy:** The parser uses lookahead:
1. If the current token is a type keyword (`byte`, `word`, `var`, `const`, etc.), parse a variable declaration.
2. If the current token is an identifier followed by `[` (like `Enemy[8]`), it is an array type declaration.
3. If the current token is an identifier: speculatively parse as an expression. After parsing the primary/postfix expression, check the next token:
   - If `Identifier Colon` follows (or just `Colon` for the receiver), this is a message send.
   - If `=` or a compound assignment operator follows, this is an assignment.
   - If `;` follows, this is an expression statement.
   - If the identifier could be a user-defined type name (uppercase by convention, or tracked in a symbol table from a prior pass), treat it as a type for a variable declaration.

Actually, the cleaner approach: **two-pass name resolution**. In the first pass (or as the parser encounters class/enum declarations), register type names. Then when parsing statements, if an identifier is a known type name followed by another identifier, it is a declaration. This mirrors how C# compilers handle the same ambiguity.

However, for phase 1, a simpler heuristic works: type keywords (`byte`, `word`, `bool`, etc.) are always unambiguous. User-defined types start with uppercase (by strong convention). The parser can use `IsUpperCase(identifier)` as a heuristic for phase 1 and replace it with proper name resolution later.

**Challenge 2: Parsing message sends.**

At statement level, after parsing the receiver expression, the parser checks if the next token sequence matches `Identifier Colon`. If so, it is a message send. The parser then collects selector segments:

```
receiver selector1: arg1 selector2: arg2 ;
```

Each selector segment is `Identifier Colon [argument]`. The first segment's argument is optional (for no-arg calls). Subsequent segments always have arguments.

An argument is parsed at unary-expression precedence (as specified in the grammar's `message_argument` production). This means `enemy takeDamage: weapon.power + bonus;` is ambiguous -- is `bonus` part of the expression or the start of a new selector? The grammar resolves this: message arguments are `unary_expression`, so the `+` is NOT consumed. The developer must write `enemy takeDamage: (weapon.power + bonus);` to include the addition. This is exactly how Objective-C works.

**In expression context**, message sends are always wrapped in parentheses: `(enemy distanceFrom: player)`. The parser, upon seeing `(`, tentatively parses the inner content. If after parsing a postfix expression it sees `Identifier Colon`, it switches to message-send parsing. Otherwise, it is a plain parenthesized expression.

**Challenge 3: `new` expressions.**

`new Type:` (no-arg) vs `new Type selector: arg` (named constructor). After `new Identifier`, if the next token is `Colon` alone, it is a no-arg constructor. If it is `Identifier Colon`, it is the start of a named constructor's selector chain.

Wait -- looking at the grammar more carefully: `new_arguments = ":" | message_first_segment { message_additional_segment }`. So `new Enemy:` has just `:` (no selector name). `new Enemy withHealth: 100 atX: 50` starts with `withHealth:` which is a `message_first_selector`. The parser checks: after `new Identifier`, if the next token is `Colon`, it is no-arg. If it is `Identifier Colon`, parse as a named constructor with selector chain.

**Parser structure (key methods):**

```
ParseProgram() -> ProgramNode
ParseTopLevelDeclaration() -> TopLevelNode
ParseSceneDeclaration() -> SceneNode
ParseClassDeclaration() -> ClassNode
ParseEnumDeclaration() -> EnumNode
ParseInterfaceDeclaration() -> InterfaceNode
ParseMethodDeclaration() -> MethodNode
ParseBlock() -> BlockNode
ParseStatement() -> StatementNode
ParseVariableDeclaration() -> VarDeclNode
ParseIfStatement() -> IfNode
ParseWhileStatement() -> WhileNode
ParseForStatement() -> ForNode
ParseSwitchStatement() -> SwitchNode
ParseMessageSendStatement() -> MessageSendNode
ParseAssignment() -> AssignmentNode
ParseExpression() -> ExprNode  (entry: logical_or)
ParseLogicalOr() -> ExprNode
...down through precedence levels...
ParseUnary() -> ExprNode
ParsePostfix() -> ExprNode
ParsePrimary() -> ExprNode
ParseParenthesizedExprOrMessageSend() -> ExprNode
ParseMessageSend(receiver) -> MessageSendExprNode
ParseNewExpression() -> NewExprNode
```

---

### 5. AST Design

The AST uses a sealed class hierarchy with a visitor pattern. All nodes are immutable (readonly properties, set in constructors). Each node carries a `SourceSpan` for error reporting.

```csharp
public abstract class AstNode
{
    public SourceSpan Span { get; }
}

public sealed record SourceSpan(
    string FilePath, int StartLine, int StartCol, int EndLine, int EndCol
);
```

**Top-level nodes:**

```
ProgramNode                -- children: TopLevelDeclaration[]
ImportNode                 -- path: string, isLibrary: bool
GlobalVarDeclNode          -- type, name, initializer
ResourceDeclNode           -- hardwareType, name, importPath
EnumDeclNode               -- name, backingType, isFlags, members[]
StructDeclNode             -- name, fields[], methods[]
SceneDeclNode              -- name, isEntry, members[]
```

**Scene member nodes:**

```
HardwareBlockNode          -- settings: (name, expr)[]
SpriteBlockNode            -- name, settings: (name, expr)[]
SceneVarDeclNode           -- (same as variable decl)
SceneResourceDeclNode      -- (same as resource decl)
EnterBlockNode             -- body: BlockNode
FrameBlockNode             -- isRelaxed: bool, body: BlockNode
RasterBlockNode            -- line: ExprNode, body: BlockNode
ExitBlockNode              -- body: BlockNode
SceneMethodNode            -- (same as method decl)
```

**Struct member nodes:**

```
FieldNode                  -- type, name, initializer?
MethodNode                 -- returnType?, selector: SelectorSegment[], body
```

**Method signature representation:**

```csharp
public sealed class SelectorSegment
{
    public string Name { get; }         // e.g., "moveTo", "y"
    public ParameterNode? Parameter { get; }  // null for no-arg first segment
}

public sealed class ParameterNode
{
    public TypeNode Type { get; }
    public string Name { get; }
}
```

The full method identity (its "selector") is constructed by concatenating segment names with colons: `"moveTo:y:"`.

**Statement nodes:**

```
BlockNode                  -- statements: StatementNode[]
VarDeclStatementNode       -- isConst, type?, name, initializer
AssignmentNode             -- target: LValueNode, op, value: ExprNode
MessageSendStatementNode   -- send: MessageSendExprNode
ExpressionStatementNode    -- expr: ExprNode
IfNode                     -- condition, thenBlock, elseIfClauses[], elseBlock?
WhileNode                  -- condition, body
ForNode                    -- init, condition, step, body
ForEachNode                -- varName, collection: ExprNode, body
SwitchNode                 -- subject, cases[], default?
FallthroughSwitchNode      -- subject, cases[], default?
GoNode                     -- sceneName
ReturnNode                 -- value: ExprNode?
BreakNode
```

**Expression nodes:**

```
IntLiteralExpr             -- value: long, inferredType (byte or word)
FixedLiteralExpr           -- value: double (stored as 8.8 fixed internally)
StringLiteralExpr          -- value: string
BoolLiteralExpr            -- value: bool
ThisExpr
IdentifierExpr             -- name: string
BinaryExpr                 -- left, op, right
UnaryExpr                  -- op, operand
MemberAccessExpr           -- receiver, memberName
IndexExpr                  -- receiver, index
MessageSendExpr            -- receiver, segments: (name, arg?)[]
NewExpr                    -- typeName, segments: (name, arg?)[]  (empty for no-arg)
ArrayInitializerExpr       -- elements: ExprNode[]
ResourceImportExpr         -- path: string
```

**Visitor interface:**

```csharp
public interface IAstVisitor<T>
{
    T VisitProgram(ProgramNode node);
    T VisitScene(SceneDeclNode node);
    T VisitClass(ClassDeclNode node);
    // ... one Visit method per node type
}
```

For void visitors (semantic analysis), use `IAstVisitor<Unit>` or a separate `IAstWalker` base class with virtual methods that default to visiting children.

---

### 6. Semantic Analysis

Semantic analysis is performed as a series of passes over the AST. Each pass is a separate visitor class.

**Pass 1: Symbol Registration (`SymbolRegistrar`)**

Walks the top level of the AST and registers all type names (structs, enums), scene names, and global variables into a global `SymbolTable`. This is needed before any other pass because the parser needs to know what identifiers are types vs variables.

This pass does NOT descend into method bodies. It only registers declarations.

Output: a populated `SymbolTable` with type entries, scene entries, and global variable entries.

**Pass 2: Scope Resolution (`ScopeResolver`)**

Walks the entire AST and builds a scope tree. Each scope (global, scene, class, method, block) has a parent pointer. Variables are registered in their enclosing scope when their declaration is visited.

When an `IdentifierExpr` is visited, the resolver walks up the scope chain to find the binding. If not found, it is an error. The resolver annotates each identifier node with a reference to its declaration (stored in a side table keyed by AST node identity, or as a mutable annotation field).

This pass also resolves:
- `this` references (must be inside a class method)
- `value` references (must be inside a property setter)
- `go` targets (must name a valid scene)
- Enum member access (`Direction.up`)
- Hardware property access (`vic.border`, `joystick.port2.fire`)

**Pass 3: Type Checking (`TypeChecker`)**

Infers and checks types for every expression. Works bottom-up: literals have known types, identifiers get their type from the symbol table, operators have rules (e.g., `byte + byte -> byte`, `byte + word -> word`, `fixed + fixed -> fixed`).

Key type rules:
- Arithmetic on `byte` stays `byte` unless the result might overflow, in which case the compiler widens to `word`. (In practice: `byte + byte` is `byte`, but the developer must cast explicitly if they want `word`. The compiler warns on potential overflow.)
- `fixed` arithmetic: `fixed + fixed -> fixed`, `fixed * byte -> fixed`, etc.
- Comparisons always produce `bool`.
- Message send return types are looked up from the method's declared return type.
- `new` expressions have the type of the class being constructed.
- Array indexing: `byte[N]` indexed by `byte` or `word` produces `byte`.

Type inference for `var`: the initializer's type becomes the variable's type.

**Pass 4: Constant Evaluation (`ConstEvaluator`)**

Evaluates `const` declarations, enum member values, and array size expressions. These must all be compile-time constants. Errors if a const expression references a non-const value or cannot be computed.

**Pass 5: Memory Size Calculation (`MemorySizeCalculator`)**

Calculates the byte size of every type, every global variable, every scene's local data, every class instance, and every resource import. Sums these into:
- `globalSize`: total bytes for global data + code
- `sceneSize[sceneName]`: total bytes for each scene's local data + code

Then validates: for each scene, `globalSize + sceneSize[scene] <= availableMemory`. The available memory is roughly 38-40KB after accounting for zero page, stack, I/O, KERNAL/BASIC ROM areas, screen RAM, and color RAM. The exact number depends on the memory layout strategy (see Section 9).

If the validation fails, emit `CORK001` with a breakdown.

**Pass 6: Struct Size Calculator (`StructSizeCalculator`)**

Calculates the byte size of every struct type, handling nested structs (composition). Validates that no struct has circular composition (struct A contains struct B which contains struct A). This information feeds into the memory size calculator and the struct-of-arrays layout generator.

---

### 7. Intermediate Representation

The compiler uses a simple IR between semantic analysis and 6510 code emission. Going directly from AST to machine code is feasible for Phase 1 but becomes unmanageable when adding optimizations.

The IR is a **three-address code in SSA-like form**, organized into basic blocks:

```csharp
public sealed class IrFunction
{
    public string Name { get; }
    public List<IrBasicBlock> Blocks { get; }
    public List<IrLocal> Locals { get; }
}

public sealed class IrBasicBlock
{
    public int Id { get; }
    public List<IrInstruction> Instructions { get; }
    public IrTerminator Terminator { get; }  // branch, jump, return
}
```

**IR instruction types:**

```
// Arithmetic
IrAdd(dest, left, right)      -- dest = left + right
IrSub(dest, left, right)
IrMul(dest, left, right)      -- expands to subroutine call or shift sequence
IrDiv(dest, left, right)
IrMod(dest, left, right)
IrNeg(dest, src)
IrAnd(dest, left, right)
IrOr(dest, left, right)
IrXor(dest, left, right)
IrNot(dest, src)
IrShiftLeft(dest, src, amount)
IrShiftRight(dest, src, amount)

// Comparison (sets a bool result)
IrCompareEq(dest, left, right)
IrCompareLt(dest, left, right)
// ... etc

// Data movement
IrLoadConst(dest, value)
IrLoadGlobal(dest, globalSymbol)
IrStoreGlobal(globalSymbol, src)
IrLoadLocal(dest, localIndex)
IrStoreLocal(localIndex, src)
IrLoadField(dest, objectRef, fieldOffset)
IrStoreField(objectRef, fieldOffset, src)
IrLoadIndex(dest, arrayBase, index)
IrStoreIndex(arrayBase, index, src)
IrLoadHardwareReg(dest, address)     -- e.g., reading $D012
IrStoreHardwareReg(address, src)     -- e.g., writing $D020

// Calls (all static dispatch — no vtables)
IrCall(dest?, selectorName, receiver?, args[])

// Object access (all statically allocated, no heap)
IrObjectSlotAddress(dest, arrayBase, index, classSize)

// Terminators
IrBranch(condition, trueBlock, falseBlock)
IrJump(targetBlock)
IrReturn(value?)
IrSceneTransition(sceneName)
```

Each `IrValue` (dest, src, left, right) is either:
- A temporary (SSA-like, `t0`, `t1`, ...)
- A constant
- A reference to a local variable slot

The IR uses `byte` and `word` as its two primary data widths, matching the 6510. `fixed` is `word` at the IR level with a flag indicating fixed-point semantics (so the code generator knows to shift after multiply).

**When to introduce the IR:**

Phase 1 skips the IR and goes AST-to-machine-code directly. The IR is introduced in Phase 3 or 4, when the complexity justifies it. The key trigger is when optimization passes (constant folding, dead code elimination, common subexpression elimination) become necessary.

---

### 8. Code Generation

The 6510 code generator translates IR (or AST in Phase 1) into a stream of bytes representing 6510 machine code.

**6510 instruction encoding:**

The code generator maintains an `AssemblyBuffer` -- a growable byte array with a current address pointer. It provides methods like:

```csharp
public void EmitLda(AddressingMode mode, ushort operand);
public void EmitSta(AddressingMode mode, ushort operand);
public void EmitJsr(ushort address);
public void EmitJmp(ushort address);
// ... one method per 6510 instruction
```

Internally, each method writes the opcode byte and operand bytes. The `AssemblyBuffer` tracks the current address so forward references (branches, jumps) can be patched later.

**Register allocation on a 3-register CPU:**

The 6510 has three registers: A (accumulator, 8-bit), X (index, 8-bit), Y (index, 8-bit). There is no general-purpose register file. The strategy:

1. **A is the primary computation register.** All arithmetic, comparisons, and data movement flows through A.
2. **X and Y are index registers.** Used for array indexing, loop counters, and as temporary storage when A is busy.
3. **Zero page is the register file.** The 6510's zero-page addressing modes are fast (3 cycles for load/store vs 4 for absolute). The compiler reserves a block of zero-page addresses (e.g., $02-$7F, avoiding KERNAL/BASIC workspace if ROMs are banked out) as "virtual registers." Frequently-used local variables and temporaries are assigned zero-page slots.
4. **Stack is for overflow.** When zero-page slots are exhausted, temporaries go on the hardware stack (PHA/PLA) or in absolute RAM.

**Register allocation algorithm (simple, practical for 8-bit):**

For each IR basic block:
1. Perform a linear scan of live ranges for temporaries.
2. Assign the most-used temporary to A where possible.
3. Assign loop counters to X or Y.
4. Assign remaining temporaries to zero-page slots.
5. Spill to RAM if zero-page is full.

This is a simplified version of linear scan allocation, appropriate for the small number of registers and the relatively simple code patterns.

**Handling 16-bit (word) operations:**

The 6510 is 8-bit, so 16-bit operations require two instructions. For example, `word a = b + c`:

```asm
    LDA b_lo
    CLC
    ADC c_lo
    STA a_lo
    LDA b_hi
    ADC c_hi
    STA a_hi
```

The code generator tracks which values are byte vs word and emits the appropriate instruction sequences.

**Handling multiplication and division:**

No hardware multiply/divide. Options:
- **Constant multiplies**: strength-reduced to shifts and adds. `x * 10` becomes `(x << 3) + (x << 1)`.
- **Variable multiplies**: call a runtime library subroutine. For `byte * byte -> word`, use a 256-byte lookup table or shift-and-add loop (see research/math-and-algorithms.md).
- **Division**: similarly delegated to a runtime subroutine.

**Struct code generation:**

Structs have no indirection. Each field has a known offset. Method calls are compiled as direct `JSR` to the method's address, with the struct's base address passed via a zero-page pointer. All dispatch is static — no vtables, no virtual calls.

**Single struct layout:**

```
For struct Enemy { byte health; fixed x; fixed y; bool active; }
  offset 0: health (1 byte)
  offset 1: x_lo (1 byte)
  offset 2: x_hi (1 byte)
  offset 3: y_lo (1 byte)
  offset 4: y_hi (1 byte)
  offset 5: active (1 byte)
  Total: 6 bytes per instance
```

**Struct-of-arrays layout for arrays:**

For `Enemy[8]`, the compiler uses struct-of-arrays -- storing all `health` bytes contiguously, all `x` values contiguously, etc. This allows fast indexed addressing:

```asm
    ; enemies[i].health where i is in X register
    LDA enemy_health,X     ; 4 cycles, no pointer indirection
    
    ; vs array-of-structs which would require:
    ;   multiply i by struct size, add base, use indirect addressing
    ;   ~15-20 cycles
```

Struct-of-arrays is strictly better for the 6510's indexed addressing modes and dominates C64 game programming patterns (iterating all enemies, all bullets, etc.).

**Struct method calls:**

Methods receive the struct's address (or array index for array elements) via a zero-page register. The method body accesses fields relative to this:

```asm
    ; enemy update:;  where enemy is at known static address
    LDA #<enemy_base
    STA zp_this_lo
    LDA #>enemy_base
    STA zp_this_hi
    JSR Enemy_update        ; direct call, no indirection

    ; enemies[i] update:;  where i is in X
    STX zp_array_index
    JSR Enemy_update_indexed ; method uses X to index struct-of-arrays
```

**Struct initializer codegen:**

`var e = Enemy { health = 10, x = 100 };` generates code only for non-default values:

```asm
    ; health defaults to 3, but we want 10
    LDA #10
    STA enemy_health
    ; x defaults to 0, but we want 100
    LDA #100
    STA enemy_x_lo
    LDA #0
    STA enemy_x_hi
    ; y and active keep their defaults (0/false) — no code emitted
```

**Static ownership (no reference counting):**

Cork uses fully static ownership. No reference counting, no heap allocator, no runtime overhead for memory management. Every object lives in one of three places:
- **Global scope** — lifetime is the entire program
- **Scene scope** — lifetime is while the scene is active
- **Array slot** — lifetime is the lifetime of the owning array

References to objects are borrows. The compiler's ownership analyzer (a semantic analysis pass) proves at compile time that no reference outlives its owner. If it can't prove this, it emits a compile error. This eliminates all runtime memory management overhead — no refcount increments, no decrements, no free lists, no heap fragmentation.

---

### 9. Memory Layout

The compiler owns the entire 64KB address space and must decide where everything goes. The standard configuration banks out BASIC ROM (gaining $A000-$BFFF as usable RAM) and keeps KERNAL ROM (needed for I/O, especially disk access for scene loading) and I/O registers.

**Memory map for a Cork program (processor port $01 = $36: BASIC ROM out, KERNAL + I/O in):**

```
$0000-$0001  Processor port (untouchable)
$0002-$007F  Zero-page workspace: compiler's virtual registers, pointers
$0080-$00FF  Zero-page workspace: available for user code if needed
$0100-$01FF  Hardware stack
$0200-$03FF  KERNAL/BASIC workspace (can be reclaimed if KERNAL banked out)
$0400-$07FF  Screen RAM (1KB) -- can be relocated if needed
$0800-$0802  Reserved (BASIC needs these bytes even for the SYS stub)
$0801-$080F  BASIC stub (SYS entry point) -- ~14 bytes
$0810-$9FFF  Main program area: code + data (~38KB)
$A000-$BFFF  Freed BASIC ROM space: code or data overflow (8KB)
$C000-$CFFF  Upper RAM (4KB) -- often used for music player or scene data
$D000-$DFFF  I/O registers (VIC-II, SID, Color RAM, CIA) -- not usable for data
$D800-$DBFF  Color RAM (1KB, within I/O space)
$E000-$FFFF  KERNAL ROM (kept for I/O routines)
```

Usable RAM: approximately $0810-$9FFF + $A000-$BFFF + $C000-$CFFF = ~47KB.

**The compiler's memory planner (`MemoryPlanner`):**

The planner works in this order:

1. **Assign fixed-address resources first**: screen RAM ($0400 or relocated), charset data (must be at a 2KB boundary within the VIC bank), sprite data (must be at 64-byte boundaries), bitmap data ($2000 within the VIC bank).

2. **Assign global data**: global variables, const arrays, resource imports (music files, charset binaries). These are always resident.

3. **Assign the runtime library**: refcount helpers, multiply/divide routines, IRQ dispatcher. These are always resident. Placed at a fixed known location (e.g., $C000 or end of program area).

4. **Assign per-scene data**: for each scene, calculate its local variables, local resources, and local code. These go into a "scene slot" -- a region that is reused across scenes. The scene slot starts after global data. On scene transition, the new scene's data is either:
   - Already in RAM (if the program is single-scene or all scenes fit simultaneously)
   - Loaded from disk (for multi-scene programs where scenes are too large to coexist)

5. **Validate**: `global_size + max(scene_size[i]) <= total_available_ram`. If not, emit `CORK001`.

**Scene packing algorithm:**

For single-scene programs (or programs where all scenes fit): just place everything sequentially. No disk loading needed, emit as .prg.

For multi-scene programs: the compiler determines which scenes can be co-resident and which must be loaded from disk. Two approaches:
- **Greedy bin-packing**: sort scenes by size descending, pack as many as possible into RAM alongside globals.
- **Simple overlay**: one scene at a time, loaded from disk on `go`. The "scene slot" is a fixed region; each scene's data is overlaid into it.

For Phase 3, use the simple overlay approach. The scene data is stored as separate PRG segments on a D64 disk image. The `go` statement compiles to a call to the runtime's scene loader, which reads the new scene's data from disk into the scene slot, then jumps to the new scene's `enter` block.

---

### 10. Cycle Estimation

The cycle estimator calculates the worst-case CPU cycle cost of a `frame` block. This is a static analysis performed after code generation (or on the IR, if available).

**How it works:**

1. Walk the generated code for the `frame` block instruction by instruction.
2. For each 6510 instruction, look up its cycle cost from a table (the table in Section 2.2 of the timing research).
3. For branches: take the worse of the two paths (branch taken vs not taken). For loops: multiply the body cost by the maximum iteration count (which must be determinable at compile time, or use a conservative estimate).
4. For subroutine calls (`JSR`): recursively estimate the called function's cost.
5. For virtual dispatch calls: use the worst-case across all possible targets.

**Budget calculation:**

The available budget depends on:
- **Video standard**: PAL (19,656 cycles/frame) or NTSC (17,095 cycles/frame). Default to PAL; allow a compiler flag for NTSC.
- **Badline overhead**: 25 badlines per frame in standard text/bitmap mode, each stealing ~40 cycles = 1,000 cycles.
- **Sprite DMA**: each active sprite steals ~2 cycles per rasterline it spans (21 lines for unexpanded). With 8 sprites: ~336 cycles for the lines where sprites are visible. (This is approximate; exact calculation depends on sprite positions.)
- **Raster handler overhead**: the compiler knows exactly what code runs in each `raster` block. Sum their cycle costs and subtract from the budget.
- **IRQ dispatch overhead**: entering and leaving an IRQ handler costs ~40-50 cycles (push registers, acknowledge IRQ, restore registers, RTI).

Formula:
```
available = cycles_per_frame
          - badline_stolen_cycles
          - sprite_dma_cycles
          - sum(raster_handler_cycles + irq_overhead) for each raster block
          - frame_loop_overhead (vsync wait, pointer updates, etc.)
```

If `estimated_frame_cost > available`:
- For `frame`: emit `CORK002` (error).
- For `relaxed frame`: emit `CORK003` (warning).

**Conservative estimates for loops:**

For `for (var i = 0; i < enemies.length; i++)` where `enemies` is `Enemy[8]`, the iteration count is 8. The estimator multiplies the loop body cost by 8.

For `while` loops with non-constant bounds, the estimator either:
- Requires the developer to annotate a maximum (future feature), or
- Uses a conservative fixed estimate (e.g., 256 iterations for a byte counter), or
- Flags a warning that the loop cost cannot be estimated.

---

### 11. Output Formats

**PRG format (`PrgWriter`):**

The simplest output. Structure:
1. Two-byte load address header: `$01 $08` (little-endian $0801).
2. BASIC stub: a single line `10 SYS 2061` (or whatever the ML entry address is), terminated by null bytes.
3. Machine code and data, starting at the SYS target address.

The compiler builds this as:
```csharp
var output = new List<byte>();
output.Add(0x01); output.Add(0x08);  // load address
output.AddRange(GenerateBasicStub(entryAddress));
output.AddRange(machineCode);
output.AddRange(data);
File.WriteAllBytes(outputPath, output.ToArray());
```

The BASIC stub generation follows the exact byte pattern from the file-formats research: next-line pointer, line number, SYS token, space, address as PETSCII digits, null terminators.

**D64 format (`D64Writer`):**

For multi-scene programs. The writer:
1. Creates a blank 174,848-byte D64 image (683 sectors of 256 bytes).
2. Initializes the BAM on track 18, sector 0.
3. Writes the main program as a PRG file in the directory (the bootloader + global data + first scene).
4. Writes each additional scene as a separate PRG file (or SEQ data file) on the disk.
5. The scene loader in the runtime reads these files using KERNAL disk I/O routines.

The D64 writer needs to implement:
- Sector allocation (find free sectors via BAM)
- File chain writing (each file is a linked list of sectors; first two bytes of each sector are track/sector of next sector)
- Directory entry creation
- BAM update

This is moderately complex but well-documented in the file-formats research. The implementation can be a straightforward port of the documented format.

**CRT format (`CrtWriter`):**

For cartridge output. The writer:
1. Writes the 64-byte CRT header (signature, version, hardware type, EXROM/GAME line states, cartridge name).
2. Writes one or more CHIP packets containing the ROM image.
3. For a simple 8K cartridge (type 0): one CHIP packet at $8000, with the CBM80 signature at $8004-$8008, cold-start vector at $8000-$8001.
4. For a 16K cartridge: two CHIP packets or one 16K packet at $8000.

Cartridge programs do not use BASIC stubs. The entry point is the cold-start vector. The compiler generates different startup code for cartridge mode (no KERNAL initialization, direct hardware setup).

For programs larger than 16KB, the compiler can target EasyFlash (type 32) cartridges with bank switching, but this is a stretch goal.

---

### 12. Runtime Library

The runtime is a small set of 6510 assembly routines embedded in the compiler as byte arrays (or as assembly source compiled at build time). The compiler includes only the routines each program actually uses (dead-stripping at the routine level).

**Core routines (always included):**

- `rt_init`: Program initialization. Sets processor port, disables BASIC ROM, clears screen, initializes the IRQ vector.
- `rt_mainloop`: The frame loop. Waits for vsync (raster line $FF or a specific line), calls the current scene's `frame` handler, loops.
- `rt_vsync_wait`: Spin-waits until raster passes line 255 (simple; Phase 1) or uses a raster IRQ to signal frame start (Phase 6).

**Math routines (included when used):**

- `rt_mul8x8`: 8-bit unsigned multiply, result in 16 bits. ~100-150 cycles using shift-and-add, or ~30 cycles with a 512-byte lookup table (quarter-square method).
- `rt_mul16x16`: 16-bit multiply (for `word * word`).
- `rt_div8`: 8-bit unsigned divide.
- `rt_div16`: 16-bit unsigned divide.
- `rt_fixed_mul`: 8.8 fixed-point multiply (multiply as 16-bit, shift right 8).

**No reference counting or heap allocator.** Cork uses fully static ownership. All objects are statically allocated (global, scene-scoped, or array-owned). No runtime memory management code is needed.

**Scene management (included for multi-scene programs):**

- `rt_scene_go`: Load a scene from disk (if needed), call `exit` on current scene, copy scene data, call `enter` on new scene, update the mainloop's scene pointer.
- `rt_disk_load`: Load a file from D64 disk using KERNAL LOAD routine (or a fast loader for better performance).

**IRQ management (included when raster blocks are used):**

- `rt_irq_init`: Set up the IRQ vector at $FFFE/$FFFF (or $0314/$0315 if using KERNAL), disable CIA timer interrupts, enable VIC-II raster interrupts.
- `rt_irq_dispatch`: The master IRQ handler. Acknowledges the VIC-II interrupt, reads $D012 to determine which raster handler to call, chains to the next raster line.

The compiler generates the raster handler chain statically: it knows all raster lines and their handlers at compile time. The IRQ dispatcher is a sequence of compare-and-branch instructions:

```asm
irq_handler:
    PHA
    TXA
    PHA
    TYA
    PHA
    LDA $D019
    STA $D019        ; acknowledge
    LDA $D012
    CMP #raster_line_1
    BEQ .handler_1
    CMP #raster_line_2
    BEQ .handler_2
    ; ... etc
    JMP .done
.handler_1:
    ; user's raster block code
    LDA #raster_line_2   ; set next raster trigger
    STA $D012
    JMP .done
.handler_2:
    ; user's raster block code
    LDA #raster_line_1   ; wrap to first
    STA $D012
.done:
    PLA
    TAY
    PLA
    TAX
    PLA
    RTI
```

**Input routines:**

- `rt_joy_read`: Read joystick port 1 or 2 (CIA #1 port A/B, addresses $DC00/$DC01). Returns a byte with direction and fire bits.
- `rt_key_read`: Read the keyboard matrix (CIA #1, scanning rows via $DC00 and reading columns via $DC01).

---

### 13. Testing Strategy

**Unit tests (Cork.Language.Tests):**

- **Lexer tests**: Feed source strings, assert token sequences. Cover every token type, edge cases (hex literals, fixed-point literals, underscores in numbers, string escapes, comment stripping, `?.` vs `?` + `.`).
- **Parser tests**: Feed token sequences (or source strings through the lexer), assert AST structure. One test per grammar production. Test error recovery for malformed input.
- **Semantic analysis tests**: Feed ASTs, assert that type checking passes or produces specific errors. Test scope resolution (variable not found, duplicate declaration). Test const evaluation. Test struct size calculation and composition validation.

**Unit tests (Cork.CodeGen.Tests):**

- **Instruction encoding tests**: Assert that `EmitLda(Immediate, 0x42)` produces bytes `$A9 $42`. Cover all addressing modes for all instructions.
- **Code pattern tests**: For small IR snippets (e.g., `byte a = b + c`), assert the generated 6510 instruction sequence is correct.
- **Memory layout tests**: Assert that the memory planner places resources at valid addresses (charsets at 2KB boundaries, sprites at 64-byte boundaries, total fits in RAM).
- **Cycle estimation tests**: For known code sequences, assert the estimated cycle count matches the known correct value.

**Integration tests (Cork.Integration.Tests):**

These compile a Cork source file to a .prg, load it into VICE (the C64 emulator), run it, and verify the result.

VICE supports a headless mode and scripting: `x64sc -limitcycles N -exitscreenshot output.png +sound program.prg`. The test can:
1. Compile the Cork source to a .prg in a temp directory.
2. Run VICE with the .prg, limited to a fixed number of cycles (e.g., 5 million, roughly 250 frames).
3. Read specific C64 memory locations via VICE's binary monitor interface or by having the Cork program write results to known addresses.
4. Assert the memory values match expected results.

For example, a test program that computes `3 + 4` and stores the result at $C000:
```cork
entry scene Test {
    enter {
        byte result = 3 + 4;
        poke: 0xC000 value: result;
    }
    frame { }
}
```
The test compiles this, runs VICE, reads $C000, asserts it equals 7.

VICE's `-remotemonitor` option allows connecting via TCP to read/write memory programmatically.

**Snapshot testing for code generation:**

For regression testing, store the expected 6510 disassembly output for each test program. When the code generator changes, the test shows a diff of the generated assembly. This catches unintentional regressions.

**Golden file testing for error messages:**

Store the expected compiler output (errors, warnings) for programs designed to trigger specific diagnostics. Assert the output matches exactly. This ensures error messages remain clear and stable.

---

### 14. AOT Compatibility

.NET 10 AOT compilation has specific restrictions. The Cork compiler must avoid:

1. **`System.Reflection.Emit`**: No dynamic code generation. The compiler does not need this anyway -- it generates 6510 bytes, not .NET IL.

2. **Unconstrained `Type.MakeGenericType` / `MethodInfo.MakeGenericMethod`**: These require runtime code generation. Avoid generic patterns that would trigger this. Closed generic types (e.g., `Dictionary<string, SymbolEntry>`) are fine.

3. **`System.Linq.Expressions` compilation**: `Expression.Compile()` requires the JIT. Avoid entirely.

4. **Assembly loading at runtime**: `Assembly.Load`, `Assembly.LoadFrom`. Not needed.

5. **Serialization via reflection**: `System.Text.Json` with source generators is AOT-safe. `JsonSerializer.Serialize<T>()` with `[JsonSerializable]` context works. Avoid the reflection-based serializer.

6. **`dynamic` keyword**: Uses the DLR, not AOT-compatible. Do not use.

**What IS safe and should be used freely:**

- All standard collections (`List<T>`, `Dictionary<K,V>`, `ImmutableArray<T>`, `ReadOnlySpan<T>`, etc.) with concrete type arguments.
- Pattern matching (`switch` expressions with `is` patterns).
- `Span<T>`, `Memory<T>`, `ReadOnlySpan<T>` for high-performance buffer manipulation.
- Records and record structs.
- Interfaces and virtual dispatch in the compiler's own code (this is .NET virtual dispatch, fully supported by AOT).
- `System.IO` for file operations.
- `System.CommandLine` for CLI argument parsing (AOT-compatible since .NET 8).
- Source generators for any code generation needed in the compiler itself.

**Project configuration:**

Each `.csproj` should include:
```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

The main executable project additionally:
```xml
<PropertyGroup>
    <PublishAot>true</PublishAot>
</PropertyGroup>
```

Enable the `ILLink` trimming analyzer and AOT analyzer as warnings-as-errors during CI to catch regressions:
```xml
<PropertyGroup>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <EnableAotAnalyzer>true</EnableAotAnalyzer>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

---

### Phase-by-Phase Milestone Summary

| Phase | Target Program | Key Deliverables |
|-------|---------------|------------------|
| 1 | "HELLO WORLD" on screen (poke characters) | Lexer, Parser (subset), AST, direct codegen, PRG output |
| 2 | Player moves with joystick | Message-passing calls, scene methods, input, frame loop |
| 3 | Title screen -> Game -> Game Over flow | Enums, multiple scenes, `go`, 16-bit math, memory validation |
| 4 | Struct-based game entities | Structs, fields, methods, struct-of-arrays layout, composition |
| 5 | Full game with sprites, music, raster splits | Hardware SDK, sprite codegen, SID, raster IRQs, cycle estimation |
| 6 | Polished multi-scene game on D64 | D64/CRT output, optimizations, full diagnostics |

---

### Critical Files for Implementation

- `/Users/js/c64-gcode/design/grammar.ebnf` -- The formal grammar that the parser must implement production-by-production. Every parsing method maps to a production rule in this file.
- `/Users/js/c64-gcode/design/language-design.md` -- The language specification that drives semantic analysis rules (type system, memory management, scene lifecycle).
- `/Users/js/c64-gcode/research/6510-cpu.md` -- Instruction set reference needed to build the 6510 instruction encoder and cycle cost tables.
- `/Users/js/c64-gcode/research/file-formats.md` -- PRG, D64, and CRT format specifications needed to implement all three output writers.
- `/Users/js/c64-gcode/research/timing-and-optimization.md` -- Cycle counting rules and VIC-II stolen cycle data needed for the frame budget estimator.