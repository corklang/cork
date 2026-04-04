using Cork.Language.Ast;
using Cork.Language.Lexing;
using Cork.Language.Parsing;

namespace Cork.Language.Tests.Parsing;

public class ParserTests
{
    private static ProgramNode Parse(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        return parser.ParseProgram();
    }

    // --- Scene declarations ---

    [Test]
    public async Task Parses_entry_scene()
    {
        var program = Parse("entry scene Main { frame { } }");
        await Assert.That(program.Declarations).Count().IsEqualTo(1);
        var scene = program.Declarations[0] as SceneNode;
        await Assert.That(scene).IsNotNull();
        await Assert.That(scene!.Name).IsEqualTo("Main");
        await Assert.That(scene.IsEntry).IsTrue();
    }

    [Test]
    public async Task Parses_non_entry_scene()
    {
        var program = Parse("scene GameOver { frame { } }");
        var scene = program.Declarations[0] as SceneNode;
        await Assert.That(scene).IsNotNull();
        await Assert.That(scene!.Name).IsEqualTo("GameOver");
        await Assert.That(scene.IsEntry).IsFalse();
    }

    // --- Struct ---

    [Test]
    public async Task Parses_struct_with_fields_and_methods()
    {
        var program = Parse(@"
            struct Point {
                byte x = 0;
                byte y = 0;
                reset: {
                    x = 0;
                    y = 0;
                }
            }
            entry scene Main { frame { } }
        ");
        var structDecl = program.Declarations[0] as StructDeclNode;
        await Assert.That(structDecl).IsNotNull();
        await Assert.That(structDecl!.Name).IsEqualTo("Point");
        await Assert.That(structDecl.Fields).Count().IsEqualTo(2);
        await Assert.That(structDecl.Fields[0].Name).IsEqualTo("x");
        await Assert.That(structDecl.Fields[1].Name).IsEqualTo("y");
        await Assert.That(structDecl.Methods).Count().IsEqualTo(1);
    }

    // --- Enum ---

    [Test]
    public async Task Parses_enum_declaration()
    {
        var program = Parse(@"
            enum Direction : byte {
                Up = 0,
                Down = 1,
                Left = 2,
                Right = 3
            }
            entry scene Main { frame { } }
        ");
        var enumDecl = program.Declarations[0] as EnumDeclNode;
        await Assert.That(enumDecl).IsNotNull();
        await Assert.That(enumDecl!.Name).IsEqualTo("Direction");
        await Assert.That(enumDecl.BackingType).IsEqualTo("byte");
        await Assert.That(enumDecl.IsFlags).IsFalse();
        await Assert.That(enumDecl.Members).Count().IsEqualTo(4);
        await Assert.That(enumDecl.Members[0].Name).IsEqualTo("Up");
        await Assert.That(enumDecl.Members[0].Value).IsEqualTo(0L);
        await Assert.That(enumDecl.Members[3].Name).IsEqualTo("Right");
        await Assert.That(enumDecl.Members[3].Value).IsEqualTo(3L);
    }

    // --- Hardware block ---

    [Test]
    public async Task Parses_hardware_block()
    {
        var program = Parse(@"
            entry scene Main {
                hardware {
                    border: 0;
                    background: 6;
                }
                frame { }
            }
        ");
        var scene = program.Declarations[0] as SceneNode;
        var hw = scene!.Members[0] as HardwareBlockNode;
        await Assert.That(hw).IsNotNull();
        await Assert.That(hw!.Settings).Count().IsEqualTo(2);
        await Assert.That(hw.Settings[0].Name).IsEqualTo("border");
        await Assert.That(hw.Settings[1].Name).IsEqualTo("background");
    }

    // --- For loop ---

    [Test]
    public async Task Parses_for_loop()
    {
        var program = Parse(@"
            entry scene Main {
                frame {
                    for (byte i = 0; i < 10; i += 1) {
                        poke: 53280 value: i;
                    }
                }
            }
        ");
        var scene = program.Declarations[0] as SceneNode;
        var frame = scene!.Members.OfType<FrameBlockNode>().First();
        var forStmt = frame.Body.Statements[0] as ForStmt;
        await Assert.That(forStmt).IsNotNull();
        await Assert.That(forStmt!.Init).IsTypeOf<VarDeclStmt>();
        await Assert.That(forStmt.Condition).IsTypeOf<BinaryExpr>();
        await Assert.That(forStmt.Step).IsTypeOf<AssignmentStmt>();
    }

    // --- Switch statement ---

    [Test]
    public async Task Parses_switch_statement()
    {
        var program = Parse(@"
            entry scene Main {
                byte x = 0;
                frame {
                    switch (x) {
                        case 0:
                            poke: 53280 value: 0;
                        case 1:
                            poke: 53280 value: 1;
                        default:
                            poke: 53280 value: 2;
                    }
                }
            }
        ");
        var scene = program.Declarations[0] as SceneNode;
        var frame = scene!.Members.OfType<FrameBlockNode>().First();
        var switchStmt = frame.Body.Statements[0] as SwitchStmt;
        await Assert.That(switchStmt).IsNotNull();
        await Assert.That(switchStmt!.Cases).Count().IsEqualTo(2);
        await Assert.That(switchStmt.DefaultBody).IsNotNull();
        await Assert.That(switchStmt.IsFallthrough).IsFalse();
    }

    // --- Message send ---

    [Test]
    public async Task Parses_no_arg_message_send()
    {
        var program = Parse(@"
            entry scene Main {
                update: {
                }
                frame {
                    update:;
                }
            }
        ");
        var scene = program.Declarations[0] as SceneNode;
        var frame = scene!.Members.OfType<FrameBlockNode>().First();
        var msg = frame.Body.Statements[0] as MessageSendStmt;
        await Assert.That(msg).IsNotNull();
        await Assert.That(msg!.Receiver).IsNull();
        await Assert.That(msg.Segments).Count().IsEqualTo(1);
        await Assert.That(msg.Segments[0].Name).IsEqualTo("update");
        await Assert.That(msg.Segments[0].Argument).IsNull();
    }

    [Test]
    public async Task Parses_single_arg_message_send()
    {
        var program = Parse(@"
            entry scene Main {
                frame {
                    poke: 53280 value: 0;
                }
            }
        ");
        var scene = program.Declarations[0] as SceneNode;
        var frame = scene!.Members.OfType<FrameBlockNode>().First();
        var msg = frame.Body.Statements[0] as MessageSendStmt;
        await Assert.That(msg).IsNotNull();
        await Assert.That(msg!.Receiver).IsNull();
        await Assert.That(msg.Segments).Count().IsEqualTo(2);
        await Assert.That(msg.Segments[0].Name).IsEqualTo("poke");
        await Assert.That(msg.Segments[1].Name).IsEqualTo("value");
    }

    [Test]
    public async Task Parses_multi_segment_message_send()
    {
        var program = Parse(@"
            moveTo: (byte x) y: (byte y) {
            }
            entry scene Main {
                frame {
                    moveTo: 10 y: 20;
                }
            }
        ");
        var scene = program.Declarations[1] as SceneNode;
        var frame = scene!.Members.OfType<FrameBlockNode>().First();
        var msg = frame.Body.Statements[0] as MessageSendStmt;
        await Assert.That(msg).IsNotNull();
        await Assert.That(msg!.Segments).Count().IsEqualTo(2);
        await Assert.That(msg.Segments[0].Name).IsEqualTo("moveTo");
        await Assert.That(msg.Segments[1].Name).IsEqualTo("y");
    }

    // --- If/else if/else ---

    [Test]
    public async Task Parses_if_else_if_else()
    {
        var program = Parse(@"
            entry scene Main {
                byte x = 0;
                frame {
                    if (x == 0) {
                        poke: 53280 value: 0;
                    } else if (x == 1) {
                        poke: 53280 value: 1;
                    } else {
                        poke: 53280 value: 2;
                    }
                }
            }
        ");
        var scene = program.Declarations[0] as SceneNode;
        var frame = scene!.Members.OfType<FrameBlockNode>().First();
        var ifStmt = frame.Body.Statements[0] as IfStmt;
        await Assert.That(ifStmt).IsNotNull();
        await Assert.That(ifStmt!.ElseIfs).Count().IsEqualTo(1);
        await Assert.That(ifStmt.ElseBody).IsNotNull();
    }

    // --- While loop ---

    [Test]
    public async Task Parses_while_loop()
    {
        var program = Parse(@"
            entry scene Main {
                byte x = 0;
                frame {
                    while (x < 10) {
                        x += 1;
                    }
                }
            }
        ");
        var scene = program.Declarations[0] as SceneNode;
        var frame = scene!.Members.OfType<FrameBlockNode>().First();
        var whileStmt = frame.Body.Statements[0] as WhileStmt;
        await Assert.That(whileStmt).IsNotNull();
        await Assert.That(whileStmt!.Condition).IsTypeOf<BinaryExpr>();
    }

    // --- Go statement ---

    [Test]
    public async Task Parses_go_statement()
    {
        var program = Parse(@"
            entry scene Main {
                frame {
                    go GameOver;
                }
            }
            scene GameOver { frame { } }
        ");
        var scene = program.Declarations[0] as SceneNode;
        var frame = scene!.Members.OfType<FrameBlockNode>().First();
        var goStmt = frame.Body.Statements[0] as GoStmt;
        await Assert.That(goStmt).IsNotNull();
        await Assert.That(goStmt!.SceneName).IsEqualTo("GameOver");
    }

    // --- Const declaration ---

    [Test]
    public async Task Parses_const_declaration()
    {
        var program = Parse(@"
            const byte[3] data = { 1, 2, 3 };
            entry scene Main { frame { } }
        ");
        var constArr = program.Declarations[0] as ConstArrayDeclNode;
        await Assert.That(constArr).IsNotNull();
        await Assert.That(constArr!.ElementType).IsEqualTo("byte");
        await Assert.That(constArr.Size).IsEqualTo(3);
        await Assert.That(constArr.Name).IsEqualTo("data");
        await Assert.That(constArr.Values).Count().IsEqualTo(3);
    }

    // --- Global method ---

    [Test]
    public async Task Parses_global_method()
    {
        var program = Parse(@"
            doSomething: (byte x) {
                poke: 53280 value: x;
            }
            entry scene Main { frame { } }
        ");
        var method = program.Declarations[0] as GlobalMethodNode;
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.SelectorName).IsEqualTo("doSomething:");
        await Assert.That(method.Parameters).Count().IsEqualTo(1);
        await Assert.That(method.Parameters[0].ParamName).IsEqualTo("x");
    }
}
