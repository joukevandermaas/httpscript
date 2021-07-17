using System;

namespace HttpScript.Parsing.Tokens
{
    public class Token : IEquatable<Token>
    {
        public static Token Empty { get; } = new() { Type = TokenType.Unknown, Range = default };

        public TokenType Type { get; init; }
        public Range Range { get; init; }

        public bool Equals(Token? other) =>
            Type == other?.Type
            && Range == other?.Range;

        public override bool Equals(object? obj) =>
            obj is Token token && Equals(token);

        public static bool operator ==(Token? left, Token? right) => 
            // if both are null, they are equal
            object.ReferenceEquals(left, null) && object.ReferenceEquals(right, null)
            // if left is null but not right, they are not equal,
            // otherwise 'equals' impl handles nullability
            || (left?.Equals(right) ?? false);
        public static bool operator !=(Token? left, Token? right) => !(left == right);
        public override int GetHashCode() => HashCode.Combine(Type, Range);
        public override string ToString() => $"{Type} <{Range}>";
    }
}
