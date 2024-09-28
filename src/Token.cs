using System.Globalization;

namespace brigen;

public enum TokenType
{
    Newline = 1,
    Keyword,
    Equal,
    Comma,
    Semicolon,
    Colon,
    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    IntLiteral,
    Identifier,
    Dot,
    NumberSign,
    DoubleNumberSign,
    Eof,
    ForwardSlash,
    QuoteMark,
    String,
    Plus,
    Minus,
    Asterisk,
    CarriageReturn,
    LineComment,
    LeftBracket,
    RightBracket,
    DoubleLeftBracket,
    DoubleRightBracket,
    UnknownSymbol
}

public sealed class Token
{
    public readonly double? NumericValue;
    public readonly CodeRange Range;
    public readonly TokenType Type;

    public readonly string Value;

    public Token(string value, TokenType type, CodeRange range, bool allowTypeChange = true, double? numericValue = null)
    {
        Value = value;
        Type = type;
        Range = range;
        NumericValue = numericValue;

        if (value != string.Empty && allowTypeChange)
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valueAsInt))
            {
                NumericValue = valueAsInt;
                Type = TokenType.IntLiteral;
            }
    }

    public bool IsEof => Is(TokenType.Eof);

    public int Line => Range.Line;

    public int Column => Range.StartColumn;

    public bool Is(TokenType type) => Type == type;

    public override string ToString() =>
      Type switch
      {
          TokenType.String => $"\"{Value}\"",
          TokenType.Newline => @"\n",
          TokenType.CarriageReturn => @"\r",
          _ => Value
      };
}