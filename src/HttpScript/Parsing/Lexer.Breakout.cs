using HttpScript.Parsing.Tokens;
using System;
using System.Collections.Generic;

namespace HttpScript.Parsing
{
    public partial class Lexer
    {
        private bool TryGetBreakoutToken(out Token token, out ErrorToken? errorToken)
        {
            // most token types won't have lexer errors
            errorToken = null;

            if (TryGetParenToken(out token)) { return true; }
            if (TryGetOperatorToken(out token)) { return true; }
            if (TryGetWhiteSpaceToken(out token)) { return true; }
            if (TryGetCommentToken(out token, out errorToken)) { return true; }
            if (TryGetStringToken(out token, out errorToken)) { return true; }
            if (TryGetSymbolToken(out token)) { return true; }

            // we don't know wtf is going on, we should let the main
            // loop deal with this tho, we don't have enough info to
            // emit an error token here
            return false;
        }

        private bool TryGetSymbolToken(out Token token)
        {
            var prevPos = this.currentState;

            if (TryPeek(out var firstChr) && (
                firstChr == '$' ||
                firstChr == '_' ||
                char.IsLetter(firstChr)))
            {
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

            this.currentState = prevPos;
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

        private bool TryGetStringToken(out Token token, out ErrorToken? errorToken)
        {
            errorToken = null;
            var prevPos = this.currentState;

            if (TryMatchAndAdvance(out var openQuote, '\'', '"'))
            {
                // we found an open quote, so we know we've a string

                while (TryPeek(out var chr))
                {
                    var skipEndQuote = true;

                    if (chr == '\n')
                    {
                        errorToken = new ErrorToken()
                        {
                            // debatable where the error should be, it kind of makes
                            // sense to mark the whole string token
                            Range = LexerState.GetRange(prevPos, this.currentState),
                            ErrorCode = ErrorType.MissingEndQuote,
                        };

                        // pretend that we found a close quote so we can
                        // lex the rest of the file
                        chr = openQuote;

                        // however we should not be skipping this newline
                        // character because it should be considered whitespace,
                        // not part of the string
                        skipEndQuote = false;
                    }

                    if (chr == openQuote)
                    {
                        // end of string
                        var stringContent = this.buffer.Substring(
                            prevPos.CharOffset + 1,
                            this.currentState.CharOffset - prevPos.CharOffset - 1);

                        if (skipEndQuote)
                        {
                            Skip();
                        }

                        token = new StringToken()
                        {
                            Range = LexerState.GetRange(prevPos, this.currentState),
                            Value = stringContent,
                        };

                        return true;
                    }
                    else
                    {
                        // we don't care what this is, skip to next character
                        Skip();
                    }
                }

                // if we get here that means we've matched the open quote and then
                // hit eof before ever seeing the closing quote, so that's an error
                errorToken = new ErrorToken()
                {
                    // debatable where the error should be, it kind of makes
                    // sense to mark the whole string token
                    Range = LexerState.GetRange(prevPos, this.currentState),
                    ErrorCode = ErrorType.MissingEndQuote,
                };

                // however we can still just report the string token we have matched
                // so far 
                var unfinishedStringContent = this.buffer.Substring(
                    prevPos.CharOffset + 1,
                    this.currentState.CharOffset - prevPos.CharOffset - 1);

                token = new StringToken()
                {
                    Range = LexerState.GetRange(prevPos, this.currentState),
                    Value = unfinishedStringContent,
                };

                return true;
            }

            this.currentState = prevPos;
            token = Token.Empty;
            return false;
        }

        private bool TryGetCommentToken(out Token token, out ErrorToken? errorToken)
        {
            var prevPos = this.currentState;
            errorToken = null;

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
                    // we didn't find the end, but we can just assume
                    // the rest of the file is the comment and report
                    // an error.
                    token = new()
                    {
                        Type = TokenType.Comment,
                        Range = LexerState.GetRange(prevPos, this.currentState),
                    };

                    errorToken = new()
                    {
                        ErrorCode = ErrorType.MissingEndComment,
                        Range = LexerState.GetRange(prevPos, this.currentState),
                    };

                    return true;
                }
            }

            this.currentState = prevPos;
            token = Token.Empty;
            return false;
        }
    }
}
