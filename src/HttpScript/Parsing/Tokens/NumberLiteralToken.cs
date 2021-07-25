namespace HttpScript.Parsing.Tokens
{
    public class NumberLiteralToken : Token
    {
        public NumberLiteralToken()
        {
            base.Type = TokenType.NumberLiteral;
        }

        public int Value { get; init; }

        public override string ToString()
        {
            return base.ToString() + $" ('{this.Value}')";
        }
    }
}
