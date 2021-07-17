namespace HttpScript.Parsing.Tokens
{
    public class OperatorToken : Token
    {
        public OperatorToken()
        {
            base.Type = TokenType.Operator;
        }
        public OperatorType OperatorType { get; init; }

        public override string ToString()
        {
            return base.ToString() + $" ({OperatorType})";
        }
    }
}
