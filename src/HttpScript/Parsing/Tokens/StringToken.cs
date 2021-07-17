namespace HttpScript.Parsing.Tokens
{
    public class StringToken : Token
    {
        public StringToken()
        {
            base.Type = TokenType.String;
        }

        public string Value { get; init; } = string.Empty;

        public override string ToString()
        {
            return base.ToString() + $" ('{Value}')";
        }
    }
}
