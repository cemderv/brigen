using brigen.decl;
using brigen.types;
using System.Diagnostics;
using System.Text;

namespace brigen;

public sealed class Parser
{
    private static readonly List<Token> _defaultTokenList = [];

    private int _tokenIdx;
    private IReadOnlyList<Token> _tokens = _defaultTokenList;

    private Token Tk => _tokens![_tokenIdx];
    private Token PrevTk => _tokens![_tokenIdx - 1];
    private Token NextTk => _tokens![_tokenIdx + 1];

    public List<Decl> Parse(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
        _tokenIdx = 0;

        List<Decl> decls = [];

        while (!Tk.IsEof)
        {
            CommentDecl? comment = TryParseCommentDecl();
            AttributeDecl? attrib = TryParseAttributeDecl();

            if (Tk.Type == TokenType.Keyword)
            {
                Decl decl = Tk.Value switch
                {
                    Strings.KwEnum => ParseEnumDecl(),
                    Strings.KwStruct => ParseStructDecl(),
                    Strings.KwClass => ParseClassDecl(),
                    Strings.KwDelegate => ParseDelegateDecl(),
                    Strings.KwModule => ParseModuleDecl(),
                    Strings.KwSet => ParseSetVariableDecl(),
                    _ => throw CompileError.UnexpectedTopLevelToken(Tk)
                };

                decl.Comment = comment;
                decl.Attribute = attrib;

                decls.Add(decl);
            }
            else
            {
                throw CompileError.UnexpectedTopLevelToken(Tk);
            }
        }

        _tokens = _defaultTokenList;

        return decls;
    }

    private AttributeDecl? TryParseAttributeDecl()
    {
        if (!Tk.Is(TokenType.DoubleLeftBracket))
            return null;

        Advance();
        ExpectIdentifier();
        CodeRange nameRange = Tk.Range;
        string attributeName = ConsumeToken();
        Expect(TokenType.DoubleRightBracket, true);

        return new AttributeDecl(attributeName, nameRange);
    }

    private EnumDecl ParseEnumDecl()
    {
        Debug.Assert(Tk.Value == Strings.KwEnum);
        Token tkSaved = Tk;
        Advance();

        ExpectIdentifier();
        string enumName = ConsumeToken();

        Expect(TokenType.LeftBrace, true);

        List<EnumMemberDecl> members = [];

        while (!Tk.IsEof && !Tk.Is(TokenType.RightBrace))
        {
            CommentDecl? comment = TryParseCommentDecl();
            AttributeDecl? attrib = TryParseAttributeDecl();

            ExpectIdentifier();
            CodeRange memberRange = Tk.Range;
            string memberName = ConsumeToken();
            int? memberValue = null;

            if (Tk.Is(TokenType.Equal))
            {
                Advance();
                memberValue = ConsumeNumber();
            }

            if (!Tk.Is(TokenType.RightBrace))
            {
                Expect(TokenType.Comma, true);
            }

            members.Add(new EnumMemberDecl(memberName, memberRange, memberValue) { Comment = comment, Attribute = attrib });
        }

        Expect(TokenType.RightBrace, true);

        return new EnumDecl(enumName, tkSaved.Range, members);
    }

    private StructDecl ParseStructDecl()
    {
        Debug.Assert(Tk.Value == Strings.KwStruct);
        Token structTk = Tk;
        Advance();
        ExpectIdentifier();
        string structName = ConsumeToken();
        Expect(TokenType.LeftBrace, true);

        var fields = new List<StructFieldDecl>();

        while (!Tk.IsEof && !Tk.Is(TokenType.RightBrace))
        {
            CommentDecl? comment = TryParseCommentDecl();
            AttributeDecl? attrib = TryParseAttributeDecl();

            IDataType fieldType = ParseType();

            ExpectIdentifier();
            CodeRange nameRange = Tk.Range;
            string fieldName = ConsumeToken();

            Expect(TokenType.Semicolon, true);

            fields.Add(new StructFieldDecl(fieldName, nameRange, fieldType)
            {
                Comment = comment,
                Attribute = attrib
            });
        }

        VerifyNotEof();

        Expect(TokenType.RightBrace, true);

        return new StructDecl(structName, structTk.Range, fields);
    }

    private List<FunctionParamDecl> ParseFuncParams()
    {
        List<FunctionParamDecl> list = [];

        while (!Tk.IsEof && !Tk.Is(TokenType.RightParen))
        {
            IDataType type = ParseType();

            ExpectIdentifier();
            CodeRange nameRange = Tk.Range;
            string name = ConsumeToken();

            list.Add(new FunctionParamDecl(name, nameRange, type));

            if (Tk.Is(TokenType.Comma) && !NextTk.Is(TokenType.RightParen))
                Advance();
            else
                break;
        }

        VerifyNotEof();
        Expect(TokenType.RightParen);

        return list;
    }

    private IDataType ParseType()
    {
        ExpectIdentifier();
        string name = ConsumeToken();

        IDataType type = UnresolvedType.Get(name);

        if (Tk.Is(TokenType.Keyword))
        {
            if (Tk.Value == Strings.KwArray)
            {
                Advance();
                type = ArrayType.Get(type);
            }
        }

        return type;
    }

    private ClassDecl ParseClassDecl()
    {
        Debug.Assert(Tk.Value == Strings.KwClass);
        Advance();

        ExpectIdentifier();
        CodeRange classNameRange = Tk.Range;
        string className = ConsumeToken();

        var modifiers = ClassDecl.Modifier.None;

        if (Tk.Type == TokenType.Keyword)
        {
            do
            {
                ClassDecl.Modifier modifier = Tk.Value switch
                {
                    Strings.KwStatic => ClassDecl.Modifier.Static,
                    _ => throw CompileError.UnknownClassModifier(Tk)
                };

                if (modifiers.HasFlag(modifier))
                    throw new CompileError($"Class modifier '{Tk.Value}' specified multiple times", Tk.Range);

                modifiers |= modifier;
                Advance();
            } while (!Tk.IsEof && !Tk.Is(TokenType.Keyword));
        }

        List<FunctionDecl> functions = [];
        List<PropertyDecl> properties = [];

        if (Tk.Is(TokenType.Semicolon))
        {
            Advance();
        }
        else
        {
            Expect(TokenType.LeftBrace, true);

            while (!Tk.IsEof && !Tk.Is(TokenType.RightBrace))
            {
                CommentDecl? comment = TryParseCommentDecl();
                AttributeDecl? attrib = TryParseAttributeDecl();

                if (Tk.Value is Strings.KwFunc or Strings.KwCtor)
                {
                    FunctionDecl func = ParseFunctionDecl();
                    func.Comment = comment;
                    func.Attribute = attrib;
                    functions.Add(func);
                }
                else if (Tk.Value is Strings.KwGet or Strings.KwSet)
                {
                    PropertyDecl prop = ParsePropertyDecl();
                    prop.Comment = comment;
                    prop.Attribute = attrib;
                    properties.Add(prop);
                }
                else
                {
                    break;
                }
            }

            Expect(TokenType.RightBrace, true);
        }

        return new ClassDecl(className, classNameRange, functions, properties, modifiers);
    }

    private DelegateDecl ParseDelegateDecl()
    {
        Debug.Assert(Tk.Value == Strings.KwDelegate);
        Advance();

        IDataType type = ParseType();

        ExpectIdentifier();
        CodeRange nameRange = Tk.Range;
        string name = ConsumeToken();

        Expect(TokenType.LeftParen, true);
        List<FunctionParamDecl> pars = ParseFuncParams();
        Expect(TokenType.RightParen, true);

        Expect(TokenType.Semicolon, true);

        return new DelegateDecl(name, nameRange, pars, type);
    }

    private CommentDecl? TryParseCommentDecl()
    {
        if (!Tk.Is(TokenType.LineComment))
            return null;

        var list = new List<Token>();

        while (!Tk.IsEof && Tk.Is(TokenType.LineComment))
        {
            list.Add(Tk);
            Advance();
        }

        StringBuilder content = new(64);
        foreach (Token tk in list)
            content.AppendLine(tk.Value);

        return new CommentDecl(CodeRange.Merge(list.First().Range, list[^1].Range), content.ToString());
    }

    private FunctionDecl ParseFunctionDecl()
    {
        var flags = FunctionFlags.None;
        bool isCtor = Tk.Value == Strings.KwCtor;

        if (isCtor)
            flags |= FunctionFlags.Ctor;

        Token tkSaved = Tk;
        Advance();

        IDataType returnType = isCtor ? PrimitiveType.Void : ParseType();

        ExpectIdentifier();
        string funcName = ConsumeToken();

        Expect(TokenType.LeftParen, true);
        List<FunctionParamDecl> pars = ParseFuncParams();
        Expect(TokenType.RightParen, true);

        if (!isCtor)
        {
            if (Tk.Is(TokenType.Identifier) && Tk.Value == Strings.KwConst)
            {
                flags |= FunctionFlags.Const;
                Advance();
            }
        }

        Expect(TokenType.Semicolon, true);

        return new FunctionDecl(funcName, tkSaved.Range, flags, returnType, pars);
    }

    private PropertyDecl ParsePropertyDecl()
    {
        Token tkSaved = Tk;

        StringBuilder sumSb = new();
        sumSb.Append(ConsumeToken());

        if (Tk.Is(TokenType.Keyword) && (Tk.Value == Strings.KwGet || Tk.Value == Strings.KwSet))
            sumSb.Append(ConsumeToken());

        string sum = sumSb.ToString();

        bool CheckFlag(string s)
        {
            int idx = sum.IndexOf(s, StringComparison.Ordinal);
            if (idx >= 0)
            {
                sum = sum.Remove(idx, s.Length);
                return true;
            }

            return false;
        }

        PropertyDecl.PropMask mask = 0;

        if (CheckFlag(Strings.KwGet))
            mask |= PropertyDecl.PropMask.Getter;

        if (CheckFlag(Strings.KwSet))
            mask |= PropertyDecl.PropMask.Setter;

        sum = sum.Trim();

        if (!string.IsNullOrEmpty(sum))
            throw new CompileError("get/set defined multiple times for property", Tk.Range);

        ExpectIdentifier();
        IDataType type = ParseType();

        ExpectIdentifier();
        string propName = ConsumeToken();

        Expect(TokenType.Semicolon, true);

        return new PropertyDecl(propName, tkSaved.Range, mask, type);
    }

    private ModuleDecl ParseModuleDecl()
    {
        Debug.Assert(Tk.Value == Strings.KwModule);
        Advance();

        var range = Tk.Range;
        ExpectIdentifier();
        var moduleName = ConsumeToken();

        Expect(TokenType.Semicolon, true);

        return new ModuleDecl(moduleName, range);
    }

    private SetVariableDecl ParseSetVariableDecl()
    {
        Debug.Assert(Tk.Value == Strings.KwSet);
        Advance();

        var range = Tk.Range;
        ExpectIdentifier();
        var variableName = ConsumeToken();

        object? variableValue = null;

        if (Tk.Value is Strings.KwTrue or Strings.KwFalse)
        {
            variableValue = Tk.Value switch
            {
                Strings.KwTrue => true,
                Strings.KwFalse => false,
                _ => throw CompileError.Internal("")
            };

            Advance();
        }
        else
        {
            variableValue = Tk.Type switch
            {
                TokenType.IntLiteral => ConsumeNumber(),
                TokenType.String => ConsumeToken(),
                TokenType.Identifier => ConsumeToken(),
                _ => variableValue
            };
        }

        return variableValue == null
          ? throw CompileError.Internal($"no variable value set for variable '{variableName}'")
          : new SetVariableDecl(variableName, range, variableValue);
    }

    private void Expect(TokenType type, bool advance = false)
    {
        if (!Tk.Is(type))
        {
            if (Tk.Is(TokenType.Identifier))
            {
                throw new CompileError(
                  $"Expected '{type.GetDisplayString()}', but encountered an identifier instead.",
                  Tk.Range);
            }
            else
            {
                if (type == TokenType.Identifier)
                {
                    throw new CompileError(
                      $"Expected an identifier, but encountered '{Tk.Type.GetDisplayString()}' instead.",
                      Tk.Range);
                }
                else
                {
                    throw new CompileError(
                      $"Expected '{type.GetDisplayString()}', but encountered '{Tk.Type.GetDisplayString()}' instead.",
                      Tk.Range);
                }
            }
        }

        if (advance)
        {
            Advance();
        }
    }

    private void ExpectEitherOr(TokenType either, TokenType or, bool advance = false)
    {
        if (!Tk.Is(either) && !Tk.Is(or))
        {
            throw new CompileError(
              $"Expected either token type '{either.GetDisplayString()}' or '{or.GetDisplayString()}'.",
              Tk.Range);
        }

        if (advance)
        {
            Advance();
        }
    }

    private void ExpectIdentifier(bool advance = false)
        => Expect(TokenType.Identifier, advance);

    private string ConsumeToken()
    {
        string value = Tk.Value;
        Advance();
        return value;
    }

    private int ConsumeNumber()
    {
        Expect(TokenType.IntLiteral);
        int number = (int)Tk.NumericValue!.Value;
        Advance();
        return number;
    }

    private void VerifyNotEof()
    {
        if (Tk.IsEof)
        {
            throw CompileError.UnexpectedEof(PrevTk);
        }
    }

    private void Advance() => ++_tokenIdx;
}