using HttpScript.Parsing.Tokens;
using System;
using System.Collections.Generic;

namespace HttpScript.Parsing
{
    public partial class Lexer
    {
        private readonly string buffer;
        private readonly bool breakoutOnly;

        private LexerState currentState;

        enum Mode
        {
            Http,
            Breakout,
        }

        struct LexerState
        {
            public int CharOffset { get; init; }
            public int PreviousLineOffset { get; init; }
            public int LineStartOffset { get; init; }
            public int LineNumber { get; init; }
            public Mode Mode { get; init; }

            public static Range GetRange(LexerState start, LexerState end)
            {
                var endLine = end.LineNumber;
                var endCharacter = end.CharOffset - end.LineStartOffset;

                if (endCharacter == 0)
                {
                    // instead of being the 0th on the new line, we should be the
                    // last character on the previous line instead
                    endLine -= 1;
                    endCharacter = end.LineStartOffset - end.PreviousLineOffset;
                }

                return new()
                {
                    StartOffset = start.CharOffset,
                    EndOffset = end.CharOffset,
                    StartLine = start.LineNumber,
                    EndLine = endLine,
                    StartCharacter = start.CharOffset - start.LineStartOffset + 1,
                    EndCharacter = endCharacter,
                };
            }
        }

        public Lexer(string content, bool breakoutOnly)
        {
            this.buffer = content;
            this.breakoutOnly = breakoutOnly;

            this.currentState = new()
            {
                CharOffset = 0,
                LineNumber = 1,
                LineStartOffset = 0,
                PreviousLineOffset = 0,
                Mode = breakoutOnly ? Mode.Breakout : Mode.Http,
            };
        }

        public IEnumerable<Token> GetTokens()
        {
            // errors are just tokens, it is up to the parser to deal
            // with them. anyway the token should contain all the information
            // the parser should need to report the error.
            //
            // we basically always know what we were expecting so we can just
            // act as if we found it and anyway finish the previous token, then
            // report an additional error token.
            //
            // that allows us to lazily go through the file in the lexer in a
            // single pass

            bool hasValidToken;

            do
            {
                // convention is that if hasValidToken == false, then no tokens
                // were produced (including error tokens). if an error token is
                // produced, that means we know what the problem is and we were
                // able to recover. if we cannot recover in a specific way, we
                // try to do so in a generic way below.
                hasValidToken = TryGetToken(out var token, out var errorToken);

                if (hasValidToken)
                {
                    yield return token;

                    if (errorToken != null)
                    {
                        yield return errorToken;
                    }
                }


                if (!hasValidToken && !IsAtEndOfBuffer())
                {
                    // we'll skip characters until we can match a valid token
                    // again (or until we hit the end of the file) and report
                    // the whole range of invalid stuff as a single error.
                    var error = AdvanceToValidToken();
                    yield return error;

                    // we've recovered
                    hasValidToken = true;
                }
            } while (hasValidToken);
        }

        private bool TryGetToken(out Token token, out ErrorToken? errorToken)
        {
            var result = this.currentState.Mode == Mode.Breakout
                ? TryGetBreakoutToken(out token, out errorToken)
                : TryGetHttpToken(out token, out errorToken);

            return result;
        }

        private ErrorToken AdvanceToValidToken()
        {
            var errorStartPos = this.currentState;
            var errorEndPos = this.currentState;

            while (true)
            {
                // skip the current character, then try to match a token again
                if (TrySkip())
                {
                    errorEndPos = this.currentState;

                    // we're throwing away the result because we don't really have
                    // a clean way of handling it. it doesn't matter that much if
                    // the lexer is *slightly* slower in that case anyway
                    var result = this.currentState.Mode == Mode.Breakout
                        ? TryGetBreakoutToken(out _, out _)
                        : TryGetHttpToken(out _, out _);

                    // since we threw away the result we need to go back to *before*
                    // the token we just parsed, otherwise that token is lost
                    this.currentState = errorEndPos;

                    if (result)
                    {
                        // ok we found a good token now, or there's nothing more to
                        // be found, we should return our error
                        return new ErrorToken()
                        {
                            Range = LexerState.GetRange(errorStartPos, errorEndPos),
                            ErrorCode = ErrorType.UnknownToken,
                        };
                    }
                }
                else
                {
                    // we reached the end of the buffer
                    return new ErrorToken()
                    {
                        Range = LexerState.GetRange(errorStartPos, errorEndPos),
                        ErrorCode = ErrorType.UnknownToken,
                    };
                }
            }

        }

        private bool TryGetWhiteSpaceToken(out Token token)
        {
            var prevPos = this.currentState;
            var lastPosition = this.currentState;
            bool anyConsumed = false;

            while (TryPeek(out var chr) && char.IsWhiteSpace(chr))
            {
                anyConsumed = true;
                // store position right before the skip, because if
                // the skip contains a newline, we don't want to end
                // the token on the new line
                lastPosition = this.currentState;
                Skip();
            }

            if (anyConsumed)
            {
                var range = LexerState.GetRange(prevPos, this.currentState);

                if (range.EndCharacter == 1)
                {
                    // we ended the whitespace with a newline character
                    // we should end the token on the line before
                    range = LexerState.GetRange(prevPos, lastPosition);
                }

                token = new()
                {
                    Type = TokenType.WhiteSpace,
                    Range = range,
                };

                return true;
            }

            this.currentState = prevPos;
            token = Token.Empty;
            return false;
        }

        private bool TryMatchSequenceAndAdvance(string match)
        {
            var offset = 0;
            var prevPos = this.currentState;

            while (offset < match.Length && TryMatchAndAdvance(match[offset]))
            {
                offset += 1;
            }

            if (offset == match.Length)
            {
                return true;
            }

            this.currentState = prevPos;
            return false;
        }

        private bool TryMatchAndAdvance(out char matchedChar, params char[] matches)
        {
            if (TryPeek(out var chr))
            {
                var matchFound = false;
                for (var i = 0; i < matches.Length; i++)
                {
                    if (matches[i] == chr)
                    {
                        matchFound = true;
                        break;
                    }
                }

                if (matchFound)
                {
                    matchedChar = Advance();
                    return true;
                }
            }

            matchedChar = default;
            return false;
        }

        private bool TryMatchAndAdvance(char match)
        {
            if (TryPeek(out var chr) && chr == match)
            {
                Skip();
                return true;
            }

            return false;
        }

        private bool TrySkip() => TryAdvance(out _);

        private bool TryAdvance(out char character)
        {
            if (IsAtEndOfBuffer())
            {
                character = default;
                return false;
            }

            character = Advance();
            return true;
        }

        private bool TryPeek(out char character) => TryPeek(out character, out _);

        private bool TryPeek(out char character, out int consumed)
        {
            if (IsAtEndOfBuffer())
            {
                character = default;
                consumed = 0;
                return false;
            }

            character = Peek(out consumed);
            return true;
        }

        private bool IsAtEndOfBuffer()
        {
            var pos = this.currentState.CharOffset;

            return pos >= this.buffer.Length;
        }

        private void Skip() => Advance();

        private char Advance()
        {
            var character = Peek(out var consumed);

            var newCharOffset = this.currentState.CharOffset + consumed;
            var newLineStartOffset = this.currentState.LineStartOffset;
            var previousLineOffset = this.currentState.PreviousLineOffset;
            var newLineNumber = this.currentState.LineNumber;

            if (character == '\n')
            {
                // we consumed a new line, so should reset the offset and
                // increase the counter
                previousLineOffset = newLineStartOffset;
                newLineStartOffset = this.currentState.CharOffset + consumed;
                newLineNumber += 1;
            }

            this.currentState = new()
            {
                CharOffset = newCharOffset,
                LineStartOffset = newLineStartOffset,
                PreviousLineOffset = previousLineOffset,
                LineNumber = newLineNumber,
                Mode = this.currentState.Mode,
            };

            return character;
        }

        private char Peek() => Peek(out _);

        private char Peek(out int consumed)
        {
            // this can fail if pos is out of range
            var pos = this.currentState.CharOffset;
            var character = this.buffer[pos];

            // normalize newlines to \n
            var consumedCharacters = 1;

            if (character == '\r' && (pos + 1 < buffer.Length))
            {
                if (this.buffer[pos + 1] == '\n')
                {
                    consumedCharacters = 2;
                    character = '\n';
                }
            }

            consumed = consumedCharacters;
            return character;
        }
    }
}
