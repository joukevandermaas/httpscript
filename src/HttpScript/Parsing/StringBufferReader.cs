using System;

namespace HttpScript.Parsing
{
    internal class StringBufferReader
    {
        public ReadOnlyMemory<char> Buffer { get; }

        // normally this class is responsible for setting these but
        // sometimes it's cleaner to just do it from the outside
        public StringBufferReaderState CurrentState { get; set; } = new()
        {
            CharOffset = 0,
            LineNumber = 1,
            LineStartOffset = 0,
            PreviousLineOffset = 0
        };
        public StringBufferReaderState SnapshotState { get; set; }

        public StringBufferReader(ReadOnlyMemory<char> buffer)
        {
            Buffer = buffer;
        }

        public void CreateSnapshot()
        {
            this.SnapshotState = this.CurrentState;
        }

        public void RestoreSnapshot()
        {
            this.CurrentState = this.SnapshotState;
            this.SnapshotState = default;
        }

        public void DiscardSnapshot()
        {
            this.SnapshotState = default;
        }

        public Range GetRangeFromSnapshot()
        {
            return StringBufferReaderState.GetRange(SnapshotState, CurrentState);
        }

        public bool TryMatchSequenceAndAdvance(string match)
        {
            var offset = 0;
            var prevPos = this.CurrentState;

            while (offset < match.Length && TryMatchAndAdvance(match[offset]))
            {
                offset += 1;
            }

            if (offset == match.Length)
            {
                return true;
            }

            this.CurrentState = prevPos;
            return false;
        }

        public bool TryMatchAndAdvance(out char matchedChar, params char[] matches)
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

        public bool TryMatchAndAdvance(char match)
        {
            if (TryPeek(out var chr) && chr == match)
            {
                Skip();
                return true;
            }

            return false;
        }

        public bool TrySkip() => TryAdvance(out _);

        public bool TryAdvance(out char character)
        {
            if (IsAtEndOfBuffer())
            {
                character = default;
                return false;
            }

            character = Advance();
            return true;
        }

        public bool TryPeek(out char character) => TryPeek(out character, out _);

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

        public bool IsAtEndOfBuffer()
        {
            var pos = this.CurrentState.CharOffset;

            return pos >= this.Buffer.Length;
        }

        public void Skip() => Advance();

        public char Advance()
        {
            var character = Peek(out var consumed);

            var newCharOffset = this.CurrentState.CharOffset + consumed;
            var newLineStartOffset = this.CurrentState.LineStartOffset;
            var previousLineOffset = this.CurrentState.PreviousLineOffset;
            var newLineNumber = this.CurrentState.LineNumber;

            if (character == '\n')
            {
                // we consumed a new line, so should reset the offset and
                // increase the counter
                previousLineOffset = newLineStartOffset;
                newLineStartOffset = newCharOffset;
                newLineNumber += 1;
            }

            this.CurrentState = new()
            {
                CharOffset = newCharOffset,
                LineStartOffset = newLineStartOffset,
                PreviousLineOffset = previousLineOffset,
                LineNumber = newLineNumber,
            };

            return character;
        }

        public char Peek() => Peek(out _);

        private char Peek(out int consumed)
        {
            // this can fail if pos is out of range
            var pos = this.CurrentState.CharOffset;

            var character = this.Buffer.Span[pos];

            // normalize newlines to \n
            var consumedCharacters = 1;

            if (character == '\r' && (pos + 1 < Buffer.Length))
            {
                if (this.Buffer.Span[pos + 1] == '\n')
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
