using HttpScript.Parsing.Tokens;
using System;
using System.Collections.Generic;

namespace HttpScript.Parsing
{
    public partial class Lexer
    {
        private bool TryGetBreakoutToken(out Token token)
        {
            if (TryGetParenToken(out token)) { return true; }
            if (TryGetOperatorToken(out token)) { return true; }
            if (TryGetWhiteSpaceToken(out token)) { return true; }
            if (TryGetCommentToken(out token)) { return true; }
            if (TryGetStringToken(out token)) { return true; }
            if (TryGetSymbolToken(out token)) { return true; }

            return false;
        }

        private bool TryGetSymbolToken(out Token token)
        {
            if (TryPeek(out var firstChr) && (
                firstChr == '$' ||
                firstChr == '_' ||
                char.IsLetter(firstChr)))
            {
                var prevPos = this.currentState;

                // valid first characters include dollar,
                // while subsequent ones cannot be
                Skip();

                while (TryPeek(out var chr))
                {
                    if (char.IsLetter(chr) || chr == '_')
                    {
                        Skip();
                    }
                    else
                    {
                        break;
                    }
                }

                var name = this.buffer.Substring(
                    prevPos.CharOffset,
                    this.currentState.CharOffset - prevPos.CharOffset);

                token = new SymbolToken()
                {
                    Name = name,
                    Range = LexerState.GetRange(prevPos, this.currentState),
                };

                return true;
            }

            token = Token.Empty;
            return false;
        }

        private bool TryGetOperatorToken(out Token token)
        {
            var prevPos = this.currentState;

            if (TryMatchAndAdvance(out var op, '.', '=', ','))
            {
                token = new OperatorToken()
                {
                    Range = LexerState.GetRange(prevPos, this.currentState),
                    OperatorType = op switch
                    {
                        '.' => OperatorType.MemberAccess,
                        '=' => OperatorType.Assignment,
                        ',' => OperatorType.Separator,

                        // just in case we forget to add it here
                        _ => throw new NotImplementedException(),
                    }
                };

                return true;
            }

            token = Token.Empty;
            return false;
        }

        private bool TryGetParenToken(out Token token)
        {
            var prevPos = this.currentState;

            if (TryMatchAndAdvance(out var paren, '(', ')'))
            {
                token = new ParenToken()
                {
                    Range = LexerState.GetRange(prevPos, this.currentState),
                    ParenType = paren == '(' ? ParenType.Open : ParenType.Close,
                };

                return true;
            }

            token = Token.Empty;
            return false;
        }

        private bool TryGetStringToken(out Token token)
        {
            var prevPos = this.currentState;

            if (TryMatchAndAdvance(out var openQuote, '\'', '"'))
            {
                // we found an open quote, so we know we've a string

                while (TryPeek(out var chr))
                {
                    if (chr == '\n')
                    {
                        // error, need to handle this somehow
                    }
                    else if (chr == openQuote)
                    {
                        // end of string
                        var content = this.buffer.Substring(
                            prevPos.CharOffset + 1,
                            this.currentState.CharOffset - prevPos.CharOffset - 1);

                        // skip the end quote
                        Skip();

                        token = new StringToken()
                        {
                            Range = LexerState.GetRange(prevPos, this.currentState),
                            Value = content,
                        };

                        return true;
                    }
                    else
                    {
                        // we don't care what this is, skip to next character
                        Skip();
                    }
                }
            }

            token = Token.Empty;
            return false;
        }

        private bool TryGetCommentToken(out Token token)
        {
            var prevPos = this.currentState;

            if (TryMatchSequenceAndAdvance("//"))
            {
                while (TryAdvance(out var chr) && chr != '\n')
                {
                    // keep going till we hit eof or newline
                }

                token = new()
                {
                    Type = TokenType.Comment,
                    Range = LexerState.GetRange(prevPos, this.currentState),
                };

                return true;
            }
            else if (TryMatchSequenceAndAdvance("/*"))
            {
                var foundEnd = false;
                var depth = 0;

                while (TryAdvance(out var chr))
                {
                    if (chr == '/' && TryMatchAndAdvance('*'))
                    {
                        // nested comment
                        depth += 1;
                    }

                    if (chr == '*' && TryMatchAndAdvance('/'))
                    {
                        if (depth == 0)
                        {
                            foundEnd = true;
                            break;
                        }
                        else
                        {
                            depth -= 1;
                        }
                    }
                }

                if (foundEnd)
                {
                    token = new()
                    {
                        Type = TokenType.Comment,
                        Range = LexerState.GetRange(prevPos, this.currentState),
                    };
                    return true;
                }
                else
                {
                    // error, handle this somehow
                }
            }

            token = Token.Empty;
            return false;
        }
    }
}
