using System;
using System.Diagnostics.CodeAnalysis;

namespace HttpScript.Parsing
{
    internal struct StringBufferReaderState : IComparable<StringBufferReaderState>, IEquatable<StringBufferReaderState>
    {
        public int CharOffset { get; init; }
        public int PreviousLineOffset { get; init; }
        public int LineStartOffset { get; init; }
        public int LineNumber { get; init; }

        public override string ToString()
        {
            return $"L{LineNumber}C{CharOffset - LineStartOffset + 1}";
        }

        public bool Equals(StringBufferReaderState other)
        {
            return CompareTo(other) == 0;
        }

        public int CompareTo(StringBufferReaderState other)
        {
            // we only care about charoffset because for the same string
            // the other values should be the same too if this matches.
            // that said if you compare the states for two different strings
            // that won't hold (but we don't care about that)
            return CharOffset - other.CharOffset;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is StringBufferReaderState state && Equals(state);
        }

        public override int GetHashCode()
        {
            return CharOffset.GetHashCode();
        }

        public static Range GetRange(StringBufferReaderState start, StringBufferReaderState end)
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

        public static bool operator ==(StringBufferReaderState left, StringBufferReaderState right)
        {
            return left.CompareTo(right) == 0;
        }

        public static bool operator !=(StringBufferReaderState left, StringBufferReaderState right)
        {
            return left.CompareTo(right) != 0;
        }

        public static bool operator <(StringBufferReaderState left, StringBufferReaderState right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(StringBufferReaderState left, StringBufferReaderState right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(StringBufferReaderState left, StringBufferReaderState right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(StringBufferReaderState left, StringBufferReaderState right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
