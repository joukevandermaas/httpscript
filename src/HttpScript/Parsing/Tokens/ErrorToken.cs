namespace HttpScript.Parsing.Tokens
{
    /// <summary>
    /// Used for lexing errors, e.g. missing end quote on a string
    /// </summary>
    public class ErrorToken : Token
    {
        public ErrorToken()
        {
            base.Type = TokenType.Error;
        }

        public ErrorType ErrorCode { get; init; }

        public override string ToString()
        {
            return base.ToString() + $" ({this.ErrorCode})";
        }
    }
}
