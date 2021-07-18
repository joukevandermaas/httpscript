using HttpScript.Parsing.Tokens;
using System.Collections.Generic;
using System.Diagnostics;

namespace HttpScript.Parsing
{
    public partial class Lexer
    {
        private readonly string buffer;
        private readonly Queue<Token> lookAhead = new();

        private StringBufferReader reader;
        private StringBufferReaderState beforeLookAheadState;
        private ParsingMode parsingMode;

        public Lexer(string content)
        {
            this.buffer = content;
            this.reader = new(buffer);
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
                this.reader.CurrentState = this.beforeLookAheadState;
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
            this.beforeLookAheadState = this.reader.CurrentState;

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

            if (!hasValidToken && !this.reader.IsAtEndOfBuffer())
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
            var errorStartPos = this.reader.CurrentState;
            var errorEndPos = this.reader.CurrentState;

            while (true)
            {
                // skip the current character, then try to match a token again
                if (reader.TrySkip())
                {
                    errorEndPos = this.reader.CurrentState;

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
                            Range = StringBufferReaderState.GetRange(errorStartPos, errorEndPos),
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
                        Range = StringBufferReaderState.GetRange(errorStartPos, errorEndPos),
                        ErrorCode = ErrorType.UnknownToken,
                    });

                    return;
                }
            }

        }

        private bool TryGetWhiteSpaceToken(out Token token)
        {
            this.reader.CreateSnapshot();
            bool anyConsumed = false;

            while (this.reader.TryPeek(out var chr) && char.IsWhiteSpace(chr))
            {
                anyConsumed = true;
                this.reader.Skip();
            }

            if (anyConsumed)
            {
                var range = reader.GetRangeFromSnapshot();

                token = new()
                {
                    Type = TokenType.WhiteSpace,
                    Range = range,
                };

                return true;
            }

            this.reader.RestoreSnapshot();
            token = Token.Empty;
            return false;
        }

    }
}
