using HttpScript.Parsing.Tokens;
using System;

namespace HttpScript.Parsing
{
    internal class BreakoutTokenizer : ISubLangTokenizer
    {
        private readonly StringBufferReader reader;

        public BreakoutTokenizer(StringBufferReader reader)
        {
            this.reader = reader;
        }

        public bool TryGetToken(out Token token, out Token? errorToken)
        {
            // most token types won't have tokenizer errors
            errorToken = null;

            if (this.TryGetParenToken(out token)) { return true; }
            if (this.TryGetOperatorToken(out token)) { return true; }
            if (this.TryGetCommentToken(out token, out errorToken)) { return true; }
            if (this.TryGetStringLiteralToken(out token, out errorToken)) { return true; }
            if (this.TryGetNumberLiteralToken(out token)) { return true; }
            if (this.TryGetSymbolToken(out token)) { return true; }

            // we don't know wtf is going on, we should let the main
            // loop deal with this tho, we don't have enough info to
            // emit an error token here
            return false;
        }

        private bool TryGetSymbolToken(out Token token)
        {
            this.reader.CreateSnapshot();

            if (this.reader.TryPeek(out var firstChr) && (
                firstChr == '_' ||
                char.IsLetter(firstChr)))
            {
                // valid first characters do not include digits,
                // while subsequent ones can be
                this.reader.Skip();

                while (this.reader.TryPeek(out var chr))
                {
                    if (char.IsLetterOrDigit(chr) || chr == '_')
                    {
                        this.reader.Skip();
                    }
                    else
                    {
                        break;
                    }
                }

                var text = this.reader.GetTextFromSnapshot();

                token = new Token()
                {
                    Type = TokenType.Symbol,
                    Range = this.reader.GetRangeFromSnapshot(),
                    Text = text,
                    Value = text,
                };

                return true;
            }

            this.reader.RestoreSnapshot();
            token = default!;
            return false;
        }

        private bool TryGetOperatorToken(out Token token)
        {
            this.reader.CreateSnapshot();

            if (this.reader.TryMatchAndAdvance(out var op, '.', '=', ',', ';'))
            {
                token = new Token()
                {
                    Type = TokenType.Operator,
                    Range = this.reader.GetRangeFromSnapshot(),
                    Text = this.reader.GetTextFromSnapshot(),
                    Value = op,
                };

                return true;
            }

            token = default!;
            return false;
        }

        private bool TryGetParenToken(out Token token)
        {
            this.reader.CreateSnapshot();

            if (this.reader.TryMatchAndAdvance(out var paren, '(', ')', '{', '}', '[', ']'))
            {
                token = new Token()
                {
                    Type = TokenType.Paren,
                    Range = this.reader.GetRangeFromSnapshot(),
                    Text = this.reader.GetTextFromSnapshot(),
                    Value = paren,
                };

                return true;
            }

            token = default!;
            return false;
        }

        private bool TryGetStringLiteralToken(out Token token, out Token? errorToken)
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
                        errorToken = new Token()
                        {
                            // debatable where the error should be, it kind of makes
                            // sense to mark the whole string token
                            Type = TokenType.Error,
                            Range = this.reader.GetRangeFromSnapshot(),
                            Text = this.reader.GetTextFromSnapshot(),
                            Value = ErrorStrings.MissingEndQuote,
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

                        if (skipEndQuote)
                        {
                            this.reader.Skip();
                        }

                        var text = this.reader.GetTextFromSnapshot();

                        token = new Token()
                        {
                            Type = TokenType.StringLiteral,
                            Range = this.reader.GetRangeFromSnapshot(),
                            Text = text,
                            Value = text[1..^(skipEndQuote ? 1 : 0)],
                        };

                        return true;
                    }
                    else
                    {
                        // we don't care what this is, skip to next character
                        this.reader.Skip();
                    }
                }

                var incompleteText = this.reader.GetTextFromSnapshot();

                // if we get here that means we've matched the open quote and then
                // hit eof before ever seeing the closing quote, so that's an error
                errorToken = new Token()
                {
                    // debatable where the error should be, it kind of makes
                    // sense to mark the whole string token
                    Type = TokenType.Error,
                    Range = this.reader.GetRangeFromSnapshot(),
                    Value = ErrorStrings.MissingEndQuote,
                    Text = incompleteText,
                };

                // however we can still just report the string token we have matched
                // so far 
                token = new Token()
                {
                    Type = TokenType.StringLiteral,
                    Range = this.reader.GetRangeFromSnapshot(),
                    Text = incompleteText,
                    Value = incompleteText[1..^0],
                };

                return true;
            }

            this.reader.RestoreSnapshot();
            token = default!;
            return false;
        }

        private bool TryGetNumberLiteralToken(out Token token)
        {
            this.reader.CreateSnapshot();

            bool consumedAny = false;

            while (this.reader.TryPeek(out var chr) && char.IsDigit(chr))
            {
                this.reader.Advance();
                consumedAny = true;
            }

            if (consumedAny)
            {
                // success
                var startCursor = this.reader.SnapshotState.Cursor;
                var endCursor = this.reader.CurrentState.Cursor;

                var span = this.reader.Buffer.Slice(startCursor, endCursor - startCursor).Span;

                var value = int.Parse(span);

                token = new Token()
                {
                    Type = TokenType.NumberLiteral,
                    Range = this.reader.GetRangeFromSnapshot(),
                    Text = this.reader.GetTextFromSnapshot(),
                    Value = value,
                };

                return true;
            }

            this.reader.RestoreSnapshot();
            token = default!;
            return false;
        }

        private bool TryGetCommentToken(out Token token, out Token? errorToken)
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
                    Text = this.reader.GetTextFromSnapshot(),
                    Value = null,
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
                        Text = this.reader.GetTextFromSnapshot(),
                        Value = null,
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
                        Type = TokenType.Error,
                        Range = this.reader.GetRangeFromSnapshot(),
                        Text = this.reader.GetTextFromSnapshot(),
                        Value = ErrorStrings.MissingEndComment,
                    };

                    return true;
                }
            }

            this.reader.RestoreSnapshot();
            token = default!;
            return false;
        }
    }
}
