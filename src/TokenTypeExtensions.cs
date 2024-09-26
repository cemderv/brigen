namespace brigen;

internal static class TokenTypeExtensions
{
    public static string GetDisplayString(this TokenType type)
      => type switch
      {
          TokenType.Newline => "<newline>",
          TokenType.Keyword => "<keyword>",
          TokenType.Equal => "=",
          TokenType.Comma => ",",
          TokenType.Semicolon => ";",
          TokenType.Colon => ":",
          TokenType.LeftParen => "(",
          TokenType.RightParen => ")",
          TokenType.IntLiteral => "<int>",
          TokenType.Identifier => "<identifier>",
          TokenType.Dot => ".",
          TokenType.NumberSign => "#",
          TokenType.DoubleNumberSign => "##",
          TokenType.Eof => "<eof>",
          TokenType.ForwardSlash => "/",
          TokenType.QuoteMark => "\"",
          TokenType.String => "<string>",
          TokenType.Plus => "+",
          TokenType.Minus => "-",
          TokenType.Asterisk => "*",
          TokenType.CarriageReturn => "\r",
          TokenType.LineComment => "<line comment>",
          TokenType.UnknownSymbol => "<unknown symbol>",
          TokenType.LeftBracket => "[",
          TokenType.RightBracket => "]",
          TokenType.DoubleLeftBracket => "[[",
          TokenType.DoubleRightBracket => "]]",
          _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
      };
}