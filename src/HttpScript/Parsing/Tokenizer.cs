using HttpScript.Parsing.Tokens;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HttpScript.Parsing
{
    public class Tokenizer
    {
        // the string representing the program we are parsing
        private readonly ReadOnlyMemory<char> buffer;

        // when we match tokens we add them to this queue, then
        // process the queue before parsing more. this allows the
        // consumer to peek tokens without overhead.
        private readonly BufferedQueue<Token> lookAheadQueue = new();

        // maps parsing mode to the sublang tokenizer we need to use
        // to generate tokens
        private readonly Dictionary<ParsingMode, ISubLangTokenizer> subLangTokenizers;

        private StringBufferReader reader;
        private StringBufferReaderState beforeLookAheadState;
        private ParsingMode parsingMode;

        public Tokenizer(ReadOnlyMemory<char> content)
        {
            this.buffer = content;
            this.reader = new(this.buffer);

            // depending on where we are in the file, we are parsing completely
            // different languages (http vs breakout for example). this is
            // implemented through a "mode" switch which is controlled by the
            // parser.
            // based on the mode we switch to a different tokenizer, which reduces
            // the likelyhood that the languages accidentally bleed into each
            // other.
            this.subLangTokenizers = new()
            {
                [ParsingMode.Breakout] = new BreakoutTokenizer(this.reader)
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
                // TODO: this is probably wrong; we may want to backtrack
                // after switching modes which does not work with the current
                // implmentation of BufferedQueue.Clear
                this.lookAheadQueue.Clear();
                this.reader.CurrentState = this.beforeLookAheadState;
            }
        }

        public bool HasMoreTokens => !this.reader.IsAtEndOfBuffer() || this.lookAheadQueue.Count > 0;

        public bool TryPeekTokenOfType(TokenType tokenType, out Token token)
        {
            var success = this.TryPeekToken(out var maybeToken);

            if (success && maybeToken.Type == tokenType)
            {
                token = maybeToken;
                return true;
            }

            token = Token.Empty;
            return false;
        }

        public bool TryPeekTokenOfType<T>(out T token) where T : Token
        {
            var success = this.TryPeekToken(out var untypedToken);

            if (!success)
            {
                token = default!;
                return false;
            }

            var typedToken = untypedToken as T;

            if (typedToken == null)
            {
                token = default!;
                return false;
            }

            token = typedToken;
            return true;
        }

        public bool TryPeekToken(out Token token)
        {
            var success = this.lookAheadQueue.TryPeek(out var maybeToken);

            if (!success && this.TryLookAheadAndAdvance())
            {
                success = this.lookAheadQueue.TryPeek(out maybeToken);
            }

            token = maybeToken ?? Token.Empty;
            return success;
        }

        public bool TryConsumeTokenOfType(TokenType tokenType, out Token token)
        {
            var success = this.TryPeekTokenOfType(tokenType, out token);

            if (success)
            {
                // dequeue before returning, this will succeed
                // since the peek succeeded
                this.lookAheadQueue.TryDequeue(out _);
            }

            return success;
        }

        public bool TryConsumeTokenOfType<T>(out T token) where T : Token
        {
            var success = this.TryPeekTokenOfType(out token);

            if (success)
            {
                // dequeue before returning, this will succeed
                // since the peek succeeded
                this.lookAheadQueue.TryDequeue(out _);
            }

            return success;
        }

        public bool TryConsumeToken(out Token token)
        {
            var success = this.TryPeekToken(out token);

            if (success)
            {
                this.lookAheadQueue.TryDequeue(out _);
            }

            return success;
        }

        private bool TryLookAheadAndAdvance()
        {
            Debug.Assert(this.lookAheadQueue.Count == 0);

            // we're going to enqueue some tokens into the lookahead queue so
            // we don't have to do double parsing when someone peeks and then
            // consumes.
            // but if someone changes the parsing mode, those tokens are bad
            // and we should revert to the previous state.
            this.beforeLookAheadState = this.reader.CurrentState;

            // convention is that if hasValidToken == false, then no tokens
            // were produced (including error tokens). if an error token is
            // produced, that means we know what the problem is and we were
            // able to recover. if we cannot recover in a specific way, we
            // try to do so in a generic way below.
            var hasValidToken = this.TryGetToken(out var parsedToken, out var errorToken);

            if (hasValidToken)
            {
                if (errorToken != null)
                {
                    this.lookAheadQueue.Enqueue(errorToken);
                }

                this.lookAheadQueue.Enqueue(parsedToken);
            }

            if (!hasValidToken && !this.reader.IsAtEndOfBuffer())
            {
                // we'll skip characters until we can match a valid token
                // again (or until we hit the end of the file) and report
                // the whole range of invalid stuff as a single error.
                this.AdvanceToValidToken();

                // we've recovered
                hasValidToken = true;
            }

            if (hasValidToken && this.lookAheadQueue.Count != 0)
            {
                return true;
            }

            return false;
        }

        public IEnumerable<Token> GetTokens()
        {
            while (this.TryConsumeToken(out var token))
            {
                yield return token;
            }
        }

        private bool TryGetToken(out Token token, out ErrorToken? errorToken)
        {
            if (this.TryGetWhiteSpaceToken(out token))
            {
                errorToken = null;
                return true;
            }

            var result = this.subLangTokenizers[this.ParsingMode].TryGetToken(out token, out errorToken);

            return result;
        }

        private void AdvanceToValidToken()
        {
            var errorStartPos = this.reader.CurrentState;
            var errorEndPos = this.reader.CurrentState;

            bool foundValidToken = false;

            while (!foundValidToken)
            {
                // skip the current character, then try to match a token again
                if (this.reader.TrySkip())
                {
                    errorEndPos = this.reader.CurrentState;

                    var result = this.TryGetToken(out var token, out var errorToken);

                    if (result)
                    {
                        // ok we found a good token now, or there's nothing more to
                        // be found, we should enqueue our error
                        this.lookAheadQueue.Enqueue(new ErrorToken()
                        {
                            Range = StringBufferReaderState.GetRange(errorStartPos, errorEndPos),
                            ErrorCode = ErrorType.UnknownToken,
                        });

                        // if the token was faulty in predictable ways we should
                        // first enqueue that error now
                        if (errorToken != null)
                        {
                            this.lookAheadQueue.Enqueue(errorToken);
                        }

                        // finally we should enqueue the good token we found
                        this.lookAheadQueue.Enqueue(token);

                        foundValidToken = true;
                    }
                }
                else
                {
                    // we reached the end of the buffer
                    this.lookAheadQueue.Enqueue(new ErrorToken()
                    {
                        Range = StringBufferReaderState.GetRange(errorStartPos, errorEndPos),
                        ErrorCode = ErrorType.UnknownToken,
                    });

                    // we didn't find a token but we can't find any
                    // because we're at the end of the file
                    foundValidToken = true;
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
                var range = this.reader.GetRangeFromSnapshot();

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

        public void PushRestorePoint()
        {
            this.lookAheadQueue.PushRestorePoint();
        }

        public void PopRestorePoint()
        {
            this.lookAheadQueue.PopRestorePoint();
        }

        public void DiscardRestorePoint()
        {
            this.lookAheadQueue.DiscardRestorePoint();
        }
    }
}
