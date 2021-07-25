namespace HttpScript.Parsing.Tokens
{
    public enum TokenType
    {
        Unknown = 0,
        Error = 1,

        WhiteSpace = 2,
        Comment = 3,

        Paren = 4,
        BinaryOperator = 5,
        Symbol = 6,
        Separator = 7,

        StringLiteral = 10,
        NumberLiteral = 11,
    }
}