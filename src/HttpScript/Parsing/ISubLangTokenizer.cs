using HttpScript.Parsing.Tokens;

namespace HttpScript.Parsing
{
    internal interface ISubLangTokenizer
    {
        bool TryGetToken(out Token token, out Token? errorToken);
    }
}
