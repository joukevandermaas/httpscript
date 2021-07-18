using HttpScript.Parsing.Tokens;

namespace HttpScript.Parsing
{
    internal interface ISublangLexer
    {
        bool TryGetToken(out Token token, out ErrorToken? errorToken);
    }
}
