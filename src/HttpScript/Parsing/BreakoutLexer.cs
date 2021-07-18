using HttpScript.Parsing.Tokens;
using System;

namespace HttpScript.Parsing
{
    internal class BreakoutLexer : ISublangLexer
    {
        private readonly StringBufferReader reader;

        public BreakoutLexer(StringBufferReader reader)
        {
            this.reader = reader;
        }

        public bool TryGetToken(out Token token, out ErrorToken? errorToken)
        {
            // most token types won't have lexer errors
            errorToken = null;

            if (TryGetParenToken(out token)) { return true; }
            if (TryGetOperatorToken(out token)) { return true; }
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
            this.reader.CreateSnapshot();

            if (this.reader.TryPeek(out var firstChr) && (
                firstChr == '$' ||
                firstChr == '_' ||
                char.IsLetter(firstChr)))
            {
                // valid first characters include dollar,
                // while subsequent ones cannot be
                this.reader.Skip();

                while (this.reader.TryPeek(out var chr))
                {
                    if (char.IsLetter(chr) || chr == '_')
                    {
                        this.reader.Skip();
                    }
                    else
                    {
                        break;
                    }
                }

                var prevCharOffset = this.reader.SnapshotState.CharOffset;
                var currCharOffset = this.reader.CurrentState.CharOffset;
                var name = this.reader.Buffer.Substring(
                    prevCharOffset,
                    currCharOffset - prevCharOffset);

                token = new SymbolToken()
                {
                    Name = name,
                    Range = this.reader.GetRangeFromSnapshot(),
                };

                return true;
            }

            this.reader.RestoreSnapshot();
            token = Token.Empty;
            return false;
        }

        private bool TryGetOperatorToken(out Token token)
        {
            this.reader.CreateSnapshot();

            if (this.reader.TryMatchAndAdvance(out var op, '.', '=', ',', ';'))
            {
                token = new OperatorToken()
                {
                    Range = this.reader.GetRangeFromSnapshot(),
                    OperatorType = op switch
                    {
                        '.' => OperatorType.MemberAccess,
                        '=' => OperatorType.Assignment,
                        ',' => OperatorType.Separator,
                        ';' => OperatorType.EndStatement,

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
            this.reader.CreateSnapshot();

            if (this.reader.TryMatchAndAdvance(out var paren, '(', ')'))
            {
                token = new ParenToken()
                {
                    Range = this.reader.GetRangeFromSnapshot(),
                    ParenType = ParenType.Round,
                    ParenMode = paren == '(' ? ParenMode.Open : ParenMode.Close,
                };

                return true;
            }

            if (this.reader.TryMatchAndAdvance(out var bracket, '[', ']'))
            {
                token = new ParenToken()
                {
                    Range = this.reader.GetRangeFromSnapshot(),
                    ParenType = ParenType.Square,
                    ParenMode = bracket == '[' ? ParenMode.Open : ParenMode.Close,
                };

                return true;
            }

            if (this.reader.TryMatchAndAdvance(out var curly, '{', '}'))
            {
                token = new ParenToken()
                {
                    Range = this.reader.GetRangeFromSnapshot(),
                    ParenType = ParenType.Curly,
                    ParenMode = curly == '{' ? ParenMode.Open : ParenMode.Close,
                };

                return true;
            }

            token = Token.Empty;
            return false;
        }

        private bool TryGetStringToken(out Token token, out ErrorToken? errorToken)
        {
            errorToken = null;
            this.reader.CreateSnapshot();

            if (this.reader.TryMatchAndAdvance(out var openQuote, '\'', '"'))
            {
                // we found an open quote, so we know we've a string

                while (this.reader.TryPeek(out var chr))
                {
                    var skipEndQuote = true;

                    if (chr == '\n')
                    {
                        errorToken = new ErrorToken()
                        {
                            // debatable where the error should be, it kind of makes
                            // sense to mark the whole string token
                            Range = this.reader.GetRangeFromSnapshot(),
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
                        var prevOffset = this.reader.SnapshotState.CharOffset;
                        var currOffset = this.reader.CurrentState.CharOffset;
                        var stringContent = this.reader.Buffer.Substring(
                            prevOffset + 1,
                            currOffset - prevOffset - 1);

                        if (skipEndQuote)
                        {
                            this.reader.Skip();
                        }

                        token = new StringToken()
                        {
                            Range = this.reader.GetRangeFromSnapshot(),
                            Value = stringContent,
                        };

                        return true;
                    }
                    else
                    {
                        // we don't care what this is, skip to next character
                        this.reader.Skip();
                    }
                }

                // if we get here that means we've matched the open quote and then
                // hit eof before ever seeing the closing quote, so that's an error
                errorToken = new ErrorToken()
                {
                    // debatable where the error should be, it kind of makes
                    // sense to mark the whole string token
                    Range = this.reader.GetRangeFromSnapshot(),
                    ErrorCode = ErrorType.MissingEndQuote,
                };

                // however we can still just report the string token we have matched
                // so far 
                var unfinishedPrevOffset = this.reader.SnapshotState.CharOffset;
                var unfinishedCurrOffset = this.reader.CurrentState.CharOffset;
                var unfinishedStringContent = this.reader.Buffer.Substring(
                    unfinishedPrevOffset + 1,
                    unfinishedCurrOffset - unfinishedPrevOffset - 1);

                token = new StringToken()
                {
                    Range = this.reader.GetRangeFromSnapshot(),
                    Value = unfinishedStringContent,
                };

                return true;
            }

            this.reader.RestoreSnapshot();
            token = Token.Empty;
            return false;
        }

        private bool TryGetCommentToken(out Token token, out ErrorToken? errorToken)
        {
            this.reader.CreateSnapshot();
            errorToken = null;

            if (this.reader.TryMatchSequenceAndAdvance("//"))
            {
                while (this.reader.TryAdvance(out var chr) && chr != '\n')
                {
                    // keep going till we hit eof or newline
                }

                token = new()
                {
                    Type = TokenType.Comment,
                    Range = this.reader.GetRangeFromSnapshot(),
                };

                return true;
            }
            else if (this.reader.TryMatchSequenceAndAdvance("/*"))
            {
                var foundEnd = false;
                var depth = 0;

                while (this.reader.TryAdvance(out var chr))
                {
                    if (chr == '/' && this.reader.TryMatchAndAdvance('*'))
                    {
                        // nested comment
                        depth += 1;
                    }

                    if (chr == '*' && this.reader.TryMatchAndAdvance('/'))
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
                        Range = this.reader.GetRangeFromSnapshot(),
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
                        Range = this.reader.GetRangeFromSnapshot(),
                    };

                    errorToken = new()
                    {
                        ErrorCode = ErrorType.MissingEndComment,
                        Range = this.reader.GetRangeFromSnapshot(),
                    };

                    return true;
                }
            }

            this.reader.RestoreSnapshot();
            token = Token.Empty;
            return false;
        }
    }
}
