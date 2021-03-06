using System;

namespace HttpScript.Parsing
{
    public struct Range : IEquatable<Range>
    {
        public int StartOffset { get; init; }
        public int EndOffset { get; init; }

        public int StartLine { get; init; }
        public int EndLine { get; init; }

        public int StartCharacter { get; init; }
        public int EndCharacter { get; init; }

        public bool Equals(Range other) => this.StartOffset == other.StartOffset && this.EndOffset == other.EndOffset;
        public override bool Equals(object? obj) => obj is Range range && this.Equals(range);
        public static bool operator ==(Range left, Range right) => left.Equals(right);
        public static bool operator !=(Range left, Range right) => !(left == right);
        public override int GetHashCode() => HashCode.Combine(this.StartOffset, this.EndOffset);

        public override string ToString()
        {
            return $"L{this.StartLine}C{this.StartCharacter}-L{this.EndLine}C{this.EndCharacter}";
        }
    }
}
