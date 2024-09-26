namespace brigen;

public sealed class Tokenizer
{
    private static readonly HashSet<string> _keywords = new()
  {
    Strings.KwEnum,
    Strings.KwStruct,
    Strings.KwClass,
    Strings.KwStatic,
    Strings.KwDelegate,
    Strings.KwFunc,
    Strings.KwCtor,
    Strings.KwGet,
    Strings.KwSet,
    Strings.KwArray,
    Strings.KwConst,
    Strings.KwModule,
    Strings.KwImport,
    Strings.KwTrue,
    Strings.KwFalse,
  };

    private static readonly Dictionary<char, TokenType> _tokenTypeMap = new()
  {
    { '=', TokenType.Equal },
    { ',', TokenType.Comma },
    { ';', TokenType.Semicolon },
    { ':', TokenType.Colon },
    { '(', TokenType.LeftParen },
    { ')', TokenType.RightParen },
    { '.', TokenType.Dot },
    { '#', TokenType.NumberSign },
    { '/', TokenType.ForwardSlash },
    { '"', TokenType.QuoteMark },
    { '+', TokenType.Plus },
    { '-', TokenType.Minus },
    { '*', TokenType.Asterisk },
    { '[', TokenType.LeftBracket },
    { ']', TokenType.RightBracket },
    { '\n', TokenType.Newline },
    { '\r', TokenType.CarriageReturn }
  };

    private static readonly Dictionary<(TokenType, TokenType), TokenType> _twoTokenTypesToSingleTokenTypeMap =
      new()
      {
      { (TokenType.NumberSign, TokenType.NumberSign), TokenType.DoubleNumberSign },
      { (TokenType.ForwardSlash, TokenType.ForwardSlash), TokenType.LineComment },
      { (TokenType.LeftBracket, TokenType.LeftBracket), TokenType.DoubleLeftBracket },
      { (TokenType.RightBracket, TokenType.RightBracket), TokenType.DoubleRightBracket }
      };

    private readonly List<TokenReplacement> _tokenReplacements = new(256);
    private readonly List<Token> _tokens = new(2048);
    private string _filename = string.Empty;
    private int _line = 1;
    private string _text = string.Empty;

    public IReadOnlyList<Token> Tokens => _tokens;

    public void Tokenize(string text, string filename)
    {
        _tokens.Clear();
        _filename = filename;
        _text = text;
        _line = 1;

        if (_text == string.Empty)
        {
            _filename = string.Empty;
            return;
        }

        int newCapacity = _text.Length * 4;
        if (_tokens.Capacity < newCapacity)
            _tokens.Capacity = newCapacity;

        int currTokenStart = 0;

        int textLength = _text.Length;
        for (int i = 1; i < textLength; ++i)
        {
            if (_text[i] == ' ')
            {
                AddRecordedToken(i, currTokenStart);
                currTokenStart = i + 1;
                continue;
            }

            if (CanSkip(i))
                continue;

            if (AddRecordedToken(i, currTokenStart))
                currTokenStart = i;
        }

        if (textLength > currTokenStart)
            AddRecordedToken(textLength, currTokenStart);

        _tokens.Add(new Token(string.Empty, TokenType.Eof, new CodeRange(filename)));

        FindMultiCharTokens();
        FindStringLiterals();
        FindIdentifiers();
        FindComments();
        RemoveTokensUnnecessaryForParsing();
    }

    private void RemoveTokensUnnecessaryForParsing()
      => _tokens.RemoveAll(t => t.Type is TokenType.Newline or TokenType.CarriageReturn);

    private void FindIdentifiers()
    {
        _tokenReplacements.Clear();

        for (int t = 0; t < _tokens.Count - 1; ++t)
        {
            if (!_tokens[t].Is(TokenType.Identifier))
                continue;

            static bool CanJoin(in Token t1, in Token t2)
            {
                return CodeRange.AreDirectNeighbors(t1.Range, t2.Range)
                       && t1.Type is TokenType.IntLiteral or TokenType.Identifier
                       && t2.Type is TokenType.IntLiteral or TokenType.Identifier;
            }

            int nlTk = t;
            while (nlTk != _tokens.Count && CanJoin(_tokens[nlTk], _tokens[nlTk + 1]))
                ++nlTk;

            if (nlTk == _tokens.Count)
                throw new Exception();

            if (nlTk == t)
                continue;

            CodeRange startRange = _tokens[t].Range;
            CodeRange endRange = _tokens[nlTk].Range;
            var mergedRange = new CodeRange(startRange.Filename, startRange.Line,
              startRange.Start, endRange.End, startRange.StartColumn, endRange.EndColumn);

            string value = _text.Substring(mergedRange.Start, mergedRange.End - mergedRange.Start);
            var newTk = new Token(value, TokenType.Identifier, mergedRange);

            int count = nlTk - t;
            _tokenReplacements.Add(new TokenReplacement(t, count, newTk));
            t += count;
        }

        ApplyReplacements();
    }

    private void FindMultiCharTokens()
    {
        _tokenReplacements.Clear();

        for (int t = 0; t < _tokens.Count - 2; ++t)
        {
            if (!_twoTokenTypesToSingleTokenTypeMap.TryGetValue((_tokens[t].Type, _tokens[t + 1].Type), out TokenType type))
                continue;

            if (!CodeRange.AreDirectNeighbors(_tokens[t].Range, _tokens[t + 1].Range))
                continue;

            CodeRange tRange = _tokens[t].Range;

            var mergedRange = new CodeRange(
              tRange.Filename,
              tRange.Line,
              tRange.Start,
              _tokens[t + 1].Range.Start + _tokens[t + 1].Value.Length,
              tRange.StartColumn,
              tRange.EndColumn);

            string value = _text.Substring(mergedRange.Start, mergedRange.End - mergedRange.Start);
            var newToken = new Token(value, type, mergedRange);

            _tokenReplacements.Add(new TokenReplacement(t, 1, newToken));
            ++t;
        }

        ApplyReplacements();
    }

    private void FindStringLiterals()
    {
        _tokenReplacements.Clear();

        for (int t = 0; t < _tokens.Count - 1; ++t)
        {
            if (!_tokens[t].Is(TokenType.QuoteMark))
                continue;

            // Skip to the next quotation mark.
            int nextQuoteMarkIt = t + 1;
            while (nextQuoteMarkIt != _tokens.Count
                   && !_tokens[nextQuoteMarkIt].Is(TokenType.QuoteMark))
                ++nextQuoteMarkIt;

            if (nextQuoteMarkIt == _tokens.Count)
                throw new Exception("Unfinished string");

            CodeRange strBeginRange = _tokens[t].Range;
            CodeRange strEndRange = _tokens[nextQuoteMarkIt].Range;

            var mergedRange = new CodeRange(strBeginRange.Filename, strBeginRange.Line, strBeginRange.End, strEndRange.Start,
              strBeginRange.EndColumn, strEndRange.StartColumn);

            string value = _text[mergedRange.Start..mergedRange.End];

            var newToken = new Token(value, TokenType.String, mergedRange, false);

            _tokenReplacements.Add(new TokenReplacement(t, nextQuoteMarkIt - t, newToken));
            t += nextQuoteMarkIt - t;
        }

        ApplyReplacements();
    }

    private void FindComments()
    {
        _tokenReplacements.Clear();

        for (int t = 0; t < _tokens.Count - 2; ++t)
        {
            if (!_tokens[t].Is(TokenType.ForwardSlash) || !_tokens[t + 1].Is(TokenType.ForwardSlash))
                continue;

            int nlTk = t;
            while (nlTk != _tokens.Count && !(_tokens[nlTk].Type is TokenType.CarriageReturn or TokenType.Newline))
                ++nlTk;

            if (nlTk == _tokens.Count)
                throw CompileError.UnexpectedEof(_tokens[nlTk - 1]);

            if (nlTk == t)
                continue;

            CodeRange startRange = _tokens[t].Range;
            CodeRange endRange = _tokens[nlTk - 1].Range;
            var mergedRange = new CodeRange(startRange.Filename, startRange.Line,
              startRange.Start, endRange.End, startRange.StartColumn, endRange.EndColumn);

            string value = _text.Substring(mergedRange.Start, mergedRange.End - mergedRange.Start);
            var newTk = new Token(value, TokenType.LineComment, mergedRange);

            int count = nlTk - t;
            _tokenReplacements.Add(new TokenReplacement(t, count, newTk));
            t += count;
        }

        ApplyReplacements();
    }

    private int GetColumn(int textIdx)
    {
        int si = textIdx - 1;
        int count = 0;
        while (si >= 0 && _text[si] != '\n')
        {
            --si;
            ++count;
        }

        return count + 1;
    }

    private static bool IsAllWhiteSpaceOrEmpty(string s)
      => s == string.Empty || s.All(c => c != '\r' && c != '\n' && char.IsWhiteSpace(c));

    private bool AddRecordedToken(int i, int currTokenStart)
    {
        string value = _text.Substring(currTokenStart, i - currTokenStart);

        if (IsAllWhiteSpaceOrEmpty(value))
            return false;

        int column = GetColumn(i - value.Length);

        _tokens.Add(new Token(value, DetermineTokenType(value),
          new CodeRange(_filename, _line, currTokenStart, i, column, column + value.Length)));

        if (value == "\n")
            ++_line;

        return true;
    }

    private bool CanSkip(int i)
    {
        static int GetCharClassification(char ch)
        {
            if (char.IsWhiteSpace(ch))
                return 4;

            if (char.IsSymbol(ch))
                return 5;

            if (char.IsLetter(ch) || ch == '_')
                return 1;

            if (char.IsDigit(ch))
                return 2;

            return 3;
        }

        int currClass = GetCharClassification(_text[i]);

        if (currClass is 3 or 4 or 5)
            return false;

        int prevClass = GetCharClassification(_text[i - 1]);

        if (prevClass != currClass)
            return false;

        return true;
    }

    private static TokenType DetermineTokenType(string value)
    {
        if (value.Length == 1)
        {
            if (_tokenTypeMap.TryGetValue(value[0], out TokenType result))
                return result;

            if (char.IsSymbol(value[0]))
                return TokenType.UnknownSymbol;
        }

        if (_keywords.Contains(value))
            return TokenType.Keyword;

        return TokenType.Identifier;
    }

    private void ApplyReplacements()
    {
        int replacementCount = _tokenReplacements.Count;
        for (int i = replacementCount - 1; i > -1; --i)
        {
            TokenReplacement replacement = _tokenReplacements[i];
            int startIdx = replacement.Start;
            _tokens.RemoveRange(startIdx, replacement.Count);
            _tokens[startIdx] = replacement.NewToken;
        }
    }

    private readonly struct TokenReplacement
    {
        public TokenReplacement(int start, int count, in Token newToken)
        {
            Start = start;
            Count = count;
            NewToken = newToken;
        }

        public readonly int Start;
        public readonly int Count;
        public readonly Token NewToken;
    }
}