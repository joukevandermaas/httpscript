using System;

namespace HttpScript.Parsing.Tokens
{
    public class SymbolToken : Token
    {
        public SymbolToken()
        {
            base.Type = TokenType.Symbol;
        }

        public ReadOnlyMemory<char> Name { get; init; }

        public override string ToString()
        {
            return base.ToString() + $" ({this.Name})";
        }
    }
}
