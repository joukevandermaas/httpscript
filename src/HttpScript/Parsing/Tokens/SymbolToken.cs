namespace HttpScript.Parsing.Tokens
{
    public class SymbolToken : Token
    {
        public SymbolToken()
        {
            base.Type = TokenType.Symbol;
        }

        public string Name { get; init; } = string.Empty;

        public override string ToString()
        {
            return base.ToString() + $" ({this.Name})";
        }
    }
}
