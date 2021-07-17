using HttpScript.Parsing.Tokens;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HttpScript.Parsing
{
    public partial class Lexer
    {
        private readonly string buffer;
        private readonly Queue<Token> lookAhead = new();

        private LexerState currentState;
        private LexerState beforeLookAheadState;
        private ParsingMode parsingMode;

        struct LexerState
        {
            public int CharOffset { get; init; }
            public int PreviousLineOffset { get; init; }
            public int LineStartOffset { get; init; }
            public int LineNumber { get; init; }

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

        public Lexer(string content)
        {
            this.buffer = content;

            this.currentState = new()
            {
                CharOffset = 0,
                LineNumber = 1,
                LineStartOffset = 0,
                PreviousLineOffset = 0,
            };
        }

        public ParsingMode ParsingMode
        {
            get
            {
                return this.parsingMode;
            }
            set
            {
                this.parsingMode = value;

                // we're switching modes, we should clear the
                // lookahead queue because it is no longer valid
                this.lookAhead.Clear();
                this.currentState = this.beforeLookAheadState;
            }
        }

        public bool TryPeekToken(out Token token)
        {
            var success = lookAhead.TryPeek(out var maybeToken);

            if (!success && TryLookAheadAndAdvance())
            {
                success = lookAhead.TryPeek(out maybeToken);
            }

            token = maybeToken ?? Token.Empty;
            return success;
        }

        public bool TryConsumeToken(out Token token)
        {
            var success = lookAhead.TryDequeue(out var maybeToken);

            if (!success && TryLookAheadAndAdvance())
            {
                success = lookAhead.TryDequeue(out maybeToken);
            }

            token = maybeToken ?? Token.Empty;
            return success;
        }

        private bool TryLookAheadAndAdvance()
        {
            Debug.Assert(this.lookAhead.Count == 0);

            // we're going to enqueue some tokens into the lookahead queue so
            // we don't have to do double parsing when someone peeks and then
            // consumes.
            // but if someone changes the parsing mode, those tokens are bad
            // and we should rever to the previous state.
            this.beforeLookAheadState = this.currentState;

            // convention is that if hasValidToken == false, then no tokens
            // were produced (including error tokens). if an error token is
            // produced, that means we know what the problem is and we were
            // able to recover. if we cannot recover in a specific way, we
            // try to do so in a generic way below.
            var hasValidToken = TryGetToken(out var parsedToken, out var errorToken);

            if (hasValidToken)
            {
                if (errorToken != null)
                {
                    lookAhead.Enqueue(errorToken);
                }

                lookAhead.Enqueue(parsedToken);
            }

            if (!hasValidToken && !IsAtEndOfBuffer())
            {
                // we'll skip characters until we can match a valid token
                // again (or until we hit the end of the file) and report
                // the whole range of invalid stuff as a single error.
                AdvanceToValidToken();

                // we've recovered
                hasValidToken = true;
            }

            if (hasValidToken && lookAhead.Count != 0)
            {
                return true;
            }

            return false;
        }

        public IEnumerable<Token> GetTokens()
        {
            while (TryConsumeToken(out var token))
            {
                yield return token;
            }
        }

        private bool TryGetToken(out Token token, out ErrorToken? errorToken)
        {
            var result = this.ParsingMode == ParsingMode.Breakout
                ? TryGetBreakoutToken(out token, out errorToken)
                : TryGetHttpToken(out token, out errorToken);

            return result;
        }

        private void AdvanceToValidToken()
        {
            var errorStartPos = this.currentState;
            var errorEndPos = this.currentState;

            while (true)
            {
                // skip the current character, then try to match a token again
                if (TrySkip())
                {
                    errorEndPos = this.currentState;

                    Token token;
                    ErrorToken? errorToken;

                    var result = this.ParsingMode == ParsingMode.Breakout
                        ? TryGetBreakoutToken(out token, out errorToken)
                        : TryGetHttpToken(out token, out errorToken);

                    if (result)
                    {
                        // ok we found a good token now, or there's nothing more to
                        // be found, we should enqueue our error
                        lookAhead.Enqueue(new ErrorToken()
                        {
                            Range = LexerState.GetRange(errorStartPos, errorEndPos),
                            ErrorCode = ErrorType.UnknownToken,
                        });

                        // if the token was faulty in predictable ways we should
                        // first enqueue that error now
                        if (errorToken != null)
                        {
                            lookAhead.Enqueue(errorToken);
                        }

                        // finally we should enqueue the good token we found
                        lookAhead.Enqueue(token);

                        return;
                    }
                }
                else
                {
                    // we reached the end of the buffer
                    lookAhead.Enqueue(new ErrorToken()
                    {
                        Range = LexerState.GetRange(errorStartPos, errorEndPos),
                        ErrorCode = ErrorType.UnknownToken,
                    });

                    return;
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
