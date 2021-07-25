using System;

namespace HttpScript.Parsing.Tokens
{
    public struct Token : IEquatable<Token>
    {
        public TokenType Type { get; init; }
        public Range Range { get; init; }
        public ReadOnlyMemory<char> Text { get; init; }
        public object? Value { get; init; }

        public T GetValue<T>() => this.Value is T v ? v : default!;

        public bool Equals(Token other) =>
            this.Type == other.Type
            && this.Range == other.Range;

        public override bool Equals(object? obj) =>
            obj is Token token && this.Equals(token);

        public static bool operator ==(Token left, Token right) => left.Equals(right);
        public static bool operator !=(Token left, Token right) => !(left == right);
        
        public override int GetHashCode() => HashCode.Combine(this.Type, this.Range);
        public override string ToString() => $"{this.Type} <{this.Range}>" + (this.Value != null ? this.Value.ToString() : string.Empty);

        private string ValueToString()
        {
            if (this.Value is ReadOnlyMemory<char> mem)
            {
                return new string(mem.Span);
            }

            return this.Value?.ToString() ?? string.Empty;
        }
    }
}
