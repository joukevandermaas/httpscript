namespace HttpScript.Parsing.Tokens
{
    public class ParenToken : Token
    {
        public ParenToken()
        {
            base.Type = TokenType.Paren;
        }
            
        public ParenType ParenType { get; init; }
        public ParenMode ParenMode { get; init; }

        public override string ToString()
        {
            return base.ToString() + $" ({ParenMode}{ParenType})";
        }
    }
}
