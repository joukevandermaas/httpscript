namespace HttpScript.Parsing.Tokens
{
    public enum TokenType
    {
        Unknown = 0,
        WhiteSpace = 1,
        String = 2,
        Paren = 3,
        Operator = 4,
        Symbol = 5,
        Comment = 6,
    }
}