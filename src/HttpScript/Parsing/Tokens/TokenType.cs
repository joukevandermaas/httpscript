namespace HttpScript.Parsing.Tokens
{
    public enum TokenType
    {
        Unknown = 0,
        Error = 1,

        WhiteSpace = 2,
        StringContent = 3, // 'String' conflicts with System.String
        Paren = 4,
        Operator = 5,
        Symbol = 6,
        Comment = 7,
    }
}