using System;

namespace HttpScript.Parsing.Tokens
{
    public class StringLiteralToken : Token
    {
        public StringLiteralToken()
        {
            base.Type = TokenType.StringLiteral;
        }

        public ReadOnlyMemory<char> Value { get; init; }

        public override string ToString()
        {
            return base.ToString() + $" ('{this.Value}')";
        }
    }
}
